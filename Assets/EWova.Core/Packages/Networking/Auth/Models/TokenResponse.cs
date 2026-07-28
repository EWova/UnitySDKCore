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
        [Preserve, JsonProperty("access_token")] public string AccessToken { get; set; } = string.Empty;
        [Preserve, JsonProperty("id_token")] public string IdToken { get; set; } = string.Empty;
        [Preserve, JsonProperty("expires_in")] public int ExpiresIn { get; set; } = 0;
        [Preserve, JsonProperty("token_type")] public string TokenType { get; set; } = string.Empty;
        [Preserve, JsonProperty("refresh_token")] public string? RefreshToken { get; set; }
        [Preserve, JsonProperty("refresh_token_expires_in")] public int? RefreshTokenExpiresIn { get; set; }
    }
#nullable disable
}