using EWova.Auth;

using UnityEngine;

namespace EWova.Core.Tests
{
    public class Login : MonoBehaviour
    {
        IAuthorizeProcess _loginProcess;

        [ContextMenu("Open Login Page")]
        public void OpenLoginPage()
        {
            IAuthManager auth = EwovaAuthManager.Instance;

            if (auth.CurrentAuthState == AuthState.Authenticated)
            {
                Debug.LogWarning("User is already authenticated.");
                return;
            }

            _loginProcess = auth.AuthorizeViaBrowser();
            _loginProcess.OnCompleted += () =>
            {
                Debug.Log($"Login 處理完成. 驗證者身分 {auth.CurrentUser?.Name}.");
                _loginProcess = null;
            };
            _loginProcess.OnCancelled += () =>
            {
                Debug.Log("Login 取消處理.");
                _loginProcess = null;
            };

            Debug.Log("Login 處理開始. 請完成驗證流程.");
        }

        [ContextMenu("Cancel Login Process")]
        public void Cancel()
        {
            if (_loginProcess == null)
            {
                Debug.LogWarning("No login process to cancel.");
                return;
            }

            _loginProcess.Dispose();
            _loginProcess = null;
        }

        [ContextMenu("Open Login Page and Cancel After 1 Seconds")]
        public void OpenLoginPageAndCancelAfter1Seconds()
        {
            OpenLoginPage();
            Invoke(nameof(Cancel), 1f);
        }

        [ContextMenu("Logout")]
        public void Logout()
        {
            IAuthManager auth = EwovaAuthManager.Instance;
            if (auth.CurrentAuthState != AuthState.Authenticated)
            {
                Debug.LogWarning("User is not authenticated.");
                return;
            }
            auth.Logout();
            Debug.Log("User logged out.");
        }
    }
}