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

            var hasSelf = HasClosetComponents(gameObject);
            var hasChildToggle = !hasSelf && HasChildToggleComponent(gameObject);

            if (!hasSelf && !hasChildToggle) return;

            // 아이콘을 오른쪽 끝에 배치
            var iconRect = new Rect(selectionRect.xMax - IconSize, selectionRect.y, IconSize, IconSize);

            if (hasSelf)
            {
                var components = gameObject.GetComponents<Component>();
                DrawIcons(components, iconRect);
            }
            else if (hasChildToggle)
            {
                // 자식에 토글이 있으면 부모에도 토글 아이콘 표시
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Packages/kr.needon.modular-auto-closet/Resource/toggleON.png");
                if (icon != null)
                {
                    GUI.DrawTexture(iconRect, icon);
                }
            }
        }

        private static bool HasClosetComponents(GameObject gameObject)
        {
            return gameObject.GetComponent<AutoCloset>()
                   || gameObject.GetComponent<ClosetToggle>()
                   || gameObject.GetComponent<ClosetConfig>()
                   || gameObject.GetComponent<AutoClosetObjectToggle>();
        }

        private static bool HasChildToggleComponent(GameObject gameObject)
        {
            var toggles = gameObject.GetComponentsInChildren<AutoClosetObjectToggle>(true);
            if (toggles == null || toggles.Length == 0) return false;

            foreach (var t in toggles)
            {
                if (t == null || t.gameObject == gameObject) continue;

                // 해당 토글이 속한 Closet 루트
                var closetRoot = FindClosetRoot(t.transform);
                if (closetRoot == null) continue;

                // gameObject 가 closetRoot와 토글 사이(또는 closetRoot 자체)일 때만 표시
                if (IsAncestor(gameObject.transform, t.transform) && IsAncestor(closetRoot, gameObject.transform))
                {
                    return true;
                }
            }
            return false;
        }

        private static Transform FindClosetRoot(Transform t)
        {
            while (t != null)
            {
                if (t.GetComponent<AutoCloset>() != null)
                    return t;
                t = t.parent;
            }
            return null;
        }

        private static bool IsAncestor(Transform maybeAncestor, Transform child)
        {
            if (maybeAncestor == null || child == null) return false;
            var cur = child;
            while (cur != null)
            {
                if (cur == maybeAncestor) return true;
                cur = cur.parent;
            }
            return false;
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
                if (componentType != typeof(AutoCloset) && componentType != typeof(ClosetToggle) && componentType != typeof(ClosetConfig) && componentType != typeof(AutoClosetObjectToggle)) continue;

                // Toggle는 전용 아이콘 사용 시도, 없으면 기본 썸네일
                Texture2D icon = null;
                if (componentType == typeof(AutoClosetObjectToggle))
                {
                    icon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                        "Packages/kr.needon.modular-auto-closet/Resource/toggleON.png");
                }
                if (icon == null)
                {
                    icon = AssetPreview.GetMiniThumbnail(component);
                }
                if (icon == null) continue;
                GUI.DrawTexture(iconRect, icon);
                iconRect.x -= IconSize; // 여러 아이콘이 겹치지 않게 왼쪽으로 이동
            }
        }
    }
}
#endif
