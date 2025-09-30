using System.Collections.Generic;

namespace Roomzin.Sdk.Types.Responses
{
    /// <summary>
    /// One property + all its days
    /// </summary>
    public class PropertyAvail
    {
        public string PropertyId { get; set; } = string.Empty;
        public List<DayAvail> Days { get; set; } = new List<DayAvail>();
    }
}