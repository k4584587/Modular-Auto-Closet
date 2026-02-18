using System;
using System.Collections.Generic;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC_AnimatorLayerControl = VRC.SDKBase.VRC_AnimatorLayerControl;

namespace needon.Editor.Pass
{
    /// <summary>
    /// Optimizing 페이즈 최종 단계:
    /// 1. Weight가 0인 레이어를 1로 복원
    /// 2. 다른 툴의 VRCAnimatorLayerControl이 우리 레이어의 Weight를 0으로 설정하는 것을 무력화
    /// </summary>
    internal class EnsureLayerWeightsPass : Pass<EnsureLayerWeightsPass>
    {
        protected override void Execute(BuildContext context)
        {
            var avatar = context.AvatarDescriptor;
            var fxController = AutoClosetUtil.GetAvatarFxAnimator(avatar) as AnimatorController;
            if (fxController == null) return;

            // Step 1: Weight가 0인 레이어를 1로 복원
            var layers = fxController.layers;
            var changed = false;

            for (var i = 1; i < layers.Length; i++)
            {
                if (Math.Abs(layers[i].defaultWeight) < 0.001f)
                {
                    layers[i].defaultWeight = 1f;
                    changed = true;
                    needon.Editor.Util.ClosetLogger.Log(
                        context.AvatarRootObject,
                        "Log.LayerWeight.Fixed",
                        layers[i].name, i);
                }
            }

            if (changed)
            {
                fxController.layers = layers;
                EditorUtility.SetDirty(fxController);
            }

            // Step 2: 우리 레이어 인덱스 식별 (VRCAnimatorLayerControl goalWeight=1을 가진 레이어)
            layers = fxController.layers; // 갱신된 배열 다시 읽기
            var protectedLayers = new HashSet<int>();

            for (var i = 1; i < layers.Length; i++)
            {
                if (layers[i].stateMachine != null && HasOurLayerControl(layers[i].stateMachine, i))
                {
                    protectedLayers.Add(i);
                }
            }

            if (protectedLayers.Count == 0) return;

            // Step 3: 전체 컨트롤러 스캔 - 우리 레이어를 goalWeight<1로 설정하는 행위 무력화
            for (var i = 0; i < layers.Length; i++)
            {
                if (layers[i].stateMachine != null)
                {
                    NeutralizeOffendingBehaviors(layers[i].stateMachine, protectedLayers, context);
                }
            }
        }

        /// <summary>
        /// 해당 스테이트머신에 우리가 추가한 VRCAnimatorLayerControl(goalWeight=1, 자기 레이어 타겟)이 있는지 확인
        /// </summary>
        private static bool HasOurLayerControl(AnimatorStateMachine sm, int layerIndex)
        {
            foreach (var childState in sm.states)
            {
                foreach (var behavior in childState.state.behaviours)
                {
                    if (behavior is VRCAnimatorLayerControl lc
                        && lc.playable == VRC_AnimatorLayerControl.BlendableLayer.FX
                        && lc.layer == layerIndex
                        && lc.goalWeight >= 1f)
                    {
                        return true;
                    }
                }
            }

            foreach (var childSM in sm.stateMachines)
            {
                if (HasOurLayerControl(childSM.stateMachine, layerIndex))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 다른 레이어의 스테이트에서 우리 레이어를 goalWeight<1로 설정하는 VRCAnimatorLayerControl을 찾아 무력화
        /// </summary>
        private static void NeutralizeOffendingBehaviors(
            AnimatorStateMachine sm,
            HashSet<int> protectedLayers,
            BuildContext context)
        {
            foreach (var childState in sm.states)
            {
                foreach (var behavior in childState.state.behaviours)
                {
                    if (behavior is VRCAnimatorLayerControl lc
                        && lc.playable == VRC_AnimatorLayerControl.BlendableLayer.FX
                        && protectedLayers.Contains(lc.layer)
                        && lc.goalWeight < 1f)
                    {
                        lc.goalWeight = 1f;
                        lc.blendDuration = 0f;
                        EditorUtility.SetDirty(lc);

                        needon.Editor.Util.ClosetLogger.Log(
                            context.AvatarRootObject,
                            "Log.LayerWeight.Neutralized",
                            childState.state.name, lc.layer);
                    }
                }
            }

            foreach (var childSM in sm.stateMachines)
            {
                NeutralizeOffendingBehaviors(childSM.stateMachine, protectedLayers, context);
            }
        }
    }
}
