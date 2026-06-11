using System;

namespace EWova.DeepLink
{
    public interface IDeepLinkProvider
    {
        /// <summary>
        /// The priority of the deep link provider. Higher values indicate higher priority when multiple providers are available.
        /// </summary>
        int Priority { get; }
        bool IsSupported { get; }
        void Initialize(string scheme);
        event Action<string> OnDeepLinkActivated;
    }
}
