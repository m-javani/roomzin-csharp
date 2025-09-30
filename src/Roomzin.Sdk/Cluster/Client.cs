using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Roomzin.Sdk.Internal.Api;
using Roomzin.Sdk.Internal.Cluster;
using Roomzin.Sdk.Internal.Command;
using Roomzin.Sdk.Types;
using Roomzin.Sdk.Types.Requests;
using Roomzin.Sdk.Types.Responses;

namespace Roomzin.Sdk.Internal.Cluster
{
    public class Client : ICacheClientApi
    {
        private readonly Handler _handler;
        private readonly ClusterConfig _cfg;
        private readonly CancellationTokenSource _cts;
        private Types.Codecs? _codecs;

        public static Client New(ClusterConfig cfg)
        {
            if (cfg == null)
                throw RoomzinException.From("cfg must not be null", ErrorKind.Internal);

            var cts = new CancellationTokenSource();

            var handlerConfig = new Config
            {
                SeedNodeIds = cfg.SeedNodeIds,           // renamed
                ApiPort = cfg.ApiPort,
                TcpPort = cfg.TcpPort,
                AuthToken = cfg.AuthToken,
                Timeout = cfg.Timeout,
                HttpTimeout = cfg.HttpTimeout,
                KeepAlive = cfg.KeepAlive,
                MaxActiveConns = cfg.MaxActiveConns,
                NodeProbeInterval = TimeSpan.FromSeconds(2),
                DiscoveryAddr = cfg.DiscoveryAddr,       // added
                StaticDiscovery = cfg.StaticDiscovery    // added
            };

            var clusterClient = new Handler(handlerConfig);
            clusterClient.Start();

            var client = new Client(clusterClient, cfg, cts);

            clusterClient.SetOnReconnectCallback(() => client._codecs = null);
            client._codecs = client.FetchCodecs();
            if (client._codecs == null)
                throw RoomzinException.From("failed to fetch codecs");

            return client;
        }

        private Codecs? GetCodecsInternal()
        {
            if (_codecs != null)
                return _codecs;
            _codecs = FetchCodecs();
            return _codecs;
        }

        private Codecs FetchCodecs()
        {
            var payload = GetCodecsCommand.BuildGetCodecsPayload();
            var res = _handler.ExecuteAsync(false, payload).GetAwaiter().GetResult();
            return GetCodecsCommand.ParseGetCodecsResponse(res.Status, res.Fields);
        }

        public Task<Types.Codecs> GetCodecsAsync()
        {
            if (_codecs != null)
                return Task.FromResult(_codecs);

            var codecs = FetchCodecs();
            if (codecs == null)
                throw RoomzinException.From("failed to fetch codecs");

            _codecs = codecs;
            return Task.FromResult(_codecs);
        }

        private Client(Handler handler, ClusterConfig cfg, CancellationTokenSource cts)
        {
            _handler = handler;
            _cfg = cfg;
            _cts = cts;
        }

        public Task CloseAsync()
        {
            _cts.Cancel();
            _handler.Dispose();
            return Task.CompletedTask;
        }

        // --------------------------------------------------
        //
        //	public API
        //
        // --------------------------------------------------

