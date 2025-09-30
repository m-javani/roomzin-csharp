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
    /// Command builder and parser for PROPROOMDATELIST command
    /// </summary>
    public static class PropRoomDateListCommand
    {
        /// <summary>
        /// Builds the payload for PROPROOMDATELIST command
        /// </summary>
        public static byte[] BuildPropRoomDateListPayload(PropRoomDateListPayload payload)
        {
            var cmdName = "PROPROOMDATELIST";
            var cmdNameBytes = Encoding.UTF8.GetBytes(cmdName);

            // Define fields
            var fields = new[]
            {
                new { Id = (ushort)0x01, Type = (byte)0x01, Data = Encoding.UTF8.GetBytes(payload.PropertyId) },
                new { Id = (ushort)0x02, Type = (byte)0x01, Data = Encoding.UTF8.GetBytes(payload.RoomType) }
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
        /// Parses the PROPROOMDATELIST response
        /// </summary>
        public static List<string> ParsePropRoomDateListResp(string status, List<Protocol.Field> fields)
        {
            if (status != "SUCCESS")
            {
                if (fields != null && fields.Count > 0 && fields[0].FieldType == 0x01)
                {
                    var errorMsg = Encoding.UTF8.GetString(fields[0].Data);
                    throw RoomzinException.From(errorMsg);
                }
                throw RoomzinException.From("RESPONSE_ERROR");
            }

            var outList = new List<string>();
            if (fields != null)
            {
                foreach (var field in fields)
                {
                    var s = Encoding.UTF8.GetString(field.Data);
                    if (!string.IsNullOrEmpty(s))
                    {
                        outList.Add(s);
                    }
                }
            }

            outList.Sort();
            return outList;
        }
    }
}