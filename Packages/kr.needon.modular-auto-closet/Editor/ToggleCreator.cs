using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using nadena.dev.modular_avatar.core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using needon.Editor.Pass;

namespace needon.Editor
{
    public static class ToggleCreator
    {
        private const string ToggleMenuPath = "GameObject/Hirami/Add Create Toggle";
        private const int Priority = 1;

        [MenuItem(ToggleMenuPath, true, Priority)]
        private static bool ValidateCreateToggle() => Selection.gameObjects.Length > 0;

        [MenuItem(ToggleMenuPath, false, Priority)]
        private static void CreateToggle()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length == 0) return;

            // Toggle 루트 생성/조회
            var parent = selectedObjects[0].transform.parent;
            var toggleRoot = parent != null ? parent.Find("Toggle")?.gameObject : null;
            if (toggleRoot == null)
            {
                toggleRoot = new GameObject("Toggle");
                if (parent != null) toggleRoot.transform.SetParent(parent, false);
            }

            // 메뉴 아이콘
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/kr.needon.modular-auto-closet/Resource/ClosetIcon.png");

            // Modular Avatar 세팅
            if (toggleRoot.GetComponent<ModularAvatarMenuInstaller>() == null)
                toggleRoot.AddComponent<ModularAvatarMenuInstaller>();

            var parameters = toggleRoot.GetComponent<ModularAvatarParameters>()
                             ?? toggleRoot.AddComponent<ModularAvatarParameters>();

            var rootItem = toggleRoot.GetComponent<ModularAvatarMenuItem>()
                           ?? toggleRoot.AddComponent<ModularAvatarMenuItem>();
            rootItem.Control ??= new VRCExpressionsMenu.Control();
            rootItem.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            rootItem.MenuSource = SubmenuSource.Children;
            rootItem.Control.icon = icon;

            // Avatar Descriptor 및 FX Controller 가져오기
            var avatarRoot = FindAvatarRoot(toggleRoot.transform)
                ?? throw new Exception("VRCAvatarDescriptor를 찾을 수 없습니다.");
            var avatarDescriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            var fxController = AutoClosetUtil.GetAvatarFxAnimator(avatarDescriptor) as AnimatorController
                                ?? throw new Exception("FX Animator Controller를 가져올 수 없습니다.");

            // 선택된 각 오브젝트에 대해 Toggle 생성 및 애니메이션 설정
            foreach (var obj in selectedObjects)
            {
                var paramName = $"Toggle_{obj.name}";

                // ParameterConfig 등록
                if (parameters.parameters.All(p => p.nameOrPrefix != paramName))
                {
                    parameters.parameters.Add(new ParameterConfig
                    {
                        nameOrPrefix = paramName,
                        syncType = ParameterSyncType.Bool,
                        defaultValue = 0,
                        saved = true
                    });
                }

                // 메뉴 아이템 GameObject 생성
                if (toggleRoot.transform.Find(paramName) == null)
                {
                    var itemGO = new GameObject(paramName);
                    itemGO.transform.SetParent(toggleRoot.transform, false);

                    var toggleComp = itemGO.AddComponent<ModularAvatarObjectToggle>();
                    var path = GetRelativePath(obj.transform, avatarRoot);
                    toggleComp.Objects.Add(new ToggledObject
                    {
                        Object = new AvatarObjectReference { referencePath = path },
                        Active = true
                    });

                    var childItem = itemGO.AddComponent<ModularAvatarMenuItem>();
                    childItem.Control ??= new VRCExpressionsMenu.Control();
                    childItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
                    childItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter { name = paramName };
                    childItem.Control.icon = icon;
                }

                // FX Controller에 파라미터와 레이어 추가
                AutoClosetUtil.AddAnimatorParameter(fxController, paramName, AnimatorControllerParameterType.Bool);
                AutoClosetUtil.ApplyCreateAnimatorLayer(fxController, paramName);

                // On/Off 애니메이션 클립 생성
                var onClip  = AutoClosetUtil.CreateToggleAnimationClip(obj,      paramName, obj.name, true);
                var offClip = AutoClosetUtil.CreateToggleAnimationClip(obj,      paramName, obj.name, false);

                var layer = fxController.layers.Last();
                var sm    = layer.stateMachine;

                var stateOff = sm.AddState($"{paramName}_Off");
                stateOff.motion = offClip;

                var stateOn = sm.AddState($"{paramName}_On");
                stateOn.motion = onClip;

                var tOn = stateOff.AddTransition(stateOn);
                tOn.AddCondition(AnimatorConditionMode.If,    0, paramName);
                tOn.hasExitTime = false;

                var tOff = stateOn.AddTransition(stateOff);
                tOff.AddCondition(AnimatorConditionMode.IfNot, 0, paramName);
                tOff.hasExitTime = false;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("비파괴 토글 애니메이션 생성 완료");
        }

        private static Transform FindAvatarRoot(Transform t)
        {
            while (t != null)
            {
                if (t.GetComponent<VRCAvatarDescriptor>() != null)
                    return t;
                t = t.parent;
            }
            return null;
        }

        private static string GetRelativePath(Transform target, Transform root)
        {
            if (target == root) return string.Empty;
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
