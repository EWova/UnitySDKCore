using System;
using System.Collections.Generic;
using System.Linq;

namespace EWova.DeepLink
{
    internal static class DeepLinkProviderDiscovery
    {
        private static IDeepLinkProvider s_provider;

        public static IDeepLinkProvider Find()
        {
            if (s_provider != null)
                return s_provider;

            var providers =
                AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return Array.Empty<Type>(); }
                    })
                    .Where(t =>
                        typeof(IDeepLinkProvider).IsAssignableFrom(t) &&
                        !t.IsInterface &&
                        !t.IsAbstract);

            List<IDeepLinkProvider> instances = new();

            foreach (var type in providers)
            {
                var attr = type.GetCustomAttributes(typeof(DeepLinkProviderAttribute), false)
                               .FirstOrDefault() as DeepLinkProviderAttribute;

                if (attr == null)
                    continue;

                if (Activator.CreateInstance(type) is not IDeepLinkProvider provider)
                    continue;

                //UnityEngine.Debug.Log($"Discovered DeepLinkProvider: {type.FullName} with priority {provider.Priority} and supported: {provider.IsSupported}");

                if (!provider.IsSupported)
                    continue;

                instances.Add(provider);
            }

            s_provider = instances
                .OrderByDescending(x => x.Priority)
                .FirstOrDefault();

            return s_provider;
        }
    }
}
