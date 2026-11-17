#if UNITY_EDITOR
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace needon.Editor
{
    internal static class ClosetMaintenanceUtility
    {
        private const string UndoLabel = "Clear Closet Components";

        internal static bool HasClosetInParentOrSelf(GameObject go)
        {
            return FindClosetRoot(go) != null;
        }

        internal static GameObject FindClosetRoot(GameObject obj)
        {
            var t = obj != null ? obj.transform : null;
            while (t != null)
            {
                if (t.GetComponent<AutoCloset>() != null)
                    return t.gameObject;

                t = t.parent;
            }

            return null;
        }

        internal static void ClearCloset(GameObject closetRoot)
        {
            if (closetRoot == null) return;

            Undo.RegisterFullObjectHierarchyUndo(closetRoot, UndoLabel);

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

            foreach (Transform child in closetRoot.transform)
            {
                var go = child.gameObject;

                DestroyIfExists<ClosetConfig>(go);
                DestroyIfExists<ClosetBlendshape>(go);
                DestroyIfExists<ClosetToggle>(go);

                var mi = go.GetComponent<ModularAvatarMenuItem>();
                if (mi != null && mi.Control != null && mi.Control.parameter != null &&
                    !string.IsNullOrEmpty(uniqueName) && mi.Control.parameter.name == uniqueName)
                {
                    Undo.DestroyObjectImmediate(mi);
                }
            }

            foreach (var comp in closetRoot.GetComponentsInChildren<AutoClosetObjectToggle>(true))
                Undo.DestroyObjectImmediate(comp);
            foreach (var comp in closetRoot.GetComponentsInChildren<BlendshapeToggle>(true))
                Undo.DestroyObjectImmediate(comp);
            // MA Shape Changer는 지우지 않는다. 지금 도구는 만들지 않는 컴포넌트라
            // 의상 제작자·유저가 넣어 둔 것과 구분할 수 없다.

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

        internal static bool ContainsLegacyChangedShape(GameObject closetRoot)
        {
            if (closetRoot == null) return false;

            return closetRoot
                .GetComponentsInChildren<Component>(true)
                .Any(IsChangedShapeComponent);
        }

        private static bool IsChangedShapeComponent(Component component)
        {
            if (component == null) return false;

            var type = component.GetType();
            if (type == null) return false;

            var typeName = type.Name;
            if (typeName == null) return false;

            if (typeName == "ChangedShape")
                return true;

            var fullName = type.FullName;
            return !string.IsNullOrEmpty(fullName) && fullName.EndsWith(".ChangedShape");
        }

        private static void DestroyIfExists<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c != null) Undo.DestroyObjectImmediate(c);
        }
    }
}
#endif

