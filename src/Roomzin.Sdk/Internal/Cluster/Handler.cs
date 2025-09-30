using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Roomzin.Sdk.Internal.Protocol;
using Roomzin.Sdk.Types;

namespace Roomzin.Sdk.Internal.Cluster
{
    public class Handler : IDisposable
    {
        private readonly Config _cfg;
        private readonly DiscoveryMap _discoveryMap;
        private readonly LeaderHandler _leaderHandler;
        private readonly FollowersHandler _followersHandler;
        private readonly ObjectPool<Request> _reqPool;
        private readonly ObjectPool<Channel<RawResult>> _respChanPool;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public void SetOnReconnectCallback(Action callback)
        {
            _leaderHandler.OnReconnect = callback;
        }

        public Handler(Config cfg)
        {
            _cfg = cfg;
            _discoveryMap = BuildDiscoveryMap(cfg);
            _leaderHandler = new LeaderHandler(cfg, _discoveryMap);
            _followersHandler = new FollowersHandler(cfg, _discoveryMap);
            _reqPool = new ObjectPool<Request>(() => new Request());
            _respChanPool = new ObjectPool<Channel<RawResult>>(() => Channel.CreateBounded<RawResult>(1));

            // Start discovery task if in HTTP mode
            if (!string.IsNullOrEmpty(cfg.DiscoveryAddr))
            {
                _ = StartDiscoveryTask(_cts.Token);
            }
        }

        private DiscoveryMap BuildDiscoveryMap(Config cfg)
        {
            var dm = new DiscoveryMap();

            if (!string.IsNullOrEmpty(cfg.DiscoveryAddr))
            {
                // HTTP mode: start empty, populated later by background task
                return dm;
            }

            // Static mode
            if (cfg.StaticDiscovery == null || cfg.StaticDiscovery.Count == 0)
            {
                throw RoomzinException.From("static discovery enabled but StaticDiscovery is empty");
            }

            dm.SetStatic(cfg.StaticDiscovery, cfg.TcpPort, cfg.ApiPort);
            return dm;
        }

        private void UpdateDiscoveryMap(List<NodeAddr> nodes)
        {
            _discoveryMap.Update(nodes, _cfg.TcpPort, _cfg.ApiPort);
        }

