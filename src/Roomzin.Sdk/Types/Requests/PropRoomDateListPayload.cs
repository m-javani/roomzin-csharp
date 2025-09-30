namespace Roomzin.Sdk.Types.Requests
{
    /// <summary>
    /// Payload for listing dates with availability for a room type (PROPROOMDATELIST command)
    /// </summary>
    public class PropRoomDateListPayload
    {
        public string PropertyId { get; set; } = string.Empty;
        public string RoomType { get; set; } = string.Empty;

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
            return (true, string.Empty);
        }
    }
}