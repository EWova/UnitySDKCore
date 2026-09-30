#if UNITY_EDITOR
using System;

using UnityEditor;

using UnityEngine;

namespace EWova.Authoring
{
    public static class EditorDomainReleaseHelper
    {
        public static event Action CleanupOneShot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Initialize()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode)
                return;

            CleanupOneShot?.Invoke();
            CleanupOneShot = null;
        }
    }
}
#endif