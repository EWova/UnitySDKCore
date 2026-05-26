using Cysharp.Threading.Tasks;

using EWova.NetService;

using UnityEngine;

public class Login : MonoBehaviour
{
    [ContextMenu("Execute")]
    public void Execute()
    {
        Debug.LogWarning($"IsLogin: {AuthenticatedApiClient.IsUserAuthenticated}");
    }
}
