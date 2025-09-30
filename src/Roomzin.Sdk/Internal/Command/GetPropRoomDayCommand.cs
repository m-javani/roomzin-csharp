using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Roomzin.Sdk.Internal.Protocol;
using Roomzin.Sdk.Types;
using Roomzin.Sdk.Types.Requests;
using Roomzin.Sdk.Types.Responses;

namespace Roomzin.Sdk.Internal.Command
{
    /// <summary>
    /// Command builder and parser for GETPROPROOMDAY command
    /// </summary>
    public static class GetPropRoomDayCommand
    {
        /// <summary>
        /// Builds the payload for GETPROPROOMDAY command
        /// </summary>
        public static byte[] BuildGetPropRoomDayPayload(GetRoomDayRequest payload)
        {
            var cmdName = "GETPROPROOMDAY";
            var cmdNameBytes = Encoding.UTF8.GetBytes(cmdName);

            // Define fields
            var fields = new[]
            {
                new { Id = (ushort)0x01, Type = (byte)0x01, Data = Encoding.UTF8.GetBytes(payload.PropertyId) },
                new { Id = (ushort)0x02, Type = (byte)0x01, Data = Encoding.UTF8.GetBytes(payload.RoomType) },
                new { Id = (ushort)0x03, Type = (byte)0x01, Data = Encoding.UTF8.GetBytes(payload.Date) }
            };

            // Calculate total size
            var totalSize = 1 + cmdNameBytes.Length + 2; // cmdLen + cmdName + fieldCount
            foreach (var field in fields)
            {
                totalSize += 2 + 1 + 4 + field.Data.Length; // id(2) + type(1) + len(4) + data
            }

            var buffer = new byte[totalSize];
            var offset = 0;

            // Write command name length and name
            buffer[offset++] = (byte)cmdNameBytes.Length;
            Buffer.BlockCopy(cmdNameBytes, 0, buffer, offset, cmdNameBytes.Length);
            offset += cmdNameBytes.Length;

            // Write field count (uint16)
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset), (ushort)fields.Length);
            offset += 2;

            // Write each field
            foreach (var field in fields)
            {
                // Write field ID (uint16)
                BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset), field.Id);
                offset += 2;

                // Write field type
                buffer[offset++] = field.Type;

                // Write data length (uint32)
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset), (uint)field.Data.Length);
                offset += 4;

                // Write data
                Buffer.BlockCopy(field.Data, 0, buffer, offset, field.Data.Length);
                offset += field.Data.Length;
            }

            return buffer;
        }

        /// <summary>
        /// Parses the GETPROPROOMDAY response
        /// </summary>
        public static GetRoomDayResult ParseGetPropRoomDayResp(Codecs? codecs, string status, List<Protocol.Field> fields)
        {
            if (status == "SUCCESS")
            {
                if (fields == null || fields.Count < 5)
                    throw RoomzinException.From("not enough fields in response");

                var chunk = fields.GetRange(0, 5);

                // Validate rate_feature field length (now u32 = 4 bytes)
                if (chunk[4].Data.Length != 4)
                    throw RoomzinException.From($"invalid rate_feature field length: expected 4 bytes, got {chunk[4].Data.Length}");

                var rateFeatureMask = BinaryPrimitives.ReadUInt32LittleEndian(chunk[4].Data);

                return new GetRoomDayResult
                {
                    PropertyId = Encoding.UTF8.GetString(chunk[0].Data),
                    Date = Encoding.UTF8.GetString(chunk[1].Data),
                    Availability = chunk[2].Data[0],
                    FinalPrice = BinaryPrimitives.ReadUInt32LittleEndian(chunk[3].Data),
                    RateFeature = new List<string>(ProtocolHelper.BitmaskToRateFeatureStrings(codecs, rateFeatureMask))
                };
            }

            if (fields != null && fields.Count > 0 && fields[0].FieldType == 0x01)
            {
                var errorMsg = Encoding.UTF8.GetString(fields[0].Data);
                throw RoomzinException.From(errorMsg);
            }

            throw RoomzinException.From("RESPONSE_ERROR");
        }
    }
}