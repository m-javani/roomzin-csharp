using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Roomzin.Sdk.Internal.Protocol;
using Roomzin.Sdk.Types;
using Roomzin.Sdk.Types.Requests;

namespace Roomzin.Sdk.Internal.Command
{
    /// <summary>
    /// Command builder and parser for SETROOMAVL command
    /// </summary>
    public static class SetRoomAvlCommand
    {
        /// <summary>
        /// Builds the payload for SETROOMAVL command
        /// </summary>
        public static byte[] BuildSetRoomAvlPayload(UpdRoomAvlPayload payload)
        {
            var cmdName = "SETROOMAVL";
            var cmdNameBytes = Encoding.UTF8.GetBytes(cmdName);

            var fields = new[]
            {
                new { Id = (ushort)0x01, Type = (byte)0x01, Data = Encoding.UTF8.GetBytes(payload.PropertyId) },
                new { Id = (ushort)0x02, Type = (byte)0x01, Data = Encoding.UTF8.GetBytes(payload.RoomType) },
                new { Id = (ushort)0x03, Type = (byte)0x01, Data = Encoding.UTF8.GetBytes(payload.Date) },
                new { Id = (ushort)0x04, Type = (byte)0x02, Data = new byte[] { payload.Amount } }
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
        /// Parses the SETROOMAVL response
        /// </summary>
        public static byte ParseSetRoomAvlResp(string status, List<Field> fields)
        {
            if (status == "SUCCESS")
            {
                if (fields == null || fields.Count == 0)
                    throw RoomzinException.From("RESPONSE_ERROR: missing or invalid scalar value");

                var b = fields[0].Data;
                if (b.Length != 1)
                    throw RoomzinException.From("RESPONSE_ERROR: missing or invalid scalar value");

                return b[0];
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