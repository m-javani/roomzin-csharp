namespace Roomzin.Sdk.Types.Requests
{
    /// <summary>
    /// Payload for retrieving room details for a specific date (GETPROPROOMDAY command)
    /// </summary>
    public class GetRoomDayRequest
    {
        public string PropertyId { get; set; } = string.Empty;
        public string RoomType { get; set; } = string.Empty;
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
            if (string.IsNullOrEmpty(RoomType))
            {
                return (false, "RoomType is required");
            }
            return Validators.ValidateDate(Date);
        }
    }
}