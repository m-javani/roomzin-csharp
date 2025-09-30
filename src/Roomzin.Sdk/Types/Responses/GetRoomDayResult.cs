using System.Collections.Generic;

namespace Roomzin.Sdk.Types.Responses
{
    /// <summary>
    /// Result for retrieving room details for a specific date (GETPROPROOMDAY command)
    /// </summary>
    public class GetRoomDayResult
    {
        public string PropertyId { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public byte Availability { get; set; }
        public uint FinalPrice { get; set; }
        public List<string> RateFeature { get; set; } = new List<string>();
    }
}