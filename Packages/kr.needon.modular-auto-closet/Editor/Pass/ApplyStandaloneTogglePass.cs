using System;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace needon.Editor.Pass
{
    internal class ApplyStandaloneTogglePass : Pass<ApplyStandaloneTogglePass>
    {
        protected override void Execute(BuildContext context)
        {
            var avatar = context.AvatarDescriptor;
            var fxController = AutoClosetUtil.GetAvatarFxAnimator(avatar) as AnimatorController;
            if (fxController == null)
                throw new InvalidOperationException("Cannot find FX Animator Controller.");

            var toggles = avatar.GetComponentsInChildren<StandaloneToggle>(true);
            foreach (var toggle in toggles)
            {
                var items = toggle.targets?.Where(i => i != null && i.target != null).ToArray();
                if (items == null || items.Length == 0) continue;

                // 1) StandaloneToggle가 붙은 오브젝트 이름 기준으로 기존 Bool 파라미터 자동 연결
                var paramName = FindLinkedBoolParameterName(fxController, toggle);

                // 2) 연결할 파라미터가 없으면 건너뛴다. 메뉴도 Expression Parameter도 없는 파라미터를
                //    새로 만들면 값이 영원히 false라 대상이 설정의 반대 상태로 고정된다.
                if (paramName == null)
                {
                    needon.Editor.Util.ClosetLogger.LogWarning(toggle, "Log.Standalone.NoParameter", toggle.gameObject.name);
                    continue;
                }

                // 3) 기존 파라미터의 보조 레이어 생성
                var layer = CreateUniqueLayer(fxController, $"{paramName}__StandaloneExt");
                var sm = layer.stateMachine;

                // 4) On/Off 클립(타겟 전부 같은 Bool로 제어)
                var onClip = new AnimationClip { name = $"{paramName}_On" };
                var offClip = new AnimationClip { name = $"{paramName}_Off" };

                foreach (var item in items)
                {
                    var target = item.target;
                    var clipOn = AutoClosetUtil.CreateToggleAnimationClip(target, avatar.transform, item.active);
                    var clipOff = AutoClosetUtil.CreateToggleAnimationClip(target, avatar.transform, !item.active);
                    MergeClip(onClip, clipOn);
                    MergeClip(offClip, clipOff);
                }

                // 레이어 인덱스 계산 (이름으로 검색하여 안정성 확보)
                var layerIndex = Array.FindIndex(fxController.layers, l => l.name == layer.name);
                if (layerIndex < 0) layerIndex = fxController.layers.Length - 1;

                // 5) 상태/전이 구성
                var writeDefaults = AutoClosetUtil.ResolveWriteDefaults(context, toggle.gameObject);
                AddStates(sm, paramName, onClip, offClip, layerIndex, writeDefaults);

                // 6) 초기 상태 - 파라미터 기본값/아이템 기본값에 맞춤
                var wantOn = items.Any(x => x.active);

                // 가능하면 Animator 파라미터의 defaultBool도 반영
                var p = fxController.parameters.FirstOrDefault(pp => pp.name == paramName && pp.type == AnimatorControllerParameterType.Bool);
                if (p != null) wantOn = wantOn || p.defaultBool;

                var sOn = sm.states.First(s => s.state.motion == onClip).state;
                var sOff = sm.states.First(s => s.state.motion == offClip).state;
                sm.defaultState = wantOn ? sOn : sOff;
            }

            EditorUtility.SetDirty(fxController);
            AssetDatabase.SaveAssets();
        }

        // 기존 Bool 파라미터 자동 탐색. ToggleCreator 규칙(Toggle_{Owner}_****, Toggle_{Owner})이 우선이고,
        // 없으면 예전처럼 이름이 비슷한 파라미터로 대체하되 엉뚱한 파라미터일 수 있으므로 경고를 남긴다.
        private static string FindLinkedBoolParameterName(AnimatorController fx, StandaloneToggle toggle)
        {
            const StringComparison ignoreCase = StringComparison.OrdinalIgnoreCase;
            var ownerName = toggle.gameObject.name;
            var exactName = $"Toggle_{ownerName}";
            var boolParams = fx.parameters
                .Where(p => p.type == AnimatorControllerParameterType.Bool)
                .Select(p => p.name)
                .ToArray();

            var linked = boolParams.FirstOrDefault(n => n.StartsWith(exactName + "_", ignoreCase))
                         ?? boolParams.FirstOrDefault(n => string.Equals(n, exactName, ignoreCase));
            if (linked != null) return linked;

            var fuzzy = boolParams.FirstOrDefault(n => n.StartsWith(exactName, ignoreCase))
                        ?? boolParams.FirstOrDefault(n => n.IndexOf(ownerName, ignoreCase) >= 0);
            if (fuzzy != null)
                needon.Editor.Util.ClosetLogger.LogWarning(toggle, "Log.Standalone.FuzzyLinked", ownerName, fuzzy);

            return fuzzy;
        }

        // 기존 레이어와 이름 충돌 없이 보조 레이어 생성
        private static AnimatorControllerLayer CreateUniqueLayer(AnimatorController fx, string baseName)
        {
            var name = baseName;
            int i = 1;
            while (fx.layers.Any(l => l.name == name)) name = $"{baseName}_{i++}";
            var newLayer = new AnimatorControllerLayer
            {
                name = name,
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = new AnimatorStateMachine { name = name }
            };
            fx.AddLayer(newLayer);
            return fx.layers.First(l => l.name == name);
        }

        private static void MergeClip(AnimationClip dest, AnimationClip src)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(src))
            {
                var curve = AnimationUtility.GetEditorCurve(src, binding);
                dest.SetCurve(binding.path, binding.type, binding.propertyName, curve);
            }
        }

        private static void ConfigureState(AnimatorState st, bool writeDefaults)
        {
            st.writeDefaultValues = writeDefaults;
            st.speed = 1f;
            st.mirror = false;
            st.timeParameterActive = false;
        }

        private static void ConfigureZeroTransition(AnimatorStateTransition t)
        {
            t.hasExitTime = false;
            t.exitTime = 0f;

            // 즉시 전이
            t.hasFixedDuration = true;
            t.duration = 0f;

            t.offset = 0f;
            t.interruptionSource = TransitionInterruptionSource.None;
            t.orderedInterruption = false;
            t.canTransitionToSelf = false;
        }

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

            // 혹시 기본/중복 전이가 있다면 그것들도 0초로 통일
            foreach (var tr in stateOff.transitions.Where(x => x.destinationState == stateOn))
                MakeInstant(tr);
            foreach (var tr in stateOn.transitions.Where(x => x.destinationState == stateOff))
                MakeInstant(tr);

            // 레이어 Weight 보호: AFK 등으로 Weight가 0이 되는 것을 방지
            AutoClosetUtil.ApplyLayerWeightControl(stateOff, layerIndex);
            AutoClosetUtil.ApplyLayerWeightControl(stateOn, layerIndex);
        }

        private static void MakeInstant(AnimatorStateTransition t)
        {
            t.hasExitTime = false;
            t.exitTime = 0f;
            t.hasFixedDuration = true; // (s) 단위
            t.duration = 0f; // 0초 전환
            t.offset = 0f;

            // 선택: 끼어들기 관련 깔끔하게
            t.orderedInterruption = false;
            t.interruptionSource = TransitionInterruptionSource.None;

            // 변경사항 저장 표시
            EditorUtility.SetDirty(t);
        }
    }
}