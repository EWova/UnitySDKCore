using EWova.Auth;

using UnityEngine;

public class Login : MonoBehaviour
{
    [ContextMenu("Open Login Page")]
    public void OpenLoginPage()
    {
        IAuthManager auth = EwovaAuthManager.Instance;

        if (auth.CurrentAuthState == AuthState.Authenticated)
        {
            Debug.LogWarning("User is already authenticated.");
            return;
        }

        var page = auth.GetAuthorizeUrl();
        Debug.LogWarning($"Opening login page: {page}");
        Application.OpenURL(page);
    }
    [ContextMenu("Get User Info")]
    public void GetUser()
    {
        IAuthManager auth = EwovaAuthManager.Instance;

        if (auth.CurrentAuthState == AuthState.Authenticated)
        {
            Debug.LogWarning("User is authenticated.");

            Debug.LogWarning($"UserProfile: {auth.AuthenticatedUserProfile}");
        }
        else
        {
            Debug.LogWarning("User is not authenticated.");
        }
    }
}
