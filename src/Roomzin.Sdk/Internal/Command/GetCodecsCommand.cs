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
    /// Command builder and parser for GETCODECS command
    /// </summary>
    public static class GetCodecsCommand
    {
        /// <summary>
        /// Builds the payload for GETCODECS command
        /// </summary>
        public static byte[] BuildGetCodecsPayload()
        {
            var cmdName = "GETCODECS";
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
        /// Parses the GETCODECS response
        /// </summary>
        public static Codecs ParseGetCodecsResponse(string status, List<Protocol.Field> fields)
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

            // GETCODECS response should have exactly 1 field with type 0x09 (binary data)
            if (fields == null || fields.Count != 1)
            {
                throw RoomzinException.From($"invalid field count: expected 1 field, got {fields?.Count ?? 0}");
            }

            var field = fields[0];
            if (field.FieldType != 0x09)
            {
                throw RoomzinException.From($"expected binary data field type 0x09, got type {field.FieldType}");
            }

            // Parse the binary data using delimited format
            return ParseCodecsFromDelimited(field.Data);
        }

        private static Codecs ParseCodecsFromDelimited(byte[] data)
        {
            var dataStr = Encoding.UTF8.GetString(data);
            var rateFeatures = new List<string>();

            foreach (var rateFeature in dataStr.Split(','))
            {
                if (!string.IsNullOrEmpty(rateFeature))
                    rateFeatures.Add(rateFeature);
            }

            return new Codecs(rateFeatures);
        }
    }
}