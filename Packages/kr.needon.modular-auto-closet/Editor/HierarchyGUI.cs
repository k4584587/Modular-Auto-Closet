#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace needon.Editor
{
    public static class HierarchyGUI
    {
        private const int IconSize = 16;
        // VRC Hierarchy Highlighter 우측 체크박스(kIconSize=20)와 겹치지 않도록 1칸 왼쪽에 배치
        private const float RightOffset = 22f;
        private const string ToggleIconPath = "Packages/kr.needon.modular-auto-closet/Resource/toggleON.png";

        private static Texture2D _toggleIcon;
        private static bool _toggleIconLoaded;
        private static readonly HashSet<int> _toggleAncestorIds = new HashSet<int>();
        private static bool _cacheDirty = true;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnGUI;
            EditorApplication.hierarchyChanged += MarkCacheDirty;
        }

        private static void MarkCacheDirty()
        {
            _cacheDirty = true;
        }

        private static void EnsureToggleAncestorCache()
        {
            if (!_cacheDirty) return;
            _cacheDirty = false;
            _toggleAncestorIds.Clear();

            var toggles = UnityEngine.Object.FindObjectsOfType<AutoClosetObjectToggle>(true);
            foreach (var t in toggles)
            {
                if (t == null) continue;

                // 상위 옷장 루트까지(옷장 없는 아바타 토글이면 아바타 루트 직전까지) 조상을 캐싱
                var stopAt = FindClosetRoot(t.transform);
                if (stopAt == null)
                {
                    var avatar = t.GetComponentInParent<VRCAvatarDescriptor>(true);
                    stopAt = avatar != null ? avatar.transform : null;
                }

                if (stopAt == null) continue;

                var cur = t.transform.parent;
                while (cur != null && cur != stopAt)
                {
                    _toggleAncestorIds.Add(cur.gameObject.GetInstanceID());
                    cur = cur.parent;
                }
            }
        }

        private static Texture2D GetToggleIcon()
        {
            if (!_toggleIconLoaded)
            {
                _toggleIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(ToggleIconPath);
                _toggleIconLoaded = true;
            }
            return _toggleIcon;
        }

        private static Texture2D GetComponentIcon(Component component)
        {
            var componentType = component.GetType();
            if (componentType == typeof(AutoClosetObjectToggle))
            {
                var toggleIcon = GetToggleIcon();
                if (toggleIcon != null) return toggleIcon;
            }

            // 타입별로 캐싱하지 않는다: ScriptIconInitializer가 delayCall로 아이콘을 늦게 입히므로,
            // 그 전에 캐싱하면 기본 스크립트 아이콘이 도메인 리로드까지 고정된다.
            return AssetPreview.GetMiniThumbnail(component);
        }

        private static void OnGUI(int instanceID, Rect selectionRect)
        {
            var gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (gameObject == null)
            {
                return;
            }

            EnsureToggleAncestorCache();

            var hasSelf = HasClosetComponents(gameObject);
            var hasChildToggle = !hasSelf && _toggleAncestorIds.Contains(instanceID);

            if (!hasSelf && !hasChildToggle) return;

            // 우측 끝 체크박스(VRC Hierarchy Highlighter 등)와 겹치지 않도록 왼쪽으로 오프셋
            var iconRect = new Rect(selectionRect.xMax - RightOffset - IconSize, selectionRect.y, IconSize, IconSize);

            if (hasSelf)
            {
                var components = gameObject.GetComponents<Component>();
                DrawIcons(components, iconRect);
            }
            else if (hasChildToggle)
            {
                // 자식에 토글이 있으면 부모(Toggle 폴더 등)에도 토글 아이콘 표시
                var icon = GetToggleIcon();
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

                var icon = GetComponentIcon(component);
                if (icon == null) continue;
                GUI.DrawTexture(iconRect, icon);
                iconRect.x -= IconSize; // 여러 아이콘이 겹치지 않게 왼쪽으로 이동
            }
        }
    }
}
#endif
