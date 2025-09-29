#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
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
            return Selection.gameObjects.Any(ClosetMaintenanceUtility.HasClosetInParentOrSelf);
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
                var root = ClosetMaintenanceUtility.FindClosetRoot(go);
                if (root != null) roots.Add(root);
            }

            foreach (var root in roots)
            {
                ClosetMaintenanceUtility.ClearCloset(root);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
#endif

