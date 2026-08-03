using System;
using System.Collections.Generic;
using System.Linq;

namespace EWova.DeepLink
{
    internal static class DeepLinkProviderDiscovery
    {
        public static IDeepLinkProvider Find()
        {
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
                if (type.GetCustomAttributes(typeof(DeepLinkProviderAttribute), false)
                               .FirstOrDefault() is not DeepLinkProviderAttribute attr)
                    continue;

                if (Activator.CreateInstance(type) is not IDeepLinkProvider provider)
                    continue;

                if (!provider.IsSupported)
                    continue;

                instances.Add(provider);
            }

            if (instances.Count != 0)
                return instances
                    .OrderByDescending(x => x.Priority)
                    .FirstOrDefault();

            return Activator.CreateInstance(typeof(BlankProvider)) as IDeepLinkProvider;
        }
    }
}
