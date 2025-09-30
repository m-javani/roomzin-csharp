using System.Collections.Generic;

namespace Roomzin.Sdk.Internal.Protocol
{
    /// <summary>
    /// Raw result delivered from the read loop to waiting calls
    /// </summary>
    public class RawResult
    {
        /// <summary>
        /// Response status ("SUCCESS" or "ERROR")
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// List of fields in the response
        /// </summary>
        public List<Field> Fields { get; set; } = new List<Field>();
    }
}