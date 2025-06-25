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
                var targets = toggle.targets?
                    .Where(t => t != null && t.target != null)
                    .Select(t => t.target)
                    .ToArray();
                if (targets == null || targets.Length == 0) continue;

                var paramName = "StandaloneToggle_" + Guid.NewGuid().ToString("N").Substring(0, 8);

                AutoClosetUtil.AddAnimatorParameter(fxController, paramName, AnimatorControllerParameterType.Bool);
                AutoClosetUtil.ApplyCreateAnimatorLayer(fxController, paramName);

                var layer = fxController.layers.Last();
                var sm = layer.stateMachine;

                var onClip = new AnimationClip { name = $"{paramName}_On" };
                var offClip = new AnimationClip { name = $"{paramName}_Off" };

                foreach (var target in targets)
                {
                    var clipOn = AutoClosetUtil.CreateToggleAnimationClip(target, paramName, target.name, true);
                    var clipOff = AutoClosetUtil.CreateToggleAnimationClip(target, paramName, target.name, false);
                    MergeClip(onClip, clipOn);
                    MergeClip(offClip, clipOff);
                }

                AddStates(sm, paramName, onClip, offClip);
            }

            EditorUtility.SetDirty(fxController);
            AssetDatabase.SaveAssets();
        }

        private static void MergeClip(AnimationClip dest, AnimationClip src)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(src))
            {
                var curve = AnimationUtility.GetEditorCurve(src, binding);
                dest.SetCurve(binding.path, binding.type, binding.propertyName, curve);
            }
        }

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
    }
}
