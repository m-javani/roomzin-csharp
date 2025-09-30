using System.Collections.Generic;

namespace Roomzin.Sdk.Types.Requests
{
    /// <summary>
    /// Payload for searching availability
    /// </summary>
    public class SearchAvailPayload
    {
        public string Segment { get; set; } = string.Empty;
        public string RoomType { get; set; } = string.Empty;
        public string? Area { get; set; }
        public string? PropertyId { get; set; }
        public string? Type { get; set; }
        public byte? Stars { get; set; }
        public string? Category { get; set; }
        public List<string> Amenities { get; set; } = new List<string>();
        public double? Longitude { get; set; }
        public double? Latitude { get; set; }
        public List<string> Date { get; set; } = new List<string>();
        public byte? Availability { get; set; }
        public uint? FinalPrice { get; set; }
        public List<string> RateFeature { get; set; } = new List<string>();
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

            if (string.IsNullOrEmpty(RoomType))
            {
                return (false, "RoomType is required");
            }

            if (Latitude.HasValue && (Latitude < -90 || Latitude > 90))
            {
                return (false, "Latitude must be between -90 and 90");
            }

            if (Longitude.HasValue && (Longitude < -180 || Longitude > 180))
            {
                return (false, "Longitude must be between -180 and 180");
            }

            if (Date == null || Date.Count == 0)
            {
                return (false, "At least one date is required");
            }

            var (datesValid, datesError) = Validators.ValidateDates(Date);
            if (!datesValid)
            {
                return (false, datesError);
            }

            if (RateFeature != null && RateFeature.Count > 0)
            {
                var (rateFeatureValid, rateFeatureError) = CodecsValidation.ValidateRateFeatures(codecs, RateFeature);
                if (!rateFeatureValid)
                {
                    return (false, rateFeatureError);
                }
            }

            if (Limit.HasValue && Limit == 0)
            {
                return (false, "Limit must be greater than 0");
            }

            return (true, string.Empty);
        }
    }
}
