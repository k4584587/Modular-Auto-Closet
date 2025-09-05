using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace needon.Editor.Pass
{
    internal class ApplyAutoClosetPass : Pass<ApplyAutoClosetPass>
    {
        private AnimatorController _autoClosetController;

        protected override void Execute(BuildContext context)
        {
            try
            {
                var avatar = context.AvatarDescriptor;
                _autoClosetController = (AnimatorController)AutoClosetUtil.GetAvatarFxAnimator(avatar);

                if (_autoClosetController == null)
                {
                    throw new InvalidOperationException("Cannot find FX Animator Controller.");
                }

                // 아바타에 부착된 모든 AutoCloset 컴포넌트를 가져옴 (비활성 포함)
                var closetComponents = avatar.GetComponentsInChildren<AutoCloset>(true);
                foreach (var closetComponent in closetComponents)
                {
                    var closetGameObject = closetComponent.gameObject;
                    if (!ValidateCloset(closetGameObject))
                    {
                        continue;
                    }

                    // 각 옷장의 ModularAvatarParameters에 생성된 고유 파라미터 이름을 가져옴
                    var uniqueName = GetUniqueParameterName(closetGameObject);
                    if (string.IsNullOrEmpty(uniqueName))
                    {
                        // 고유 파라미터가 없으면 새로 생성 (예: AutoCloset_8자리해시)
                        uniqueName = "AutoCloset_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    }

                    RecalculateClosetChildren(closetGameObject, uniqueName);
                    
                    // 각 옷장마다 별도의 애니메이터 레이어와 파라미터 생성
                    CreateLayerAndParameter(uniqueName);
                    var layerIndex = FindAutoClosetLayerIndex(uniqueName);
                    var stateMachine = _autoClosetController.layers[layerIndex].stateMachine;

                    CreateClosetStates(closetGameObject, stateMachine, uniqueName);
                }
            }
            catch (Exception e)
            {
                needon.Editor.Util.ClosetLogger.LogError(context.AvatarRootObject, "Log.Apply.Error", e.Message);
                throw;
            }
        }

        private string GetUniqueParameterName(GameObject closet)
        {
            var maParameters = closet.GetComponent<ModularAvatarParameters>();
            if (maParameters?.parameters == null) return null;

            // "AutoCloset_"로 시작하는 파라미터를 찾되 null 안전하게 처리
            var parameterConfig = maParameters.parameters
                .FirstOrDefault(p => !string.IsNullOrEmpty(p.nameOrPrefix) && p.nameOrPrefix.StartsWith("AutoCloset_"));

            return parameterConfig.nameOrPrefix;
        }

        private void CreateLayerAndParameter(string uniqueName)
        {
            // 1) 레이어 존재 여부 확인 후 없을 때만 생성 (Unity 2022 호환)
            var hasLayer = _autoClosetController.layers.Any(l => l.name == uniqueName);
            if (!hasLayer)
            {
                AutoClosetUtil.ApplyCreateAnimatorLayer(_autoClosetController, uniqueName);
            }

            // 2) 파라미터(Int) 존재 여부 확인 후 없을 때만 추가
            var hasIntParam = _autoClosetController.parameters.Any(p =>
                p.name == uniqueName && p.type == AnimatorControllerParameterType.Int);
            if (!hasIntParam)
            {
                AutoClosetUtil.AddAnimatorParameter(_autoClosetController, uniqueName, AnimatorControllerParameterType.Int);
            }

            // 매 호출마다 저장하지 않고 더티 마킹만 수행 (Unity 2022: 성능/에셋 락 이슈 방지)
            EditorUtility.SetDirty(_autoClosetController);
        }

        private int FindAutoClosetLayerIndex(string uniqueName)
        {
            for (var i = 0; i < _autoClosetController.layers.Length; i++)
            {
                if (_autoClosetController.layers[i].name == uniqueName)
                {
                    return i;
                }
            }

            throw new InvalidOperationException($"Cannot find layer '{uniqueName}'.");
        }

        private bool ValidateCloset(GameObject closet)
        {
            if (closet == null) return false;
            if (closet.transform.childCount != 0) return true;
            needon.Editor.Util.ClosetLogger.LogWarning(closet, "Log.Closet.NoChildren");
            return false;

        }

        private void CreateClosetStates(GameObject closet, AnimatorStateMachine stateMachine, string uniqueName)
        {
            var parentName = closet.transform.parent != null ? closet.transform.parent.name : closet.name;
            var defaultClothes = closet.transform.GetChild(0);

            // 기본 옷 상태 생성
            CreateDefaultClothesState(closet, parentName, defaultClothes, stateMachine, uniqueName);

            // 추가 옷 상태 생성
            CreateAdditionalClothesStates(closet, parentName, defaultClothes, stateMachine, uniqueName);
        }

        private void CreateDefaultClothesState(GameObject closet, string parentName, Transform defaultClothes, AnimatorStateMachine stateMachine, string uniqueName)
        {
            var defaultClip = AutoClosetUtil.CreateClosetAnimationClip(closet, parentName, defaultClothes.name);
            var defaultState = stateMachine.AddState(defaultClothes.name, new Vector3(300, 0, 0));
            defaultState.motion = defaultClip;
            stateMachine.defaultState = defaultState;

            var anyToDefaultTransition = stateMachine.AddAnyStateTransition(defaultState);
            ConfigureTransition(anyToDefaultTransition, 0, uniqueName);
        }

        private void CreateAdditionalClothesStates(GameObject closet, string parentName, Transform defaultClothes, AnimatorStateMachine stateMachine, string uniqueName)
        {
            var index = 1;
            foreach (Transform child in closet.transform)
            {
                if (child == defaultClothes) continue;

                var stateClip = AutoClosetUtil.CreateClosetAnimationClip(closet, parentName, child.name);
                var newState = stateMachine.AddState(child.name, new Vector3(300, index * 60, 0));
                newState.motion = stateClip;

                var anyStateTransition = stateMachine.AddAnyStateTransition(newState);
                ConfigureTransition(anyStateTransition, index, uniqueName);

                index++;
            }
        }

        private void ConfigureTransition(AnimatorStateTransition transition, int parameterValue, string uniqueName)
        {
            transition.hasExitTime = false;
            transition.exitTime = 0f;
            transition.duration = 0f;
            transition.AddCondition(AnimatorConditionMode.Equals, parameterValue, uniqueName);
        }
        
        private void RecalculateClosetChildren(GameObject closetObject, string uniqueName)
        {
            var children = new List<Transform>();
            foreach (Transform child in closetObject.transform)
            {
                var menuItem = child.GetComponent<ModularAvatarMenuItem>();
                if (menuItem != null && menuItem.Control is { parameter: not null } && menuItem.Control.parameter.name == uniqueName)
                    children.Add(child);
            }

            children = children.OrderBy(t => t.GetSiblingIndex()).ToList();

            for (var i = 0; i < children.Count; i++)
            {
                var mi = children[i].GetComponent<ModularAvatarMenuItem>();
                if (mi != null && mi.Control != null)
                    mi.Control.value = i;
            }
        }
    }
}
