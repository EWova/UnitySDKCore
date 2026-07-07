namespace EWova.Auth
{
    public class EWovaAuth : AuthProvider
    {
        internal EWovaAuth() : base(null) { }

        public static EWovaAuth Instance
        {
            get
            {
                throw new System.Exception("目前不支援 EWovaAuth");
            }
        }
        private readonly static EWovaAuth _instance;
    }
}
