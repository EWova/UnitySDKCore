using EWova.Auth;

using UnityEngine;

public class PkgTest
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Init()
    {
        EwovaAuthManager.Logger.PrintLevel = EWova.Logger.Level.Full;
    }
}
