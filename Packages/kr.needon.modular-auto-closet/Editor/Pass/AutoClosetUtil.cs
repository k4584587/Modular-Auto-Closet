using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using System.Collections.Generic;
using VRC.SDKBase;
using nadena.dev.ndmf;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC_AnimatorLayerControl = VRC.SDKBase.VRC_AnimatorLayerControl;

namespace needon.Editor.Pass
{
    internal static class AutoClosetUtil
    {
        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "_";
            return new string(name.Select(c => InvalidFileNameChars.Contains(c) ? '_' : c).ToArray());
        }

        private static AnimatorControllerLayer AddLayer(AnimatorController animator, string layerName)
        {
            var layer = new AnimatorControllerLayer();
            layer.name = animator.MakeUniqueLayerName(layerName);
            layer.defaultWeight = 1f;

            layer.stateMachine = new AnimatorStateMachine();
            layer.stateMachine.name = layer.name.Replace(".", "_");
            layer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(animator)))
                AssetDatabase.AddObjectToAsset(layer.stateMachine, AssetDatabase.GetAssetPath(animator));
            EditorUtility.SetDirty(layer.stateMachine);

            animator.AddLayer(layer);
            return layer;
        }

        private static void ErrorDialog(string message)
        {
            EditorUtility.DisplayDialog("ERROR!", message, "OK");
            throw new Exception(message);
        }

        internal static string GetRelativePath(Transform target, Transform root)
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

        public static RuntimeAnimatorController GetAvatarFxAnimator(VRCAvatarDescriptor avatarDescriptor)
        {
            var controllerLayer = Array.FindIndex(
                avatarDescriptor.baseAnimationLayers,
                item => item.type == VRCAvatarDescriptor.AnimLayerType.FX && item.animatorController
            );

            if (controllerLayer == -1)
            {
                ErrorDialog("Cannot find FX animator controller!");
                return null;
            }

            var animatorController = avatarDescriptor.baseAnimationLayers[controllerLayer].animatorController;
            if (animatorController) return animatorController;
            ErrorDialog("Cannot find FX animator controller!");
            return null;
        }

        public static void AddAnimatorParameter(AnimatorController controller, string paramName, AnimatorControllerParameterType type)
        {
            var index = 0;
            for (; index < controller.parameters.Length; index++)
            {
                var param = controller.parameters[index];
                if (param.name == paramName) return;
            }

            controller.AddParameter(new AnimatorControllerParameter
            {
                name = paramName,
                type = type
            });
        }

        public static AnimationClip CreateClosetAnimationClip(GameObject closet, string parentName, string activeClothesName, HashSet<Transform> dynamicsChildren = null)
        {
            var safeParentName = SanitizeFileName(parentName);
            var safeClothesName = SanitizeFileName(activeClothesName);
            var clipPath = $"Packages/nadena.dev.ndmf/__Generated/{safeParentName}/_assets/{safeClothesName}.anim";

            // 디렉토리가 없으면 생성
            var directory = System.IO.Path.GetDirectoryName(clipPath);
            if (!System.IO.Directory.Exists(directory))
            {
                if (directory != null) System.IO.Directory.CreateDirectory(directory);
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
            }
            else
            {
                // 기존 커브 모두 제거
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    AnimationUtility.SetEditorCurve(clip, binding, null);
                }
            }

            // 아바타 루트 찾기
            var avatarRoot = closet.transform.root;

            // WD OFF 대응: 모든 상태에서 모든 프로퍼티를 명시적으로 애니메이션하여
            // MMD 월드 등 외부 시스템에 의한 블렌드셰이프/토글 값 오버라이드를 방지
            // key: path|propertyName, value: (binding, curve)
            var blendshapeCurves = new Dictionary<string, (EditorCurveBinding binding, AnimationCurve curve)>();
            var toggleCurves = new Dictionary<string, (EditorCurveBinding binding, AnimationCurve curve)>();
            var rendererCurves = new Dictionary<string, (EditorCurveBinding binding, AnimationCurve curve)>();

            // 모든 옷장 자식(옷)의 활성화 상태를 애니메이션으로 기록
            foreach (Transform child in closet.transform)
            {
                var isActive = (child.name == activeClothesName);
                var keepActiveForDynamics = dynamicsChildren != null
                    ? dynamicsChildren.Contains(child)
                    : HasPreservablePhysBones(child);

                // 두 키프레임으로 클립 길이를 확보 (VRChat 안정성)
                var val = isActive || keepActiveForDynamics ? 1f : 0f;
                var curve = new AnimationCurve(
                    new Keyframe(0f, val),
                    new Keyframe(1f / 60f, val)
                );

                // 아바타 루트로부터의 상대 경로 계산
                var relativePath = GetRelativePath(child.transform, avatarRoot);
                needon.Editor.Util.ClosetLogger.Log(child, "Log.Anim.Path", relativePath, isActive);

                var binding = EditorCurveBinding.FloatCurve(
                    relativePath,
                    typeof(GameObject),
                    "m_IsActive"
                );

                AnimationUtility.SetEditorCurve(clip, binding, curve);

                if (keepActiveForDynamics)
                {
                    AccumulateRendererCurves(child, avatarRoot, rendererCurves, isActive);
                }

                // ClosetConfig (unified) takes precedence when present
                var closetConfig = child.GetComponent<ClosetConfig>();
                if (closetConfig != null)
                {
                    // Toggles - WD OFF 대응: 모든 의상의 토글을 명시적으로 애니메이션
                    // 활성 의상: 설정값 사용, 비활성 의상: 반대값으로 리셋
                    if (closetConfig.toggles != null)
                    {
                        foreach (var toggle in closetConfig.toggles)
                        {
                            if (toggle == null || toggle.target == null) continue;

                            // 활성 의상: 설정된 값, 비활성 의상: 반대값(리셋)
                            float toggleValue = (isActive == toggle.active) ? 1f : 0f;

                            var togglePath = GetRelativePath(toggle.target.transform, avatarRoot);
                            needon.Editor.Util.ClosetLogger.Log(child, "Log.Toggle.Path", togglePath, isActive);

                            var toggleBinding = EditorCurveBinding.FloatCurve(
                                togglePath,
                                typeof(GameObject),
                                "m_IsActive"
                            );

                            AccumulateToggleCurve(toggleCurves, toggleBinding, toggleValue, isActive);
                        }
                    }

                    // Blendshapes
                    if (closetConfig.shapes != null)
                    {
                        foreach (var item in closetConfig.shapes)
                        {
                            if (item == null || item.mesh == null) continue;

                            var value = isActive ? item.value : 0f;

                            var bsPath = GetRelativePath(item.mesh.transform, avatarRoot);
                            needon.Editor.Util.ClosetLogger.Log(child, "Log.Blendshape.Path", bsPath, isActive);

                            var property = $"blendShape.{item.shapeKey}";
                            var bsBinding = EditorCurveBinding.FloatCurve(
                                bsPath,
                                typeof(SkinnedMeshRenderer),
                                property
                            );

                            var key = bsBinding.path + "|" + bsBinding.propertyName;
                            if (!blendshapeCurves.ContainsKey(key))
                            {
                                blendshapeCurves[key] = (bsBinding, new AnimationCurve(new Keyframe(0f, value), new Keyframe(1f / 60f, value)));
                            }
                            else if (isActive)
                            {
                                // 활성 의상의 값이 최우선이며, 비활성(0) 값으로는 덮어쓰지 않음
                                blendshapeCurves[key] = (bsBinding, new AnimationCurve(new Keyframe(0f, value), new Keyframe(1f / 60f, value)));
                            }
                        }
                    }
                }
                else
                {
                    // Fallback to legacy components for backward compatibility
                    // Toggles - WD OFF 대응: 레거시에서도 모든 의상의 토글을 명시적으로 애니메이션
                    var closetToggle = child.GetComponent<ClosetToggle>();
                    if (closetToggle != null && closetToggle.toggles != null)
                    {
                        foreach (var toggle in closetToggle.toggles)
                        {
                            if (toggle == null || toggle.target == null) continue;

                            float toggleValue = (isActive == toggle.active) ? 1f : 0f;

                            var togglePath = GetRelativePath(toggle.target.transform, avatarRoot);
                            needon.Editor.Util.ClosetLogger.Log(child, "Log.Toggle.Path", togglePath, isActive);

                            var toggleBinding = EditorCurveBinding.FloatCurve(
                                togglePath,
                                typeof(GameObject),
                                "m_IsActive"
                            );

                            AccumulateToggleCurve(toggleCurves, toggleBinding, toggleValue, isActive);
                        }
                    }

                    var closetBlendshape = child.GetComponent<ClosetBlendshape>();
                    if (closetBlendshape != null && closetBlendshape.shapes != null)
                    {
                        foreach (var item in closetBlendshape.shapes)
                        {
                            if (item == null || item.mesh == null) continue;

                            var value = isActive ? item.value : 0f;
                            var bsPath = GetRelativePath(item.mesh.transform, avatarRoot);
                            needon.Editor.Util.ClosetLogger.Log(child, "Log.Blendshape.Path", bsPath, isActive);
                            var property = $"blendShape.{item.shapeKey}";
                            var bsBinding = EditorCurveBinding.FloatCurve(
                                bsPath,
                                typeof(SkinnedMeshRenderer),
                                property
                            );

                            var key = bsBinding.path + "|" + bsBinding.propertyName;
                            if (!blendshapeCurves.ContainsKey(key))
                            {
                                blendshapeCurves[key] = (bsBinding, new AnimationCurve(new Keyframe(0f, value), new Keyframe(1f / 60f, value)));
                            }
                            else if (isActive)
                            {
                                blendshapeCurves[key] = (bsBinding, new AnimationCurve(new Keyframe(0f, value), new Keyframe(1f / 60f, value)));
                            }
                        }
                    }
                }
            }

            // 누적된 토글 곡선을 한 번만 기록 (중복 덮어쓰기 방지)
            foreach (var kv in toggleCurves.Values)
            {
                AnimationUtility.SetEditorCurve(clip, kv.binding, kv.curve);
            }

            // PhysBone 보존을 위해 루트를 계속 켜둔 의상은 렌더러만 끄고 켭니다.
            foreach (var kv in rendererCurves.Values)
            {
                AnimationUtility.SetEditorCurve(clip, kv.binding, kv.curve);
            }

            // 누적된 블렌드셰이프 곡선을 한 번만 기록 (중복 덮어쓰기 방지)
            foreach (var kv in blendshapeCurves.Values)
            {
                AnimationUtility.SetEditorCurve(clip, kv.binding, kv.curve);
            }

            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) == null)
            {
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            else
            {
                EditorUtility.SetDirty(clip);
            }

            AssetDatabase.SaveAssets();
            needon.Editor.Util.ClosetLogger.Log(closet, "Log.Anim.Saved", clipPath);
            return clip;
        }

        public static void ApplyCreateAnimatorLayer(AnimatorController controller, string animatorLayerName)
        {
            var layer = AddLayer(controller, animatorLayerName);
            layer.defaultWeight = 1f;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            AssetDatabase.SaveAssets();

            var layers = controller.layers;
            for (var i = 0; i < layers.Length; i++)
            {
                if (layers[i].name != animatorLayerName) continue;
                layers[i] = layer;
                controller.layers = layers;
                break;
            }
        }

        /// <summary>
        /// 단일 타겟에 대해 On 또는 Off 애니메이션 클립을 생성합니다.
        /// </summary>
        /// <param name="target">애니메이션을 기록할 GameObject</param>
        /// <param name="parentName">클립이 저장될 폴더명 (패키지 내 __Generated 경로 아래)</param>
        /// <param name="clipName">클립 파일명 (확장자 없이)</param>
        /// <param name="isOn">true면 On 클립, false면 Off 클립</param>
        public static AnimationClip CreateToggleAnimationClip(GameObject target, string parentName, string clipName, bool isOn)
        {
            // On/Off 구분 접미사
            var suffix = isOn ? "On" : "Off";
            var safeParentName = SanitizeFileName(parentName);
            var safeClipName = SanitizeFileName(clipName);
            // Packages/nadena.dev.ndmf/__Generated/{parentName}/_assets/{clipName}_{On|Off}.anim
            var clipPath = $"Packages/nadena.dev.ndmf/__Generated/{safeParentName}/_assets/{safeClipName}_{suffix}.anim";

            // 디렉토리 생성
            var directory = System.IO.Path.GetDirectoryName(clipPath);
            if (!System.IO.Directory.Exists(directory))
                System.IO.Directory.CreateDirectory(directory);

            // 기존 클립 불러오기 또는 새로 만들기
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) ?? new AnimationClip();
            // 기존 커브 제거
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                AnimationUtility.SetEditorCurve(clip, binding, null);

            // 타겟의 루트(씬 최상위) 기준 상대 경로 계산
            var root = target.transform.root;
            var relativePath = GetRelativePath(target.transform, root);
            needon.Editor.Util.ClosetLogger.Log(target, "Log.Toggle.SetPath", relativePath, isOn ? 1f : 0f);

            // 두 키프레임 커브로 클립 길이 확보 (VRChat 안정성)
            var toggleVal = isOn ? 1f : 0f;
            var curve = new AnimationCurve(new Keyframe(0f, toggleVal), new Keyframe(1f / 60f, toggleVal));
            var bindingInfo = EditorCurveBinding.FloatCurve(
                relativePath,
                typeof(GameObject),
                "m_IsActive"
            );
            AnimationUtility.SetEditorCurve(clip, bindingInfo, curve);

            // 에셋으로 저장
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) == null)
                AssetDatabase.CreateAsset(clip, clipPath);
            else
                EditorUtility.SetDirty(clip);

            AssetDatabase.SaveAssets();
            needon.Editor.Util.ClosetLogger.Log(target, "Log.Toggle.ClipSaved", clipPath);
            return clip;
        }

        /// <summary>
        /// 지정한 타겟에 대해 On/Off 두 가지 애니메이션 클립을 생성합니다.
        /// </summary>
        /// <param name="target">애니메이션을 기록할 GameObject</param>
        /// <param name="parentName">저장 폴더명</param>
        /// <param name="clipName">클립 기본 이름</param>
        public static void CreateToggleAnimations(GameObject target, string parentName, string clipName)
        {
            CreateToggleAnimationClip(target, parentName, clipName, true);
            CreateToggleAnimationClip(target, parentName, clipName, false);
        }

        /// <summary>
        /// AnimatorState에 파라미터 드라이버를 추가합니다.
        /// </summary>
        /// <param name="state">파라미터 드라이버를 추가할 AnimatorState</param>
        /// <param name="driverItems">추가할 파라미터 드라이버 아이템 목록</param>
        public static void ApplyParameterDriversToState(AnimatorState state, ClosetParameterDriverItem[] driverItems)
        {
            if (state == null || driverItems == null || driverItems.Length == 0)
                return;

            // VRCAvatarParameterDriver StateMachineBehaviour 추가
            var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driver.parameters = new List<VRC_AvatarParameterDriver.Parameter>();

            foreach (var item in driverItems)
            {
                if (item == null) continue;

                var param = new VRC_AvatarParameterDriver.Parameter();

                switch (item.type)
                {
                    case ClosetParameterDriverItem.ChangeType.Set:
                        param.type = VRC_AvatarParameterDriver.ChangeType.Set;
                        param.name = item.name;
                        param.value = item.value;
                        break;

                    case ClosetParameterDriverItem.ChangeType.Add:
                        param.type = VRC_AvatarParameterDriver.ChangeType.Add;
                        param.name = item.name;
                        param.value = item.value;
                        param.chance = item.chance;
                        break;

                    case ClosetParameterDriverItem.ChangeType.Random:
                        param.type = VRC_AvatarParameterDriver.ChangeType.Random;
                        param.name = item.name;
                        param.valueMin = item.valueMin;
                        param.valueMax = item.valueMax;
                        param.chance = item.chance;
                        break;

                    case ClosetParameterDriverItem.ChangeType.Copy:
                        param.type = VRC_AvatarParameterDriver.ChangeType.Copy;
                        param.source = item.source;
                        param.name = string.IsNullOrEmpty(item.destName) ? item.source : item.destName;
                        param.chance = item.chance;
                        break;
                }

                driver.parameters.Add(param);
            }

            needon.Editor.Util.ClosetLogger.Log(state, "Log.ParameterDriver.Applied", driver.parameters.Count);
        }

        /// <summary>
        /// 레이어 가중치 보호: 각 상태 진입 시 레이어 Weight를 1로 강제 복원합니다.
        /// AFK 등 외부 요인으로 Weight가 0으로 변경되는 것을 방지합니다.
        /// </summary>
        public static void ApplyLayerWeightControl(AnimatorState state, int layerIndex)
        {
            var layerControl = state.AddStateMachineBehaviour<VRCAnimatorLayerControl>();
            layerControl.playable = VRC_AnimatorLayerControl.BlendableLayer.FX;
            layerControl.layer = layerIndex;
            layerControl.goalWeight = 1f;
            layerControl.blendDuration = 0f;
        }

        /// <summary>
        /// 빌드 1회당 측정한 아바타의 순수 FX Write Defaults 값을 캐싱합니다.
        /// (옷장이 추가한 레이어가 측정을 오염시켜 다수결이 뒤집히는 것을 방지)
        /// </summary>
        internal class AvatarWriteDefaultsCache
        {
            public bool Computed;
            public bool Value;
        }

        /// <summary>
        /// 부모 계층의 AutoCloset 설정에 따라 생성 상태에 적용할 Write Defaults 값을 결정합니다.
        /// Auto 모드면 아바타의 기존 FX WD를 감지(다수결)하여 맞춥니다.
        /// </summary>
        public static bool ResolveWriteDefaults(BuildContext context, GameObject obj)
        {
            var closet = obj.GetComponentInParent<AutoCloset>(true);
            var mode = closet != null ? closet.writeDefaultsMode : AutoCloset.WriteDefaultsMode.Auto;

            switch (mode)
            {
                case AutoCloset.WriteDefaultsMode.On:
                    return true;
                case AutoCloset.WriteDefaultsMode.Off:
                    return false;
                default:
                    var cache = context.GetState<AvatarWriteDefaultsCache>();
                    WarmAvatarWriteDefaultsCache(context);
                    return cache.Value;
            }
        }

        /// <summary>
        /// 옷장 레이어/상태가 FX에 추가되기 전에 호출해 순수 아바타 FX의 WD를 무조건 측정·캐싱합니다.
        /// (ResolveWriteDefaults는 On/Off 모드에서 캐시를 채우지 않으므로, 늦은 Auto 측정이
        /// 이미 추가된 옷장 상태들에 의해 오염되는 것을 방지하려면 이 메서드로 선행 측정해야 함)
        /// </summary>
        public static void WarmAvatarWriteDefaultsCache(BuildContext context)
        {
            var cache = context.GetState<AvatarWriteDefaultsCache>();
            if (cache.Computed) return;

            cache.Value = DetectAvatarWriteDefaults(context.AvatarDescriptor);
            cache.Computed = true;
        }

        /// <summary>
        /// 아바타의 기존 FX 컨트롤러 상태들의 Write Defaults를 다수결로 판정합니다.
        /// 동률이거나 측정할 상태가 없으면 ON(현대 아바타 기본)을 반환합니다.
        /// </summary>
        public static bool DetectAvatarWriteDefaults(VRCAvatarDescriptor avatarDescriptor)
        {
            // GetAvatarFxAnimator는 FX가 없으면 모달 다이얼로그 + 예외를 발생시키므로
            // (Inspector가 리페인트마다 호출함) 여기서는 조용히 탐색한다.
            var fx = FindAvatarFxAnimator(avatarDescriptor);
            if (fx == null) return true;

            var on = 0;
            var off = 0;
            foreach (var layer in fx.layers)
            {
                if (layer.stateMachine == null) continue;

                // 단일 상태 BlendTree 레이어는 "WD OFF 아바타에서도 의도적으로 ON"인
                // 표준 관례(Direct Blend Tree)이므로 다수결에서 제외한다.
                if (IsSingleStateBlendTreeLayer(layer.stateMachine)) continue;

                CountStatesWriteDefaults(layer.stateMachine, ref on, ref off);
            }

            if (on == 0 && off == 0) return true;
            return on >= off;
        }

        private static bool IsSingleStateBlendTreeLayer(AnimatorStateMachine sm)
        {
            return sm.stateMachines.Length == 0 &&
                   sm.states.Length == 1 &&
                   sm.states[0].state != null &&
                   sm.states[0].state.motion is BlendTree;
        }

        /// <summary>
        /// FX 컨트롤러를 다이얼로그/예외 없이 탐색합니다. 없으면 null.
        /// </summary>
        private static AnimatorController FindAvatarFxAnimator(VRCAvatarDescriptor avatarDescriptor)
        {
            if (avatarDescriptor == null || avatarDescriptor.baseAnimationLayers == null)
                return null;

            foreach (var layer in avatarDescriptor.baseAnimationLayers)
            {
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX && layer.animatorController != null)
                    return layer.animatorController as AnimatorController;
            }

            return null;
        }

        private static void CountStatesWriteDefaults(AnimatorStateMachine sm, ref int on, ref int off)
        {
            // 서브 에셋이 삭제된 손상 컨트롤러는 null 상태/서브 머신을 가질 수 있다.
            foreach (var child in sm.states)
            {
                if (child.state == null) continue;
                if (child.state.writeDefaultValues) on++;
                else off++;
            }

            foreach (var childSm in sm.stateMachines)
            {
                if (childSm.stateMachine == null) continue;
                CountStatesWriteDefaults(childSm.stateMachine, ref on, ref off);
            }
        }

        /// <summary>
        /// 의상이 "보존 대상" PhysBone을 갖는지 판정합니다.
        /// 보존 대상 = enabled이고, 의상 루트까지의 GameObject 체인이 모두 activeSelf인 PhysBone.
        /// 유저가 의도적으로 꺼둔 서브트리의 PhysBone은 보존하지 않습니다(바닐라 동작 유지).
        /// </summary>
        internal static bool HasPreservablePhysBones(Transform closetChild)
        {
            if (closetChild == null)
                return false;

            foreach (var physBone in closetChild.GetComponentsInChildren<VRCPhysBone>(true))
            {
                if (IsPreservablePhysBone(physBone, closetChild))
                    return true;
            }

            return false;
        }

        internal static List<VRCPhysBone> CollectPreservablePhysBones(Transform closetChild)
        {
            var result = new List<VRCPhysBone>();
            if (closetChild == null)
                return result;

            foreach (var physBone in closetChild.GetComponentsInChildren<VRCPhysBone>(true))
            {
                if (IsPreservablePhysBone(physBone, closetChild))
                    result.Add(physBone);
            }

            return result;
        }

        private static bool IsPreservablePhysBone(VRCPhysBone physBone, Transform closetChild)
        {
            if (physBone == null || !physBone.enabled)
                return false;

            // 의상 루트(closetChild) 자신의 activeSelf는 옷장이 관리하므로 검사하지 않는다.
            var current = physBone.transform;
            while (current != null && current != closetChild)
            {
                if (!current.gameObject.activeSelf)
                    return false;

                current = current.parent;
            }

            return current == closetChild;
        }

        private static void AccumulateRendererCurves(
            Transform child,
            Transform avatarRoot,
            Dictionary<string, (EditorCurveBinding binding, AnimationCurve curve)> rendererCurves,
            bool isActive)
        {
            foreach (var renderer in child.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;

                var rendererPath = GetRelativePath(renderer.transform, avatarRoot);
                var rendererBinding = EditorCurveBinding.FloatCurve(
                    rendererPath,
                    renderer.GetType(),
                    "m_Enabled"
                );

                var value = isActive && renderer.enabled ? 1f : 0f;
                AccumulateRendererCurve(rendererCurves, rendererBinding, value, isActive);
            }
        }

        private static void AccumulateRendererCurve(
            Dictionary<string, (EditorCurveBinding binding, AnimationCurve curve)> rendererCurves,
            EditorCurveBinding binding,
            float value,
            bool isActive)
        {
            var key = binding.path + "|" + binding.type.FullName + "|m_Enabled";
            if (!rendererCurves.ContainsKey(key) || isActive)
            {
                rendererCurves[key] = (binding, new AnimationCurve(new Keyframe(0f, value), new Keyframe(1f / 60f, value)));
            }
        }

        /// <summary>
        /// 토글 커브를 딕셔너리에 누적합니다. 활성 의상의 값이 최우선으로 적용됩니다.
        /// </summary>
        private static void AccumulateToggleCurve(
            Dictionary<string, (EditorCurveBinding binding, AnimationCurve curve)> toggleCurves,
            EditorCurveBinding binding, float value, bool isActive)
        {
            var key = binding.path + "|m_IsActive";
            if (!toggleCurves.ContainsKey(key))
            {
                toggleCurves[key] = (binding, new AnimationCurve(new Keyframe(0f, value), new Keyframe(1f / 60f, value)));
            }
            else if (isActive)
            {
                toggleCurves[key] = (binding, new AnimationCurve(new Keyframe(0f, value), new Keyframe(1f / 60f, value)));
            }
        }
    }
}
