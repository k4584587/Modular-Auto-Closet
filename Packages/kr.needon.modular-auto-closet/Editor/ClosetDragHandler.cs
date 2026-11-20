#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;

namespace needon.Editor
{
    [InitializeOnLoad]
    internal static class ClosetDragHandler
    {
        private static readonly Dictionary<AutoCloset, HashSet<Transform>> ClosetChildren;

        static ClosetDragHandler()
        {
            ClosetChildren = new Dictionary<AutoCloset, HashSet<Transform>>();
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            Refresh();
        }

        private static void Refresh()
        {
            ClosetChildren.Clear();
            foreach (var closet in Object.FindObjectsOfType<AutoCloset>(true))
            {
                var children = new HashSet<Transform>();
                foreach (Transform child in closet.transform)
                {
                    children.Add(child);
                }
                ClosetChildren[closet] = children;
            }
        }

        private static void OnHierarchyChanged()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var closets = Object.FindObjectsOfType<AutoCloset>(true);
            foreach (var closet in closets)
            {
                if (!ClosetChildren.TryGetValue(closet, out var known))
                {
                    // 새로 등장한 옷장(씬 로드·프리팹 배치·복제 등)은 현재 자식 스냅샷만 기록하고 팝업을 띄우지 않음
                    known = new HashSet<Transform>();
                    foreach (Transform child in closet.transform)
                        known.Add(child);
                    ClosetChildren[closet] = known;
                    continue;
                }

                var currentChildren = new HashSet<Transform>();
                foreach (Transform child in closet.transform)
                {
                    currentChildren.Add(child);

                    // 이미 ‘옷장용’ 컴포넌트가 붙어 있으면 known 에만 추가하고 대화상자 생략
                    if (child.GetComponent<ModularAvatarMenuItem>() != null)
                    {
                        known.Add(child);
                        continue;
                    }

                    // 새로 감지된 자식에 대해 대화상자 표시 (로컬라이즈)
                    if (known.Add(child))
                    {
                        var title = needon.Editor.Util.ClosetLocalization.Get(child, "Dialog.AddClothing.Title");
                        var msg   = needon.Editor.Util.ClosetLocalization.Get(child, "Dialog.AddClothing.Message", child.name);
                        var yes   = needon.Editor.Util.ClosetLocalization.Get(child, "Dialog.Yes");
                        var no    = needon.Editor.Util.ClosetLocalization.Get(child, "Dialog.No");
                        if (EditorUtility.DisplayDialog(title, msg, yes, no))
                        {
                            AutoClosetCreate.AddClosetToClothing(child.gameObject);
                        }
                    }
                }

                // 제거된 자식 정리
                foreach (var t in known.Where(c => c == null || !currentChildren.Contains(c)).ToList())
                    known.Remove(t);
            }

            // 삭제된 옷장 정리
            var closetSet = new HashSet<AutoCloset>(closets);
            foreach (var closet in ClosetChildren.Keys.Where(c => c == null || !closetSet.Contains(c)).ToList())
                ClosetChildren.Remove(closet);
        }
    }
}
#endif
