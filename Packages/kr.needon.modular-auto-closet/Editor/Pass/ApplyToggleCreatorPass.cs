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
            {
                throw new InvalidOperationException("Cannot find FX Animator Controller.");
            }

            // 새 AutoClosetObjectToggle 컴포넌트를 모두 찾음
            var toggles = avatar.GetComponentsInChildren<AutoClosetObjectToggle>(true);
            foreach (var toggle in toggles)
            {
                var menu = toggle.GetComponent<ModularAvatarMenuItem>();
                if (menu?.Control?.parameter == null) continue;
                
                var paramName = menu.Control.parameter.name;
                if (string.IsNullOrEmpty(paramName)) continue;

                if (toggle.targets == null || toggle.targets.Length == 0) continue;
                var target = toggle.targets[0].target;
                if (target == null) continue;

                AutoClosetUtil.AddAnimatorParameter(fxController, paramName, AnimatorControllerParameterType.Bool);
                AutoClosetUtil.ApplyCreateAnimatorLayer(fxController, paramName);
                var layer = fxController.layers.Last();
                var sm = layer.stateMachine;

                var onClip = AutoClosetUtil.CreateToggleAnimationClip(target, paramName, target.name, true);
                var offClip = AutoClosetUtil.CreateToggleAnimationClip(target, paramName, target.name, false);

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

            EditorUtility.SetDirty(fxController);
            AssetDatabase.SaveAssets();
        }
    }
}
