using System;

namespace EWova.DeepLink
{
    internal class BlankProvider : IDeepLinkProvider
    {
        public int Priority => -1;
        public bool IsSupported => true;
        public event Action<string, DeepLinkInvocationType> OnDeepLinkActivated
        {
            add { }
            remove { }
        }
        public bool ConfigureScheme(string scheme, out string errorMessage)
        {
            errorMessage = null;
            return true;
        }
    }
}
