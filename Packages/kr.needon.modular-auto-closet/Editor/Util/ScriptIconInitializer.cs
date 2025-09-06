#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace needon.Editor.Util
{
    /// <summary>
    /// Ensures custom component script icons are applied on editor load,
    /// so they show immediately in Hierarchy/Inspector without selection.
    /// </summary>
    [InitializeOnLoad]
    public static class ScriptIconInitializer
    {
        static ScriptIconInitializer()
        {
            // Run after load to ensure AssetDatabase is ready
            EditorApplication.delayCall += ApplyIcons;
        }

        private static void ApplyIcons()
        {
            // Load icons
            var closetIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/kr.needon.modular-auto-closet/Resource/ClosetIcon.png");
            var toggleOnIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/kr.needon.modular-auto-closet/Resource/toggleON.png");

            if (closetIcon == null)
            {
                return;
            }

            // Map component type name -> icon
            var iconMap = new Dictionary<string, Texture2D>(StringComparer.Ordinal)
            {
                {"AutoCloset", closetIcon},
                {"ClosetToggle", closetIcon},
                {"BlendshapeToggle", closetIcon},
                {"ClosetConfig", closetIcon},
                {"ClosetBlendshape", closetIcon},
                {"AutoClosetObjectToggle", closetIcon},
                {"StandaloneToggle", toggleOnIcon != null ? toggleOnIcon : closetIcon}
            };

            // Find all MonoScripts once and map by class name
            var scriptsByName = new Dictionary<string, MonoScript>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] {"Packages/kr.needon.modular-auto-closet"}))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (ms == null) continue;
                var cls = ms.GetClass();
                if (cls == null) continue;
                scriptsByName[cls.Name] = ms;
            }

            bool changed = false;
            foreach (var kv in iconMap)
            {
                if (!scriptsByName.TryGetValue(kv.Key, out var monoScript) || monoScript == null)
                {
                    continue;
                }

                // Apply icon to script asset so instances use it immediately
                EditorGUIUtility.SetIconForObject(monoScript, kv.Value);
                changed = true;
            }

            if (changed)
            {
                EditorApplication.RepaintHierarchyWindow();
                EditorApplication.RepaintProjectWindow();
            }
        }
    }
}
#endif
