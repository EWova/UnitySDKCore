using System;

using UnityEngine;

namespace EWova
{
#if !UNITY_6000_0_OR_NEWER
    // Unity 6.0.0 以後才有 HideInCallstackAttribute，為了兼容舊版本，這裡定義一個空的同名屬性
    [System.Diagnostics.Conditional("UNITY_EDTIOR")]
    public sealed class HideInCallstackAttribute : Attribute {}
#endif
    public interface ILogSource
    {
        event Action<LogEntry> LogReceived;
        LogLevel PrintLevel { get; set; }
    }

    [Flags]
    public enum LogLevel
    {
        None = 0,
        Info = 1 << 0,
        Warn = 1 << 1,
        Error = 1 << 2,

        Full = Info | Warn | Error,
    }
    public readonly struct LogEntry
    {
        public readonly DateTime Time;
        public readonly LogLevel Level;
        public readonly string Message;

        public LogEntry(DateTime time, LogLevel level, string message)
        {
            Time = time;
            Level = level;
            Message = message;
        }

        public override readonly string ToString() => $"[{Time:HH:mm:ss}] [{Level}] {Message}";
    }

    public sealed class Logger : ILogSource
    {
        public string Prefix { get; set; } = "";
        public LogLevel PrintLevel { get; set; }

        public event Action<LogEntry> LogReceived;

        public bool InfoEnabled
        {
            get
            {
                return (PrintLevel & LogLevel.Info) != 0;
            }
            set
            {
                if (value)
                    PrintLevel |= LogLevel.Info;
                else
                    PrintLevel &= ~LogLevel.Info;
            }
        }

        public bool WarnEnabled
        {
            get
            {
                return (PrintLevel & LogLevel.Warn) != 0;
            }
            set
            {
                if (value)
                    PrintLevel |= LogLevel.Warn;
                else
                    PrintLevel &= ~LogLevel.Warn;
            }
        }

        public bool ErrorEnabled
        {
            get
            {
                return (PrintLevel & LogLevel.Error) != 0;
            }
            set
            {
                if (value)
                    PrintLevel |= LogLevel.Error;
                else
                    PrintLevel &= ~LogLevel.Error;
            }
        }

        public Logger(
            string prefix = "",
            LogLevel printLevel = LogLevel.Warn | LogLevel.Error)
        {
            Prefix = prefix;
            PrintLevel = printLevel;
        }

        private void LogEntryAction(LogLevel level, string msg)
        {
            LogReceived?.Invoke(new LogEntry(time: DateTime.Now, level: level, message: msg));
        }

        [HideInCallstack]
        public void Info(object msg, UnityEngine.Object context = null)
        {
            string str = Prefix == null ? msg.ToString() : Prefix + msg.ToString();
            LogEntryAction(LogLevel.Info, str);
            Debug.Log(str, context);
        }

        [HideInCallstack]
        public void Warn(object msg, UnityEngine.Object context = null)
        {
            string str = Prefix == null ? msg.ToString() : Prefix + msg.ToString();
            LogEntryAction(LogLevel.Warn, str);
            Debug.LogWarning(str, context);
        }

        [HideInCallstack]
        public void Err(object msg, UnityEngine.Object context = null)
        {
            string str = Prefix == null ? msg.ToString() : Prefix + msg.ToString();
            LogEntryAction(LogLevel.Error, str);
            Debug.LogError(str, context);
        }
    }
}