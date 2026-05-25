using System;

using UnityEngine;

namespace EWova
{
#if !UNITY_6000_0_OR_NEWER
    // Unity 6.0.0 以後才有 HideInCallstackAttribute，為了兼容舊版本，這裡定義一個空的同名屬性
    [System.Diagnostics.Conditional("UNITY_EDTIOR")]
    public sealed class HideInCallstackAttribute : Attribute {}
#endif
    [Serializable]
    public class Logger
    {
        [Flags]
        public enum Level
        {
            Info = 1,
            Warn = 2,
            Error = 4,

            None = 0,
            Full = Info | Warn | Error,
        }
        public Level PrintLevel;
        [NonSerialized] public string Prefix = "";

        public Logger(string prefix = "", Level printLevel = Level.Warn | Level.Error)
        {
            Prefix = prefix;
            PrintLevel = printLevel;
        }

        [HideInCallstack]
        public void Log(object msg)
        {
            if (PrintLevel.HasFlag(Level.Info))
                UnityEngine.Debug.Log(Prefix + msg);
        }
        [HideInCallstack]
        public void Warn(object msg)
        {
            if (PrintLevel.HasFlag(Level.Warn))
                UnityEngine.Debug.LogWarning(Prefix + msg);
        }
        [HideInCallstack]
        public void Err(object msg)
        {
            if (PrintLevel.HasFlag(Level.Error))
                UnityEngine.Debug.LogError(Prefix + msg);
        }
        [HideInCallstack]
        public void Exce(object msg, Exception ex)
        {
            if (PrintLevel.HasFlag(Level.Error))
                UnityEngine.Debug.LogError(Prefix + msg);

            UnityEngine.Debug.LogException(ex);
        }
    }
}