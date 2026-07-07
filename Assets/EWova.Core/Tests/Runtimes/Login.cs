using Cysharp.Threading.Tasks;

using EWova.Auth;

using UnityEngine;

namespace EWova.Core.Tests
{
    public class Login : MonoBehaviour
    {
        IAuthorizeProcess _loginProcess;

        [ContextMenu("Authorize Via Browser")]
        public void AuthorizeViaBrowser()
        {
            IAuthManager auth = EWovaAuth.Instance;

            if (auth.CurrentAuthState == AuthState.Authenticated)
            {
                Debug.LogWarning("User is already authenticated.");
                return;
            }

            _loginProcess = auth.AuthorizeViaBrowser(
                authorizeViaBrowserOptions: new AuthorizeViaBrowserOptions
                {
                    LoginBehavior = LoginBehavior.Standard,
                    ConsentRequired = true,
                    UiLocales = new[] { "zh-TW" }
                },
                onCompleted: (result) =>
                {
                    if (result.Status == AuthorizeProcessResult.Success)
                    {
                        Debug.Log($"Login 處理完成. 驗證者身分 {auth.CurrentUser?.Name}.");
                    }
                    else if (result.Status == AuthorizeProcessResult.Cancelled)
                    {
                        Debug.Log("Login 取消處理.");
                    }
                    else if (result.Status == AuthorizeProcessResult.Failed)
                    {
                        Debug.LogError($"Login 處理失敗. 錯誤訊息: {result.ErrorMessage} Execption 如下");
                        if (result.Exception != null)
                            Debug.LogException(result.Exception);
                    }

                    _loginProcess = null;
                }
            );

            Debug.Log("Login 處理開始. 請完成驗證流程.");
        }


        [ContextMenu("Authorize Via Browser 強制登入")]
        public void AuthorizeViaBrowserWithLogin()
        {
            IAuthManager auth = EWovaAuth.Instance;

            if (auth.CurrentAuthState == AuthState.Authenticated)
            {
                Debug.LogWarning("User is already authenticated.");
                return;
            }

            _loginProcess = auth.AuthorizeViaBrowser(
                authorizeViaBrowserOptions: new AuthorizeViaBrowserOptions
                {
                    LoginBehavior = LoginBehavior.ForceLogin,
                    ConsentRequired = true,
                    UiLocales = null
                },
                onCompleted: (result) =>
                {
                    if (result.Status == AuthorizeProcessResult.Success)
                    {
                        Debug.Log($"Login 處理完成. 驗證者身分 {auth.CurrentUser?.Name}.");
                    }
                    else if (result.Status == AuthorizeProcessResult.Cancelled)
                    {
                        Debug.Log("Login 取消處理.");
                    }
                    else if (result.Status == AuthorizeProcessResult.Failed)
                    {
                        Debug.LogError($"Login 處理失敗. 錯誤訊息: {result.ErrorMessage} Execption 如下");
                        if (result.Exception != null)
                            Debug.LogException(result.Exception);
                    }

                    _loginProcess = null;
                }
            );

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
            AuthorizeViaBrowser();
            Invoke(nameof(Cancel), 1f);
        }

        [ContextMenu("Logout")]
        public void Logout()
        {
            IAuthManager auth = EWovaAuth.Instance;
            if (auth.CurrentAuthState != AuthState.Authenticated)
            {
                Debug.LogWarning("User is not authenticated.");
                return;
            }
            auth.Logout();
            Debug.Log("User logged out.");
        }

        public string AppId = "019d417e-29e6-7832-9ca8-7c3469d77991";
        [ContextMenu("Try launch ewova by CreateLaunchTicket")]
        public void TryCreateLaunchTicket()
        {
            IAuthManager auth = EWovaAuth.Instance;
            if (auth.CurrentAuthState != AuthState.Authenticated)
            {
                Debug.LogWarning("User is not authenticated.");
                return;
            }
            EWovaAuth.Instance.LaunchEWovaAppWithLoginAsync(AppId).Forget();
        }

        [ContextMenu("DEEPLINK")]
        public void Deeplink() 
        {
            Application.OpenURL("example://call?hello_world=123");
        }
    }
}