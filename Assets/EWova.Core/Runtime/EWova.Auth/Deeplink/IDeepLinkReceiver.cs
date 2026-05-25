using System;

namespace EWova.Auth
{
    public interface IDeepLinkReceiver : IDisposable
    {
        string Name { get; }

        void Initialize(Action<IDeepLinkReceiver, string> onUrlReceived);
    }
}
