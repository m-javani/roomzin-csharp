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
    /// Command builder and parser for SETPROP command
    /// </summary>
    public static class SetPropCommand
    {
        /// <summary>
        /// Builds the payload for SETPROP command
        /// </summary>
        public static byte[] BuildSetPropPayload(SetPropPayload payload)
        {
            var cmdName = "SETPROP";
            var cmdNameBytes = Encoding.UTF8.GetBytes(cmdName);

            // amenities string
            var amenityStr = string.Join(",", payload.Amenities);

            var fields = new[]
            {
                new { Id = (ushort)0x01, Type = (byte)0x01, Data = Encoding.UTF8.GetBytes(payload.Segment) },
                new { Id = (ushort)0x02, Type = (byte)0x01, Data = Encoding.UTF8.GetBytes(payload.Area) },
                new { Id = (ushort)0x03, Type = (byte)0x01, Data = Encoding.UTF8.GetBytes(payload.PropertyId) },
                new { Id = (ushort)0x04, Type = (byte)0x01, Data = Encoding.UTF8.GetBytes(payload.PropertyType) },
                new { Id = (ushort)0x05, Type = (byte)0x01, Data = Encoding.UTF8.GetBytes(payload.Category) },
                new { Id = (ushort)0x06, Type = (byte)0x02, Data = new byte[] { payload.Stars } },
                new { Id = (ushort)0x07, Type = (byte)0x03, Data = ProtocolHelper.MakeF64(payload.Latitude) },
                new { Id = (ushort)0x08, Type = (byte)0x03, Data = ProtocolHelper.MakeF64(payload.Longitude) },
                new { Id = (ushort)0x09, Type = (byte)0x01, Data = Encoding.UTF8.GetBytes(amenityStr) }
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
        /// Parses the SETPROP response
        /// </summary>
        public static void ParseSetPropResp(string status, List<Field> fields)
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