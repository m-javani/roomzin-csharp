using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roomzin.Sdk.Internal.Protocol;
using Roomzin.Sdk.Types;
using Roomzin.Sdk.Types.Requests;
using Roomzin.Sdk.Types.Responses;

namespace Roomzin.Sdk.Internal.Command
{
    /// <summary>
    /// Command builder and parser for SEARCHAVAIL command
    /// </summary>
    public static class SearchAvailCommand
    {
        /// <summary>
        /// Builds the payload for SEARCHAVAIL command
        /// </summary>
        public static byte[] BuildSearchAvailPayload(SearchAvailPayload payload)
        {
            var cmdName = "SEARCHAVAIL";
            var cmdNameBytes = Encoding.UTF8.GetBytes(cmdName);

            var fields = new List<Field>();

            // Required fields
            fields.Add(new Field(0x01, 0x01, Encoding.UTF8.GetBytes(payload.Segment)));
            fields.Add(new Field(0x02, 0x01, Encoding.UTF8.GetBytes(payload.RoomType)));

            // Optional fields
            if (payload.Area != null)
                fields.Add(new Field(0x03, 0x01, Encoding.UTF8.GetBytes(payload.Area)));

            if (payload.PropertyId != null)
                fields.Add(new Field(0x04, 0x01, Encoding.UTF8.GetBytes(payload.PropertyId)));

            if (payload.Type != null)
                fields.Add(new Field(0x05, 0x01, Encoding.UTF8.GetBytes(payload.Type)));

            if (payload.Stars != null)
                fields.Add(new Field(0x06, 0x02, new byte[] { payload.Stars.Value }));

            if (payload.Category != null)
                fields.Add(new Field(0x07, 0x01, Encoding.UTF8.GetBytes(payload.Category)));

            if (payload.Amenities != null && payload.Amenities.Count > 0)
                fields.Add(new Field(0x08, 0x01, Encoding.UTF8.GetBytes(string.Join(",", payload.Amenities))));

            if (payload.Longitude != null)
                fields.Add(new Field(0x09, 0x03, ProtocolHelper.MakeF64(payload.Longitude.Value)));

            if (payload.Latitude != null)
                fields.Add(new Field(0x0A, 0x03, ProtocolHelper.MakeF64(payload.Latitude.Value)));

            if (payload.Date != null && payload.Date.Count > 0)
                fields.Add(new Field(0x0B, 0x01, Encoding.UTF8.GetBytes(string.Join(",", payload.Date))));

            if (payload.Availability != null)
                fields.Add(new Field(0x0C, 0x02, new byte[] { payload.Availability.Value }));

            if (payload.FinalPrice != null)
                fields.Add(new Field(0x0D, 0x03, ProtocolHelper.MakeU32(payload.FinalPrice.Value)));

            if (payload.RateFeature != null && payload.RateFeature.Count > 0)
                fields.Add(new Field(0x0E, 0x01, Encoding.UTF8.GetBytes(string.Join(",", payload.RateFeature))));

            if (payload.Limit != null)
                fields.Add(new Field(0x0F, 0x03, ProtocolHelper.MakeU64(payload.Limit.Value)));

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
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset), (ushort)fields.Count);
            offset += 2;

            // Write each field
            foreach (var field in fields)
            {
                // Write field ID (uint16)
                BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset), field.Id);
                offset += 2;

                // Write field type
                buffer[offset++] = field.FieldType;

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
        /// Parses the SEARCHAVAIL response
        /// </summary>
        public static List<PropertyAvail> ParseSearchAvailResp(Codecs? codecs, string status, List<Protocol.Field> fields)
        {
            if (status != "SUCCESS")
            {
                if (fields != null && fields.Count > 0 && fields[0].FieldType == 0x01)
                {
                    var errorMsg = Encoding.UTF8.GetString(fields[0].Data);
                    throw RoomzinException.From(errorMsg);
                }
                throw RoomzinException.From($"search failed with status={status}");
            }

            if (fields == null || fields.Count == 0)
                throw RoomzinException.From("expected num_days field");

            var numDaysField = fields[0];
            if (numDaysField.Id != 1 || numDaysField.FieldType != 0x02 || numDaysField.Data.Length != 2)
                throw RoomzinException.From("expected num_days field (id=1, type=0x02, len=2)");

            var numDays = BinaryPrimitives.ReadUInt16LittleEndian(numDaysField.Data);

            var outList = new List<PropertyAvail>();
            var idx = 1;

            while (idx < fields.Count)
            {
                var f = fields[idx];
                if (f.FieldType != 0x01)
                    throw RoomzinException.From($"expected property field at index={idx}, got type=0x{f.FieldType:X2} id={f.Id}");

                var propId = ProtocolHelper.BytesToPropertyId(f.Data);
                idx++;

                if (idx >= fields.Count)
                    throw RoomzinException.From($"property {propId} missing days data");

                var daysField = fields[idx];
                if (daysField.FieldType != 0x08)
                    throw RoomzinException.From($"expected days vector field for property {propId}, got type=0x{daysField.FieldType:X2}");

                idx++;

                var data = daysField.Data;
                if (data.Length < 2)
                    throw RoomzinException.From($"property {propId} days vector too short");

                var daysCount = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2));
                if (daysCount != numDays)
                    throw RoomzinException.From($"property {propId} days count mismatch: expected {numDays}, got {daysCount}");

                // Updated: 11 bytes per day (date 2 + avail 1 + price 4 + rate_feature u32 4)
                var expectedDataLen = 2 + (11 * daysCount);
                if (data.Length != expectedDataLen)
                    throw RoomzinException.From($"property {propId} days vector length mismatch: expected {expectedDataLen}, got {data.Length}");

                var days = new List<DayAvail>();
                var dataCursor = 2;

                for (int d = 0; d < daysCount; d++)
                {
                    if (dataCursor + 11 > data.Length)
                        throw RoomzinException.From($"property {propId} day {d} data truncated");

                    var datePacked = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(dataCursor, 2));
                    dataCursor += 2;

                    var availability = data[dataCursor];
                    dataCursor += 1;

                    var finalPrice = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(dataCursor, 4));
                    dataCursor += 4;

                    // Now reading full u32 for rate_feature
                    var rateFeatureMask = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(dataCursor, 4));
                    dataCursor += 4;

                    var dateStr = ProtocolHelper.U16ToDate(datePacked);

                    days.Add(new DayAvail
                    {
                        Date = dateStr,
                        Availability = availability,
                        FinalPrice = finalPrice,
                        RateFeature = new List<string>(ProtocolHelper.BitmaskToRateFeatureStrings(codecs, rateFeatureMask))
                    });
                }

                outList.Add(new PropertyAvail
                {
                    PropertyId = propId,
                    Days = days
                });
            }

            if (idx != fields.Count)
                throw RoomzinException.From($"extra fields after parsing: consumed={idx} total={fields.Count}");

            return outList;
        }
    }
}