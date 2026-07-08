using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

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

                // Auto WD 감지를 옷장 레이어 추가 전(순수 아바타 FX) 1회 측정해 캐싱
                // (ResolveWriteDefaults는 On/Off 모드면 캐시를 건너뛰므로 무조건 워밍)
                AutoClosetUtil.WarmAvatarWriteDefaultsCache(context);

                // 아바타 파라미터 기본값은 옷장마다 동일하므로 빌드당 1회만 수집
                var avatarParameterDefaults = CollectAvatarParameterDefaults(context);

                // 아바타에 부착된 모든 AutoCloset 컴포넌트를 가져옴 (비활성 포함)
                var closetComponents = avatar.GetComponentsInChildren<AutoCloset>(true);

                // 원래 비활성(안 입은 상태로 저장된) 보존 의상 — 모든 옷장의 클립 생성이
                // 끝난 뒤 렌더러를 직렬화 수준에서 꺼서 rest 상태 겹침을 방지한다
                var originallyInactiveDynamics = new HashSet<Transform>();

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

                    // 보존 대상 PhysBone(= enabled이고 의상 루트까지 체인이 켜져 있는 것)을
                    // 가진 의상 목록을 1회 계산해 활성화/옵티마이저 제외/클립 생성이 공유
                    var dynamicsChildren = CollectDynamicsChildren(closetGameObject);
                    foreach (var child in dynamicsChildren)
                    {
                        if (!child.gameObject.activeSelf)
                            originallyInactiveDynamics.Add(child);
                    }

                    ActivateDynamicHierarchies(closetGameObject, dynamicsChildren);
                    ExcludeDynamicPhysBonesFromTraceAndOptimize(context.AvatarRootObject, dynamicsChildren);

                    // 옷장의 모든 옷에 대해 애니메이션 경로 리매핑 수행
                    AnimationPathRemapper.RemapClosetAnimations(closetGameObject, avatar.transform);

                    // 각 옷장마다 별도의 애니메이터 레이어와 파라미터 생성
                    CreateLayerAndParameter(uniqueName);
                    var layerIndex = FindAutoClosetLayerIndex(uniqueName);
                    var stateMachine = _autoClosetController.layers[layerIndex].stateMachine;

                    var writeDefaults = AutoClosetUtil.ResolveWriteDefaults(context, closetGameObject);
                    CreateClosetStates(closetGameObject, stateMachine, uniqueName, context, layerIndex, writeDefaults, avatarParameterDefaults, dynamicsChildren);
                }

                DisableInactiveDynamicsRenderers(originallyInactiveDynamics);
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

        private void CreateClosetStates(GameObject closet, AnimatorStateMachine stateMachine, string uniqueName, BuildContext context, int layerIndex, bool writeDefaults, Dictionary<string, float> avatarParameterDefaults, HashSet<Transform> dynamicsChildren)
        {
            var parentName = closet.transform.parent != null ? closet.transform.parent.name : closet.name;
            var defaultClothes = closet.transform.GetChild(0);

            // 옷장의 모든 파라미터 드라이버를 수집하여 스마트 리셋 정보 생성
            var parameterResetInfo = CollectParameterResetInfo(closet, uniqueName, context, avatarParameterDefaults);
            ApplyImplicitMenuParameterDefaultsToAnimator(parameterResetInfo);

            // 기본 옷 상태 생성
            CreateDefaultClothesState(closet, parentName, defaultClothes, stateMachine, uniqueName, parameterResetInfo, layerIndex, writeDefaults, dynamicsChildren);

            // 추가 옷 상태 생성
            CreateAdditionalClothesStates(closet, parentName, defaultClothes, stateMachine, uniqueName, parameterResetInfo, layerIndex, writeDefaults, dynamicsChildren);
        }

        private void CreateDefaultClothesState(GameObject closet, string parentName, Transform defaultClothes, AnimatorStateMachine stateMachine, string uniqueName, Dictionary<string, ParameterResetInfo> parameterResetInfo, int layerIndex, bool writeDefaults, HashSet<Transform> dynamicsChildren)
        {
            var defaultClip = AutoClosetUtil.CreateClosetAnimationClip(closet, parentName, defaultClothes.name, dynamicsChildren);
            var defaultState = stateMachine.AddState(defaultClothes.name, new Vector3(300, 0, 0));
            defaultState.motion = defaultClip;
            defaultState.writeDefaultValues = writeDefaults;
            stateMachine.defaultState = defaultState;

            var anyToDefaultTransition = stateMachine.AddAnyStateTransition(defaultState);
            ConfigureTransition(anyToDefaultTransition, 0, uniqueName);

            // 파라미터 드라이버 적용 (스마트 리셋 포함)
            ApplyParameterDriversToClothes(defaultClothes, defaultState, parameterResetInfo);

            // 레이어 Weight 보호: AFK 등으로 Weight가 0이 되는 것을 방지
            AutoClosetUtil.ApplyLayerWeightControl(defaultState, layerIndex);
        }

        private void CreateAdditionalClothesStates(GameObject closet, string parentName, Transform defaultClothes, AnimatorStateMachine stateMachine, string uniqueName, Dictionary<string, ParameterResetInfo> parameterResetInfo, int layerIndex, bool writeDefaults, HashSet<Transform> dynamicsChildren)
        {
            var index = 1;
            foreach (Transform child in closet.transform)
            {
                if (child == defaultClothes) continue;

                var stateClip = AutoClosetUtil.CreateClosetAnimationClip(closet, parentName, child.name, dynamicsChildren);
                var newState = stateMachine.AddState(child.name, new Vector3(300, index * 60, 0));
                newState.motion = stateClip;
                newState.writeDefaultValues = writeDefaults;

                var anyStateTransition = stateMachine.AddAnyStateTransition(newState);
                ConfigureTransition(anyStateTransition, index, uniqueName);

                // 파라미터 드라이버 적용 (스마트 리셋 포함)
                ApplyParameterDriversToClothes(child, newState, parameterResetInfo);

                // 레이어 Weight 보호: AFK 등으로 Weight가 0이 되는 것을 방지
                AutoClosetUtil.ApplyLayerWeightControl(newState, layerIndex);

                index++;
            }
        }

        private void ConfigureTransition(AnimatorStateTransition transition, int parameterValue, string uniqueName)
        {
            transition.hasExitTime = false;
            transition.exitTime = 0f;
            transition.duration = 0f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.Equals, parameterValue, uniqueName);
        }

        /// <summary>
        /// 파라미터 리셋 정보를 담는 클래스
        /// </summary>
        private class ParameterResetInfo
        {
            public string ParameterName;
            public Dictionary<string, float> ClothesValues = new Dictionary<string, float>(); // 옷 이름 -> 설정 값
            public float DefaultValue = 0f; // 기본값 (명시하지 않은 옷에서 사용)
        }

        private class MenuParameterUsage
        {
            public string ParameterName;
            public float Value;
            public bool IsDefault;
            public bool AutomaticValue;
        }

        /// <summary>
        /// 옷장의 모든 파라미터 드라이버를 수집하여 리셋 정보를 생성합니다.
        /// </summary>
        private Dictionary<string, ParameterResetInfo> CollectParameterResetInfo(GameObject closet, string uniqueName, BuildContext context, Dictionary<string, float> avatarParameters)
        {
            var result = new Dictionary<string, ParameterResetInfo>();

            CollectConfiguredParameterDrivers(closet, result);

            var autoCloset = closet.GetComponent<AutoCloset>();
            if (autoCloset == null || autoCloset.resetChildMenuParameters)
            {
                // 아바타 전체 스캔이 들어가는 메뉴 아이템 수집은 옷장당 1회만 수행
                var relatedMenuItems = GetClosetRelatedMenuItems(closet, uniqueName).ToList();
                NormalizeImplicitMenuParameters(closet, uniqueName, relatedMenuItems);

                // saved 파라미터(라디얼 퍼펫 색조 등 유저 커스터마이즈)는 옷 전환 리셋에서 제외
                var savedParameters = CollectSavedParameterNames(context);
                CollectMenuParameterResets(closet, uniqueName, relatedMenuItems, avatarParameters, savedParameters, result);
            }

            // 각 파라미터의 기본값을 아바타/MA/애니메이터 파라미터 정의에서 가져오기
            foreach (var info in result.Values)
            {
                if (avatarParameters.TryGetValue(info.ParameterName, out var defaultValue))
                {
                    info.DefaultValue = defaultValue;
                }
            }

            return result;
        }

        private void CollectConfiguredParameterDrivers(GameObject closet, Dictionary<string, ParameterResetInfo> result)
        {
            foreach (Transform child in closet.transform)
            {
                var config = child.GetComponent<ClosetConfig>();
                if (config == null || config.drivers == null || config.drivers.Length == 0)
                    continue;

                foreach (var driver in config.drivers)
                {
                    CollectConfiguredParameterDriver(child, driver, result);
                }
            }
        }

        private void CollectConfiguredParameterDriver(
            Transform clothes,
            ClosetParameterDriverItem driver,
            Dictionary<string, ParameterResetInfo> result)
        {
            if (driver == null || string.IsNullOrEmpty(driver.name))
                return;

            // Set 타입만 스마트 리셋 지원 (Add, Random, Copy는 제외)
            if (driver.type != ClosetParameterDriverItem.ChangeType.Set)
                return;

            if (!result.ContainsKey(driver.name))
            {
                result[driver.name] = new ParameterResetInfo
                {
                    ParameterName = driver.name
                };
            }

            result[driver.name].ClothesValues[clothes.name] = driver.value;
        }

        private Dictionary<string, float> CollectAvatarParameterDefaults(BuildContext context)
        {
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

            // 3. From avatar FX and Modular Avatar merge animators
            AddAnimatorParameterDefaults(_autoClosetController, avatarParameters);

            var animators = avatar.GetComponentsInChildren<Animator>(true);
            foreach (var animator in animators)
            {
                AddAnimatorParameterDefaults(animator.runtimeAnimatorController as AnimatorController, avatarParameters);
            }

            var mergeAnimators = avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true);
            foreach (var mergeAnimator in mergeAnimators)
            {
                AddAnimatorParameterDefaults(mergeAnimator.animator as AnimatorController, avatarParameters);
            }

            return avatarParameters;
        }

        /// <summary>
        /// saved로 선언된 파라미터 이름을 수집합니다. (Expression Parameters + MA ParameterConfig)
        /// 월드 이동 후에도 유지되길 기대하는 값이므로 옷 전환 시 리셋하면 안 됩니다.
        /// </summary>
        private HashSet<string> CollectSavedParameterNames(BuildContext context)
        {
            var saved = new HashSet<string>();
            var avatar = context.AvatarDescriptor;

            if (avatar.expressionParameters != null && avatar.expressionParameters.parameters != null)
            {
                foreach (var param in avatar.expressionParameters.parameters)
                {
                    if (param.saved && !string.IsNullOrEmpty(param.name))
                        saved.Add(param.name);
                }
            }

            foreach (var maParam in avatar.GetComponentsInChildren<ModularAvatarParameters>(true))
            {
                if (maParam.parameters == null) continue;

                foreach (var param in maParam.parameters)
                {
                    if (param.saved && !string.IsNullOrEmpty(param.nameOrPrefix))
                        saved.Add(param.nameOrPrefix);
                }
            }

            return saved;
        }

        private void AddAnimatorParameterDefaults(AnimatorController controller, Dictionary<string, float> defaults)
        {
            if (controller == null) return;

            foreach (var parameter in controller.parameters)
            {
                if (string.IsNullOrEmpty(parameter.name)) continue;

                // 런타임에서는 Expression Parameters/MA 기본값이 우선이므로
                // 애니메이터 기본값은 아직 알려지지 않은 파라미터에만 보충한다.
                if (defaults.ContainsKey(parameter.name)) continue;

                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Bool:
                        defaults[parameter.name] = parameter.defaultBool ? 1f : 0f;
                        break;
                    case AnimatorControllerParameterType.Int:
                        defaults[parameter.name] = parameter.defaultInt;
                        break;
                    case AnimatorControllerParameterType.Float:
                        defaults[parameter.name] = parameter.defaultFloat;
                        break;
                }
            }
        }

        // 파라미터가 비어 있는 의상 기믹 메뉴 항목에 자동 부여하는 파라미터 이름 접두사.
        // 생성(CreateStableMenuParameterName)·소유 판정(EnsureMenuParameterConfig)·
        // 애니메이터 기본값 동기화(ApplyImplicitMenuParameterDefaultsToAnimator)가 모두 이 값을 공유한다.
        private const string ImplicitMenuParameterPrefix = "AutoClosetMenu_";

        private void ApplyImplicitMenuParameterDefaultsToAnimator(Dictionary<string, ParameterResetInfo> parameterResetInfo)
        {
            if (_autoClosetController == null || parameterResetInfo == null || parameterResetInfo.Count == 0)
                return;

            // AnimatorController.parameters는 접근할 때마다 새 복사본 배열을 반환하므로
            // 복사본을 한 번만 떠서 수정한 뒤 배열 전체를 다시 대입해야 변경이 저장된다.
            var parameters = _autoClosetController.parameters;
            var existingChanged = false;
            var parametersToAdd = new List<AnimatorControllerParameter>();

            foreach (var info in parameterResetInfo.Values)
            {
                if (info == null || string.IsNullOrWhiteSpace(info.ParameterName) || !info.ParameterName.StartsWith(ImplicitMenuParameterPrefix))
                    continue;

                var parameter = parameters.FirstOrDefault(p => p.name == info.ParameterName);
                if (parameter == null)
                {
                    parametersToAdd.Add(new AnimatorControllerParameter
                    {
                        name = info.ParameterName,
                        type = AnimatorControllerParameterType.Bool,
                        defaultBool = !Mathf.Approximately(info.DefaultValue, 0f)
                    });
                    continue;
                }

                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        if (!Mathf.Approximately(parameter.defaultFloat, info.DefaultValue))
                        {
                            parameter.defaultFloat = info.DefaultValue;
                            existingChanged = true;
                        }
                        break;
                    case AnimatorControllerParameterType.Int:
                        var intValue = Mathf.RoundToInt(info.DefaultValue);
                        if (parameter.defaultInt != intValue)
                        {
                            parameter.defaultInt = intValue;
                            existingChanged = true;
                        }
                        break;
                    case AnimatorControllerParameterType.Bool:
                        var boolValue = !Mathf.Approximately(info.DefaultValue, 0f);
                        if (parameter.defaultBool != boolValue)
                        {
                            parameter.defaultBool = boolValue;
                            existingChanged = true;
                        }
                        break;
                }
            }

            if (existingChanged)
                _autoClosetController.parameters = parameters;

            foreach (var parameter in parametersToAdd)
                _autoClosetController.AddParameter(parameter);

            if (existingChanged || parametersToAdd.Count > 0)
                EditorUtility.SetDirty(_autoClosetController);
        }

        private void NormalizeImplicitMenuParameters(GameObject closet, string uniqueName, List<ModularAvatarMenuItem> relatedMenuItems)
        {
            foreach (var menuItem in relatedMenuItems)
            {
                var control = menuItem.Control;
                if (control == null || !ShouldAssignImplicitMenuParameter(menuItem))
                    continue;

                if (control.parameter != null && !string.IsNullOrWhiteSpace(control.parameter.name))
                    continue;

                control.parameter = new VRCExpressionsMenu.Control.Parameter
                {
                    name = CreateStableMenuParameterName(closet, menuItem, uniqueName)
                };

                // 묵시 파라미터를 명시적 Bool 토글로 고정한다. automaticValue를 켜두면
                // Modular Avatar가 파라미터를 Float(8bit)로 확장하므로, 명시 값(1)으로
                // 전환해 Bool(1bit)로 생성되게 한다 (동기화 예산 토글당 8→1bit 절감).
                menuItem.automaticValue = false;
                control.value = 1f;

                EnsureMenuParameterConfig(menuItem, control);
                EditorUtility.SetDirty(menuItem);
            }
        }

        private void EnsureMenuParameterConfig(ModularAvatarMenuItem menuItem, VRCExpressionsMenu.Control control)
        {
            var parameterName = control?.parameter?.name;
            if (menuItem == null || string.IsNullOrWhiteSpace(parameterName) || !parameterName.StartsWith(ImplicitMenuParameterPrefix))
                return;

            var parameters = menuItem.GetComponent<ModularAvatarParameters>();
            if (parameters == null)
            {
                parameters = menuItem.gameObject.AddComponent<ModularAvatarParameters>();
            }

            if (parameters.parameters.Any(parameter => parameter.nameOrPrefix == parameterName))
                return;

            // automaticValue인 기본 토글은 이 시점에 control.value가 아직 0이고
            // MA가 나중에 1을 할당하므로, 리셋 휴리스틱(CollectMenuParameterResets)과
            // 일치하도록 1로 간주한다.
            var defaultValue = menuItem.isDefault
                ? (menuItem.automaticValue ? 1f : control.value)
                : 0f;
            parameters.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = parameterName,
                // NormalizeImplicitMenuParameters에서 automaticValue=false + value=1로 고정하므로
                // Modular Avatar가 이 파라미터를 Bool(1bit)로 생성한다. Expression Parameters /
                // 드라이버 / 애니메이터 파라미터를 모두 Bool로 맞춰 동기화 예산을 토글당 8→1bit로 줄인다.
                syncType = ParameterSyncType.Bool,
                // MA가 빈 파라미터를 자동 할당할 때와 동일하게 메뉴 아이템의 동기화 설정을 따른다.
                // (localOnly 강제 시 원격 플레이어에게 기믹 상태가 보이지 않음)
                localOnly = !menuItem.isSynced,
                defaultValue = defaultValue,
                saved = menuItem.isSaved,
                hasExplicitDefaultValue = !Mathf.Approximately(defaultValue, 0f)
            });
            EditorUtility.SetDirty(parameters);
        }

        private bool ShouldAssignImplicitMenuParameter(ModularAvatarMenuItem item)
        {
            var control = item.Control;
            if (control == null)
                return false;

            switch (control.type)
            {
                case VRCExpressionsMenu.Control.ControlType.Button:
                case VRCExpressionsMenu.Control.ControlType.Toggle:
                    break;
                default:
                    return false;
            }

            return GetControlledReactiveComponents(item).Any();
        }

        private void CollectMenuParameterResets(
            GameObject closet,
            string uniqueName,
            List<ModularAvatarMenuItem> relatedMenuItems,
            Dictionary<string, float> avatarParameters,
            HashSet<string> savedParameters,
            Dictionary<string, ParameterResetInfo> result)
        {
            var usages = new List<MenuParameterUsage>();

            foreach (var menuItem in relatedMenuItems)
            {
                var control = menuItem.Control;
                if (control == null)
                    continue;

                CollectMenuControlParameterUsages(control, menuItem.isDefault, menuItem.automaticValue, uniqueName, usages);

                if (control.subMenu != null)
                {
                    CollectMenuAssetParameterUsages(control.subMenu, uniqueName, usages, new HashSet<VRCExpressionsMenu>());
                }
            }

            foreach (Transform child in closet.transform)
            {
                var installers = child.GetComponentsInChildren<ModularAvatarMenuInstaller>(true);
                foreach (var installer in installers)
                {
                    if (!installer.enabled || installer.menuToAppend == null)
                        continue;

                    CollectMenuAssetParameterUsages(installer.menuToAppend, uniqueName, usages, new HashSet<VRCExpressionsMenu>());
                }
            }

            foreach (var group in usages.GroupBy(u => u.ParameterName))
            {
                var parameterName = group.Key;
                if (string.IsNullOrWhiteSpace(parameterName))
                    continue;

                // 기존 유저 saved 파라미터(라디얼 퍼펫 색조 등)는 유지를 의도한 값이므로 자동 리셋에서 제외.
                // 단 AutoCloset이 직접 생성한 묵시 파라미터는 리셋 기능의 핵심 대상이므로 예외
                // (MA isSaved 기본값이 true라 제외하면 기능 전체가 무력화됨).
                if (!parameterName.StartsWith(ImplicitMenuParameterPrefix) && savedParameters.Contains(parameterName))
                    continue;

                if (!result.TryGetValue(parameterName, out var info))
                {
                    info = new ParameterResetInfo
                    {
                        ParameterName = parameterName
                    };
                    result[parameterName] = info;
                }

                if (avatarParameters.TryGetValue(parameterName, out var defaultValue))
                {
                    info.DefaultValue = defaultValue;
                    continue;
                }

                var usageList = group.ToList();
                var explicitMenuDefault = usageList.LastOrDefault(u => u.IsDefault && !u.AutomaticValue);
                if (explicitMenuDefault != null)
                {
                    info.DefaultValue = explicitMenuDefault.Value;
                }
                else if (usageList.Count == 1 && usageList[0].IsDefault && usageList[0].AutomaticValue)
                {
                    info.DefaultValue = 1f;
                }
                else
                {
                    info.DefaultValue = 0f;
                }
            }
        }

        private IEnumerable<ModularAvatarMenuItem> GetClosetRelatedMenuItems(GameObject closet, string uniqueName)
        {
            var avatar = closet.GetComponentInParent<VRCAvatarDescriptor>(true);
            var avatarRoot = avatar != null ? avatar.gameObject : closet.transform.root.gameObject;
            var closetChildren = GetClosetChildren(closet);

            return avatarRoot.GetComponentsInChildren<ModularAvatarMenuItem>(true)
                .Where(menuItem => !IsTopLevelClosetSelector(closet, menuItem, uniqueName))
                .Where(menuItem =>
                    IsUnderAnyClosetChild(menuItem.transform, closetChildren) ||
                    MenuItemTargetsClosetChild(menuItem, closetChildren))
                .Distinct();
        }

        private HashSet<Transform> GetClosetChildren(GameObject closet)
        {
            var children = new HashSet<Transform>();
            foreach (Transform child in closet.transform)
            {
                children.Add(child);
            }

            return children;
        }

        private bool MenuItemTargetsClosetChild(ModularAvatarMenuItem menuItem, HashSet<Transform> closetChildren)
        {
            foreach (var reactiveComponent in GetControlledReactiveComponents(menuItem))
            {
                if (ReactiveComponentTargetsClosetChild(reactiveComponent, closetChildren))
                    return true;
            }

            return false;
        }

        private IEnumerable<ReactiveComponent> GetControlledReactiveComponents(ModularAvatarMenuItem menuItem)
        {
            foreach (var reactiveComponent in menuItem.GetComponentsInChildren<ReactiveComponent>(true))
            {
                if (reactiveComponent.transform == menuItem.transform)
                {
                    yield return reactiveComponent;
                    continue;
                }

                var parentMenuItem = reactiveComponent.GetComponentInParent<ModularAvatarMenuItem>();
                if (parentMenuItem == menuItem)
                {
                    yield return reactiveComponent;
                }
            }
        }

        private bool ReactiveComponentTargetsClosetChild(
            ReactiveComponent reactiveComponent,
            HashSet<Transform> closetChildren)
        {
            if (reactiveComponent == null)
                return false;

            switch (reactiveComponent)
            {
                case ModularAvatarObjectToggle objectToggle:
                    return objectToggle.Objects.Any(item => IsUnderAnyClosetChild(item.Object?.Get(objectToggle)?.transform, closetChildren));
                case ModularAvatarShapeChanger shapeChanger:
                    return shapeChanger.Shapes.Any(item => IsUnderAnyClosetChild(item.Object?.Get(shapeChanger)?.transform, closetChildren));
                case ModularAvatarMaterialSetter materialSetter:
                    return materialSetter.Objects.Any(item => IsUnderAnyClosetChild(item.Object?.Get(materialSetter)?.transform, closetChildren));
                case ModularAvatarMaterialSwap materialSwap:
                    return IsUnderAnyClosetChild(materialSwap.Root?.Get(materialSwap)?.transform, closetChildren);
                case ModularAvatarMeshCutter meshCutter:
                    return IsUnderAnyClosetChild(meshCutter.Object?.Get(meshCutter)?.transform, closetChildren);
                default:
                    return false;
            }
        }

        /// <summary>
        /// 보존 대상 PhysBone(= enabled이고 의상 루트까지 체인이 켜져 있는 것)을 가진
        /// 옷장 자식(의상) 목록을 수집합니다. 활성화/옵티마이저 제외/클립 생성이 이 기준을 공유합니다.
        /// </summary>
        private HashSet<Transform> CollectDynamicsChildren(GameObject closet)
        {
            var result = new HashSet<Transform>();
            foreach (Transform child in closet.transform)
            {
                if (AutoClosetUtil.HasPreservablePhysBones(child))
                    result.Add(child);
            }

            return result;
        }

        private void ActivateDynamicHierarchies(GameObject closet, HashSet<Transform> dynamicsChildren)
        {
            if (dynamicsChildren.Count == 0)
                return;

            var avatar = closet.GetComponentInParent<VRCAvatarDescriptor>(true);
            if (avatar == null)
                return;

            // 의상 루트(와 그 위 체인)만 활성화한다. 의상 내부에서 유저가 꺼둔 서브트리는
            // 의도된 상태이므로 건드리지 않는다 (보존 기준에서도 이미 제외됨).
            foreach (var child in dynamicsChildren)
            {
                ActivatePath(child, avatar.transform);
            }
        }

        private void ActivatePath(Transform target, Transform stopAt)
        {
            // stopAt(아바타 루트) 밖의 트랜스폼이면 순회하지 않는다.
            // (예: PhysBone rootTransform이 클론 밖 원본 씬 오브젝트를 참조하는 경우
            //  씬 루트까지 올라가며 원본을 SetActive(true)해 비파괴 보장이 깨진다)
            if (target == null || stopAt == null || !target.IsChildOf(stopAt))
                return;

            var current = target;
            while (current != null && current != stopAt)
            {
                if (!current.gameObject.activeSelf)
                {
                    current.gameObject.SetActive(true);
                    EditorUtility.SetDirty(current.gameObject);
                }

                current = current.parent;
            }
        }

        /// <summary>
        /// 원래 비활성이던(안 입은 상태로 저장된) 보존 의상의 렌더러를 직렬화 수준에서 끕니다.
        /// GameObject는 활성(m_IsActive=1)으로 유지되어 PhysBone은 계속 시뮬레이션되지만,
        /// 애니메이터가 돌기 전 rest 상태(업로드 썸네일 캡처, 재생 직후 T포즈)에서는 보이지 않습니다.
        /// 클립이 모든 옷장 상태에서 렌더러 m_Enabled를 명시적으로 구동하므로 런타임 표시는 애니메이터가 복원합니다.
        /// 주의: 클립 생성(AccumulateRendererCurves)이 에디터 시점의 renderer.enabled를 "입었을 때 값"으로
        /// 읽으므로, 반드시 모든 옷장의 CreateClosetStates가 끝난 뒤에 호출해야 합니다(중첩 옷장 포함).
        /// </summary>
        private void DisableInactiveDynamicsRenderers(HashSet<Transform> originallyInactiveDynamics)
        {
            foreach (var child in originallyInactiveDynamics)
            {
                if (child == null)
                    continue;

                foreach (var renderer in child.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null || !renderer.enabled)
                        continue;

                    renderer.enabled = false;
                    EditorUtility.SetDirty(renderer);
                }
            }
        }

        private void ExcludeDynamicPhysBonesFromTraceAndOptimize(GameObject avatarRoot, HashSet<Transform> dynamicsChildren)
        {
            if (avatarRoot == null || dynamicsChildren == null || dynamicsChildren.Count == 0)
                return;

            var physBoneObjects = new HashSet<GameObject>();
            var physBoneTransforms = new HashSet<Transform>();
            foreach (var child in dynamicsChildren)
            {
                foreach (var physBone in AutoClosetUtil.CollectPreservablePhysBones(child))
                {
                    physBoneObjects.Add(physBone.gameObject);
                    physBoneTransforms.Add(physBone.transform);
                }
            }

            if (physBoneObjects.Count == 0)
                return;

            ExcludeGameObjectsFromTraceAndOptimize(avatarRoot, physBoneObjects);
            ExcludeTransformsFromD4rkAvatarOptimizer(avatarRoot, physBoneTransforms);
        }

        private void ExcludeGameObjectsFromTraceAndOptimize(GameObject avatarRoot, HashSet<GameObject> gameObjects)
        {
            var traceAndOptimize = avatarRoot.GetComponents<Component>()
                .FirstOrDefault(component => component != null &&
                                             component.GetType().FullName == "Anatawa12.AvatarOptimizer.TraceAndOptimize");
            if (traceAndOptimize == null)
                return;

            var serialized = new SerializedObject(traceAndOptimize);
            var exclusions = serialized.FindProperty("debugOptions.exclusions");
            if (exclusions == null || !exclusions.isArray)
            {
                // AAO 업데이트로 내부 직렬화 구조가 바뀌면 조용히 무력화되는 대신 경고를 남긴다.
                Debug.LogWarning("[AutoCloset] Could not find 'debugOptions.exclusions' on AvatarOptimizer TraceAndOptimize. " +
                                 "Closet PhysBones may be stripped by Trace & Optimize. (AAO version may have changed its serialized layout)");
                return;
            }

            var changed = false;
            foreach (var gameObject in gameObjects)
            {
                if (ContainsObjectReference(exclusions, gameObject))
                    continue;

                var index = exclusions.arraySize;
                exclusions.arraySize++;
                exclusions.GetArrayElementAtIndex(index).objectReferenceValue = gameObject;
                changed = true;
            }

            if (!changed)
                return;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(traceAndOptimize);
        }

        private void ExcludeTransformsFromD4rkAvatarOptimizer(GameObject avatarRoot, HashSet<Transform> transforms)
        {
            var optimizers = avatarRoot.GetComponentsInChildren<Component>(true)
                .Where(component => component != null && component.GetType().FullName == "d4rkAvatarOptimizer");

            foreach (var optimizer in optimizers)
            {
                var serialized = new SerializedObject(optimizer);
                var exclusions = serialized.FindProperty("ExcludeTransforms");
                if (exclusions == null || !exclusions.isArray)
                {
                    Debug.LogWarning("[AutoCloset] Could not find 'ExcludeTransforms' on d4rkAvatarOptimizer. " +
                                     "Closet PhysBones may be stripped by the optimizer. (optimizer version may have changed its serialized layout)");
                    continue;
                }

                var changed = false;
                foreach (var transform in transforms)
                {
                    if (ContainsObjectReference(exclusions, transform))
                        continue;

                    var index = exclusions.arraySize;
                    exclusions.arraySize++;
                    exclusions.GetArrayElementAtIndex(index).objectReferenceValue = transform;
                    changed = true;
                }

                if (!changed)
                    continue;

                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(optimizer);
            }
        }

        private bool ContainsObjectReference(SerializedProperty arrayProperty, UnityEngine.Object target)
        {
            for (var i = 0; i < arrayProperty.arraySize; i++)
            {
                if (arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue == target)
                    return true;
            }

            return false;
        }

        private bool IsUnderAnyClosetChild(Transform transform, HashSet<Transform> closetChildren)
        {
            var current = transform;
            while (current != null)
            {
                if (closetChildren.Contains(current))
                    return true;

                current = current.parent;
            }

            return false;
        }

        private void CollectMenuAssetParameterUsages(
            VRCExpressionsMenu menu,
            string uniqueName,
            List<MenuParameterUsage> usages,
            HashSet<VRCExpressionsMenu> visited)
        {
            if (menu == null || !visited.Add(menu) || menu.controls == null)
                return;

            foreach (var control in menu.controls)
            {
                if (control == null)
                    continue;

                CollectMenuControlParameterUsages(control, false, false, uniqueName, usages);

                if (control.subMenu != null)
                {
                    CollectMenuAssetParameterUsages(control.subMenu, uniqueName, usages, visited);
                }
            }
        }

        private void CollectMenuControlParameterUsages(
            VRCExpressionsMenu.Control control,
            bool isDefault,
            bool automaticValue,
            string uniqueName,
            List<MenuParameterUsage> usages)
        {
            if (control.type != VRCExpressionsMenu.Control.ControlType.SubMenu)
            {
                AddMenuParameterUsage(control.parameter?.name, control.value, isDefault, automaticValue, uniqueName, usages);
            }

            if (control.subParameters == null)
                return;

            foreach (var subParameter in control.subParameters)
            {
                AddMenuParameterUsage(subParameter?.name, 0f, false, false, uniqueName, usages);
            }
        }

        private void AddMenuParameterUsage(
            string parameterName,
            float value,
            bool isDefault,
            bool automaticValue,
            string uniqueName,
            List<MenuParameterUsage> usages)
        {
            if (string.IsNullOrWhiteSpace(parameterName) || parameterName == uniqueName)
                return;

            usages.Add(new MenuParameterUsage
            {
                ParameterName = parameterName,
                Value = value,
                IsDefault = isDefault,
                AutomaticValue = automaticValue
            });
        }

        private bool IsTopLevelClosetSelector(GameObject closet, ModularAvatarMenuItem menuItem, string uniqueName)
        {
            if (closet == null || menuItem == null || menuItem.transform.parent != closet.transform)
                return false;

            var control = menuItem.Control;
            return control?.parameter != null && control.parameter.name == uniqueName;
        }

        private string CreateStableMenuParameterName(GameObject closet, ModularAvatarMenuItem menuItem, string uniqueName)
        {
            var relativePath = AutoClosetUtil.GetRelativePath(menuItem.transform, closet.transform);
            var readableName = SanitizeParameterSegment(menuItem.gameObject.name);
            return $"{ImplicitMenuParameterPrefix}{readableName}_{ShortHash(uniqueName + "/" + relativePath)}";
        }

        private string SanitizeParameterSegment(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Param";

            var builder = new StringBuilder();
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                {
                    builder.Append(c);
                }
                else
                {
                    builder.Append('_');
                }
            }

            if (builder.Length == 0)
                builder.Append("Param");

            return builder.Length > 24 ? builder.ToString(0, 24) : builder.ToString();
        }

        private string ShortHash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input ?? ""));
                var builder = new StringBuilder();
                for (var i = 0; i < 4; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }

                return builder.ToString();
            }
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
                if (driversList.Any(driver => DriverTargetsParameter(driver, info.ParameterName)))
                    continue;

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

        private bool DriverTargetsParameter(ClosetParameterDriverItem driver, string parameterName)
        {
            if (driver == null || string.IsNullOrEmpty(parameterName))
                return false;

            if (driver.type == ClosetParameterDriverItem.ChangeType.Copy)
            {
                var destination = string.IsNullOrEmpty(driver.destName) ? driver.source : driver.destName;
                return destination == parameterName;
            }

            return driver.name == parameterName;
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
