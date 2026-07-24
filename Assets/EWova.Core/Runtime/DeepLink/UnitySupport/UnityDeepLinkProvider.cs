using System;

using UnityEngine;
namespace EWova.DeepLink
{
    [DeepLinkProvider(typeof(UnityDeepLinkProvider))]
    public class UnityDeepLinkProvider : IDeepLinkProvider
    {
        public int Priority => 0;

        public bool IsSupported
        {
            get
            {
#if (UNITY_ANDROID || UNITY_IOS || UNITY_WSA|| UNITY_STANDALONE_OSX) && !UNITY_EDITOR_WIN
                return true;
#else
                return false;
#endif
            }
        }

        public event Action<string, DeepLinkInvocationType> OnDeepLinkActivated;

        public bool ConfigureScheme(string scheme, out string errorMessage)
        {
            Application.deepLinkActivated += OnActivated;
            errorMessage = null;
            return true;
        }

        private void OnActivated(string url)
        {
            OnDeepLinkActivated?.Invoke(url, DeepLinkInvocationType.Runtime);
        }
    }
}
