using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Roomzin.Sdk.Types;

namespace Roomzin.Sdk.Internal.Command
{
    /// <summary>
    /// Command builder and parser for DELSEGMENT command
    /// </summary>
    public static class DelSegmentCommand
    {
        /// <summary>
        /// Builds the payload for DELSEGMENT command
        /// </summary>
        public static byte[] BuildDelSegmentPayload(string segment)
        {
            var cmdName = "DELSEGMENT";
            var cmdNameBytes = Encoding.UTF8.GetBytes(cmdName);
            var segmentBytes = Encoding.UTF8.GetBytes(segment);

            // Calculate total size
            var totalSize = 1 + cmdNameBytes.Length + 2 + 2 + 1 + 4 + segmentBytes.Length;

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

            // Write segment length (uint32)
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset), (uint)segmentBytes.Length);
            offset += 4;

            // Write segment
            Buffer.BlockCopy(segmentBytes, 0, buffer, offset, segmentBytes.Length);

            return buffer;
        }

        /// <summary>
        /// Parses the DELSEGMENT response
        /// </summary>
        public static void ParseDelSegmentResp(string status, List<Protocol.Field> fields)
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