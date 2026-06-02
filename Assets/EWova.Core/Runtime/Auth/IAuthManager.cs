using System;

namespace EWova.Auth
{
    /// <summary>
    ///    授權流程接口 - 表示一次完整的授權過程（從獲取授權 URL、用戶認證，到最終獲得 TokenSet 或取消）。實現類應該在流程結束後自動釋放資源（例如關閉 WebView）。外部可以通過事件或屬性來監控流程狀態（完成、取消、過期等）。
    /// </summary>
    public interface IAuthorizeProcess : IDisposable
    {
        IAuthManager AuthManager { get; }
        bool IsCompleted { get; }
        bool IsCancelled { get; }
        bool IsExpired { get; }
        event Action OnCancelled;
        event Action OnCompleted;
    }

    /// <summary>
    ///     認證管理器接口 - 負責管理 Token 的獲取、刷新和存儲
    /// </summary>
    public interface IAuthManager
    {
        bool IsAuthenticated { get; }
        /// <summary>
        ///     當前的 TokenSet（可能為 null）
        /// </summary>
        TokenSet CurrentTokenSet { get; }
        /// <summary>
        ///    當前的 AuthState（未認證、有效、過期等）
        /// </summary>
        AuthState CurrentAuthState { get; }
        /// <summary>
        ///    當前已認證用戶的 UserProfile（如果可用，否則為 null）
        /// </summary>
        UserProfile AuthenticatedUserProfile { get; }
        /// <summary>
        ///     強制刷新 access_token（如果 refresh_token 可用）
        /// </summary>
        void RefreshAccessToken();
        /// <summary>
        ///     清除當前的 TokenSet（例如登出時）
        /// </summary>
        void ClearTokenSet();
        /// <summary>
        ///    獲取授權 URL，供用戶進行認證（例如在 WebView 中打開）。URL 中應包含必要的參數（如 client_id、redirect_uri、scope 等）。
        /// </summary>
        IAuthorizeProcess ProcessAuthorizationCodeCallback(string uiLocales = null);

        /// <summary>
        ///     獲取有效的 access_token，必要時自動刷新
        /// </summary>
        /// <returns>有效的 access_token</returns>
        internal string GetAccessToken();
    }
}