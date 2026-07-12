#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace needon.Editor.Util
{
    /// <summary>파라미터 값 타입. VRC/MA/애니메이터의 타입을 Bool/Int/Float로 정규화한 것.</summary>
    public enum ClosetParameterValueType { Bool, Int, Float }

    /// <summary>파라미터가 수집된 출처.</summary>
    public enum ClosetParameterSourceKind { ExpressionParameters, ModularAvatarParameters, Animator }

    /// <summary>아바타 파라미터 한 건의 정보. 이름 기준으로 여러 소스가 병합된 최종 결과.</summary>
    public struct ClosetParameterInfo
    {
        public string name;
        public ClosetParameterValueType valueType;
        public bool synced;
        public bool saved;
        public float defaultValue;
        public ClosetParameterSourceKind source;
    }

    /// <summary>
    /// 에디터 시점에 씬의 아바타 루트에서 파라미터를 전수 수집·캐시하는 유틸리티.
    /// 수집 순서·우선순위는 빌드 패스의 CollectAvatarParameterDefaults와 동일하게 맞춘다:
    /// Expression Parameters → ModularAvatarParameters(이름 충돌 시 덮어씀) → 애니메이터(미등록 이름만 보충).
    /// </summary>
    public static class ClosetParameterCatalog
    {
        // avatarRoot 인스턴스ID -> 수집 결과 캐시.
        private static readonly Dictionary<int, CatalogEntry> _cache = new Dictionary<int, CatalogEntry>();

        private sealed class CatalogEntry
        {
            public IReadOnlyList<ClosetParameterInfo> Ordered;   // 이름 오름차순 정렬본
            public Dictionary<string, ClosetParameterInfo> ByName; // 단건 조회용
        }

        [InitializeOnLoadMethod]
        private static void RegisterInvalidationHooks()
        {
            // 계층/Undo 변경 시 캐시를 비워 다음 조회 때 재스캔한다.
            EditorApplication.hierarchyChanged += Invalidate;
            Undo.undoRedoPerformed += Invalidate;
        }

        /// <summary>아바타 전체 파라미터 목록(이름 오름차순, 중복 없음). avatarRoot가 null이면 빈 목록.</summary>
        public static IReadOnlyList<ClosetParameterInfo> GetParameters(Transform avatarRoot)
        {
            if (avatarRoot == null) return Array.Empty<ClosetParameterInfo>();
            return GetEntry(avatarRoot).Ordered;
        }

        /// <summary>이름으로 단건 조회. 없으면 false.</summary>
        public static bool TryGetParameter(Transform avatarRoot, string name, out ClosetParameterInfo info)
        {
            info = default;
            if (avatarRoot == null || string.IsNullOrEmpty(name)) return false;
            return GetEntry(avatarRoot).ByName.TryGetValue(name, out info);
        }

        /// <summary>component의 transform 부모 체인을 올라가며 VRCAvatarDescriptor를 찾는다. 없으면 null.</summary>
        public static Transform FindAvatarRoot(Component component)
        {
            if (component == null) return null;

            var t = component.transform;
            while (t != null)
            {
                if (t.GetComponent<VRCAvatarDescriptor>() != null) return t;
                t = t.parent;
            }
            return null;
        }

        /// <summary>캐시 전체를 비운다.</summary>
        public static void Invalidate()
        {
            _cache.Clear();
        }

        private static CatalogEntry GetEntry(Transform avatarRoot)
        {
            var key = avatarRoot.GetInstanceID();
            if (_cache.TryGetValue(key, out var entry) && entry != null)
                return entry;

            entry = BuildCatalog(avatarRoot);
            _cache[key] = entry;
            return entry;
        }

        private static CatalogEntry BuildCatalog(Transform avatarRoot)
        {
            // 이름 -> 병합 결과. 뒤 소스가 앞 소스를 덮어쓴다(기준 원본 CollectAvatarParameterDefaults와 동일).
            var merged = new Dictionary<string, ClosetParameterInfo>(StringComparer.Ordinal);
            // MA에서 NotSynced라 타입이 Float로 추정된 이름들. 애니메이터에서 구체 타입 발견 시 valueType만 보정.
            var unresolvedValueType = new HashSet<string>(StringComparer.Ordinal);

            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();

            // 1. VRC Expression Parameters
            if (descriptor != null && descriptor.expressionParameters != null && descriptor.expressionParameters.parameters != null)
            {
                foreach (var p in descriptor.expressionParameters.parameters)
                {
                    if (string.IsNullOrEmpty(p.name)) continue;

                    merged[p.name] = new ClosetParameterInfo
                    {
                        name = p.name,
                        valueType = MapExpressionType(p.valueType),
                        synced = p.networkSynced,
                        saved = p.saved,
                        defaultValue = p.defaultValue,
                        source = ClosetParameterSourceKind.ExpressionParameters
                    };
                }
            }

            // 2. ModularAvatarParameters — 이름 충돌 시 Expression을 덮어씀(MA 런타임 동작). isPrefix 항목 제외.
            foreach (var maParam in avatarRoot.GetComponentsInChildren<ModularAvatarParameters>(true))
            {
                if (maParam == null || maParam.parameters == null) continue;

                foreach (var p in maParam.parameters)
                {
                    if (p.isPrefix || string.IsNullOrEmpty(p.nameOrPrefix)) continue;

                    var concreteType = p.syncType != ParameterSyncType.NotSynced;
                    merged[p.nameOrPrefix] = new ClosetParameterInfo
                    {
                        name = p.nameOrPrefix,
                        valueType = MapSyncType(p.syncType),
                        synced = !p.localOnly && concreteType,
                        saved = p.saved,
                        defaultValue = p.defaultValue,
                        source = ClosetParameterSourceKind.ModularAvatarParameters
                    };

                    // NotSynced면 타입 미확정(Float 추정) — 애니메이터 보정 후보로 등록.
                    if (concreteType) unresolvedValueType.Remove(p.nameOrPrefix);
                    else unresolvedValueType.Add(p.nameOrPrefix);
                }
            }

            // 3. 애니메이터 파라미터 — 미등록 이름만 보충(Trigger 제외).
            //    FX 레이어 -> 자식 Animator -> MA MergeAnimator 순.
            //    AnimatorOverrideController는 클립만 교체하고 파라미터는 베이스에 있으므로 베이스로 풀어서 스캔한다.
            if (descriptor != null)
                AddAnimatorParameters(GetEffectiveController(GetFxController(descriptor)), merged, unresolvedValueType);

            foreach (var animator in avatarRoot.GetComponentsInChildren<Animator>(true))
            {
                if (animator == null) continue;
                AddAnimatorParameters(GetEffectiveController(animator.runtimeAnimatorController), merged, unresolvedValueType);
            }

            foreach (var mergeAnimator in avatarRoot.GetComponentsInChildren<ModularAvatarMergeAnimator>(true))
            {
                if (mergeAnimator == null) continue;
                AddAnimatorParameters(GetEffectiveController(mergeAnimator.animator), merged, unresolvedValueType);
            }

            var ordered = merged.Values
                .OrderBy(p => p.name, StringComparer.Ordinal)
                .ToList();

            return new CatalogEntry { Ordered = ordered, ByName = merged };
        }

        /// <summary>
        /// 애니메이터 파라미터를 병합 결과에 보충한다. 이미 알려진 이름은 건드리지 않되,
        /// MA NotSynced로 타입이 미확정인 이름은 valueType만 애니메이터 타입으로 보정한다.
        /// </summary>
        private static void AddAnimatorParameters(
            AnimatorController controller,
            Dictionary<string, ClosetParameterInfo> merged,
            HashSet<string> unresolvedValueType)
        {
            if (controller == null) return;

            foreach (var parameter in controller.parameters)
            {
                if (parameter == null || string.IsNullOrEmpty(parameter.name)) continue;
                if (parameter.type == AnimatorControllerParameterType.Trigger) continue;

                var mappedType = MapAnimatorType(parameter.type);

                if (merged.TryGetValue(parameter.name, out var existing))
                {
                    // 런타임에서는 Expression/MA 기본값·타입이 우선이므로 이미 알려진 이름은 보충하지 않는다.
                    // 단, MA NotSynced라 타입이 미확정인 항목은 valueType만 보정하고 나머지는 유지.
                    if (unresolvedValueType.Remove(parameter.name))
                    {
                        existing.valueType = mappedType;
                        merged[parameter.name] = existing;
                    }
                    continue;
                }

                merged[parameter.name] = new ClosetParameterInfo
                {
                    name = parameter.name,
                    valueType = mappedType,
                    synced = false,
                    saved = false,
                    defaultValue = AnimatorDefaultValue(parameter),
                    source = ClosetParameterSourceKind.Animator
                };
            }
        }

        private static RuntimeAnimatorController GetFxController(VRCAvatarDescriptor descriptor)
        {
            if (descriptor == null || descriptor.baseAnimationLayers == null) return null;

            foreach (var layer in descriptor.baseAnimationLayers)
            {
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX && layer.animatorController != null)
                    return layer.animatorController;
            }
            return null;
        }

        // AnimatorOverrideController 체인을 베이스 AnimatorController까지 풀어낸다.
        // (오버라이드는 클립만 교체하므로 파라미터는 베이스 컨트롤러에 정의되어 있음)
        // 빌드 패스(CollectAvatarParameterDefaults)와 스캔 의미론을 맞추기 위해 internal로 공유한다.
        internal static AnimatorController GetEffectiveController(RuntimeAnimatorController runtimeController)
        {
            while (runtimeController is AnimatorOverrideController overrideController)
            {
                runtimeController = overrideController.runtimeAnimatorController;
            }
            return runtimeController as AnimatorController;
        }

        private static float AnimatorDefaultValue(AnimatorControllerParameter parameter)
        {
            switch (parameter.type)
            {
                case AnimatorControllerParameterType.Bool: return parameter.defaultBool ? 1f : 0f;
                case AnimatorControllerParameterType.Int: return parameter.defaultInt;
                default: return parameter.defaultFloat;
            }
        }

        private static ClosetParameterValueType MapExpressionType(VRCExpressionParameters.ValueType t)
        {
            switch (t)
            {
                case VRCExpressionParameters.ValueType.Bool: return ClosetParameterValueType.Bool;
                case VRCExpressionParameters.ValueType.Int: return ClosetParameterValueType.Int;
                default: return ClosetParameterValueType.Float;
            }
        }

        private static ClosetParameterValueType MapSyncType(ParameterSyncType t)
        {
            switch (t)
            {
                case ParameterSyncType.Bool: return ClosetParameterValueType.Bool;
                case ParameterSyncType.Int: return ClosetParameterValueType.Int;
                case ParameterSyncType.Float: return ClosetParameterValueType.Float;
                default: return ClosetParameterValueType.Float; // NotSynced: 타입 미확정 → Float로 추정
            }
        }

        private static ClosetParameterValueType MapAnimatorType(AnimatorControllerParameterType t)
        {
            switch (t)
            {
                case AnimatorControllerParameterType.Bool: return ClosetParameterValueType.Bool;
                case AnimatorControllerParameterType.Int: return ClosetParameterValueType.Int;
                default: return ClosetParameterValueType.Float;
            }
        }
    }
}
#endif
