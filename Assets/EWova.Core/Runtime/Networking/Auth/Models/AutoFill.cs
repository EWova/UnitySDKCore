namespace EWova.Auth
{
    public readonly struct AutoFill
    {
        public readonly string Method;
        public readonly string Email;
        public readonly string QuickCode;
        public readonly string QuickName;

        public AutoFill(
            string method,
            string email,
            string quickCode,
            string quickName)
        {
            Method = method;
            Email = email;
            QuickCode = quickCode;
            QuickName = quickName;
        }
    }
}
