using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using Roomzin.Sdk.Types;

namespace Roomzin.Sdk.Internal.Protocol
{
    /// <summary>
    /// Protocol Codecs
    /// </summary>
    public static class Protocol
    {
        public const byte MagicByte = 0xFF;
        public const string LoginOk = "LOGIN OK";
        public const string LoginFailed = "LOGIN FAILED";
    }

    /// <summary>
    /// Represents a single field in the protocol response
    /// </summary>
    public class Field
    {
        /// <summary>
        /// Field identifier
        /// </summary>
        public ushort Id { get; set; }

        /// <summary>
        /// Field type (0x01 = string, 0x02 = byte, 0x03 = uint32, etc.)
        /// </summary>
        public byte FieldType { get; set; }

        /// <summary>
        /// Field data bytes
        /// </summary>
        public byte[] Data { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Initializes a new Field
        /// </summary>
        public Field() { }

        /// <summary>
        /// Initializes a new Field with values
        /// </summary>
        public Field(ushort id, byte fieldType, byte[] data)
        {
            Id = id;
            FieldType = fieldType;
            Data = data ?? Array.Empty<byte>();
        }
    }

    /// <summary>
    /// Header is the decoded fixed part of the frame
    /// </summary>
    public class Header
    {
        public uint ClrId { get; set; }
        public string Status { get; set; } = string.Empty;
        public ushort FieldCnt { get; set; }

        public Header() { }

        public Header(uint clrId, string status, ushort fieldCnt)
        {
            ClrId = clrId;
            Status = status;
            FieldCnt = fieldCnt;
        }
    }

    /// <summary>
    /// Frame handling methods for the binary protocol
    /// </summary>
    public static class FrameHelper
    {
        /// <summary>
        /// PrependHeader takes the already-serialised payload and returns a complete frame ready to write to the server
        /// Format: | magic(1) | clrid(4) | totalLen(4) | payload |
        /// totalLen == len(payload)
        /// </summary>
        public static byte[] PrependHeader(uint clrId, byte[] payload)
        {
            var totalLen = (uint)payload.Length;
            var outBuffer = new byte[9 + totalLen];

            outBuffer[0] = Protocol.MagicByte; // 0xFF
            BinaryPrimitives.WriteUInt32LittleEndian(outBuffer.AsSpan(1), clrId);
            BinaryPrimitives.WriteUInt32LittleEndian(outBuffer.AsSpan(5), totalLen);

            Buffer.BlockCopy(payload, 0, outBuffer, 9, payload.Length);
            return outBuffer;
        }

        /// <summary>
        /// DrainFrame reads a full frame and returns header + raw payload
        /// The payload starts at [statusLen][status][fieldCount]...fields
        /// </summary>
        public static async Task<(Header header, byte[] payload)> DrainFrameAsync(Socket socket)
        {
            // Read fixed header: [0xFF][ClrID:4][payloadLen:4]
            var fix = new byte[9];
            var totalRead = 0;
            while (totalRead < 9)
            {
                var bytesRead = await socket.ReceiveAsync(fix.AsMemory(totalRead, 9 - totalRead), SocketFlags.None);
                if (bytesRead == 0)
                    throw RoomzinException.From("incomplete frame");
                totalRead += bytesRead;
            }

            // Frame layout: [0xFF][ClrID:4][payloadLen:4]
            if (fix[0] != Protocol.MagicByte)
                throw RoomzinException.From($"bad magic byte: got 0x{fix[0]:X2}");

            var clrId = BinaryPrimitives.ReadUInt32LittleEndian(fix.AsSpan(1));
            var payloadLen = BinaryPrimitives.ReadUInt32LittleEndian(fix.AsSpan(5));

            // Read payload - handle partial reads
            var payload = new byte[payloadLen];
            totalRead = 0;
            while (totalRead < payloadLen)
            {
                var bytesRead = await socket.ReceiveAsync(payload.AsMemory(totalRead, (int)payloadLen - totalRead), SocketFlags.None);
                if (bytesRead == 0)
                    throw RoomzinException.From("incomplete frame payload");
                totalRead += bytesRead;
            }

            if (payload.Length < 1)
                throw RoomzinException.From("short frame: no statusLen");

            var statusLen = payload[0];
            if (payload.Length < 1 + statusLen + 2)
                throw RoomzinException.From("short frame: missing status or fieldCount");

            var status = Encoding.UTF8.GetString(payload, 1, statusLen);
            var fieldCnt = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(1 + statusLen));

            var header = new Header(clrId, status, fieldCnt);

            // Return the ENTIRE payload, not just the fields part
            return (header, payload);
        }

        /// <summary>
        /// ParseFields decodes the flat field array from payload
        /// The slice must start at the first field (not status)
        /// </summary>
        public static List<Field> ParseFields(byte[] fullPayload, ushort fieldCount)
        {
            // Extract the fields data from the full payload
            var statusLen = fullPayload[0];
            var fieldsStart = 1 + statusLen + 2; // Skip statusLen byte, status string, and fieldCount

            if (fieldsStart > fullPayload.Length)
                throw RoomzinException.From("payload too short to contain fields data");

            var data = new byte[fullPayload.Length - fieldsStart];
            Buffer.BlockCopy(fullPayload, fieldsStart, data, 0, data.Length);

            var fields = new List<Field>();
            var offset = 0;

            for (int i = 0; i < fieldCount; i++)
            {
                if (offset + 7 > data.Length)
                    throw RoomzinException.From($"short frame: not enough bytes for field header at field {i}");

                var id = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));
                var fieldType = data[offset + 2];
                var length = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 3));
                offset += 7;

                if (offset + (int)length > data.Length)
                    throw RoomzinException.From($"short frame: not enough data for field payload (field {i}, need {length}, have {data.Length - offset})");

                var fieldData = new byte[length];
                Buffer.BlockCopy(data, offset, fieldData, 0, (int)length);

                fields.Add(new Field(id, fieldType, fieldData));
                offset += (int)length;
            }

            // Rust version enforces: all fields must be consumed
            if (offset != data.Length)
                throw RoomzinException.From($"extra {data.Length - offset} bytes after parsing fields");

            return fields;
        }
    }
}