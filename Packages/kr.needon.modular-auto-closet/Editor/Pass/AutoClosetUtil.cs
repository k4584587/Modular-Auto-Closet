using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace needon.Editor.Pass
{
    internal static class AutoClosetUtil
    {
        private static AnimatorControllerLayer AddLayer(AnimatorController animator, string layerName)
        {
            var layer = new AnimatorControllerLayer();
            layer.name = animator.MakeUniqueLayerName(layerName);
            layer.defaultWeight = 1f;

            layer.stateMachine = new AnimatorStateMachine();
            layer.stateMachine.name = layer.name.Replace(".", "_");
            layer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(animator)))
                AssetDatabase.AddObjectToAsset(layer.stateMachine, AssetDatabase.GetAssetPath(animator));
            EditorUtility.SetDirty(layer.stateMachine);

            animator.AddLayer(layer);
            return layer;
        }

        private static void ErrorDialog(string message)
        {
            EditorUtility.DisplayDialog("ERROR!", message, "OK");
            throw new Exception(message);
        }

        private static string GetRelativePath(Transform target, Transform root)
        {
            if (target == root) return "";

            var path = target.name;
            var current = target.parent;

            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        public static RuntimeAnimatorController GetAvatarFxAnimator(VRCAvatarDescriptor avatarDescriptor)
        {
            var controllerLayer = Array.FindIndex(
                avatarDescriptor.baseAnimationLayers,
                item => item.type == VRCAvatarDescriptor.AnimLayerType.FX && item.animatorController
            );

            if (controllerLayer == -1)
            {
                ErrorDialog("Cannot find FX animator controller!");
                return null;
            }

            var animatorController = avatarDescriptor.baseAnimationLayers[controllerLayer].animatorController;
            if (animatorController) return animatorController;
            ErrorDialog("Cannot find FX animator controller!");
            return null;
        }

        public static void AddAnimatorParameter(AnimatorController controller, string paramName, AnimatorControllerParameterType type)
        {
            var index = 0;
            for (; index < controller.parameters.Length; index++)
            {
                var param = controller.parameters[index];
                if (param.name == paramName) return;
            }

            controller.AddParameter(new AnimatorControllerParameter
            {
                name = paramName,
                type = type
            });
        }

        public static AnimationClip CreateClosetAnimationClip(GameObject closet, string parentName, string activeClothesName)
        {
            var clipPath = $"Packages/nadena.dev.ndmf/__Generated/{parentName}/_assets/{activeClothesName}.anim";

            // 디렉토리가 없으면 생성
            var directory = System.IO.Path.GetDirectoryName(clipPath);
            if (!System.IO.Directory.Exists(directory))
            {
                if (directory != null) System.IO.Directory.CreateDirectory(directory);
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
            }
            else
            {
                // 기존 커브 모두 제거
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    AnimationUtility.SetEditorCurve(clip, binding, null);
                }
            }

            // 아바타 루트 찾기
            var avatarRoot = closet.transform.root;

            // 모든 옷장 자식(옷)의 활성화 상태를 애니메이션으로 기록
            foreach (Transform child in closet.transform)
            {
                var isActive = (child.name == activeClothesName);

                // 단일 키프레임만 사용하여 0초 시점에만 값을 기록
                var curve = new AnimationCurve(
                    new Keyframe(0f, isActive ? 1f : 0f)
                );

                // 아바타 루트로부터의 상대 경로 계산
                var relativePath = GetRelativePath(child.transform, avatarRoot);
                Debug.Log($"Setting animation path: {relativePath} (Active: {isActive})");

                var binding = EditorCurveBinding.FloatCurve(
                    relativePath,
                    typeof(GameObject),
                    "m_IsActive"
                );

                AnimationUtility.SetEditorCurve(clip, binding, curve);

                // 추가 ClosetToggle 컴포넌트 처리
                var closetToggle = child.GetComponent<ClosetToggle>();
                if (closetToggle != null && closetToggle.toggles != null)
                {
                    foreach (var toggle in closetToggle.toggles)
                    {
                        if (toggle == null || toggle.target == null) continue;

                        var toggleCurve = new AnimationCurve(
                            new Keyframe(0f, isActive ? (toggle.active ? 1f : 0f) : (toggle.active ? 0f : 1f))
                        );

                        var togglePath = GetRelativePath(toggle.target.transform, avatarRoot);
                        Debug.Log($"Toggle path: {togglePath} (Active: {isActive})");

                        var toggleBinding = EditorCurveBinding.FloatCurve(
                            togglePath,
                            typeof(GameObject),
                            "m_IsActive"
                        );

                        AnimationUtility.SetEditorCurve(clip, toggleBinding, toggleCurve);
                    }
                }

                // 추가 ClosetBlendshape 컴포넌트 처리
                var closetBlendshape = child.GetComponent<ClosetBlendshape>();
                if (closetBlendshape != null && closetBlendshape.shapes != null)
                {
                    foreach (var item in closetBlendshape.shapes)
                    {
                        if (item == null || item.mesh == null) continue;

                        var bsCurve = new AnimationCurve(
                            new Keyframe(0f, isActive ? item.value : 0f)
                        );

                        var bsPath = GetRelativePath(item.mesh.transform, avatarRoot);
                        Debug.Log($"Blendshape path: {bsPath} (Active: {isActive})");

                        var bsBinding = EditorCurveBinding.FloatCurve(
                            bsPath,
                            typeof(SkinnedMeshRenderer),
                            $"blendShape.{item.shapeKey}"
                        );

                        AnimationUtility.SetEditorCurve(clip, bsBinding, bsCurve);
                    }
                }
            }

            // 애니메이션 저장
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) == null)
            {
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            else
            {
                EditorUtility.SetDirty(clip);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Animation clip saved at: {clipPath}");
            return clip;
        }

        public static void ApplyCreateAnimatorLayer(AnimatorController controller, string animatorLayerName)
        {
            var layer = AddLayer(controller, animatorLayerName);
            layer.defaultWeight = 1f;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            AssetDatabase.SaveAssets();

            var layers = controller.layers;
            for (var i = 0; i < layers.Length; i++)
            {
                if (layers[i].name != animatorLayerName) continue;
                layers[i] = layer;
                controller.layers = layers;
                break;
            }
        }

        /// <summary>
        /// 단일 타겟에 대해 On 또는 Off 애니메이션 클립을 생성합니다.
        /// </summary>
        /// <param name="target">애니메이션을 기록할 GameObject</param>
        /// <param name="parentName">클립이 저장될 폴더명 (패키지 내 __Generated 경로 아래)</param>
        /// <param name="clipName">클립 파일명 (확장자 없이)</param>
        /// <param name="isOn">true면 On 클립, false면 Off 클립</param>
        public static AnimationClip CreateToggleAnimationClip(GameObject target, string parentName, string clipName, bool isOn)
        {
            // On/Off 구분 접미사
            var suffix = isOn ? "On" : "Off";
            // Packages/nadena.dev.ndmf/__Generated/{parentName}/_assets/{clipName}_{On|Off}.anim
            var clipPath = $"Packages/nadena.dev.ndmf/__Generated/{parentName}/_assets/{clipName}_{suffix}.anim";

            // 디렉토리 생성
            var directory = System.IO.Path.GetDirectoryName(clipPath);
            if (!System.IO.Directory.Exists(directory))
                System.IO.Directory.CreateDirectory(directory);

            // 기존 클립 불러오기 또는 새로 만들기
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) ?? new AnimationClip();
            // 기존 커브 제거
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                AnimationUtility.SetEditorCurve(clip, binding, null);

            // 타겟의 루트(씬 최상위) 기준 상대 경로 계산
            var root = target.transform.root;
            var relativePath = GetRelativePath(target.transform, root);
            Debug.Log($"[Toggle] Setting path: {relativePath} = {(isOn ? 1f : 0f)}");

            // 단일 키프레임 커브(0초에만 값 기록)
            var curve = new AnimationCurve(new Keyframe(0f, isOn ? 1f : 0f));
            var bindingInfo = EditorCurveBinding.FloatCurve(
                relativePath,
                typeof(GameObject),
                "m_IsActive"
            );
            AnimationUtility.SetEditorCurve(clip, bindingInfo, curve);

            // 에셋으로 저장
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) == null)
                AssetDatabase.CreateAsset(clip, clipPath);
            else
                EditorUtility.SetDirty(clip);

            AssetDatabase.SaveAssets();
            Debug.Log($"Toggle animation clip saved: {clipPath}");
            return clip;
        }

        /// <summary>
        /// 지정한 타겟에 대해 On/Off 두 가지 애니메이션 클립을 생성합니다.
        /// </summary>
        /// <param name="target">애니메이션을 기록할 GameObject</param>
        /// <param name="parentName">저장 폴더명</param>
        /// <param name="clipName">클립 기본 이름</param>
        public static void CreateToggleAnimations(GameObject target, string parentName, string clipName)
        {
            CreateToggleAnimationClip(target, parentName, clipName, true);
            CreateToggleAnimationClip(target, parentName, clipName, false);
        }
    }
}