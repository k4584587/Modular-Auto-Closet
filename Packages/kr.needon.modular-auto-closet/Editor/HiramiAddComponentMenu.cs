#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace needon.Editor
{
    internal static class HiramiAddComponentMenu
    {
        private const int Priority = 1000; // Component 메뉴 하단 쪽 배치

        // AutoCloset
        [MenuItem("Component/Hirami/AutoCloset", true, Priority)]
        private static bool ValidateAddAutoCloset()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0 &&
                   Selection.gameObjects.Any(go => go.GetComponent<AutoCloset>() == null);
        }

        [MenuItem("Component/Hirami/AutoCloset", false, Priority)]
        private static void AddAutoCloset()
        {
            foreach (var go in Selection.gameObjects.Where(go => go.GetComponent<AutoCloset>() == null))
                Undo.AddComponent<AutoCloset>(go);
        }

        // ClosetConfig (Unified)
        [MenuItem("Component/Hirami/ClosetConfig (Unified)", true, Priority + 1)]
        private static bool ValidateAddClosetConfig()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0 &&
                   Selection.gameObjects.Any(go => go.GetComponent<ClosetConfig>() == null);
        }

        [MenuItem("Component/Hirami/ClosetConfig (Unified)", false, Priority + 1)]
        private static void AddClosetConfig()
        {
            foreach (var go in Selection.gameObjects.Where(go => go.GetComponent<ClosetConfig>() == null))
                Undo.AddComponent<ClosetConfig>(go);
        }

        // Legacy: ClosetToggle
        [MenuItem("Component/Hirami/Legacy/ClosetToggle", true, Priority + 20)]
        private static bool ValidateAddClosetToggle()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0 &&
                   Selection.gameObjects.Any(go => go.GetComponent<ClosetToggle>() == null);
        }

        [MenuItem("Component/Hirami/Legacy/ClosetToggle", false, Priority + 20)]
        private static void AddClosetToggle()
        {
            foreach (var go in Selection.gameObjects.Where(go => go.GetComponent<ClosetToggle>() == null))
                Undo.AddComponent<ClosetToggle>(go);
        }

        // Legacy: ClosetBlendshape
        [MenuItem("Component/Hirami/Legacy/ClosetBlendshape", true, Priority + 21)]
        private static bool ValidateAddClosetBlendshape()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0 &&
                   Selection.gameObjects.Any(go => go.GetComponent<ClosetBlendshape>() == null);
        }

        [MenuItem("Component/Hirami/Legacy/ClosetBlendshape", false, Priority + 21)]
        private static void AddClosetBlendshape()
        {
            foreach (var go in Selection.gameObjects.Where(go => go.GetComponent<ClosetBlendshape>() == null))
                Undo.AddComponent<ClosetBlendshape>(go);
        }

        // Utility: AutoClosetObjectToggle
        [MenuItem("Component/Hirami/Utility/AutoClosetObjectToggle", true, Priority + 40)]
        private static bool ValidateAddAutoClosetObjectToggle()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0 &&
                   Selection.gameObjects.Any(go => go.GetComponent<AutoClosetObjectToggle>() == null);
        }

        [MenuItem("Component/Hirami/Utility/AutoClosetObjectToggle", false, Priority + 40)]
        private static void AddAutoClosetObjectToggle()
        {
            foreach (var go in Selection.gameObjects.Where(go => go.GetComponent<AutoClosetObjectToggle>() == null))
                Undo.AddComponent<AutoClosetObjectToggle>(go);
        }

        // Utility: BlendshapeToggle
        [MenuItem("Component/Hirami/Utility/BlendshapeToggle", true, Priority + 41)]
        private static bool ValidateAddBlendshapeToggle()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0 &&
                   Selection.gameObjects.Any(go => go.GetComponent<BlendshapeToggle>() == null);
        }

        [MenuItem("Component/Hirami/Utility/BlendshapeToggle", false, Priority + 41)]
        private static void AddBlendshapeToggle()
        {
            foreach (var go in Selection.gameObjects.Where(go => go.GetComponent<BlendshapeToggle>() == null))
                Undo.AddComponent<BlendshapeToggle>(go);
        }
    }
}
#endif

