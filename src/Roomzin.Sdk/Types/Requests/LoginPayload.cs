namespace Roomzin.Sdk.Types.Requests
{
    /// <summary>
    /// Payload for the AUTH command
    /// </summary>
    public class LoginPayload
    {
        /// <summary>
        /// Static token for authentication (optional)
        /// </summary>
        public string Token { get; set; } = string.Empty;
    }
}