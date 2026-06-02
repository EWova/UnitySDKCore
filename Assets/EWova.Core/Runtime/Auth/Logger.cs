using UnityEngine;

namespace EWova.Auth
{
    internal static class Logger
    {
        private static readonly global::EWova.Logger Debug = new global::EWova.Logger("[Ewova] Auth : ", global::EWova.Logger.Level.Full);

        public static global::EWova.Logger.Level PrintLevel
        {
            get => Debug.PrintLevel;
            set => Debug.PrintLevel = value;
        }

        [HideInCallstack]
        public static void Log(string message) => Debug.Log(message);
        [HideInCallstack]
        public static void Warn(string message) => Debug.Warn(message);
        [HideInCallstack]
        public static void Err(string message) => Debug.Err(message);
    }
}
