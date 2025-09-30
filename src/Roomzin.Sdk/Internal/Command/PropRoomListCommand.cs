using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Roomzin.Sdk.Internal.Protocol;
using Roomzin.Sdk.Types;

namespace Roomzin.Sdk.Internal.Command
{
    /// <summary>
    /// Command builder and parser for PROPROOMLIST command
    /// </summary>
    public static class PropRoomListCommand
    {
        /// <summary>
        /// Builds the payload for PROPROOMLIST command
        /// </summary>
        public static byte[] BuildPropRoomListPayload(string propertyId)
        {
            var cmdName = "PROPROOMLIST";
            var cmdNameBytes = Encoding.UTF8.GetBytes(cmdName);
            var propertyIdBytes = Encoding.UTF8.GetBytes(propertyId);

            // Calculate total size
            var totalSize = 1 + cmdNameBytes.Length + 2 + // cmdLen + cmdName + fieldCount
                           2 + 1 + 4 + propertyIdBytes.Length; // id(2) + type(1) + len(4) + data

            var buffer = new byte[totalSize];
            var offset = 0;

            // Write command name length and name
            buffer[offset++] = (byte)cmdNameBytes.Length;
            Buffer.BlockCopy(cmdNameBytes, 0, buffer, offset, cmdNameBytes.Length);
            offset += cmdNameBytes.Length;

            // Write field count (uint16) = 1
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset), 1);
            offset += 2;

            // Write field ID (uint16)
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset), 0x01);
            offset += 2;

            // Write field type (string)
            buffer[offset++] = 0x01;

            // Write data length (uint32)
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset), (uint)propertyIdBytes.Length);
            offset += 4;

            // Write data
            Buffer.BlockCopy(propertyIdBytes, 0, buffer, offset, propertyIdBytes.Length);

            return buffer;
        }

        /// <summary>
        /// Parses the PROPROOMLIST response
        /// </summary>
        public static List<string> ParsePropRoomListResp(string status, List<Protocol.Field> fields)
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

            var list = new List<string>();
            if (fields != null)
            {
                foreach (var field in fields)
                {
                    list.Add(Encoding.UTF8.GetString(field.Data));
                }
            }

            return list;
        }
    }
}