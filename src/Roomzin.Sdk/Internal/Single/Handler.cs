using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Roomzin.Sdk.Internal.Protocol;
using Roomzin.Sdk.Types;

namespace Roomzin.Sdk.Internal.Single
{
    public class HandlerConfig
    {
        public string Addr { get; set; } = string.Empty;
        public int TcpPort { get; set; }
        public string AuthToken { get; set; } = string.Empty;
        public TimeSpan Timeout { get; set; }
        public TimeSpan KeepAlive { get; set; }
    }

    public class Handler : IDisposable
    {
        private readonly HandlerConfig _config;
        private Socket? _socket;
        private int _nextId;
        private readonly object _lock = new object();
        private bool _closed;
        private readonly Dictionary<uint, Channel<Protocol.RawResult>> _demux = new Dictionary<uint, Channel<Protocol.RawResult>>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public Action? OnReconnect { get; set; }

        public Handler(HandlerConfig config)
        {
            _config = config;
            Reconnect().Wait(_config.Timeout);
        }

        private async Task Reconnect()
        {
            Socket? oldSocket = null;

            lock (_lock)
            {
                oldSocket = _socket;
                _socket = null;
            }

            if (oldSocket != null)
            {
                try { oldSocket.Close(); } catch { }
            }

            var host = ParseHost(_config.Addr);
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                // Set socket options before connecting
                socket.NoDelay = true;
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                using var connectCts = new CancellationTokenSource(_config.Timeout);
                await socket.ConnectAsync(host, _config.TcpPort, connectCts.Token).ConfigureAwait(false);

                if (!await Handshake(socket, _config.AuthToken, _config.Timeout).ConfigureAwait(false))
                {
                    throw RoomzinException.From("Handshake failed");
                }

                lock (_lock)
                {
                    _socket = socket;
                }

                // Start reader exactly here
                _ = Task.Run(ReadLoop);
            }
            catch
            {
                socket.Close();
                throw;
            }
        }

        private static async Task<bool> Handshake(Socket socket, string token, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);

            // 1. Send framed login
            var payload = LoginHelper.BuildLoginPayload(token);
            var frame = FrameHelper.PrependHeader(0, payload);
            await socket.SendAsync(frame, SocketFlags.None, cts.Token).ConfigureAwait(false);

            // 2. Read plain-text reply
            var buffer = new byte[32]; // 12/13 bytes is enough
            var bytesRead = await socket.ReceiveAsync(buffer, SocketFlags.None, cts.Token).ConfigureAwait(false);

            var response = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
            return response switch
            {
                "LOGIN OK" => true,
                "LOGIN FAILED" => throw new UnauthorizedAccessException("Login failed: invalid token"),
                _ => throw RoomzinException.From($"Unexpected login reply: {response}")
            };
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_closed) return;

                _closed = true;
                _cancellationTokenSource.Cancel();

                _socket?.Close();

                foreach (var channel in _demux.Values)
                {
                    channel.Writer.TryComplete();
                }
                _demux.Clear();
            }

            _cancellationTokenSource.Dispose();
        }

        public uint NextId() => (uint)Interlocked.Increment(ref _nextId);

        public async Task<Protocol.RawResult> RoundTrip(uint clrId, byte[] payload, CancellationToken cancellationToken = default)
        {
            // Check connection and self-heal before locking
            bool needsReconnect = false;
            lock (_lock)
            {
                if (_closed)
                    throw RoomzinException.From("Connection closed");

                needsReconnect = _socket == null || !_socket.Connected;
            }

            if (needsReconnect)
            {
                await Reconnect().ConfigureAwait(false);
            }

            var channel = Channel.CreateBounded<Protocol.RawResult>(1);

            lock (_lock)
            {
                _demux[clrId] = channel;
            }

            try
            {
                var frame = FrameHelper.PrependHeader(clrId, payload);

                Socket? socket;
                lock (_lock)
                {
                    socket = _socket;
                }

                if (socket == null) throw RoomzinException.From("Socket is null");
                await socket.SendAsync(frame, SocketFlags.None, cancellationToken).ConfigureAwait(false);

                using var timeoutCts = new CancellationTokenSource(_config.Timeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    timeoutCts.Token, cancellationToken);

                var result = await channel.Reader.ReadAsync(linkedCts.Token).ConfigureAwait(false);
                return result;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Cleanup(clrId);
                await Reconnect().ConfigureAwait(false);
                throw new TimeoutException("Operation timed out");
            }
            catch
            {
                Cleanup(clrId);
                await Reconnect().ConfigureAwait(false);
                throw;
            }
        }

        private void Cleanup(uint clrId)
        {
            lock (_lock)
            {
                _demux.Remove(clrId);
            }
        }

        private async Task ReadLoop()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    Socket? socket;
                    lock (_lock)
                    {
                        socket = _socket;
                    }

                    if (socket == null) break;

                    var (header, payload) = await FrameHelper.DrainFrameAsync(socket);
                    var fields = FrameHelper.ParseFields(payload, header.FieldCnt);

                    lock (_lock)
                    {
                        if (_demux.TryGetValue(header.ClrId, out var channel))
                        {
                            _demux.Remove(header.ClrId);
                            channel.Writer.TryWrite(new Protocol.RawResult { Status = header.Status, Fields = fields });
                            channel.Writer.TryComplete();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    OnReconnect?.Invoke();
                    FailAll();
                    break;
                }
            }
        }

        private void FailAll()
        {
            lock (_lock)
            {
                foreach (var channel in _demux.Values)
                {
                    channel.Writer.TryWrite(new Protocol.RawResult());
                    channel.Writer.TryComplete();
                }
                _demux.Clear();
            }
        }

        private static string ParseHost(string addr)
        {
            if (string.IsNullOrEmpty(addr)) return "localhost";

            try
            {
                var uri = new Uri($"tcp://{addr}");
                return uri.Host;
            }
            catch
            {
                // If URI parsing fails, assume it's host-only (no port)
                return addr;
            }
        }
    }

    namespace Protocol
    {
        public class RawResult
        {
            public string Status { get; set; } = string.Empty;
            public List<Field> Fields { get; set; } = new List<Field>();
        }

        public static class ProtocolHelper
        {
            public static byte[] BuildLoginPayload(string token)
            {
                // Implementation depends on your login protocol
                // This is a placeholder - replace with actual login payload construction
                var tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);
                var payload = new byte[1 + tokenBytes.Length];
                payload[0] = (byte)tokenBytes.Length;
                Buffer.BlockCopy(tokenBytes, 0, payload, 1, tokenBytes.Length);
                return payload;
            }
        }
    }
}