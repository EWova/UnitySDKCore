namespace EWova.Auth
{
    public readonly struct AutoFill
    {
        public readonly string Method;
        public readonly string Email;
        public readonly string QuickOrg;
        public readonly string QuickCode;

        public AutoFill(
            string method,
            string email,
            string quickOrg,
            string quickCode)
        {
            Method = method;
            Email = email;
            QuickOrg = quickOrg;
            QuickCode = quickCode;
        }
    }
}
