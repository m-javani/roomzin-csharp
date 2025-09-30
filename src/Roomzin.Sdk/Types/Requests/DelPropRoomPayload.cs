namespace Roomzin.Sdk.Types.Requests
{
    /// <summary>
    /// Payload for deleting a room type from a property (DELPROPROOM command)
    /// </summary>
    public class DelPropRoomPayload
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