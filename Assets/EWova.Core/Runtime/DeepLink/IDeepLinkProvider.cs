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
        bool ConfigureScheme(string scheme, out string errorMessage);
        event Action<string, DeepLinkInvocationType> OnDeepLinkActivated;
    }
}
