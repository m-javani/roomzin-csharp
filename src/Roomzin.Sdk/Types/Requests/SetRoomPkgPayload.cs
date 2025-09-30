using System.Collections.Generic;

namespace Roomzin.Sdk.Types.Requests
{
    /// <summary>
    /// Payload for setting room availability, pricing, and cancellation policy (SETROOMPKG command)
    /// </summary>
    public class SetRoomPkgPayload
    {
        public string PropertyId { get; set; } = string.Empty;
        public string RoomType { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty; // YYYY-MM-DD
        public byte? Availability { get; set; }
        public uint? FinalPrice { get; set; }
        public List<string> RateFeature { get; set; } = new List<string>();

        /// <summary>
        /// Validates the payload
        /// </summary>
        public (bool isValid, string errorMessage) Verify(Codecs? codecs)
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(PropertyId))
            {
                return (false, "PropertyId is required");
            }
            if (string.IsNullOrEmpty(RoomType))
            {
                return (false, "RoomType is required");
            }

            var (validDate, dateError) = Validators.ValidateDate(Date);
            if (!validDate)
                errors.Add(dateError);

            var (validRateFeature, rateFeatureError) = CodecsValidation.ValidateRateFeatures(codecs, RateFeature);
            if (!validRateFeature)
                errors.Add(rateFeatureError);

            if (errors.Count > 0)
                return (false, string.Join("; ", errors));

            return (true, string.Empty);
        }
    }
}