using Newtonsoft.Json;

using UnityEngine.Scripting;

namespace EWova.Auth
{
#nullable enable
    /// <summary>
    ///     Authorization Server /token 端點回傳的 JSON 結構
    /// </summary>
    [Preserve]
    public class TokenResponse
    {
        [JsonProperty("access_token")] public string AccessToken { get; set; } = string.Empty;
        [JsonProperty("id_token")] public string IdToken { get; set; } = string.Empty;
        [JsonProperty("expires_in")] public int ExpiresIn { get; set; } = 0;
        [JsonProperty("token_type")] public string TokenType { get; set; } = string.Empty;
        [JsonProperty("refresh_token")] public string? RefreshToken { get; set; }
        [JsonProperty("refresh_token_expires_in")] public int? RefreshTokenExpiresIn { get; set; }
    }
#nullable disable
}