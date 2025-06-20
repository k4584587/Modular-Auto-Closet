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
            foreach (var closet in Object.FindObjectsOfType<AutoCloset>())
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
            var closets = Object.FindObjectsOfType<AutoCloset>();
            foreach (var closet in closets)
            {
                if (!ClosetChildren.TryGetValue(closet, out var known))
                    ClosetChildren[closet] = known = new HashSet<Transform>();

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

                    // 새로 감지된 자식에 대해 대화상자 표시 (한국어 + 영어)
                    if (known.Add(child))
                    {
                        if (EditorUtility.DisplayDialog(
                                "옷장 추가 / Add Closet",
                                $"새로운 옷 '{child.name}'을 옷장에 추가하시겠습니까?\n" +
                                $"Would you like to add the new clothing item '{child.name}' to the closet?",
                                "예 / Yes",
                                "아니요 / No"))
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
