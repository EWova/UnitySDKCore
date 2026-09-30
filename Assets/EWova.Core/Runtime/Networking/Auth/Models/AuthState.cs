namespace EWova.Auth
{
    /// <summary>
    ///     OIDC 認證狀態
    /// </summary>
    public enum AuthState
    {
        /// <summary>初始狀態，尚未開始認證流程。此狀態下不應該有任何有效的 token，且不應該嘗試使用 token 進行 API 呼叫。</summary>
        Initializing = 0,

        /// <summary>初始狀態 / 登出後 / Refresh 失敗</summary>
        Unauthenticated = 1,

        /// <summary>Phase 1–2 進行中（WebView 顯示中，等待 code exchange）</summary>
        Authenticating = 2,

        /// <summary>持有有效 access_token，已認證</summary>
        Authenticated = 3,

        /// <summary>背景靜默 Refresh 進行中</summary>
        RefreshingToken = 4
    }
}