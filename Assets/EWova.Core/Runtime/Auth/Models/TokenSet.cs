using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;

using UnityEngine.Scripting;

namespace EWova.Auth
{
    /// <summary>
    ///     應用層使用的 Token 集合（由 TokenResponse 轉換而來）
    /// </summary>
    [Serializable]
    [Preserve]
    public class TokenSet
    {
        public string AccessToken { get; set; }
        public string IdToken { get; set; }

        public JwtObject Jwt { get; set; }

        public string RefreshToken { get; set; }

        /// <summary>access_token 的絕對過期時間（UTC）</summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>refresh_token 的絕對過期時間（UTC）。若 AS 未提供則為 DateTime.MaxValue</summary>
        public DateTime RefreshExpiresAt { get; set; }

        /// <summary>access_token 是否已過期（提前 30 秒視為過期，留緩衝）</summary>
        public bool IsAccessTokenExpired => DateTime.UtcNow >= ExpiresAt.AddSeconds(-30);

        /// <summary>refresh_token 是否已過期</summary>
        public bool IsRefreshTokenExpired => DateTime.UtcNow >= RefreshExpiresAt;

        /// <summary>從 TokenResponse 建立 TokenSet</summary>
        public static TokenSet FromResponse(TokenResponse response)
        {
            var now = DateTime.UtcNow;
            return new TokenSet
            {
                AccessToken = response.AccessToken,
                IdToken = response.IdToken,
                Jwt = !string.IsNullOrEmpty(response.IdToken) ? JWT.Read(response.IdToken) : null,
                RefreshToken = response.RefreshToken,
                ExpiresAt = now.AddSeconds(response.ExpiresIn),
                RefreshExpiresAt = response.RefreshExpiresIn > 0
                    ? now.AddSeconds(response.RefreshExpiresIn)
                    : DateTime.MaxValue
            };
        }
    }
}