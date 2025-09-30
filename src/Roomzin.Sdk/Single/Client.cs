using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Roomzin.Sdk.Internal.Api;
using Roomzin.Sdk.Internal.Command;
using Roomzin.Sdk.Internal.Single;
using Roomzin.Sdk.Types;
using Roomzin.Sdk.Types.Requests;
using Roomzin.Sdk.Types.Responses;
using ValidationException = Roomzin.Sdk.Types.ValidationException;

namespace Roomzin.Sdk.Internal.Single
{
    public class Client : ICacheClientApi
    {
        private readonly Handler _handler;
        private readonly Config _cfg;
        private readonly System.Threading.CancellationTokenSource _cancellationTokenSource;

        private Types.Codecs? _codecs;

        public static Client New(Config cfg)
        {
            if (cfg == null)
                throw RoomzinException.From("cfg must not be null", ErrorKind.Internal);

            var cancellationTokenSource = new System.Threading.CancellationTokenSource();

            var handlerConfig = new HandlerConfig
            {
                Addr = cfg.Host,
                TcpPort = cfg.TcpPort,
                AuthToken = cfg.AuthToken,
                Timeout = cfg.Timeout,
                KeepAlive = cfg.KeepAlive
            };

            var handler = new Handler(handlerConfig);
            var client = new Client(handler, cfg, cancellationTokenSource);

            handler.OnReconnect = () => client._codecs = null;
            client._codecs = client.FetchCodecs();
            if (client._codecs == null)
                throw RoomzinException.From("failed to fetch codecs");

            return client;
        }

        private Types.Codecs? GetCodecsInternal()
        {
            if (_codecs != null)
                return _codecs;
            _codecs = FetchCodecs();
            return _codecs;
        }

