#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using needon.Editor.Util;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace needon.Editor
{
    internal static class UpgradeLegacyClosetMenu
    {
        private const string MenuPath = "GameObject/Hirami/Upgrade Legacy Closet";
        private const int MenuPriority = 51;

        [MenuItem(MenuPath, true, MenuPriority)]
        private static bool ValidateUpgrade()
        {
            return Selection.gameObjects.Any(HasLegacyClosetInSelection);
        }

        [MenuItem(MenuPath, false, MenuPriority)]
        private static void Upgrade()
        {
            var selected = Selection.gameObjects;
            if (selected.Length == 0) return;

            var legacyRoots = new HashSet<GameObject>();
            foreach (var go in selected)
            {
                var root = ClosetMaintenanceUtility.FindClosetRoot(go);
                if (root == null) continue;

                if (ClosetMaintenanceUtility.ContainsLegacyChangedShape(root))
                    legacyRoots.Add(root);
            }

            if (legacyRoots.Count == 0) return;

            var context = legacyRoots.First();
            var title = ClosetLocalization.Get(context, "Dialog.LegacyCloset.Title");
            var message = ClosetLocalization.Get(context, "Dialog.LegacyCloset.Message");
            var confirm = ClosetLocalization.Get(context, "Dialog.LegacyCloset.Confirm");
            var cancel = ClosetLocalization.Get(context, "Dialog.Cancel");

            if (!EditorUtility.DisplayDialog(title, message, confirm, cancel))
                return;

            foreach (var root in legacyRoots)
            {
                ClosetMaintenanceUtility.ClearCloset(root);
            }

            AssetDatabase.SaveAssets();

            var previousSelection = Selection.objects;
            try
            {
                Selection.objects = legacyRoots.Cast<Object>().ToArray();
                AutoClosetCreate.ApplyToAvatar();
            }
            finally
            {
                Selection.objects = previousSelection;
            }
        }

        private static bool HasLegacyClosetInSelection(GameObject go)
        {
            var root = ClosetMaintenanceUtility.FindClosetRoot(go);
            if (root == null) return false;

            return ClosetMaintenanceUtility.ContainsLegacyChangedShape(root);
        }
    }
}
#endif

