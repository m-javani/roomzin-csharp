using System.Collections.Generic;

namespace Roomzin.Sdk.Types.Requests
{
    /// <summary>
    /// Payload for searching properties
    /// </summary>
    public class SearchPropPayload
    {
        public string Segment { get; set; } = string.Empty;
        public string? Area { get; set; }
        public string? Type { get; set; }
        public byte? Stars { get; set; }
        public string? Category { get; set; }
        public List<string>? Amenities { get; set; }
        public double? Longitude { get; set; }
        public double? Latitude { get; set; }
        public ulong? Limit { get; set; }

        /// <summary>
        /// Validates the payload
        /// </summary>
        public (bool isValid, string errorMessage) Verify(Codecs? codecs)
        {
            if (string.IsNullOrEmpty(Segment))
            {
                return (false, "Segment is required");
            }

            if (Stars.HasValue && (Stars < 1 || Stars > 5))
            {
                return (false, "Stars must be between 1 and 5");
            }

            if (Latitude.HasValue && (Latitude < -90 || Latitude > 90))
            {
                return (false, "Latitude must be between -90 and 90");
            }

            if (Longitude.HasValue && (Longitude < -180 || Longitude > 180))
            {
                return (false, "Longitude must be between -180 and 180");
            }

            return (true, string.Empty);
        }
    }
}