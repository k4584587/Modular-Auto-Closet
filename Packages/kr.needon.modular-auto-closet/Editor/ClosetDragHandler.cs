using System.Collections.Generic;
using System.Linq;
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
                {
                    known = new HashSet<Transform>();
                    ClosetChildren[closet] = known;
                }

                var currentChildren = new HashSet<Transform>();
                foreach (Transform child in closet.transform)
                {
                    currentChildren.Add(child);
                    if (known.Add(child))
                    {
                        if (EditorUtility.DisplayDialog("Add Closet", $"새로운 옷 '{child.name}'을 옷장에 추가하시겠습니까?", "Yes", "No"))
                        {
                            AutoClosetCreate.AddClosetToClothing(child.gameObject);
                        }
                    }
                }

                foreach (var t in known.Where(c => c == null || !currentChildren.Contains(c)).ToList())
                {
                    known.Remove(t);
                }
            }

            var closetSet = new HashSet<AutoCloset>(closets);
            foreach (var closet in ClosetChildren.Keys.Where(c => c == null || !closetSet.Contains(c)).ToList())
            {
                ClosetChildren.Remove(closet);
            }
        }
    }
}
