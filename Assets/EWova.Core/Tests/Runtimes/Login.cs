using EWova.Auth;
using EWova.NetService;

using UnityEngine;

public class Login : MonoBehaviour
{
    [ContextMenu("Execute")]
    public void Execute()
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
