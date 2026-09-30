using System;

using UnityEngine;

namespace EWova
{
    public static class EventExtensions
    {
        public static void InvokeSafely(
            this Action action,
            Action<Exception> onException = null)
        {
            if (action == null)
                return;

            Delegate[] handlers = action.GetInvocationList();

            for (int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    ((Action)handlers[i])();
                }
                catch (Exception ex)
                {
                    HandleException(ex, onException);
                }
            }
        }


        public static void InvokeSafely<T>(
            this Action<T> action,
            T arg,
            Action<Exception> onException = null)
        {
            if (action == null)
                return;

            Delegate[] handlers = action.GetInvocationList();

            for (int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    ((Action<T>)handlers[i])(arg);
                }
                catch (Exception ex)
                {
                    HandleException(ex, onException);
                }
            }
        }


        public static void InvokeSafely<T1, T2>(
            this Action<T1, T2> action,
            T1 arg1,
            T2 arg2,
            Action<Exception> onException = null)
        {
            if (action == null)
                return;

            Delegate[] handlers = action.GetInvocationList();

            for (int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    ((Action<T1, T2>)handlers[i])(arg1, arg2);
                }
                catch (Exception ex)
                {
                    HandleException(ex, onException);
                }
            }
        }


        private static void HandleException(
            Exception ex,
            Action<Exception> onException)
        {
            try
            {
                if (onException != null)
                {
                    onException(ex);
                }
                else
                {
                    Debug.LogException(ex);
                }
            }
            catch (Exception callbackException)
            {
                Debug.LogException(callbackException);
            }
        }
    }
}