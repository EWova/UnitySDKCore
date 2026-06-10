
using System;

namespace EWova.Auth
{

    public enum AuthorizeProcessResult
    {
        Success,
        Cancelled,
        Failed
    }

    public readonly struct AuthorizeResult
    {
        public readonly AuthorizeProcessResult Status;
        public readonly string ErrorMessage;
        public readonly Exception Exception;

        private AuthorizeResult(AuthorizeProcessResult status, string error = null, Exception ex = null)
        {
            Status = status;
            ErrorMessage = error;
            Exception = ex;
        }

        public static AuthorizeResult Ok() => new(AuthorizeProcessResult.Success);
        public static AuthorizeResult Cancel() => new(AuthorizeProcessResult.Cancelled);
        public static AuthorizeResult Fail(string msg) => new(AuthorizeProcessResult.Failed, msg);
        public static AuthorizeResult FromException(Exception ex) => new(AuthorizeProcessResult.Failed, ex?.Message, ex);
    }

    /// <summary>
    /// 授權流程的介面，代表一次完整的授權過程（例如使用系統瀏覽器進行 OAuth 授權）。
    /// 應用程式可以透過此介面來監控授權流程的狀態（完成、取消、過期等）並在適當的時機處理授權結果或清理資源。
    /// 每次呼叫 IAuthManager.AuthorizeViaBrowser 都會返回一個新的 IAuthorizeProcess 實例，代表一次獨立的授權嘗試。
    /// </summary>
    public interface IAuthorizeProcess : IDisposable
    {
        IAuthManager AuthManager { get; }
        AuthorizeResult? Result { get; }
        bool IsCompleted { get; }
        bool IsExpired { get; }
        event Action<AuthorizeResult> OnCompleted;
    }
}
