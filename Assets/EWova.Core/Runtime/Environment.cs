namespace EWova
{
    public enum DeploymentMode
    {
        Production = 0,
        Development = 1,
    }

    public static class Environment
    {
        private const DeploymentMode k_DefaultDeploymentMode = DeploymentMode.Production;
        public static DeploymentMode DeploymentMode
        {
            get
            {
#if UNITY_EDITOR
                int mode = Authoring.EWovaEditorPrefs.GetInt(
                    "Env_EditorDeploymentMode",
                    (int)k_DefaultDeploymentMode);
                return (DeploymentMode)mode;
#else
                return k_DefaultDeploymentMode;
#endif
            }
        }


    }
}
