using System.Collections.Generic;
using System.Threading.Tasks;
using Roomzin.Sdk.Types.Requests;
using Roomzin.Sdk.Types.Responses;

namespace Roomzin.Sdk.Internal.Api
{
    public interface ICacheClientApi
    {
        // All methods may throw RoomzinException

        Task SetPropAsync(SetPropPayload p);
        Task<List<string>> SearchPropAsync(SearchPropPayload p);
        Task<List<PropertyAvail>> SearchAvailAsync(SearchAvailPayload p);
        Task SetRoomPkgAsync(SetRoomPkgPayload p);
        Task<byte> SetRoomAvlAsync(UpdRoomAvlPayload p);
        Task<byte> IncRoomAvlAsync(UpdRoomAvlPayload p);
        Task<byte> DecRoomAvlAsync(UpdRoomAvlPayload p);
        Task<bool> PropExistAsync(string propertyId);
        Task<bool> PropRoomExistAsync(PropRoomExistPayload p);
        Task<List<string>> PropRoomListAsync(string propertyId);
        Task<List<string>> PropRoomDateListAsync(PropRoomDateListPayload p);
        Task DelPropAsync(string propertyId);
        Task DelSegmentAsync(string segment);
        Task DelPropDayAsync(DelPropDayRequest p);
        Task DelPropRoomAsync(DelPropRoomPayload p);
        Task DelRoomDayAsync(DelRoomDayRequest p);
        Task<GetRoomDayResult> GetPropRoomDayAsync(GetRoomDayRequest p);
        Task<List<SegmentInfo>> GetSegmentsAsync();
        Task CloseAsync();
    }
}