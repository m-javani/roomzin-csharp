namespace Roomzin.Sdk.Types.Requests
{
    /// <summary>
    /// Payload for deleting all room data for a property on a specific date (DELPROPDAY command)
    /// </summary>
    public class DelPropDayRequest
    {
        public string PropertyId { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty; // YYYY-MM-DD

        /// <summary>
        /// Validates the payload
        /// </summary>
        public (bool isValid, string errorMessage) Verify()
        {
            if (string.IsNullOrEmpty(PropertyId))
            {
                return (false, "PropertyId is required");
            }
            return Validators.ValidateDate(Date);
        }
    }
}