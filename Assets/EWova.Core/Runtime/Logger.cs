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
            None = 0,
            Info = 1 << 0,
            Warn = 1 << 1,
            Error = 1 << 2,

            Full = Info | Warn | Error,
        }

        public Level PrintLevel;

        [NonSerialized]
        public string Prefix = "";

        public bool InfoEnabled =>
            (PrintLevel & Level.Info) != 0;

        public bool WarnEnabled =>
            (PrintLevel & Level.Warn) != 0;

        public bool ErrorEnabled =>
            (PrintLevel & Level.Error) != 0;

        public Logger(
            string prefix = "",
            Level printLevel = Level.Warn | Level.Error)
        {
            Prefix = prefix;
            PrintLevel = printLevel;
        }

        [HideInCallstack]
        public void Info(object msg)
        {
            Debug.Log(Prefix + msg);
        }

        [HideInCallstack]
        public void Warn(object msg)
        {
            Debug.LogWarning(Prefix + msg);
        }

        [HideInCallstack]
        public void Err(object msg)
        {
            Debug.LogError(Prefix + msg);
        }
    }
}