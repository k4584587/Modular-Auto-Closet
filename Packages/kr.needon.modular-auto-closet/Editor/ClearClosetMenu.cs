#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace needon.Editor
{
    internal static class ClearClosetMenu
    {
        private const string MenuPath = "GameObject/Hirami/Clear Closet Components";
        private const int MenuPriority = 50;

        [MenuItem(MenuPath, true, MenuPriority)]
        private static bool ValidateClear()
        {
            return Selection.gameObjects.Any(HasClosetInParentOrSelf);
        }

        [MenuItem(MenuPath, false, MenuPriority)]
        private static void Clear()
        {
            var selected = Selection.gameObjects;
            if (selected.Length == 0) return;

            if (!EditorUtility.DisplayDialog(
                    "Clear Closet Components",
                    "선택된 옷장에서 AutoCloset 관련 컴포넌트를 제거합니다.\n옷 오브젝트(게임오브젝트)는 삭제되지 않습니다.",
                    "OK", "Cancel"))
            {
                return;
            }

            var roots = new HashSet<GameObject>();
            foreach (var go in selected)
            {
                var root = FindClosetRoot(go);
                if (root != null) roots.Add(root);
            }

            foreach (var root in roots)
            {
                ClearOneCloset(root);
            }

            AssetDatabase.SaveAssets();
        }

        private static void ClearOneCloset(GameObject closetRoot)
        {
            Undo.RegisterFullObjectHierarchyUndo(closetRoot, "Clear Closet Components");

            // Find unique parameter name (AutoCloset_XXXX)
            string uniqueName = null;
            var maParams = closetRoot.GetComponent<ModularAvatarParameters>();
            if (maParams != null && maParams.parameters != null)
            {
                foreach (var p in maParams.parameters)
                {
                    if (!string.IsNullOrEmpty(p.nameOrPrefix) && p.nameOrPrefix.StartsWith("AutoCloset_"))
                    {
                        uniqueName = p.nameOrPrefix;
                        break;
                    }
                }
            }

            // Remove per-child components
            foreach (Transform child in closetRoot.transform)
            {
                var go = child.gameObject;

                // Remove ClosetConfig + legacy components
                DestroyIfExists<ClosetConfig>(go);
                DestroyIfExists<ClosetBlendshape>(go);
                DestroyIfExists<ClosetToggle>(go);

                // Remove menu item only if it belongs to this closet (parameter matches uniqueName)
                var mi = go.GetComponent<ModularAvatarMenuItem>();
                if (mi != null && mi.Control != null && mi.Control.parameter != null &&
                    !string.IsNullOrEmpty(uniqueName) && mi.Control.parameter.name == uniqueName)
                {
                    Undo.DestroyObjectImmediate(mi);
                }
            }

            // Remove helper components anywhere under root that are clearly closet-specific
            foreach (var comp in closetRoot.GetComponentsInChildren<AutoClosetObjectToggle>(true))
                Undo.DestroyObjectImmediate(comp);
            foreach (var comp in closetRoot.GetComponentsInChildren<BlendshapeToggle>(true))
                Undo.DestroyObjectImmediate(comp);

            // Clean root: menu item, parameters (only our AutoCloset_*), installer, and AutoCloset itself
            var rootMenu = closetRoot.GetComponent<ModularAvatarMenuItem>();
            if (rootMenu != null) Undo.DestroyObjectImmediate(rootMenu);

            if (maParams != null && maParams.parameters != null)
            {
                maParams.parameters.RemoveAll(p => !string.IsNullOrEmpty(p.nameOrPrefix) && p.nameOrPrefix.StartsWith("AutoCloset_"));
                if (maParams.parameters.Count == 0)
                {
                    Undo.DestroyObjectImmediate(maParams);
                }
                else
                {
                    EditorUtility.SetDirty(maParams);
                }
            }

            var installer = closetRoot.GetComponent<ModularAvatarMenuInstaller>();
            if (installer != null) Undo.DestroyObjectImmediate(installer);

            var closet = closetRoot.GetComponent<AutoCloset>();
            if (closet != null) Undo.DestroyObjectImmediate(closet);
        }

        private static void DestroyIfExists<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c != null) Undo.DestroyObjectImmediate(c);
        }

        private static bool HasClosetInParentOrSelf(GameObject go)
        {
            return FindClosetRoot(go) != null;
        }

        private static GameObject FindClosetRoot(GameObject obj)
        {
            var t = obj != null ? obj.transform : null;
            while (t != null)
            {
                if (t.GetComponent<AutoCloset>() != null) return t.gameObject;
                t = t.parent;
            }
            return null;
        }
    }
}
#endif

