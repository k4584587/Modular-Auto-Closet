using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace needon.Editor.Pass
{
    /// <summary>
    /// 옷장으로 옷을 이동할 때 기존 애니메이션 클립의 경로를 자동으로 리매핑합니다.
    /// </summary>
    internal static class AnimationPathRemapper
    {
        /// <summary>
        /// 옷 오브젝트와 연관된 모든 애니메이션 클립을 수집합니다.
        /// </summary>
        private static List<AnimationClip> CollectAnimationClips(GameObject clothesObject)
        {
            var clips = new HashSet<AnimationClip>();

            // 1. Animator 컴포넌트에서 애니메이션 클립 수집
            var animators = clothesObject.GetComponentsInChildren<Animator>(true);
            foreach (var animator in animators)
            {
                if (animator.runtimeAnimatorController is AnimatorController controller)
                {
                    foreach (var clip in controller.animationClips)
                    {
                        if (clip != null)
                            clips.Add(clip);
                    }
                }
            }

            // 2. Animation 컴포넌트에서 애니메이션 클립 수집 (레거시)
            var animations = clothesObject.GetComponentsInChildren<Animation>(true);
            foreach (var animation in animations)
            {
                foreach (AnimationState state in animation)
                {
                    if (state.clip != null)
                        clips.Add(state.clip);
                }
            }

            return clips.ToList();
        }

        /// <summary>
        /// 경로 매핑을 위한 정보를 수집합니다.
        /// </summary>
        private static Dictionary<string, string> BuildPathMapping(Transform clothesTransform, Transform avatarRoot, Transform closetTransform)
        {
            var mapping = new Dictionary<string, string>();

            // 옷과 그 하위의 모든 Transform에 대해 경로 매핑 생성
            var allTransforms = clothesTransform.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                var oldPath = GetRelativePathFromRoot(t, avatarRoot, excludeParent: closetTransform);
                var newPath = GetRelativePathFromRoot(t, avatarRoot, excludeParent: null);

                if (oldPath != newPath && !string.IsNullOrEmpty(oldPath))
                {
                    mapping[oldPath] = newPath;
                }
            }

            return mapping;
        }

        /// <summary>
        /// 루트로부터의 상대 경로를 계산합니다.
        /// excludeParent가 지정되면 해당 부모를 경로에서 제외합니다.
        /// </summary>
        private static string GetRelativePathFromRoot(Transform target, Transform root, Transform excludeParent)
        {
            if (target == root) return "";

            var pathParts = new List<string>();
            var current = target;

            while (current != null && current != root)
            {
                // excludeParent가 지정되고 현재가 그 자식이면, excludeParent는 경로에서 제외
                if (excludeParent != null && current.parent == excludeParent)
                {
                    pathParts.Add(current.name);
                    current = excludeParent.parent;
                    continue;
                }

                pathParts.Add(current.name);
                current = current.parent;
            }

            pathParts.Reverse();
            return string.Join("/", pathParts);
        }

        /// <summary>
        /// 경로를 리매핑합니다. 정확히 일치하거나 부분 매칭되는 경로를 찾습니다.
        /// </summary>
        private static string RemapPath(string originalPath, Dictionary<string, string> pathMapping, out bool wasChanged)
        {
            wasChanged = false;

            // 정확히 일치하는 경로가 있으면 사용
            if (pathMapping.TryGetValue(originalPath, out var newPath))
            {
                wasChanged = true;
                return newPath;
            }

            // 부분 매칭 확인 (가장 긴 경로부터 매칭)
            foreach (var kvp in pathMapping.OrderByDescending(p => p.Key.Length))
            {
                if (originalPath.StartsWith(kvp.Key + "/"))
                {
                    // Substring을 사용하여 경로의 시작 부분만 안전하게 치환
                    wasChanged = true;
                    return kvp.Value + originalPath.Substring(kvp.Key.Length);
                }
            }

            return originalPath;
        }

        /// <summary>
        /// 애니메이션 클립의 모든 바인딩 경로를 리매핑합니다.
        /// </summary>
        private static AnimationClip RemapClipPaths(AnimationClip originalClip, Dictionary<string, string> pathMapping, string clipSavePath)
        {
            if (originalClip == null || pathMapping.Count == 0)
                return originalClip;

            bool hasChanges = false;
            var newClip = new AnimationClip();

            // 클립 설정 복사
            newClip.frameRate = originalClip.frameRate;
            newClip.wrapMode = originalClip.wrapMode;
            newClip.legacy = originalClip.legacy;

            // 모든 curve bindings 처리
            var curveBindings = AnimationUtility.GetCurveBindings(originalClip);
            foreach (var binding in curveBindings)
            {
                var curve = AnimationUtility.GetEditorCurve(originalClip, binding);
                var newBinding = binding;

                var remappedPath = RemapPath(binding.path, pathMapping, out bool pathChanged);
                if (pathChanged)
                {
                    newBinding.path = remappedPath;
                    hasChanges = true;
                    needon.Editor.Util.ClosetLogger.Log(originalClip, "Log.Remap.PathChanged", binding.path, remappedPath);
                }

                AnimationUtility.SetEditorCurve(newClip, newBinding, curve);
            }

            // 오브젝트 참조 바인딩 처리
            var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(originalClip);
            foreach (var binding in objectBindings)
            {
                var curve = AnimationUtility.GetObjectReferenceCurve(originalClip, binding);
                var newBinding = binding;

                var remappedPath = RemapPath(binding.path, pathMapping, out bool pathChanged);
                if (pathChanged)
                {
                    newBinding.path = remappedPath;
                    hasChanges = true;
                    needon.Editor.Util.ClosetLogger.Log(originalClip, "Log.Remap.PathChanged", binding.path, remappedPath);
                }

                AnimationUtility.SetObjectReferenceCurve(newClip, newBinding, curve);
            }

            // 변경사항이 없으면 원본 반환
            if (!hasChanges)
            {
                return originalClip;
            }

            // 새 클립 저장
            var directory = System.IO.Path.GetDirectoryName(clipSavePath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipSavePath) == null)
            {
                AssetDatabase.CreateAsset(newClip, clipSavePath);
            }
            else
            {
                // 기존 에셋 덮어쓰기
                var existingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipSavePath);
                EditorUtility.CopySerialized(newClip, existingClip);
                EditorUtility.SetDirty(existingClip);
                newClip = existingClip;
            }

            AssetDatabase.SaveAssets();
            needon.Editor.Util.ClosetLogger.Log(originalClip, "Log.Remap.ClipSaved", clipSavePath);

            return newClip;
        }

        /// <summary>
        /// 옷장에 추가된 옷의 애니메이션 경로를 리매핑합니다.
        /// </summary>
        /// <param name="clothesObject">옷 GameObject</param>
        /// <param name="closetObject">옷장 GameObject</param>
        /// <param name="avatarRoot">아바타 루트 Transform</param>
        public static void RemapClothesAnimations(GameObject clothesObject, GameObject closetObject, Transform avatarRoot)
        {
            try
            {
                if (clothesObject == null || closetObject == null || avatarRoot == null)
                {
                    needon.Editor.Util.ClosetLogger.LogWarning(clothesObject, "Log.Remap.InvalidParameters");
                    return;
                }

                // 1. 애니메이션 클립 수집
                var clips = CollectAnimationClips(clothesObject);
                if (clips.Count == 0)
                {
                    needon.Editor.Util.ClosetLogger.Log(clothesObject, "Log.Remap.NoClipsFound");
                    return;
                }

                needon.Editor.Util.ClosetLogger.Log(clothesObject, "Log.Remap.ClipsFound", clips.Count);

                // 2. 경로 매핑 생성
                var pathMapping = BuildPathMapping(clothesObject.transform, avatarRoot, closetObject.transform);
                if (pathMapping.Count == 0)
                {
                    needon.Editor.Util.ClosetLogger.Log(clothesObject, "Log.Remap.NoPathChanges");
                    return;
                }

                // 3. 각 클립 리매핑
                int remappedCount = 0;
                foreach (var clip in clips)
                {
                    // 원본 클립이 프로젝트 에셋이 아니면 스킵 (런타임 생성 클립 등)
                    var assetPath = AssetDatabase.GetAssetPath(clip);
                    if (string.IsNullOrEmpty(assetPath))
                        continue;

                    // 이미 생성된 클립이면 스킵 (무한 루프 방지)
                    if (assetPath.Contains("__Generated") && assetPath.Contains("_remapped"))
                        continue;

                    var clipName = clip.name;
                    var savePath = $"Packages/nadena.dev.ndmf/__Generated/{closetObject.name}/_remapped/{clipName}.anim";

                    var remappedClip = RemapClipPaths(clip, pathMapping, savePath);
                    if (remappedClip != clip)
                    {
                        remappedCount++;
                    }
                }

                if (remappedCount > 0)
                {
                    needon.Editor.Util.ClosetLogger.Log(clothesObject, "Log.Remap.Success", remappedCount);
                }
            }
            catch (Exception e)
            {
                needon.Editor.Util.ClosetLogger.LogError(clothesObject, "Log.Remap.Error", e.Message);
            }
        }

        /// <summary>
        /// 옷장의 모든 옷에 대해 애니메이션 경로를 리매핑합니다.
        /// </summary>
        public static void RemapClosetAnimations(GameObject closetObject, Transform avatarRoot)
        {
            if (closetObject == null || avatarRoot == null)
                return;

            foreach (Transform child in closetObject.transform)
            {
                RemapClothesAnimations(child.gameObject, closetObject, avatarRoot);
            }
        }
    }
}
