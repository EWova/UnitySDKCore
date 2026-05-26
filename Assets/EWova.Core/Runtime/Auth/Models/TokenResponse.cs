using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace EWova.Auth
{
    /// <summary>
    ///     Authorization Server /token 端點回傳的 JSON 結構
    /// </summary>
    [Preserve]
    public class TokenResponse
    {
        [JsonProperty("access_token")] public string AccessToken { get; set; }

        [JsonProperty("id_token")] public string IdToken { get; set; }

        [JsonProperty("refresh_token")] public string RefreshToken { get; set; }

        [JsonProperty("token_type")] public string TokenType { get; set; }

        [JsonProperty("expires_in")] public int ExpiresIn { get; set; }

        /// <summary>refresh_token 的過期秒數（若 AS 有提供）</summary>
        [JsonProperty("refresh_expires_in")]
        public int RefreshExpiresIn { get; set; }
    }
}