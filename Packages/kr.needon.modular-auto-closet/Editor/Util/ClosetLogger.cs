#if UNITY_EDITOR
using System;
using UnityEngine;

namespace needon.Editor.Util
{
    internal static class ClosetLogger
    {
        public static void Log(UnityEngine.Object ctx, string key, params object[] args)
        {
            Debug.Log(ClosetLocalization.Get(ctx, key, args), ctx);
        }

        public static void LogWarning(UnityEngine.Object ctx, string key, params object[] args)
        {
            Debug.LogWarning(ClosetLocalization.Get(ctx, key, args), ctx);
        }

        public static void LogError(UnityEngine.Object ctx, string key, params object[] args)
        {
            Debug.LogError(ClosetLocalization.Get(ctx, key, args), ctx);
        }
    }
}
#endif