        /* ----------  READ helpers (follower)  ---------- */
        public async Task<List<string>> SearchPropAsync(SearchPropPayload p)
        {
            var codecs = GetCodecsInternal();
            var (isValid, error) = p.Verify(codecs);
            if (!isValid)
                throw new ValidationException(error);

            var req = SearchPropCommand.BuildSearchPropPayload(p);

            using var timeoutCts = new CancellationTokenSource(_cfg.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            var resp = await _handler.ExecuteAsync(false, req, linkedCts.Token);
            if (resp.Status == "ERROR" && resp.Fields.Count > 0)
            {
                var errorMsg = System.Text.Encoding.UTF8.GetString(resp.Fields[0].Data);
                throw RoomzinException.From(errorMsg);
            }

            return SearchPropCommand.ParseSearchPropResp(resp.Status, resp.Fields);
        }

        public async Task<List<PropertyAvail>> SearchAvailAsync(SearchAvailPayload p)
        {
            var codecs = GetCodecsInternal();
            var (isValid, error) = p.Verify(codecs);
            if (!isValid)
                throw new ValidationException(error);

            var req = SearchAvailCommand.BuildSearchAvailPayload(p);

            using var timeoutCts = new CancellationTokenSource(_cfg.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            var resp = await _handler.ExecuteAsync(false, req, linkedCts.Token);
            if (resp.Status == "ERROR" && resp.Fields.Count > 0)
            {
                var errorMsg = System.Text.Encoding.UTF8.GetString(resp.Fields[0].Data);
                throw RoomzinException.From(errorMsg);
            }
            var resultCodecs = GetCodecsInternal();
            return SearchAvailCommand.ParseSearchAvailResp(resultCodecs, resp.Status, resp.Fields);
        }

        public async Task<bool> PropExistAsync(string propertyId)
        {
            if (string.IsNullOrEmpty(propertyId))
                throw new ValidationException("propertyId is required");

            var req = PropExistCommand.BuildPropExistPayload(propertyId);

            using var timeoutCts = new CancellationTokenSource(_cfg.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            var resp = await _handler.ExecuteAsync(false, req, linkedCts.Token);
            if (resp.Status == "ERROR" && resp.Fields.Count > 0)
            {
                var errorMsg = System.Text.Encoding.UTF8.GetString(resp.Fields[0].Data);
                throw RoomzinException.From(errorMsg);
            }

            return PropExistCommand.ParsePropExistResp(resp.Status, resp.Fields);
        }

        public async Task<bool> PropRoomExistAsync(PropRoomExistPayload p)
        {
            var (isValid, error) = p.Verify();
            if (!isValid)
                throw new ValidationException(error);

            var req = PropRoomExistCommand.BuildPropRoomExistPayload(p);

            using var timeoutCts = new CancellationTokenSource(_cfg.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            var resp = await _handler.ExecuteAsync(false, req, linkedCts.Token);
            if (resp.Status == "ERROR" && resp.Fields.Count > 0)
            {
                var errorMsg = System.Text.Encoding.UTF8.GetString(resp.Fields[0].Data);
                throw RoomzinException.From(errorMsg);
            }

            return PropRoomExistCommand.ParsePropRoomExistResp(resp.Status, resp.Fields);
        }

        public async Task<List<string>> PropRoomListAsync(string propertyId)
        {
            if (string.IsNullOrEmpty(propertyId))
                throw new ValidationException("propertyId is required");

            var req = PropRoomListCommand.BuildPropRoomListPayload(propertyId);

            using var timeoutCts = new CancellationTokenSource(_cfg.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            var resp = await _handler.ExecuteAsync(false, req, linkedCts.Token);
            if (resp.Status == "ERROR" && resp.Fields.Count > 0)
            {
                var errorMsg = System.Text.Encoding.UTF8.GetString(resp.Fields[0].Data);
                throw RoomzinException.From(errorMsg);
            }

            return PropRoomListCommand.ParsePropRoomListResp(resp.Status, resp.Fields);
        }

        public async Task<List<string>> PropRoomDateListAsync(PropRoomDateListPayload p)
        {
            var (isValid, error) = p.Verify();
            if (!isValid)
                throw new ValidationException(error);

            var req = PropRoomDateListCommand.BuildPropRoomDateListPayload(p);

            using var timeoutCts = new CancellationTokenSource(_cfg.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            var resp = await _handler.ExecuteAsync(false, req, linkedCts.Token);
            if (resp.Status == "ERROR" && resp.Fields.Count > 0)
            {
                var errorMsg = System.Text.Encoding.UTF8.GetString(resp.Fields[0].Data);
                throw RoomzinException.From(errorMsg);
            }

            return PropRoomDateListCommand.ParsePropRoomDateListResp(resp.Status, resp.Fields);
        }

        public async Task<GetRoomDayResult> GetPropRoomDayAsync(GetRoomDayRequest p)
        {
            var (isValid, error) = p.Verify();
            if (!isValid)
                throw new ValidationException(error);

            var req = GetPropRoomDayCommand.BuildGetPropRoomDayPayload(p);

            using var timeoutCts = new CancellationTokenSource(_cfg.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            var resp = await _handler.ExecuteAsync(false, req, linkedCts.Token);
            if (resp.Status == "ERROR" && resp.Fields.Count > 0)
            {
                var errorMsg = System.Text.Encoding.UTF8.GetString(resp.Fields[0].Data);
                throw RoomzinException.From(errorMsg);
            }
            var codecs = GetCodecsInternal();
            return GetPropRoomDayCommand.ParseGetPropRoomDayResp(codecs, resp.Status, resp.Fields);
        }

        /* ----------  WRITE helpers (leader)  ---------- */
        public async Task SetPropAsync(SetPropPayload p)
        {
            var codecs = GetCodecsInternal();
            var (isValid, error) = p.Verify(codecs);
            if (!isValid)
                throw new ValidationException(error);

            var req = SetPropCommand.BuildSetPropPayload(p);

            using var timeoutCts = new CancellationTokenSource(_cfg.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            var resp = await _handler.ExecuteAsync(true, req, linkedCts.Token);
            if (resp.Status == "ERROR" && resp.Fields.Count > 0)
            {
                var errorMsg = System.Text.Encoding.UTF8.GetString(resp.Fields[0].Data);
                throw RoomzinException.From(errorMsg);
            }

            SetPropCommand.ParseSetPropResp(resp.Status, resp.Fields);
        }

        public async Task SetRoomPkgAsync(SetRoomPkgPayload p)
        {
            var codecs = GetCodecsInternal();
            var (isValid, error) = p.Verify(codecs);
            if (!isValid)
                throw new ValidationException(error);

            var req = SetRoomPkgCommand.BuildSetRoomPkgPayload(p);

            using var timeoutCts = new CancellationTokenSource(_cfg.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            var resp = await _handler.ExecuteAsync(true, req, linkedCts.Token);
            if (resp.Status == "ERROR" && resp.Fields.Count > 0)
            {
                var errorMsg = System.Text.Encoding.UTF8.GetString(resp.Fields[0].Data);
                throw RoomzinException.From(errorMsg);
            }

            SetRoomPkgCommand.ParseSetRoomPkgResp(resp.Status, resp.Fields);
        }

        public async Task<byte> SetRoomAvlAsync(UpdRoomAvlPayload p)
        {
            var (isValid, error) = p.Verify();
            if (!isValid)
                throw new ValidationException(error);

            var req = SetRoomAvlCommand.BuildSetRoomAvlPayload(p);

            using var timeoutCts = new CancellationTokenSource(_cfg.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            var resp = await _handler.ExecuteAsync(true, req, linkedCts.Token);
            if (resp.Status == "ERROR" && resp.Fields.Count > 0)
            {
                var errorMsg = System.Text.Encoding.UTF8.GetString(resp.Fields[0].Data);
                throw RoomzinException.From(errorMsg);
            }

            return SetRoomAvlCommand.ParseSetRoomAvlResp(resp.Status, resp.Fields);
        }

        public async Task<byte> IncRoomAvlAsync(UpdRoomAvlPayload p)
        {
            var (isValid, error) = p.Verify();
            if (!isValid)
                throw new ValidationException(error);

            var req = IncRoomAvlCommand.BuildIncRoomAvlPayload(p);

            using var timeoutCts = new CancellationTokenSource(_cfg.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            var resp = await _handler.ExecuteAsync(true, req, linkedCts.Token);
            if (resp.Status == "ERROR" && resp.Fields.Count > 0)
            {
                var errorMsg = System.Text.Encoding.UTF8.GetString(resp.Fields[0].Data);
                throw RoomzinException.From(errorMsg);
            }

            return IncRoomAvlCommand.ParseIncRoomAvlResp(resp.Status, resp.Fields);
        }

        public async Task<byte> DecRoomAvlAsync(UpdRoomAvlPayload p)
        {
            var (isValid, error) = p.Verify();
            if (!isValid)
                throw new ValidationException(error);

            var req = DecRoomAvlCommand.BuildDecRoomAvlPayload(p);

            using var timeoutCts = new CancellationTokenSource(_cfg.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            var resp = await _handler.ExecuteAsync(true, req, linkedCts.Token);
            if (resp.Status == "ERROR" && resp.Fields.Count > 0)
            {
                var errorMsg = System.Text.Encoding.UTF8.GetString(resp.Fields[0].Data);
                throw RoomzinException.From(errorMsg);
            }

            return DecRoomAvlCommand.ParseDecRoomAvlResp(resp.Status, resp.Fields);
        }

        public async Task DelPropAsync(string propertyId)
        {
            if (string.IsNullOrEmpty(propertyId))
                throw new ValidationException("propertyId is required");

            var req = DelPropCommand.BuildDelPropPayload(propertyId);

            using var timeoutCts = new CancellationTokenSource(_cfg.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            var resp = await _handler.ExecuteAsync(true, req, linkedCts.Token);
            if (resp.Status == "ERROR" && resp.Fields.Count > 0)
            {
                var errorMsg = System.Text.Encoding.UTF8.GetString(resp.Fields[0].Data);
                throw RoomzinException.From(errorMsg);
            }

            DelPropCommand.ParseDelPropResp(resp.Status, resp.Fields);
        }

        public async Task DelSegmentAsync(string segment)
        {
            if (string.IsNullOrEmpty(segment))
                throw new ValidationException("segment is required");

            var req = DelSegmentCommand.BuildDelSegmentPayload(segment);

            using var timeoutCts = new CancellationTokenSource(_cfg.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            var resp = await _handler.ExecuteAsync(true, req, linkedCts.Token);
            if (resp.Status == "ERROR" && resp.Fields.Count > 0)
            {
                var errorMsg = System.Text.Encoding.UTF8.GetString(resp.Fields[0].Data);
                throw RoomzinException.From(errorMsg);
            }

            DelSegmentCommand.ParseDelSegmentResp(resp.Status, resp.Fields);
        }

        public async Task DelPropDayAsync(DelPropDayRequest p)
        {
            var (isValid, error) = p.Verify();
            if (!isValid)
                throw new ValidationException(error);

            var req = DelPropDayCommand.BuildDelPropDayPayload(p);

            using var timeoutCts = new CancellationTokenSource(_cfg.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            var resp = await _handler.ExecuteAsync(true, req, linkedCts.Token);
            if (resp.Status == "ERROR" && resp.Fields.Count > 0)
            {
                var errorMsg = System.Text.Encoding.UTF8.GetString(resp.Fields[0].Data);
                throw RoomzinException.From(errorMsg);
            }

            DelPropDayCommand.ParseDelPropDayResp(resp.Status, resp.Fields);
        }

        public async Task DelPropRoomAsync(DelPropRoomPayload p)
        {
            var (isValid, error) = p.Verify();
            if (!isValid)
                throw new ValidationException(error);

            var req = DelPropRoomCommand.BuildDelPropRoomPayload(p);

            using var timeoutCts = new CancellationTokenSource(_cfg.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            var resp = await _handler.ExecuteAsync(true, req, linkedCts.Token);
            if (resp.Status == "ERROR" && resp.Fields.Count > 0)
            {
                var errorMsg = System.Text.Encoding.UTF8.GetString(resp.Fields[0].Data);
                throw RoomzinException.From(errorMsg);
            }

            DelPropRoomCommand.ParseDelPropRoomResp(resp.Status, resp.Fields);
        }

        public async Task DelRoomDayAsync(DelRoomDayRequest p)
        {
            var (isValid, error) = p.Verify();
            if (!isValid)
                throw new ValidationException(error);

            var req = DelRoomDayCommand.BuildDelRoomDayPayload(p);

            using var timeoutCts = new CancellationTokenSource(_cfg.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            var resp = await _handler.ExecuteAsync(true, req, linkedCts.Token);
            if (resp.Status == "ERROR" && resp.Fields.Count > 0)
            {
                var errorMsg = System.Text.Encoding.UTF8.GetString(resp.Fields[0].Data);
                throw RoomzinException.From(errorMsg);
            }

            DelRoomDayCommand.ParseDelRoomDayResp(resp.Status, resp.Fields);
        }

        /* ----------  MISC  ---------- */
        public async Task<List<SegmentInfo>> GetSegmentsAsync()
        {
            var req = GetSegmentsCommand.BuildGetSegmentsPayload();

            using var timeoutCts = new CancellationTokenSource(_cfg.Timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

            var resp = await _handler.ExecuteAsync(false, req, linkedCts.Token);
            if (resp.Status == "ERROR" && resp.Fields.Count > 0)
            {
                var errorMsg = System.Text.Encoding.UTF8.GetString(resp.Fields[0].Data);
                throw RoomzinException.From(errorMsg);
            }

            return GetSegmentsCommand.ParseGetSegmentsResp(resp.Status, resp.Fields);
        }
    }
}

