using System.Collections.Generic;

namespace Roomzin.Sdk.Types.Requests
{
    /// <summary>
    /// Payload for adding a new property (ADDPROP command)
    /// </summary>
    public class SetPropPayload
    {
        public string Segment { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string PropertyId { get; set; } = string.Empty;
        public string PropertyType { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public byte Stars { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public List<string> Amenities { get; set; } = new List<string>();

        /// <summary>
        /// Validates the payload
        /// </summary>
        public (bool isValid, string errorMessage) Verify(Codecs? codecs)
        {
            if (string.IsNullOrEmpty(Segment))
            {
                return (false, "Segment is required");
            }

            if (string.IsNullOrEmpty(Area))
            {
                return (false, "Area is required");
            }

            if (string.IsNullOrEmpty(PropertyId))
            {
                return (false, "PropertyId is required");
            }

            if (string.IsNullOrEmpty(PropertyType))
            {
                return (false, "PropertyType is required");
            }

            if (string.IsNullOrEmpty(Category))
            {
                return (false, "Category is required");
            }

            if (Stars < 1 || Stars > 5)
            {
                return (false, "Stars must be between 1 and 5");
            }

            if (Latitude < -90 || Latitude > 90)
            {
                return (false, "Latitude must be between -90 and 90");
            }

            if (Longitude < -180 || Longitude > 180)
            {
                return (false, "Longitude must be between -180 and 180");
            }

            return (true, string.Empty);
        }
    }
}