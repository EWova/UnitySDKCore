using EWova.NetService;

using UnityEngine;

public class Login : MonoBehaviour
{
    [ContextMenu("Execute")]
    public void Execute()
    {
        if (AuthenticatedApiClient.IsUserAuthenticated)
        {
            Debug.LogWarning("User is authenticated.");

            Debug.LogWarning($"UserProfile: {AuthenticatedApiClient.AuthenticatedUserProfile}");
        }
        else
        {
            Debug.LogWarning("User is not authenticated.");
        }
    }
}
