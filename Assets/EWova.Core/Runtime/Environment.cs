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
                return Authoring.EWovaEditorPrefs.GetEnum("Env_EditorDeploymentMode", k_DefaultDeploymentMode);
#else
                return k_DefaultDeploymentMode;
#endif
            }
        }


    }
}
