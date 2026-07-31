using System;

using UnityEngine.Scripting;

namespace EWova.Auth
{
#nullable enable
    /// <summary>
    ///     應用層使用的 Token 集合（由 TokenResponse 轉換而來）
    /// </summary>
    [Serializable]
    [Preserve]
    public class TokenSet
    {
        /// <summary>access_token，永遠為非空字串（AS 提供）。應用層應使用 access_token 進行 API 認證，並根據 ExpiresAt 判斷是否需要續期或重新登入。</summary>
        public string AccessToken { get; set; } = string.Empty;
        /// <summary>id_token，可能為空字串（AS 未提供）或非空字串（AS 提供）。應用層應根據實際情況判斷是否需要使用 id_token 進行驗證或提取使用者資訊，或直接忽略 id_token。</summary>
        public string IdToken { get; set; } = string.Empty;
        /// <summary>access_token 的絕對過期時間（UTC）</summary>
        public DateTime ExpiresAt { get; set; } = DateTime.MaxValue;
        /// <summary>refresh_token，可能為 null（AS 未提供）或空字串（AS 提供但為空）。應用層應根據實際情況判斷是否需要使用 refresh_token 進行續期，或直接要求使用者重新登入。 </summary>
        public string? RefreshToken { get; set; }
        /// <summary>refresh_token 的絕對過期時間（UTC）。若 AS 未提供則為 DateTime.MaxValue</summary>
        public DateTime? RefreshExpiresAt { get; set; }


        /// <summary>access_token 的剩餘有效時間（秒）。當剩餘時間小於等於 0 時，表示 access_token 已過期。 </summary>
        public TimeSpan ExpiresIn => ExpiresAt - DateTime.UtcNow;
        /// <summary>access_token 是否已過期（提前 30 秒視為過期，留緩衝）</summary>
        public bool IsAccessTokenExpired => DateTime.UtcNow >= ExpiresAt.AddSeconds(-30);
        /// <summary>refresh_token 的剩餘有效時間（秒）。當 AS 未提供 refresh_token_expires_in 時，永遠返回正值。</summary>
        public TimeSpan? RefreshExpiresIn => RefreshExpiresAt.HasValue ? RefreshExpiresAt.Value - DateTime.UtcNow : (TimeSpan?)null;
        /// <summary>refresh_token 是否已過期</summary>
        public bool IsRefreshTokenExpired => RefreshExpiresAt.HasValue && DateTime.UtcNow >= RefreshExpiresAt.Value;
        /// <summary>從 id_token 解析出的 JWT 物件，包含 payload 中的使用者資訊等。若 id_token 為空或無法解析則為 null。</summary>
        public JwtObject? Jwt { get; private set; }

        /// <summary>從 TokenResponse 建立 TokenSet</summary>
        public static TokenSet FromResponse(TokenResponse response)
        {
            var now = DateTime.UtcNow;
            return new TokenSet
            {
                AccessToken = response.AccessToken,
                ExpiresAt = now.AddSeconds(response.ExpiresIn),

                IdToken = response.IdToken,
                Jwt = !string.IsNullOrEmpty(response.IdToken) ? JWT.Read(response.IdToken) : null,

                RefreshToken = response.RefreshToken,
                RefreshExpiresAt = response.RefreshTokenExpiresIn.HasValue
                    ? now.AddSeconds(response.RefreshTokenExpiresIn.Value)
                    : (DateTime?)null
            };
        }
    }
#nullable disable
}