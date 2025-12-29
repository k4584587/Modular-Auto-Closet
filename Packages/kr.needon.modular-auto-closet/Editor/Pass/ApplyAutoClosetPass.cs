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

                    // 옷장의 모든 옷에 대해 애니메이션 경로 리매핑 수행
                    AnimationPathRemapper.RemapClosetAnimations(closetGameObject, avatar.transform);

                    // 각 옷장마다 별도의 애니메이터 레이어와 파라미터 생성
                    CreateLayerAndParameter(uniqueName);
                    var layerIndex = FindAutoClosetLayerIndex(uniqueName);
                    var stateMachine = _autoClosetController.layers[layerIndex].stateMachine;

                    CreateClosetStates(closetGameObject, stateMachine, uniqueName, context);
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

        private void CreateClosetStates(GameObject closet, AnimatorStateMachine stateMachine, string uniqueName, BuildContext context)
        {
            var parentName = closet.transform.parent != null ? closet.transform.parent.name : closet.name;
            var defaultClothes = closet.transform.GetChild(0);

            // 옷장의 모든 파라미터 드라이버를 수집하여 스마트 리셋 정보 생성
            var parameterResetInfo = CollectParameterResetInfo(closet, context);

            // 기본 옷 상태 생성
            CreateDefaultClothesState(closet, parentName, defaultClothes, stateMachine, uniqueName, parameterResetInfo);

            // 추가 옷 상태 생성
            CreateAdditionalClothesStates(closet, parentName, defaultClothes, stateMachine, uniqueName, parameterResetInfo);
        }

        private void CreateDefaultClothesState(GameObject closet, string parentName, Transform defaultClothes, AnimatorStateMachine stateMachine, string uniqueName, Dictionary<string, ParameterResetInfo> parameterResetInfo)
        {
            var defaultClip = AutoClosetUtil.CreateClosetAnimationClip(closet, parentName, defaultClothes.name);
            var defaultState = stateMachine.AddState(defaultClothes.name, new Vector3(300, 0, 0));
            defaultState.motion = defaultClip;
            defaultState.writeDefaultValues = true;
            stateMachine.defaultState = defaultState;

            var anyToDefaultTransition = stateMachine.AddAnyStateTransition(defaultState);
            ConfigureTransition(anyToDefaultTransition, 0, uniqueName);

            // 파라미터 드라이버 적용 (스마트 리셋 포함)
            ApplyParameterDriversToClothes(defaultClothes, defaultState, parameterResetInfo);
        }

        private void CreateAdditionalClothesStates(GameObject closet, string parentName, Transform defaultClothes, AnimatorStateMachine stateMachine, string uniqueName, Dictionary<string, ParameterResetInfo> parameterResetInfo)
        {
            var index = 1;
            foreach (Transform child in closet.transform)
            {
                if (child == defaultClothes) continue;

                var stateClip = AutoClosetUtil.CreateClosetAnimationClip(closet, parentName, child.name);
                var newState = stateMachine.AddState(child.name, new Vector3(300, index * 60, 0));
                newState.motion = stateClip;
                newState.writeDefaultValues = true;

                var anyStateTransition = stateMachine.AddAnyStateTransition(newState);
                ConfigureTransition(anyStateTransition, index, uniqueName);

                // 파라미터 드라이버 적용 (스마트 리셋 포함)
                ApplyParameterDriversToClothes(child, newState, parameterResetInfo);

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

        /// <summary>
        /// 파라미터 리셋 정보를 담는 클래스
        /// </summary>
        private class ParameterResetInfo
        {
            public string ParameterName;
            public Dictionary<string, float> ClothesValues = new Dictionary<string, float>(); // 옷 이름 -> 설정 값
            public float DefaultValue = 1f; // 기본값 (명시하지 않은 옷에서 사용)
        }

        /// <summary>
        /// 옷장의 모든 파라미터 드라이버를 수집하여 리셋 정보를 생성합니다.
        /// </summary>
        private Dictionary<string, ParameterResetInfo> CollectParameterResetInfo(GameObject closet, BuildContext context)
        {
            var result = new Dictionary<string, ParameterResetInfo>();

            foreach (Transform child in closet.transform)
            {
                var config = child.GetComponent<ClosetConfig>();
                if (config == null || config.drivers == null || config.drivers.Length == 0)
                    continue;

                foreach (var driver in config.drivers)
                {
                    if (driver == null || string.IsNullOrEmpty(driver.name))
                        continue;

                    // Set 타입만 스마트 리셋 지원 (Add, Random, Copy는 제외)
                    if (driver.type != ClosetParameterDriverItem.ChangeType.Set)
                        continue;

                    if (!result.ContainsKey(driver.name))
                    {
                        result[driver.name] = new ParameterResetInfo
                        {
                            ParameterName = driver.name
                        };
                    }

                    result[driver.name].ClothesValues[child.name] = driver.value;
                }
            }

            // Cache all available avatar parameters to avoid repeated lookups.
            var avatarParameters = new Dictionary<string, float>();
            var avatar = context.AvatarDescriptor;

            // 1. From VRC Expression Parameters
            if (avatar.expressionParameters != null && avatar.expressionParameters.parameters != null)
            {
                foreach (var param in avatar.expressionParameters.parameters)
                {
                    if (!string.IsNullOrEmpty(param.name))
                    {
                        avatarParameters[param.name] = param.defaultValue;
                    }
                }
            }

            // 2. From Modular Avatar Parameters (overwriting VRC params if names conflict, which is MA behavior)
            var maParams = avatar.GetComponentsInChildren<ModularAvatarParameters>(true);
            foreach (var maParam in maParams)
            {
                if (maParam.parameters == null) continue;

                foreach (var param in maParam.parameters)
                {
                    if (!string.IsNullOrEmpty(param.nameOrPrefix))
                    {
                        // 0f is a valid default, so no need to check for non-zero.
                        avatarParameters[param.nameOrPrefix] = param.defaultValue;
                    }
                }
            }

            // 각 파라미터의 기본값을 아바타 파라미터 정의에서 가져오기
            foreach (var info in result.Values)
            {
                if (avatarParameters.TryGetValue(info.ParameterName, out var defaultValue))
                {
                    info.DefaultValue = defaultValue;
                }
                else
                {
                    // Fallback to a safer default of 0f if the parameter is not found.
                    info.DefaultValue = 0f;
                }
            }

            return result;
        }

        /// <summary>
        /// 옷 상태에 파라미터 드라이버를 적용합니다. (스마트 리셋 포함)
        /// </summary>
        private void ApplyParameterDriversToClothes(Transform clothes, AnimatorState state, Dictionary<string, ParameterResetInfo> parameterResetInfo)
        {
            if (clothes == null || state == null)
                return;

            var driversList = new List<ClosetParameterDriverItem>();

            // 1. 이 옷에 명시적으로 설정된 드라이버들 추가
            var config = clothes.GetComponent<ClosetConfig>();
            if (config != null && config.drivers != null && config.drivers.Length > 0)
            {
                driversList.AddRange(config.drivers);
            }

            // 2. 명시하지 않은 파라미터들을 아바타 기본값으로 리셋
            foreach (var info in parameterResetInfo.Values)
            {
                // 이 옷이 해당 파라미터를 명시적으로 설정하지 않았으면 아바타의 기본값으로 리셋
                if (!info.ClothesValues.ContainsKey(clothes.name))
                {
                    var resetDriver = new ClosetParameterDriverItem
                    {
                        type = ClosetParameterDriverItem.ChangeType.Set,
                        name = info.ParameterName,
                        value = info.DefaultValue
                    };
                    driversList.Add(resetDriver);
                }
            }

            // 3. 드라이버가 있으면 AnimatorState에 적용
            if (driversList.Count > 0)
            {
                AutoClosetUtil.ApplyParameterDriversToState(state, driversList.ToArray());
            }
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
