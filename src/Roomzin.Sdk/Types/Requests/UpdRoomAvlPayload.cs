namespace Roomzin.Sdk.Types.Requests
{
    /// <summary>
    /// Payload for updating room availability (INCROOMAVL, DECROOMAVL, SETROOMAVL commands)
    /// </summary>
    public class UpdRoomAvlPayload
    {
        public string PropertyId { get; set; } = string.Empty;
        public string RoomType { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty; // YYYY-MM-DD
        public byte Amount { get; set; }

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