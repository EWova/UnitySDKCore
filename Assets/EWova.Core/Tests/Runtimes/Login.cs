using Cysharp.Threading.Tasks;

using EWova.Auth;
using EWova.NetService;

using UnityEngine;

public class Login : MonoBehaviour
{
    Client client;
    [ContextMenu("Execute")]
    public void Execute()
    {
        var ewovaAuth = EwovaAuthManager.Instance;

        client = new Client(ewovaAuth);

        Debug.LogWarning($"IsLogin: {client.IsLogin}");
        StartAsync().Forget();
    }

    private async UniTaskVoid StartAsync()
    {
        var profile = await client.GetProfile();
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