        private async Task<List<NodeAddr>> FetchExternalDiscoveryAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_cfg.DiscoveryAddr))
            {
                throw RoomzinException.From("discovery address not configured");
            }

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            try
            {
                var response = await client.GetAsync(_cfg.DiscoveryAddr, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    throw RoomzinException.From($"discovery service returned status: {response.StatusCode}");
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var discoveryResponse = System.Text.Json.JsonSerializer.Deserialize<DiscoveryResponse>(json, options);
                if (discoveryResponse?.Nodes == null || discoveryResponse.Nodes.Count == 0)
                {
                    throw RoomzinException.From("discovery service returned empty node list");
                }

                return discoveryResponse.Nodes;
            }
            catch (Exception e)
            {
                throw RoomzinException.From($"Failed to fetch discovery: {e.Message}");
            }
        }

        private async Task StartDiscoveryTask(CancellationToken cancellationToken)
        {
            var interval = _cfg.NodeProbeInterval;
            if (interval <= TimeSpan.Zero)
            {
                interval = TimeSpan.FromSeconds(2);
            }

            // Initial fetch on startup
            try
            {
                var nodes = await FetchExternalDiscoveryAsync(cancellationToken);
                if (nodes.Count > 0)
                {
                    UpdateDiscoveryMap(nodes);
                }
            }
            catch
            {
                // silent fail - keep using empty map
            }

            // Periodic fetch
            using var timer = new PeriodicTimer(interval);
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (await timer.WaitForNextTickAsync(cancellationToken))
                    {
                        var nodes = await FetchExternalDiscoveryAsync(cancellationToken);
                        if (nodes.Count > 0)
                        {
                            UpdateDiscoveryMap(nodes);
                        }
                    }
                }
                catch
                {
                    // silent fail - keep using existing map
                }
            }
        }

        private class DiscoveryResponse
        {
            public List<NodeAddr> Nodes { get; set; } = new();
        }

        public void Start()
        {
            Task.Run(() => _leaderHandler.LeaderSendWorker(_cts.Token));
            Task.Run(() => _followersHandler.FollowerSendWorker(_cts.Token));

            _ = _leaderHandler.LeaderSyncWorker(_cts.Token);
            _ = _followersHandler.FollowerSyncWorker(_cts.Token);
            _ = CleanupWorker(_cts.Token);
        }

        private async Task CleanupWorker(CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(_cfg.Timeout);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                _leaderHandler.Connection?.DemuxMap.Cleanup(_cfg.Timeout * 2);
            }
        }

        public async Task<RawResult> ExecuteAsync(bool isWrite, byte[] payload, CancellationToken cancellationToken = default)
        {
            if (payload == null || payload.Length == 0)
                throw RoomzinException.From("Payload should not be empty");

            if (isWrite && _leaderHandler.Connection == null)
            {
                throw RoomzinException.From("cluster has no leader");
            }

            var req = _reqPool.Get();
            var respChan = _respChanPool.Get();

            try
            {
                req.Payload = payload;
                req.CancellationToken = cancellationToken;
                req.ResponseChannel = respChan;
                req.CorrelationId = 0;

                var handlerChannel = isWrite ? _leaderHandler.RequestChannel : _followersHandler.RequestChannel;

                const int maxRetries = 5;
                var attempts = 0;

                async Task<bool> SendAsync()
                {
                    if (handlerChannel.Writer.TryWrite(req))
                    {
                        return true;
                    }
                    return false;
                }

                if (!await SendAsync())
                {
                    throw RoomzinException.From("Request queue full");
                }

                while (true)
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                    try
                    {
                        var res = await respChan.Reader.ReadAsync(linkedCts.Token);

                        if (res.Status == "SUCCESS")
                            return res;

                        var errorMsg = res.Fields.Count > 0 ? System.Text.Encoding.UTF8.GetString(res.Fields[0].Data) : res.Status;
                        if (attempts >= maxRetries)
                            return res;

                        switch (errorMsg)
                        {
                            case "405":
                            case "308":
                                break;
                            case "503":
                            case "429":
                                await Task.Delay(TimeSpan.FromMilliseconds(attempts * 100), cancellationToken);
                                attempts++;
                                continue;
                            default:
                                return res;
                        }

                        req.CorrelationId = 0;
                        if (!await SendAsync())
                            throw RoomzinException.From($"Retry failed after {errorMsg}");
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        if (attempts >= maxRetries)
                            throw new TimeoutException("Operation timed out");

                        attempts++;
                        req.CorrelationId = 0;
                        if (!await SendAsync())
                            throw new TimeoutException("Retry failed after timeout");
                    }
                }
            }
            finally
            {
                _reqPool.Return(req);
                _respChanPool.Return(respChan);
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _leaderHandler.Dispose();
            _followersHandler.Dispose();
        }
    }

    public class Request
    {
        public byte[] Payload { get; set; } = Array.Empty<byte>();
        public Channel<RawResult> ResponseChannel { get; set; } = null!;
        public CancellationToken CancellationToken { get; set; }
        public uint CorrelationId { get; set; }
    }

    public class DemuxMap
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private readonly Dictionary<uint, DemuxEntry> _entries = new Dictionary<uint, DemuxEntry>();

        public void Store(uint correlationId, Channel<RawResult> channel)
        {
            _lock.EnterWriteLock();
            try
            {
                _entries[correlationId] = new DemuxEntry { Channel = channel, SendTime = DateTime.UtcNow };
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public (Channel<RawResult> Channel, DateTime SendTime, bool Found) LoadRemove(uint correlationId)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_entries.TryGetValue(correlationId, out var entry))
                {
                    _entries.Remove(correlationId);
                    return (entry.Channel, entry.SendTime, true);
                }
                return (null!, DateTime.MinValue, false);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Cleanup(TimeSpan maxAge)
        {
            var threshold = DateTime.UtcNow - maxAge;
            _lock.EnterWriteLock();
            try
            {
                var toRemove = _entries.Where(kv => kv.Value.SendTime < threshold).ToList();
                foreach (var kv in toRemove)
                {
                    var timeoutResult = new RawResult
                    {
                        Status = "ERROR",
                        Fields = new List<Field>
                        {
                            new Field
                            {
                                Data = System.Text.Encoding.UTF8.GetBytes("Timeout")
                            }
                        }
                    };

                    kv.Value.Channel.Writer.TryWrite(timeoutResult);
                    _entries.Remove(kv.Key);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }

    public class DemuxEntry
    {
        public Channel<RawResult> Channel { get; set; } = null!;
        public DateTime SendTime { get; set; }
    }

    public class Connection : IDisposable
    {
        private readonly Socket _socket;
        public readonly Channel<byte[]> sendQueue;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly AtomicBoolean _closed = new AtomicBoolean();
        private readonly Once _closer = new Once();

        private readonly TaskCompletionSource<bool> _readyTcs = new TaskCompletionSource<bool>();
        public bool IsReady => _readyTcs.Task.IsCompleted && !IsClosed;

        public DemuxMap DemuxMap { get; }
        public Config Config { get; }
        public string Address { get; }

        public Connection(string address, Config cfg, DemuxMap demuxMap)
        {
            var endpoint = new DnsEndPoint(address, cfg.TcpPort);
            _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _socket.Connect(endpoint);

            var loginPayload = LoginHelper.BuildLoginPayload(cfg.AuthToken);
            var frame = FrameHelper.PrependHeader(0, loginPayload);
            _socket.Send(frame);

            const string loginRespOk = "LOGIN OK";
            var buffer = new byte[loginRespOk.Length];
            var received = _socket.Receive(buffer);
            if (received != loginRespOk.Length || System.Text.Encoding.UTF8.GetString(buffer) != loginRespOk)
                throw new UnauthorizedAccessException("Login failed");

            DemuxMap = demuxMap;
            Config = cfg;
            Address = address;
            sendQueue = Channel.CreateBounded<byte[]>(8192);
        }

        public async Task Activate()
        {
            var writeTask = WriteLoop();
            var readTask = ReadLoop();

            await Task.Delay(50).ConfigureAwait(false);
            _readyTcs.TrySetResult(true);
        }

        private async Task WriteLoop()
        {
            try
            {
                await foreach (var data in sendQueue.Reader.ReadAllAsync(_cts.Token))
                {
                    var sent = await _socket.SendAsync(data, SocketFlags.None, _cts.Token);
                }
            }
            catch
            {
                Close();
            }
        }

        private async Task ReadLoop()
        {
            var buffer = new byte[4096];
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var (header, payload) = await FrameHelper.DrainFrameAsync(_socket);
                    var (channel, sendTime, found) = DemuxMap.LoadRemove(header.ClrId);

                    if (!found)
                    {
                        Close();
                        return;
                    }

                    var fields = FrameHelper.ParseFields(payload, header.FieldCnt);

                    if (header.Status == "ERROR" && fields.Count > 0)
                    {
                        var errorCode = System.Text.Encoding.UTF8.GetString(fields[0].Data);
                        switch (errorCode)
                        {
                            case "308":
                            case "405":
                                Close();
                                return;
                            case "503":
                                Close();
                                break;
                            case "429":
                                break;
                        }
                    }

                    channel.Writer.TryWrite(new RawResult { Status = header.Status, Fields = fields });
                }
                catch
                {
                    Close();
                    return;
                }
            }
        }

        public void Close()
        {
            _closer.Do(() =>
            {
                _closed.Value = true;
                _cts.Cancel();
                sendQueue.Writer.TryComplete();
                _socket.Close();
            });
        }

        public bool IsClosed => _closed.Value;

        public void Dispose() => Close();
    }

    public class LeaderHandler : IDisposable
    {
        private readonly Config _cfg;
        private readonly DiscoveryMap _discoveryMap;
        private readonly Channel<Request> _reqChannel;
        private readonly AtomicUInt _correlationId = new AtomicUInt();
        private readonly ReaderWriterLockSlim _connLock = new ReaderWriterLockSlim();
        private Connection? _connection;

        public Action? OnReconnect { get; set; }

        public LeaderHandler(Config cfg, DiscoveryMap discoveryMap)
        {
            _cfg = cfg;
            _discoveryMap = discoveryMap;
            _reqChannel = Channel.CreateBounded<Request>(1024);
        }

        public Channel<Request> RequestChannel => _reqChannel;
        public Connection? Connection => _connection;

        public async Task LeaderSyncWorker(CancellationToken cancellationToken)
        {
            var backoff = TimeSpan.FromMilliseconds(100);
            var random = new Random();

            while (!cancellationToken.IsCancellationRequested)
            {
                var conn = _connection;
                if (conn == null || conn.IsClosed)
                {
                    OnReconnect?.Invoke();

                    try
                    {
                        await ReconnectLeader();
                    }
                    catch
                    {
                    }
                }

                await Task.Delay(backoff + TimeSpan.FromMilliseconds(random.Next(50)), cancellationToken);
                if (backoff < TimeSpan.FromSeconds(1))
                    backoff *= 2;
                if (backoff > TimeSpan.FromSeconds(2))
                    backoff = TimeSpan.FromSeconds(2);
            }
        }

        public async Task LeaderSendWorker(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    while (await _reqChannel.Reader.WaitToReadAsync(cancellationToken))
                    {
                        if (_reqChannel.Reader.TryRead(out var req))
                        {
                            Connection? conn;
                            while (true)
                            {
                                conn = _connection;
                                if (conn != null && !conn.IsClosed && conn.sendQueue != null)
                                    break;

                                if (cancellationToken.IsCancellationRequested)
                                    return;

                                await Task.Delay(100, cancellationToken);
                            }

                            var corrId = _correlationId.Increment();
                            conn.DemuxMap.Store(corrId, req.ResponseChannel);
                            var frame = FrameHelper.PrependHeader(corrId, req.Payload);

                            await conn.sendQueue.Writer.WriteAsync(frame, cancellationToken);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            catch
            {
            }
        }

        private async Task ReconnectLeader()
        {
            try
            {
                var (leader, followers) = await ClusterHelper.GetClusterInfoAsync(_cfg, _discoveryMap, CancellationToken.None);

                if (leader == null)
                {
                    return;
                }

                var demuxMap = _connection?.DemuxMap ?? new DemuxMap();
                var conn = new Connection(leader.Addr, _cfg, demuxMap);

                _connLock.EnterWriteLock();
                try
                {
                    _connection?.Dispose();
                    _connection = conn;
                }
                finally
                {
                    _connLock.ExitWriteLock();
                }

                await conn.Activate().ConfigureAwait(false);
            }
            catch
            {
                throw;
            }
        }

        public void Dispose()
        {
            _connection?.Dispose();
            _reqChannel.Writer.TryComplete();
        }
    }

    public class FollowersHandler : IDisposable
    {
        private readonly Config _cfg;
        private readonly DiscoveryMap _discoveryMap;
        private readonly Channel<Request> _reqChannel;
        private readonly AtomicUInt _correlationId = new AtomicUInt();
        private readonly ReaderWriterLockSlim _connLock = new ReaderWriterLockSlim();
        private readonly List<Connection> _connections = new List<Connection>();
        private readonly AtomicInt _rrIndex = new AtomicInt();

        public FollowersHandler(Config cfg, DiscoveryMap discoveryMap)
        {
            _cfg = cfg;
            _discoveryMap = discoveryMap;
            _reqChannel = Channel.CreateBounded<Request>(1024);
            _rrIndex = new AtomicInt();
        }

        public Channel<Request> RequestChannel => _reqChannel;

        public async Task FollowerSendWorker(CancellationToken cancellationToken)
        {
            try
            {
                while (await _reqChannel.Reader.WaitToReadAsync(cancellationToken))
                {
                    while (_reqChannel.Reader.TryRead(out var req))
                    {
                        Connection? conn;
                        while (true)
                        {
                            conn = GetNextConnection();
                            if (conn != null && !conn.IsClosed && conn.sendQueue != null)
                            {
                                break;
                            }

                            if (cancellationToken.IsCancellationRequested)
                                return;

                            await Task.Delay(100, cancellationToken);
                        }

                        var corrId = _correlationId.Increment();
                        conn.DemuxMap.Store(corrId, req.ResponseChannel);
                        var frame = FrameHelper.PrependHeader(corrId, req.Payload);

                        await conn.sendQueue.Writer.WriteAsync(frame, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
        }

        public async Task FollowerSyncWorker(CancellationToken cancellationToken)
        {
            using var probeTimer = new PeriodicTimer(_cfg.NodeProbeInterval);
            using var fastTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));

            while (!cancellationToken.IsCancellationRequested)
            {
                var probeTask = probeTimer.WaitForNextTickAsync(cancellationToken).AsTask();
                var fastTask = fastTimer.WaitForNextTickAsync(cancellationToken).AsTask();

                var completedTask = await Task.WhenAny(probeTask, fastTask);

                if (completedTask == probeTask)
                {
                    await SyncFollowers();
                }
                else
                {
                    var active = _connections.Count(conn => conn != null && !conn.IsClosed);
                    if (active == 0)
                    {
                        await SyncFollowers();
                    }
                }
            }
        }

        private Connection? GetNextConnection()
        {
            _connLock.EnterReadLock();
            try
            {
                if (_connections.Count == 0)
                {
                    return null;
                }

                for (int i = 0; i < _connections.Count; i++)
                {
                    int idx = (_rrIndex.Value + i) % _connections.Count;
                    Connection? conn = _connections[idx];

                    if (conn != null && !conn.IsClosed && conn.sendQueue != null)
                    {
                        _rrIndex.Value = (idx + 1) % _connections.Count;
                        return conn;
                    }
                }

                return null;
            }
            finally
            {
                _connLock.ExitReadLock();
            }
        }

        private async Task SyncFollowers()
        {
            try
            {
                var (_, followers) = await ClusterHelper.GetClusterInfoAsync(_cfg, _discoveryMap, CancellationToken.None);
                if (followers == null)
                    return;

                // Create a set of follower addresses for quick lookup
                var followerAddrs = new HashSet<string>(followers.Select(f => f.Addr));

                _connLock.EnterWriteLock();
                try
                {
                    // Remove connections not in followers list or closed
                    _connections.RemoveAll(conn => !followerAddrs.Contains(conn.Address) || conn.IsClosed);

                    // Reset round-robin index if needed
                    if (_rrIndex.Value >= _connections.Count)
                        _rrIndex.Value = 0;
                }
                finally
                {
                    _connLock.ExitWriteLock();
                }

                // Add new followers
                foreach (var follower in followers)
                {
                    await ReconnectFollower(follower);
                }
            }
            catch
            {
                // Ignore sync errors
            }
        }

        private async Task ReconnectFollower(NodeAddr follower)
        {
            _connLock.EnterReadLock();
            try
            {
                // Check if already connected
                var existing = _connections.FirstOrDefault(c => c.Address == follower.Addr && !c.IsClosed);
                if (existing != null)
                    return;
            }
            finally
            {
                _connLock.ExitReadLock();
            }

            try
            {
                var newConn = new Connection(follower.Addr, _cfg, new DemuxMap());

                _connLock.EnterWriteLock();
                try
                {
                    // Double-check after acquiring write lock
                    var existingIdx = _connections.FindIndex(c => c.Address == follower.Addr);
                    if (existingIdx >= 0)
                    {
                        var existingConn = _connections[existingIdx];
                        if (!existingConn.IsClosed)
                        {
                            newConn.Dispose();
                            return;
                        }

                        // Replace at same index
                        existingConn.Dispose();
                        _connections[existingIdx] = newConn;
                    }
                    else
                    {
                        _connections.Add(newConn);
                    }
                }
                finally
                {
                    _connLock.ExitWriteLock();
                }

                await newConn.Activate().ConfigureAwait(false);
            }
            catch
            {
                _connLock.EnterWriteLock();
                try
                {
                    var idx = _connections.FindIndex(c => c.Address == follower.Addr);
                    if (idx >= 0)
                    {
                        _connections.RemoveAt(idx);
                    }

                    // Adjust round-robin index if needed
                    if (_rrIndex.Value >= _connections.Count)
                    {
                        _rrIndex.Value = 0;
                    }
                }
                finally
                {
                    _connLock.ExitWriteLock();
                }
            }
        }

        public void Dispose()
        {
            _connLock.EnterWriteLock();
            try
            {
                foreach (var conn in _connections)
                {
                    conn?.Dispose();
                }
                _connections.Clear();
                _rrIndex.Value = 0;
            }
            finally
            {
                _connLock.ExitWriteLock();
            }
            _reqChannel.Writer.TryComplete();
        }
    }

    // Helper classes for atomic operations
    public class AtomicBoolean
    {
        private int _value;
        public bool Value
        {
            get => Interlocked.CompareExchange(ref _value, 0, 0) != 0;
            set => Interlocked.Exchange(ref _value, value ? 1 : 0);
        }
    }

    public class AtomicLong
    {
        private long _value;
        public long Value
        {
            get => Interlocked.CompareExchange(ref _value, 0, 0);
            set => Interlocked.Exchange(ref _value, value);
        }

        public void Add(long value) => Interlocked.Add(ref _value, value);
    }

    public class AtomicUInt
    {
        private int _value;
        public uint Value
        {
            get => (uint)Interlocked.CompareExchange(ref _value, 0, 0);
            set => Interlocked.Exchange(ref _value, (int)value);
        }
        public uint Increment() => (uint)Interlocked.Increment(ref _value);
    }

    // Additional atomic helper classes needed
    public class AtomicInt
    {
        private int _value;
        public int Value
        {
            get => Interlocked.CompareExchange(ref _value, 0, 0);
            set => Interlocked.Exchange(ref _value, value);
        }

        public int Increment() => Interlocked.Increment(ref _value);
    }

    public class Once
    {
        private int _called;
        public void Do(Action action)
        {
            if (Interlocked.CompareExchange(ref _called, 1, 0) == 0)
            {
                action();
            }
        }
    }

    public class ObjectPool<T> where T : class
    {
        private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
        private readonly Func<T> _factory;

        public ObjectPool(Func<T> factory) => _factory = factory;

        public T Get()
        {
            if (_queue.TryDequeue(out var item))
            {
                return item;
            }
            return _factory();
        }

        public void Return(T item)
        {
            _queue.Enqueue(item);
        }
    }
}