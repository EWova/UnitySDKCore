using System;
using System.Security.Cryptography;
using System.Text;

namespace EWova.Auth
{
    /// <summary>
    ///     PKCE / state / nonce 安全亂數產生工具（純靜態，無狀態）
    /// </summary>
    public static class PkceHelper
    {
        /// <summary>
        ///     產生 43–128 字元的 code_verifier（高熵隨機字串，符合 RFC 7636）
        /// </summary>
        public static string GenerateCodeVerifier()
        {
            // 32 bytes → 43 Base64Url chars（足夠高熵）
            var bytes = GenerateRandomBytes(32);
            return Base64UrlEncode(bytes);
        }

        /// <summary>
        ///     對 code_verifier 進行 SHA-256 → Base64Url 編碼，產生 code_challenge（PKCE S256）
        /// </summary>
        public static string GenerateCodeChallenge(string codeVerifier)
        {
            if (string.IsNullOrEmpty(codeVerifier))
                throw new ArgumentNullException(nameof(codeVerifier));

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
            return Base64UrlEncode(hash);
        }

        /// <summary>
        ///     產生 CSRF 防護用隨機 state 值
        /// </summary>
        public static string GenerateState()
        {
            return Base64UrlEncode(GenerateRandomBytes(16));
        }

        /// <summary>
        ///     產生 id_token Replay 防護用隨機 nonce 值
        /// </summary>
        public static string GenerateNonce()
        {
            return Base64UrlEncode(GenerateRandomBytes(16));
        }

        // ── 內部工具 ────────────────────────────────────────────────

        private static byte[] GenerateRandomBytes(int length)
        {
            var bytes = new byte[length];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return bytes;
        }

        /// <summary>
        ///     Base64Url 編碼（RFC 4648 §5）：替換 +→-, /→_, 移除 = padding
        /// </summary>
        public static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        /// <summary>
        ///     Base64Url 解碼
        /// </summary>
        public static byte[] Base64UrlDecode(string input)
        {
            var base64 = input.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            return Convert.FromBase64String(base64);
        }
    }
}