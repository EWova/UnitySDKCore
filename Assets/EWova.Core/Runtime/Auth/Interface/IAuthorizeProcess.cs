
using System;

namespace EWova.Auth
{
    public interface IAuthorizeProcess : IDisposable
    {
        IAuthManager AuthManager { get; }
        bool IsCompleted { get; }
        bool IsCancelled { get; }
        bool IsExpired { get; }
        event Action OnCancelled;
        event Action OnCompleted;
    }
}
