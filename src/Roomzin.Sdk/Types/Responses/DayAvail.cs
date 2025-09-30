using System.Collections.Generic;

namespace Roomzin.Sdk.Types.Responses
{
    /// <summary>
    /// One day inside a property
    /// </summary>
    public class DayAvail
    {
        public string Date { get; set; } = string.Empty;
        public byte Availability { get; set; }
        public uint FinalPrice { get; set; }
        public List<string> RateFeature { get; set; } = new List<string>();
    }
}