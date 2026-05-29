namespace EWova.Auth
{
    /// <summary>
    ///     認證管理器接口 - 負責管理 Token 的獲取、刷新和存儲
    /// </summary>
    public interface IAuthManager
    {
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
        ///     獲取有效的 access_token，必要時自動刷新
        /// </summary>
        /// <returns>有效的 access_token</returns>
        internal string GetAccessToken();
    }
}