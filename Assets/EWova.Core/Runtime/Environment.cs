namespace EWova
{
    public enum DeploymentMode
    {
        Development,
        Production
    }
    /// <summary>
    /// EWova 設定環境，為確保初始化正確，請在 RuntimeInitializeLoadType.BeforeSceneLoad 前完成初始化
    /// </summary>
    public static class Environment
    {
        public static DeploymentMode DeploymentMode { get; set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            = DeploymentMode.Development;
#else
            = DeploymentMode.Production;
#endif
    }
}
