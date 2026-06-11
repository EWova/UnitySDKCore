using System;

namespace EWova.DeepLink
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class DeepLinkProviderAttribute : Attribute
    {
        public Type ProviderType { get; }

        public DeepLinkProviderAttribute(Type providerType)
        {
            ProviderType = providerType;
        }
    }
}
