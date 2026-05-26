using UnityEngine;

namespace EWova.Auth
{
    internal static class Logger
    {
        private static readonly global::EWova.Logger Debug = new global::EWova.Logger("[Ewova] Auth : ", global::EWova.Logger.Level.Info | global::EWova.Logger.Level.Warn | global::EWova.Logger.Level.Error);
        [HideInCallstack]
        public static void Log(string message) => Debug.Log(message);
        [HideInCallstack]
        public static void Warn(string message) => Debug.Warn(message);
        [HideInCallstack]
        public static void Err(string message) => Debug.Err(message);
    }
}
