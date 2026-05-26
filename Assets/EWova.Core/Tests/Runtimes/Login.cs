using Cysharp.Threading.Tasks;

using EWova.NetService;

using UnityEngine;

public class Login : MonoBehaviour
{
    [ContextMenu("Execute")]
    public void Execute()
    {
        Debug.LogWarning($"IsLogin: {AuthenticatedApiClient.IsUserAuthenticated}");
        StartAsync().Forget();
    }

    private async UniTaskVoid StartAsync()
    {
        var profile = await AuthenticatedApiClient.EWovaService.GetProfile();
        if (profile != null)
        {
            Debug.LogWarning($"UserProfile: {profile}");
        }
        else
        {
            Debug.LogWarning("UserProfile is null");
        }
    }
}
