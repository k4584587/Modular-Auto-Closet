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
            var controllerLayer = Array.FindIndex(avatarDescriptor.baseAnimationLayers, 
                item => item.type == VRCAvatarDescriptor.AnimLayerType.FX && item.animatorController);
            
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
            foreach (var param in controller.parameters)
            {
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
            string directory = System.IO.Path.GetDirectoryName(clipPath);
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
                AnimationCurve curve = new AnimationCurve(
                    new Keyframe(0f, isActive ? 1f : 0f),
                    new Keyframe(1f, isActive ? 1f : 0f)
                );

                // 아바타 루트로부터의 상대 경로 계산
                string relativePath = GetRelativePath(child.transform, avatarRoot);
                Debug.Log($"Setting animation path: {relativePath} (Active: {isActive})");
                
                var binding = EditorCurveBinding.FloatCurve(
                    relativePath,
                    typeof(GameObject),
                    "m_IsActive"
                );
                
                AnimationUtility.SetEditorCurve(clip, binding, curve);
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
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == animatorLayerName)
                {
                    layers[i] = layer;
                    controller.layers = layers;
                    break;
                }
            }
        }
    }
}
