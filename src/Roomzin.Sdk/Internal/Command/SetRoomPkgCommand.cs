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
    /// Command builder and parser for SETROOMPKG command
    /// </summary>
    public static class SetRoomPkgCommand
    {
        /// <summary>
        /// Builds the payload for SETROOMPKG command
        /// </summary>
        public static byte[] BuildSetRoomPkgPayload(SetRoomPkgPayload payload)
        {
            if (string.IsNullOrEmpty(payload.PropertyId) || string.IsNullOrEmpty(payload.RoomType) || string.IsNullOrEmpty(payload.Date))
                throw RoomzinException.From("missing required fields");

            var cmdName = "SETROOMPKG";
            var cmdNameBytes = Encoding.UTF8.GetBytes(cmdName);

            var fields = new List<(ushort Id, byte Type, byte[] Data)>
            {
                (0x01, 0x01, Encoding.UTF8.GetBytes(payload.PropertyId)),
                (0x02, 0x01, Encoding.UTF8.GetBytes(payload.RoomType)),
                (0x03, 0x01, Encoding.UTF8.GetBytes(payload.Date))
            };

            // Optional fields
            if (payload.Availability != null)
                fields.Add((0x04, 0x02, new byte[] { payload.Availability.Value }));

            if (payload.FinalPrice != null)
            {
                var b = new byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(b, payload.FinalPrice.Value);
                fields.Add((0x05, 0x03, b));
            }

            if (payload.RateFeature != null && payload.RateFeature.Count > 0)
                fields.Add((0x06, 0x01, Encoding.UTF8.GetBytes(string.Join(",", payload.RateFeature))));

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
        /// Parses the SETROOMPKG response
        /// </summary>
        public static void ParseSetRoomPkgResp(string status, List<Field> fields)
        {
            if (status == "SUCCESS")
            {
                return;
            }

            if (fields != null && fields.Count > 0)
            {
                var errorMsg = Encoding.UTF8.GetString(fields[0].Data);
                throw RoomzinException.From(errorMsg); ;
            }

            throw RoomzinException.From("RESPONSE_ERROR");
        }
    }
}