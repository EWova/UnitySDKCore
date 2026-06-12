namespace EWova.Authoring
{
    public static class EditorLogger
    {
        private readonly static Logger _logger = new Logger("Editor [EWova] ", LogLevel.Full);
        public static ILogSource Source => _logger;
        public static void Info(string message)
        {
            if (_logger.InfoEnabled)
                _logger.Info(message);
        }
        public static void Warning(string message)
        {
            if (_logger.WarnEnabled)
                _logger.Warn(message);
        }
        public static void Error(string message)
        {
            if (_logger.ErrorEnabled)
                _logger.Err(message);
        }
    }
}
