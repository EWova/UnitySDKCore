using Cysharp.Threading.Tasks;

using System;
using System.Collections.Generic;
using System.Threading;

namespace EWova.Auth
{
    /// <summary>控制是否重新登入或使用既有 session（Authentication 層，不影響授權 consent）</summary>
    public enum LoginBehavior
    {
        /// <summary>預設行為，根據平台和實作決定是否需要重新登入或顯示登入 UI。通常會嘗試使用既有 session，但在某些情況下可能會要求使用者重新登入以確保安全性。</summary>
        Standard,
        /// <summary>強制要求使用者重新登入，無論是否存在有效的 session。此選項會跳過任何現有的登入狀態，直接顯示登入 UI 讓使用者輸入憑證。適用於需要確保使用者身份的情況，例如敏感操作或安全性要求較高的應用場景。</summary>
        ForceLogin,
        /// <summary>完全靜默登入，不顯示任何登入 UI。如果存在有效的 session，將會自動使用該 session 進行登入；如果沒有有效的 session，則直接失敗而不提示使用者。適用於需要在背景執行驗證流程且不希望打擾使用者的情況，例如應用啟動時自動登入或定期刷新 token。</summary>
        Silent,
        /// <summary>如果存在多個有效 session，則讓使用者選擇要使用哪一個 session 進行登入。此選項會顯示一個帳戶選擇界面，列出所有可用的 session 讓使用者選擇。適用於支援多帳戶登入的應用場景，例如同時管理多個社交媒體帳戶或企業帳戶的應用。</summary>
        SelectAccount
    }
    public struct AuthorizeViaBrowserOptions
    {
        public LoginBehavior LoginBehavior { get; set; }
        public bool ConsentRequired { get; set; }
        public IEnumerable<string> UiLocales { get; set; }

        public readonly static AuthorizeViaBrowserOptions Default = new AuthorizeViaBrowserOptions
        {
            LoginBehavior = LoginBehavior.ForceLogin,
            ConsentRequired = true,
            UiLocales = null
        };
    }

    /// <summary>
    /// 提供驗證相關功能的介面，負責管理使用者的登入狀態、存取 token、使用者資訊等。應用程式可以透過實作此介面來整合不同的驗證流程（例如使用系統瀏覽器、內嵌 WebView 或原生 SDK）並統一存取驗證結果。
    /// </summary>
    public interface IAuthManager
    {
        /// <summary>
        /// 目前是否已驗證成功且持有有效的 access_token。
        /// 此屬性僅表示目前的驗證狀態，並不保證 access_token 在未來一段時間內仍然有效。
        /// 應用程式在使用 access_token 進行 API 呼叫前，仍應該呼叫 TryGetValidAccessToken 或 GetAccessTokenAsync 來確保 token 的有效性。
        /// </summary>
        bool IsAuthenticated { get; }
        /// <summary>
        /// 目前的驗證狀態，可能是未驗證、驗證中、已驗證或驗證失敗等狀態。應用程式可以根據此狀態來決定是否顯示登入 UI 或限制存取某些功能。
        /// </summary>
        AuthState CurrentAuthState { get; }
        /// <summary>
        /// 目前已驗證的使用者資訊，若尚未驗證或無法解析使用者資訊則為 null。
        /// </summary>
        UserIdentity? CurrentUser { get; }
        /// <summary>
        /// 嘗試取得有效的 access_token，若目前的 access_token 已過期但 refresh_token 可用，將會自動嘗試刷新。
        /// </summary>
        bool TryGetValidAccessToken(out string token);
        /// <summary>
        /// 嘗試使用 refresh_token 刷新 access_token，成功則回傳新的 access_token，失敗則丟出例外。若 refresh_token 已過期或不可用，也會丟出例外。
        /// </summary>
        UniTask<string> RefreshAccessTokenAsync(CancellationToken cancellationToken = default);
        /// <summary>
        /// 取得有效的 access_token，若目前的 access_token 已過期且 refresh_token 可用，將會自動嘗試刷新。
        /// </summary>
        UniTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
        void Logout();
        Action<IAuthorizeProcess> OnAuthorizeViaBrowserStarted { get; set; }
        /// <summary>
        /// 使用系統瀏覽器進行授權流程，將會透過 DeepLink 回傳授權結果。
        /// </summary>
        IAuthorizeProcess AuthorizeViaBrowser(AuthorizeViaBrowserOptions? authorizeViaBrowserOptions = null, Action<AuthorizeResult> onCompleted = null);
        /// <summary>
        /// 使用系統瀏覽器進行授權流程，將會透過 DeepLink 回傳授權結果。
        /// </summary>
        UniTask<AuthorizeResult> AuthorizeViaBrowserAsync(AuthorizeViaBrowserOptions? authorizeViaBrowserOptions = null, CancellationToken cancellationToken = default);
        /// <summary>
        /// 是否記住使用者的帳號資訊，並自動填入登入表單。
        /// 設定為 <c>false</c> 時不會清除已記住的使用者資訊；如需清除，請呼叫 <see cref="ClearRememberedAutoFill"/>。
        /// </summary>
        bool RememberAutoFillOnAuthorizationSuccess { get; set; }
        /// <summary>
        /// 清除已記住的使用者帳號資訊。
        /// </summary>
        void ClearRememberedAutoFill();
    }
}
