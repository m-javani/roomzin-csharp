using System;
using System.Buffers.Binary;
using System.Text;
using Roomzin.Sdk.Types;

namespace Roomzin.Sdk.Internal.Protocol
{
    /// <summary>
    /// Helper methods for building login payload
    /// </summary>
    public static class LoginHelper
    {
        /// <summary>
        /// Builds the login payload for AUTH command
        /// </summary>
        public static byte[] BuildLoginPayload(string token)
        {
            if (string.IsNullOrEmpty(token))
                throw RoomzinException.From("Token cannot be null or empty");

            var cmdName = "LOGIN";
            var cmdNameBytes = Encoding.UTF8.GetBytes(cmdName);

            // Calculate total size to pre-allocate buffer
            var totalSize = 1 + cmdNameBytes.Length + 2 + 2 + 1 + 4 + Encoding.UTF8.GetByteCount(token);
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

            // Write token length (uint32)
            var tokenBytes = Encoding.UTF8.GetBytes(token);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset), (uint)tokenBytes.Length);
            offset += 4;

            // Write token
            Buffer.BlockCopy(tokenBytes, 0, buffer, offset, tokenBytes.Length);

            return buffer;
        }
    }
}