        private Types.Codecs? FetchCodecs()
        {
            try
            {
                var payload = GetCodecsCommand.BuildGetCodecsPayload();
                var res = _handler.RoundTrip(_handler.NextId(), payload).GetAwaiter().GetResult();
                return GetCodecsCommand.ParseGetCodecsResponse(res.Status, res.Fields);
            }
            catch
            {
                return null;
            }
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

        private Client(Handler handler, Config cfg, System.Threading.CancellationTokenSource cancellationTokenSource)
        {
            _handler = handler;
            _cfg = cfg;
            _cancellationTokenSource = cancellationTokenSource;
        }

        public Task CloseAsync()
        {
            _cancellationTokenSource.Cancel();
            _handler.Dispose();
            return Task.CompletedTask;
        }

        // --------------------------------------------------
        //
        //	public API
        //
        // --------------------------------------------------

        public async Task SetPropAsync(SetPropPayload p)
        {
            var codecs = GetCodecsInternal();
            var (isValid, error) = p.Verify(codecs);
            if (!isValid)
                throw new ValidationException(error);

            var payload = SetPropCommand.BuildSetPropPayload(p);
            var res = await _handler.RoundTrip(_handler.NextId(), payload);
            SetPropCommand.ParseSetPropResp(res.Status, res.Fields);
        }

        public async Task<List<string>> SearchPropAsync(SearchPropPayload p)
        {
            var codecs = GetCodecsInternal();
            var (isValid, error) = p.Verify(codecs);
            if (!isValid)
                throw new ValidationException(error);

            var payload = SearchPropCommand.BuildSearchPropPayload(p);
            var res = await _handler.RoundTrip(_handler.NextId(), payload);
            return SearchPropCommand.ParseSearchPropResp(res.Status, res.Fields);
        }

        public async Task<List<PropertyAvail>> SearchAvailAsync(SearchAvailPayload p)
        {
            var codecs = GetCodecsInternal();
            var (isValid, error) = p.Verify(codecs);
            if (!isValid)
                throw new ValidationException(error);

            var payload = SearchAvailCommand.BuildSearchAvailPayload(p);
            var res = await _handler.RoundTrip(_handler.NextId(), payload);
            var resultCodecs = GetCodecsInternal();
            return SearchAvailCommand.ParseSearchAvailResp(resultCodecs, res.Status, res.Fields);
        }

        public async Task SetRoomPkgAsync(SetRoomPkgPayload p)
        {
            var codecs = GetCodecsInternal();
            var (isValid, error) = p.Verify(codecs);
            if (!isValid)
                throw new ValidationException(error);

            var payload = SetRoomPkgCommand.BuildSetRoomPkgPayload(p);
            var res = await _handler.RoundTrip(_handler.NextId(), payload);
            SetRoomPkgCommand.ParseSetRoomPkgResp(res.Status, res.Fields);
        }

        public async Task<byte> SetRoomAvlAsync(UpdRoomAvlPayload p)
        {
            var (isValid, error) = p.Verify();
            if (!isValid)
                throw new ValidationException(error);

            var payload = SetRoomAvlCommand.BuildSetRoomAvlPayload(p);
            var res = await _handler.RoundTrip(_handler.NextId(), payload);
            return SetRoomAvlCommand.ParseSetRoomAvlResp(res.Status, res.Fields);
        }

        public async Task<byte> IncRoomAvlAsync(UpdRoomAvlPayload p)
        {
            var (isValid, error) = p.Verify();
            if (!isValid)
                throw new ValidationException(error);

            var payload = IncRoomAvlCommand.BuildIncRoomAvlPayload(p);
            var res = await _handler.RoundTrip(_handler.NextId(), payload);
            return IncRoomAvlCommand.ParseIncRoomAvlResp(res.Status, res.Fields);
        }

        public async Task<byte> DecRoomAvlAsync(UpdRoomAvlPayload p)
        {
            var (isValid, error) = p.Verify();
            if (!isValid)
                throw new ValidationException(error);

            var payload = DecRoomAvlCommand.BuildDecRoomAvlPayload(p);
            var res = await _handler.RoundTrip(_handler.NextId(), payload);
            return DecRoomAvlCommand.ParseDecRoomAvlResp(res.Status, res.Fields);
        }

        public async Task<bool> PropExistAsync(string propertyId)
        {
            if (string.IsNullOrEmpty(propertyId))
                throw new ValidationException("propertyId is required");

            var payload = PropExistCommand.BuildPropExistPayload(propertyId);
            var res = await _handler.RoundTrip(_handler.NextId(), payload);
            return PropExistCommand.ParsePropExistResp(res.Status, res.Fields);
        }

        public async Task<bool> PropRoomExistAsync(PropRoomExistPayload p)
        {
            var (isValid, error) = p.Verify();
            if (!isValid)
                throw new ValidationException(error);

            var payload = PropRoomExistCommand.BuildPropRoomExistPayload(p);
            var res = await _handler.RoundTrip(_handler.NextId(), payload);
            return PropRoomExistCommand.ParsePropRoomExistResp(res.Status, res.Fields);
        }

        public async Task<List<string>> PropRoomListAsync(string propertyId)
        {
            if (string.IsNullOrEmpty(propertyId))
                throw new ValidationException("propertyId is required");

            var payload = PropRoomListCommand.BuildPropRoomListPayload(propertyId);
            var res = await _handler.RoundTrip(_handler.NextId(), payload);
            return PropRoomListCommand.ParsePropRoomListResp(res.Status, res.Fields);
        }

        public async Task<List<string>> PropRoomDateListAsync(PropRoomDateListPayload p)
        {
            var (isValid, error) = p.Verify();
            if (!isValid)
                throw new ValidationException(error);

            var payload = PropRoomDateListCommand.BuildPropRoomDateListPayload(p);
            var res = await _handler.RoundTrip(_handler.NextId(), payload);
            return PropRoomDateListCommand.ParsePropRoomDateListResp(res.Status, res.Fields);
        }

        public async Task DelPropAsync(string propertyId)
        {
            if (string.IsNullOrEmpty(propertyId))
                throw new ValidationException("propertyId is required");

            var payload = DelPropCommand.BuildDelPropPayload(propertyId);
            var res = await _handler.RoundTrip(_handler.NextId(), payload);
            DelPropCommand.ParseDelPropResp(res.Status, res.Fields);
        }

        public async Task DelSegmentAsync(string segment)
        {
            if (string.IsNullOrEmpty(segment))
                throw new ValidationException("segment is required");

            var payload = DelSegmentCommand.BuildDelSegmentPayload(segment);
            var res = await _handler.RoundTrip(_handler.NextId(), payload);
            DelSegmentCommand.ParseDelSegmentResp(res.Status, res.Fields);
        }

        public async Task DelPropDayAsync(DelPropDayRequest p)
        {
            var (isValid, error) = p.Verify();
            if (!isValid)
                throw new ValidationException(error);

            var payload = DelPropDayCommand.BuildDelPropDayPayload(p);
            var res = await _handler.RoundTrip(_handler.NextId(), payload);
            DelPropDayCommand.ParseDelPropDayResp(res.Status, res.Fields);
        }

        public async Task DelPropRoomAsync(DelPropRoomPayload p)
        {
            var (isValid, error) = p.Verify();
            if (!isValid)
                throw new ValidationException(error);

            var payload = DelPropRoomCommand.BuildDelPropRoomPayload(p);
            var res = await _handler.RoundTrip(_handler.NextId(), payload);
            DelPropRoomCommand.ParseDelPropRoomResp(res.Status, res.Fields);
        }

        public async Task DelRoomDayAsync(DelRoomDayRequest p)
        {
            var (isValid, error) = p.Verify();
            if (!isValid)
                throw new ValidationException(error);

            var payload = DelRoomDayCommand.BuildDelRoomDayPayload(p);
            var res = await _handler.RoundTrip(_handler.NextId(), payload);
            DelRoomDayCommand.ParseDelRoomDayResp(res.Status, res.Fields);
        }

        public async Task<GetRoomDayResult> GetPropRoomDayAsync(GetRoomDayRequest p)
        {
            var (isValid, error) = p.Verify();
            if (!isValid)
                throw new ValidationException(error);

            var payload = GetPropRoomDayCommand.BuildGetPropRoomDayPayload(p);
            var res = await _handler.RoundTrip(_handler.NextId(), payload);
            var codecs = GetCodecsInternal();
            return GetPropRoomDayCommand.ParseGetPropRoomDayResp(codecs, res.Status, res.Fields);
        }

        public async Task<List<SegmentInfo>> GetSegmentsAsync()
        {
            var payload = GetSegmentsCommand.BuildGetSegmentsPayload();
            var res = await _handler.RoundTrip(_handler.NextId(), payload);
            return GetSegmentsCommand.ParseGetSegmentsResp(res.Status, res.Fields);
        }
    }
}