using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Roomzin.Sdk.Types;

namespace Roomzin.Sdk.Internal.Protocol
{
    /// <summary>
    /// Protocol helper methods for binary serialization and date handling
    /// </summary>
    public static class ProtocolHelper
    {
        /// <summary>
        /// Converts 24-bit mask to rate features strings (matches Rust/Go/Python behavior)
        /// </summary>
        public static string[] BitmaskToRateFeatureStrings(Codecs? codecs, uint mask)
        {
            var result = new List<string>();

            if (codecs == null || codecs.RateFeatures == null || codecs.RateFeatures.Count == 0)
            {
                return [.. result];
            }

            var rateFeatures = codecs.RateFeatures;

            for (int i = 0; i < 24 && i < rateFeatures.Count; i++)
            {
                if ((mask & (1u << i)) != 0)
                {
                    result.Add(rateFeatures[i]);
                }
            }

            return [.. result];
        }

        /// <summary>
        /// Unpacks the 16-bit packed date (same bit layout as Rust/Go)
        /// </summary>
        public static string U16ToDate(ushort packed)
        {
            var yearOffset = (int)((packed >> 9) & 0b111);
            var month = (int)((packed >> 5) & 0b1111) + 1;
            var day = (int)(packed & 0b11111) + 1;

            // Use current year as base (like Rust/Go)
            var baseYear = DateTime.Now.Year;

            try
            {
                var date = new DateTime(baseYear + yearOffset, month, day);

                // Validate that the date components match (handles invalid dates like Feb 30)
                if (date.Month != month || date.Day != day)
                {
                    throw RoomzinException.From("Invalid packed date");
                }

                return date.ToString("yyyy-MM-dd");
            }
            catch (ArgumentOutOfRangeException)
            {
                throw RoomzinException.From("Invalid packed date");
            }
        }

        /// <summary>
        /// Converts float64 to little-endian bytes
        /// </summary>
        public static byte[] MakeF64(double value)
        {
            var bytes = new byte[8];
            var bits = BitConverter.DoubleToInt64Bits(value);
            BinaryPrimitives.WriteInt64LittleEndian(bytes, bits);
            return bytes;
        }

        /// <summary>
        /// Converts uint64 to little-endian bytes
        /// </summary>
        public static byte[] MakeU64(ulong value)
        {
            var bytes = new byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
            return bytes;
        }

        /// <summary>
        /// Converts uint32 to little-endian bytes
        /// </summary>
        public static byte[] MakeU32(uint value)
        {
            var bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            return bytes;
        }

        /// <summary>
        /// Converts int to little-endian bytes (useful for IDs and counts)
        /// </summary>
        public static byte[] MakeU16(ushort value)
        {
            var bytes = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            return bytes;
        }

        /// <summary>
        /// Converts byte to single byte array (for consistency)
        /// </summary>
        public static byte[] MakeU8(byte value)
        {
            return new[] { value };
        }

        public static string BytesToPropertyId(byte[] data)
        {
            // 1. Too short → return empty
            if (data.Length < 7)
            {
                return "";
            }

            // 2. Short string marker
            if (data[6] == 0xF0)
            {
                // Left segment: 0..5
                int leftLen = 0;
                for (int i = 0; i < 6; i++)
                {
                    if (i >= data.Length || data[i] == 0)
                    {
                        break;
                    }
                    leftLen++;
                }

                // Right segment: 7..15
                int rightLen = 0;
                for (int i = 7; i < data.Length; i++)
                {
                    if (data[i] == 0)
                    {
                        break;
                    }
                    rightLen++;
                }

                // Reconstruct original string
                byte[] result = new byte[leftLen + rightLen];
                Array.Copy(data, 0, result, 0, leftLen);
                Array.Copy(data, 7, result, leftLen, rightLen);
                return Encoding.ASCII.GetString(result);
            }

            // 3. UUID detection (valid version)
            int version = (data[6] & 0xF0) >> 4;
            if (version == 1 || version == 2 || version == 3 || version == 4 || version == 5 || version == 7)
            {
                // Pad to 16 bytes if needed
                byte[] uuidBytes = new byte[16];
                int copyLen = Math.Min(data.Length, 16);
                Array.Copy(data, 0, uuidBytes, 0, copyLen);

                try
                {
                    Guid guid = new(uuidBytes);
                    return guid.ToString();
                }
                catch
                {
                    // UUID parsing failed
                }
            }

            // This should never happen with proper server data
            return "";
        }
    }
}