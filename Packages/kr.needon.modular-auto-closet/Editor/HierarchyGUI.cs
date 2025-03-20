#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace needon.Editor
{
    public static class HierarchyGUI
    {
        private const int IconSize = 16;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnGUI;
        }

        private static void OnGUI(int instanceID, Rect selectionRect)
        {
            var gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (gameObject == null)
            {
                return;
            }

            if (!HasClosetComponents(gameObject))
            {
                return;
            }

            var components = gameObject.GetComponents<Component>();

            // 아이콘을 오른쪽 끝에 배치
            var iconRect = new Rect(selectionRect.xMax - IconSize, selectionRect.y, IconSize, IconSize);

            // ToggleItem 또는 ToggleConfig 컴포넌트에 대해 아이콘 표시
            DrawIcons(components, iconRect);
        }

        private static bool HasClosetComponents(GameObject gameObject)
        {
            return gameObject.GetComponent<AutoCloset>();
        }

        private static void DrawIcons(Component[] components, Rect iconRect)
        {
            foreach (var component in components)
            {
                if (component == null)
                {
                    continue;
                }

                var componentType = component.GetType();
                if (componentType != typeof(AutoCloset)) continue;
                var icon = AssetPreview.GetMiniThumbnail(component);
                if (icon == null) continue;
                GUI.DrawTexture(iconRect, icon);
                iconRect.x -= IconSize; // 여러 아이콘이 겹치지 않게 왼쪽으로 이동
            }
        }
    }
}
#endif