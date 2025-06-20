using System;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace needon.Editor.Pass
{
    internal class ApplyToggleCreatorPass : Pass<ApplyToggleCreatorPass>
    {
        protected override void Execute(BuildContext context)
        {
            var avatar = context.AvatarDescriptor;
            var fxController = AutoClosetUtil.GetAvatarFxAnimator(avatar) as AnimatorController;
            if (fxController == null)
                throw new InvalidOperationException("Cannot find FX Animator Controller.");

            // 새 AutoClosetObjectToggle 컴포넌트를 모두 찾음
            var toggles = avatar.GetComponentsInChildren<AutoClosetObjectToggle>(true);
            foreach (var toggle in toggles)
            {
                var menu = toggle.GetComponent<ModularAvatarMenuItem>();
                if (menu?.Control?.parameter == null) continue;
                var paramName = menu.Control.parameter.name;
                if (string.IsNullOrEmpty(paramName)) continue;

                // 유효한 타겟만
                var targets = toggle.targets?
                    .Select(t => t.target)
                    .Where(t => t != null)
                    .ToArray();
                if (targets == null || targets.Length == 0) continue;

                // 파라미터와 레이어 추가
                AutoClosetUtil.AddAnimatorParameter(fxController, paramName, AnimatorControllerParameterType.Bool);
                AutoClosetUtil.ApplyCreateAnimatorLayer(fxController, paramName);

                // 새로 추가된 레이어와 상태머신
                var layer = fxController.layers.Last();
                var sm = layer.stateMachine;

                AnimationClip onClip;
                AnimationClip offClip;

                if (targets.Length == 1)
                {
                    // 단일 대상: 기존 로직
                    var target = targets[0];
                    onClip = AutoClosetUtil.CreateToggleAnimationClip(target, paramName, target.name, true);
                    offClip = AutoClosetUtil.CreateToggleAnimationClip(target, paramName, target.name, false);
                }
                else
                {
                    // 그룹 대상: 하나의 클립에 모든 타겟 애니메이션 합치기
                    onClip = new AnimationClip { name = $"{paramName}_Group_On" };
                    offClip = new AnimationClip { name = $"{paramName}_Group_Off" };

                    foreach (var target in targets)
                    {
                        // 각각의 개별 클립 생성
                        var clipOn  = AutoClosetUtil.CreateToggleAnimationClip(target, paramName, target.name, true);
                        var clipOff = AutoClosetUtil.CreateToggleAnimationClip(target, paramName, target.name, false);

                        // 머지 유틸로 곡선 합치기
                        MergeClip(onClip,  clipOn);
                        MergeClip(offClip, clipOff);
                    }
                }

                // Blendshape toggle curves
                var bsToggle = toggle.GetComponent<BlendshapeToggle>();
                if (bsToggle != null && bsToggle.shapes != null)
                {
                    foreach (var item in bsToggle.shapes)
                    {
                        if (item == null || item.mesh == null) continue;

                        var path = GetRelativePath(item.mesh.transform, avatar.transform);
                        var binding = EditorCurveBinding.FloatCurve(
                            path,
                            typeof(SkinnedMeshRenderer),
                            $"blendShape.{item.shapeKey}"
                        );

                        var curveOn = new AnimationCurve(new Keyframe(0f, item.active ? item.value : 0f));
                        var curveOff = new AnimationCurve(new Keyframe(0f, item.active ? 0f : item.value));

                        AnimationUtility.SetEditorCurve(onClip, binding, curveOn);
                        AnimationUtility.SetEditorCurve(offClip, binding, curveOff);
                    }
                }

                // 상태와 트랜지션 생성
                AddStates(sm, paramName, onClip, offClip);
            }

            EditorUtility.SetDirty(fxController);
            AssetDatabase.SaveAssets();
        }

        // src의 모든 커브를 dest에 복사합니다.
        private static void MergeClip(AnimationClip dest, AnimationClip src)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(src))
            {
                var curve = AnimationUtility.GetEditorCurve(src, binding);
                dest.SetCurve(binding.path, binding.type, binding.propertyName, curve);
            }
        }

        // Off/On 상태와 파라미터 기반 전환을 추가합니다.
        private static void AddStates(AnimatorStateMachine sm, string paramName, AnimationClip onClip, AnimationClip offClip)
        {
            var stateOff = sm.AddState($"{paramName}_Off");
            stateOff.motion = offClip;

            var stateOn = sm.AddState($"{paramName}_On");
            stateOn.motion = onClip;

            var tOn = stateOff.AddTransition(stateOn);
            tOn.AddCondition(AnimatorConditionMode.If, 0, paramName);
            tOn.hasExitTime = false;

            var tOff = stateOn.AddTransition(stateOff);
            tOff.AddCondition(AnimatorConditionMode.IfNot, 0, paramName);
            tOff.hasExitTime = false;
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
    }
}
