using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roomzin.Sdk.Internal.Protocol;
using Roomzin.Sdk.Types;
using Roomzin.Sdk.Types.Requests;

namespace Roomzin.Sdk.Internal.Command
{
    /// <summary>
    /// Command builder and parser for SEARCHPROP command
    /// </summary>
    public static class SearchPropCommand
    {
        /// <summary>
        /// Builds the payload for SEARCHPROP command
        /// </summary>
        public static byte[] BuildSearchPropPayload(SearchPropPayload payload)
        {
            var cmdName = "SEARCHPROP";
            var cmdNameBytes = Encoding.UTF8.GetBytes(cmdName);

            var fields = new List<(ushort Id, byte Type, byte[] Data)>
            {
                // Required field
                (0x01, 0x01, Encoding.UTF8.GetBytes(payload.Segment))
            };

            // Optional fields
            if (payload.Area != null)
                fields.Add((0x02, 0x01, Encoding.UTF8.GetBytes(payload.Area)));

            if (payload.Type != null)
                fields.Add((0x03, 0x01, Encoding.UTF8.GetBytes(payload.Type)));

            if (payload.Stars != null)
                fields.Add((0x04, 0x02, new byte[] { payload.Stars.Value }));

            if (payload.Category != null)
                fields.Add((0x05, 0x01, Encoding.UTF8.GetBytes(payload.Category)));

            if (payload.Amenities != null && payload.Amenities.Count > 0)
                fields.Add((0x06, 0x01, Encoding.UTF8.GetBytes(string.Join(",", payload.Amenities))));

            if (payload.Longitude != null)
                fields.Add((0x07, 0x03, ProtocolHelper.MakeF64(payload.Longitude.Value)));

            if (payload.Latitude != null)
                fields.Add((0x08, 0x03, ProtocolHelper.MakeF64(payload.Latitude.Value)));

            if (payload.Limit != null)
                fields.Add((0x09, 0x03, ProtocolHelper.MakeU64(payload.Limit.Value)));

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
        /// Parses the SEARCHPROP response
        /// </summary>
        public static List<string> ParseSearchPropResp(string status, List<Field> fields)
        {
            if (status != "SUCCESS")
            {
                if (fields != null && fields.Count > 0 && fields[0].Id == 0x01 && fields[0].FieldType == 0x01)
                {
                    var errorMsg = Encoding.UTF8.GetString(fields[0].Data);
                    throw RoomzinException.From(errorMsg);
                }
                throw RoomzinException.From("RESPONSE_ERROR");
            }

            var ids = new List<string>();
            if (fields != null)
            {
                for (int i = 0; i < fields.Count; i++)
                {
                    var f = fields[i];
                    if (f.Id != (ushort)(i + 1))
                        throw RoomzinException.From($"invalid field ID {f.Id}: expected {i + 1}");

                    if (f.FieldType != 0x01)
                        throw RoomzinException.From($"invalid field type at ID {f.Id}: expected 0x01");

                    ids.Add(ProtocolHelper.BytesToPropertyId(f.Data));
                }
            }

            return ids;
        }
    }
}