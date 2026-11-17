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
                var items = toggle.targets?
                    .Where(t => t != null && t.target != null)
                    .ToArray();
                if (items == null || items.Length == 0) continue;

                // 파라미터와 레이어 추가
                AutoClosetUtil.AddAnimatorParameter(fxController, paramName, AnimatorControllerParameterType.Bool);

                // 새 레이어는 항상 컨트롤러 끝에 추가된다. 같은 이름의 레이어가 이미 있으면
                // 이름만 유일하게 바뀌므로, 인덱스를 이름으로 찾으면 기존 레이어를 가리키게 된다.
                var sm = AutoClosetUtil.ApplyCreateAnimatorLayer(fxController, paramName).stateMachine;
                var layerIndex = fxController.layers.Length - 1;

                // 토글을 켰을 때 각 대상은 설정값(active: ON/OFF)으로, 껐을 때는 그 반대로 둔다.
                // 대상이 여러 개면 하나의 클립에 합친다.
                var onClip = new AnimationClip { name = $"{paramName}_On" };
                var offClip = new AnimationClip { name = $"{paramName}_Off" };

                foreach (var item in items)
                {
                    MergeClip(onClip, AutoClosetUtil.CreateToggleAnimationClip(item.target, avatar.transform, item.active));
                    MergeClip(offClip, AutoClosetUtil.CreateToggleAnimationClip(item.target, avatar.transform, !item.active));
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

                        var onVal = item.active ? item.value : 0f;
                        var offVal = item.active ? 0f : item.value;
                        var curveOn = new AnimationCurve(new Keyframe(0f, onVal), new Keyframe(1f / 60f, onVal));
                        var curveOff = new AnimationCurve(new Keyframe(0f, offVal), new Keyframe(1f / 60f, offVal));

                        AnimationUtility.SetEditorCurve(onClip, binding, curveOn);
                        AnimationUtility.SetEditorCurve(offClip, binding, curveOff);
                    }
                }

                // 상태와 트랜지션 생성
                var writeDefaults = AutoClosetUtil.ResolveWriteDefaults(context, toggle.gameObject);
                AddStates(sm, paramName, onClip, offClip, layerIndex, writeDefaults);
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
        private static void AddStates(AnimatorStateMachine sm, string paramName, AnimationClip onClip, AnimationClip offClip, int layerIndex, bool writeDefaults)
        {
            var stateOff = sm.AddState($"{paramName}_Off");
            stateOff.motion = offClip;
            stateOff.writeDefaultValues = writeDefaults;

            var stateOn = sm.AddState($"{paramName}_On");
            stateOn.motion = onClip;
            stateOn.writeDefaultValues = writeDefaults;

            // Off -> On
            var tOn = stateOff.AddTransition(stateOn);
            tOn.AddCondition(AnimatorConditionMode.If, 0, paramName);
            MakeInstant(tOn);

            // On -> Off
            var tOff = stateOn.AddTransition(stateOff);
            tOff.AddCondition(AnimatorConditionMode.IfNot, 0, paramName);
            MakeInstant(tOff);

            // 혹시 중복 전이가 생겼다면 모두 0초로 통일
            foreach (var tr in stateOff.transitions.Where(x => x.destinationState == stateOn)) MakeInstant(tr);
            foreach (var tr in stateOn.transitions.Where(x => x.destinationState == stateOff)) MakeInstant(tr);

            // 레이어 Weight 보호: AFK 등으로 Weight가 0이 되는 것을 방지
            AutoClosetUtil.ApplyLayerWeightControl(stateOff, layerIndex);
            AutoClosetUtil.ApplyLayerWeightControl(stateOn, layerIndex);
        }

        private static void MakeInstant(AnimatorStateTransition t)
        {
            t.hasExitTime = false;
            t.exitTime = 0f;
            t.hasFixedDuration = true; // Transition Duration (s) 모드
            t.duration = 0f; // 0초 전환
            t.offset = 0f;

            // 선택(깔끔하게)
            t.orderedInterruption = false;
            t.interruptionSource = TransitionInterruptionSource.None;

            EditorUtility.SetDirty(t); // 인스펙터에 값 고정 반영
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