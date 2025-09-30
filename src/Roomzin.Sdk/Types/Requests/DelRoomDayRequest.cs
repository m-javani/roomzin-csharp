namespace Roomzin.Sdk.Types.Requests
{
    /// <summary>
    /// Payload for deleting a room's data for a specific date (DELROOMDAY command)
    /// </summary>
    public class DelRoomDayRequest
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