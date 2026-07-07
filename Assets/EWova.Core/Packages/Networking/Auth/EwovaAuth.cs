namespace EWova.Auth
{
    public class EWovaAuth : AuthProvider
    {
        private EWovaAuth(EWovaAuthConfig authConfig) : base(authConfig)
        {
        }

        public static EWovaAuth Instance
        {
            get
            {
                throw new System.Exception("目前不支援 EWovaAuth");
            }
        }
        private static EWovaAuth _instance;
    }
}
