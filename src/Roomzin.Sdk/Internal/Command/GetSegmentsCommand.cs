using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Roomzin.Sdk.Internal.Protocol;
using Roomzin.Sdk.Types;
using Roomzin.Sdk.Types.Responses;

namespace Roomzin.Sdk.Internal.Command
{
    /// <summary>
    /// Command builder and parser for GETSEGMENTS command
    /// </summary>
    public static class GetSegmentsCommand
    {
        /// <summary>
        /// Builds the payload for GETSEGMENTS command
        /// </summary>
        public static byte[] BuildGetSegmentsPayload()
        {
            var cmdName = "GETSEGMENTS";
            var cmdNameBytes = Encoding.UTF8.GetBytes(cmdName);

            // Calculate total size: cmdLen(1) + cmdName + fieldCount(2)
            var totalSize = 1 + cmdNameBytes.Length + 2;

            var buffer = new byte[totalSize];
            var offset = 0;

            // Write command name length and name
            buffer[offset++] = (byte)cmdNameBytes.Length;
            Buffer.BlockCopy(cmdNameBytes, 0, buffer, offset, cmdNameBytes.Length);
            offset += cmdNameBytes.Length;

            // Write field count (uint16) = 0
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset), 0);

            return buffer;
        }

        /// <summary>
        /// Parses the GETSEGMENTS response
        /// </summary>
        public static List<SegmentInfo> ParseGetSegmentsResp(string status, List<Protocol.Field> fields)
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

            // Fields should come in pairs: segment string followed by propCount u32
            if (fields == null || fields.Count % 2 != 0)
            {
                throw RoomzinException.From("invalid field count: expected pairs of segment and propCount");
            }

            var list = new List<SegmentInfo>(fields.Count / 2);

            for (int i = 0; i < fields.Count; i += 2)
            {
                // First field should be segment (string type 0x01)
                if (fields[i].FieldType != 0x01)
                {
                    throw RoomzinException.From($"expected string segment at field {i}, got type {fields[i].FieldType}");
                }
                var segment = Encoding.UTF8.GetString(fields[i].Data);

                // Second field should be propCount (u32 type 0x03)
                if (i + 1 >= fields.Count)
                {
                    throw RoomzinException.From($"missing propCount field for segment {segment}");
                }
                if (fields[i + 1].FieldType != 0x03)
                {
                    throw RoomzinException.From($"expected u32 propCount at field {i + 1}, got type {fields[i + 1].FieldType}");
                }
                if (fields[i + 1].Data.Length != 4)
                {
                    throw RoomzinException.From($"invalid propCount length: expected 4 bytes, got {fields[i + 1].Data.Length}");
                }

                var propCount = BinaryPrimitives.ReadUInt32LittleEndian(fields[i + 1].Data);

                list.Add(new SegmentInfo
                {
                    Segment = segment,
                    PropCount = propCount
                });
            }

            return list;
        }
    }
}