using System.Threading;

namespace EWova.Networking
{
    public static class PlayerLoopHelper
    {
        public static int MainThreadId = Cysharp.Threading.Tasks.PlayerLoopHelper.MainThreadId;
        public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == MainThreadId;

        public static void ThrowIfNotMainThread()
        {
            if (!IsMainThread)
                throw new System.InvalidOperationException("This operation must be performed on the main thread.");
        }
    }
}
