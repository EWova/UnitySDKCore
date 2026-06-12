using UnityEngine;

namespace EWova.Authoring
{
    public static class EditorLogger
    {
        private readonly static Logger _logger = new Logger(null, LogLevel.Full);
        public static ILogSource Source => _logger;
        private const string Prefix = "Editor [EWova] ";

        [HideInCallstack]
        public static void Info(object message, UnityEngine.Object context = null)
        {
            if (_logger.InfoEnabled)
                _logger.Info(Prefix + message, context);
        }
        [HideInCallstack]
        public static void Warn(object message, UnityEngine.Object context = null)
        {
            if (_logger.WarnEnabled)
                _logger.Warn(Prefix + message, context);
        }
        [HideInCallstack]
        public static void Err(object message, UnityEngine.Object context = null)
        {
            if (_logger.ErrorEnabled)
                _logger.Err(Prefix + message, context);
        }
        [HideInCallstack]
        internal static void InfoNoPrefix(object message, UnityEngine.Object context)
        {
            if (_logger.InfoEnabled)
                _logger.Info(message, context);
        }
        [HideInCallstack]
        internal static void WarnNoPrefix(object message, UnityEngine.Object context = null, bool withoutPrefix = false)
        {
            if (_logger.WarnEnabled)
                _logger.Warn(message, context);
        }
        [HideInCallstack]
        internal static void ErrNoPrefix(object message, UnityEngine.Object context = null, bool withoutPrefix = false)
        {
            if (_logger.ErrorEnabled)
                _logger.Err(message, context);
        }
    }
}
