using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Roomzin.Sdk.Types;

namespace Roomzin.Sdk.Internal.Command
{
    /// <summary>
    /// Command builder and parser for DELPROP command
    /// </summary>
    public static class DelPropCommand
    {
        /// <summary>
        /// Builds the payload for DELPROP command
        /// </summary>
        public static byte[] BuildDelPropPayload(string propertyId)
        {
            var cmdName = "DELPROP";
            var cmdNameBytes = Encoding.UTF8.GetBytes(cmdName);
            var propertyIdBytes = Encoding.UTF8.GetBytes(propertyId);

            // Calculate total size
            var totalSize = 1 + cmdNameBytes.Length + 2 + 2 + 1 + 4 + propertyIdBytes.Length;

            var buffer = new byte[totalSize];
            var offset = 0;

            // Write command name length and name
            buffer[offset++] = (byte)cmdNameBytes.Length;
            Buffer.BlockCopy(cmdNameBytes, 0, buffer, offset, cmdNameBytes.Length);
            offset += cmdNameBytes.Length;

            // Write field count (uint16) - one field
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset), 1);
            offset += 2;

            // Write field ID (uint16) - 0x01
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset), 0x01);
            offset += 2;

            // Write field type - string (0x01)
            buffer[offset++] = 0x01;

            // Write property ID length (uint32)
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset), (uint)propertyIdBytes.Length);
            offset += 4;

            // Write property ID
            Buffer.BlockCopy(propertyIdBytes, 0, buffer, offset, propertyIdBytes.Length);

            return buffer;
        }

        /// <summary>
        /// Parses the DELPROP response
        /// </summary>
        public static void ParseDelPropResp(string status, List<Protocol.Field> fields)
        {
            if (status == "SUCCESS")
            {
                return;
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