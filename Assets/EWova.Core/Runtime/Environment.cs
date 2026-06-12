namespace EWova
{
    public enum DeploymentMode
    {
        Production = 0,
        Development = 1,
    }

    public static class Environment
    {
        public static DeploymentMode DeploymentMode
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                int mode = Authoring.EWovaEditorPrefs.instance.GetInt("Env_EditorDeploymentMode", 0);
                return (DeploymentMode)mode;
#else
                return DeploymentMode.Production;
#endif
            }
        }
    }
}
