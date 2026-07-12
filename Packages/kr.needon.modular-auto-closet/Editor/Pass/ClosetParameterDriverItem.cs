#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace needon.Editor.Pass
{
    /// <summary>
    /// ClosetParameterDriverItem 용 PropertyDrawer (고급 모드에서 사용).
    /// v2(parameter-driver-v2.md §6): targetMode(파라미터/참조) 선택, 파라미터 카탈로그 드롭다운,
    /// 타입 인지 값 UI, 미등록 파라미터 검증·자동 등록, MenuTarget 참조 미리보기를 지원한다.
    /// OnGUI와 GetPropertyHeight가 절대 어긋나지 않도록 BuildRows() 하나로 레이아웃을 결정한다.
    /// </summary>
    [CustomPropertyDrawer(typeof(ClosetParameterDriverItem))]
    public class ClosetParameterDriverItemDrawer : PropertyDrawer
    {
        private const float Pad = 4f;
        private const float Space = 2f;
        private static float Line => EditorGUIUtility.singleLineHeight;

        // 그려질 한 줄의 종류. OnGUI는 종류별로 위젯을 그리고, GetPropertyHeight는 높이만 합산한다.
        private enum RowKind
        {
            FieldsMissing,
            TargetModePopup,
            TypePopup,
            ParamName,
            Value,
            ValueMin,
            ValueMax,
            Chance,
            Source,
            Destination,
            NotFoundWarning,
            AutoRegisterToggle,
            AutoRegisterSyncedToggle,
            SyncedBudgetWarning,
            BoolValueTypeWarning,
            MenuTargetObject,
            MenuTargetNullInfo,
            MenuTargetNoMenuItemError,
            MenuTargetNoParameterError,
            MenuTargetPreview,
            MenuTargetOnPopup,
            CreateToggleButton,
        }

        private struct Row
        {
            public RowKind Kind;
            public float Height;
            public Row(RowKind kind, float height) { Kind = kind; Height = height; }
        }

        // ===== 레이아웃 단일 진실원 =====
        private static List<Row> BuildRows(SerializedProperty property, Transform avatarRoot)
        {
            var rows = new List<Row>();
            var targetModeProp = property.FindPropertyRelative("targetMode");
            var typeProp = property.FindPropertyRelative("type");
            if (targetModeProp == null || typeProp == null)
            {
                rows.Add(new Row(RowKind.FieldsMissing, Line * 2));
                return rows;
            }

            rows.Add(new Row(RowKind.TargetModePopup, Line));

            var mode = (ClosetParameterDriverItem.TargetMode)targetModeProp.enumValueIndex;
            if (mode == ClosetParameterDriverItem.TargetMode.Parameter)
            {
                rows.Add(new Row(RowKind.TypePopup, Line));
                var changeType = (ClosetParameterDriverItem.ChangeType)typeProp.enumValueIndex;
                switch (changeType)
                {
                    case ClosetParameterDriverItem.ChangeType.Set:
                        rows.Add(new Row(RowKind.ParamName, Line));
                        rows.Add(new Row(RowKind.Value, Line));
                        AddNameValidationRows(rows, property, avatarRoot, allowBoolWarn: false);
                        break;
                    case ClosetParameterDriverItem.ChangeType.Add:
                        rows.Add(new Row(RowKind.ParamName, Line));
                        rows.Add(new Row(RowKind.Value, Line));
                        rows.Add(new Row(RowKind.Chance, Line));
                        AddNameValidationRows(rows, property, avatarRoot, allowBoolWarn: true);
                        break;
                    case ClosetParameterDriverItem.ChangeType.Random:
                        rows.Add(new Row(RowKind.ParamName, Line));
                        rows.Add(new Row(RowKind.ValueMin, Line));
                        rows.Add(new Row(RowKind.ValueMax, Line));
                        rows.Add(new Row(RowKind.Chance, Line));
                        AddNameValidationRows(rows, property, avatarRoot, allowBoolWarn: true);
                        break;
                    case ClosetParameterDriverItem.ChangeType.Copy:
                        rows.Add(new Row(RowKind.Source, Line));
                        rows.Add(new Row(RowKind.Destination, Line));
                        rows.Add(new Row(RowKind.Chance, Line));
                        break;
                }
            }
            else // MenuTarget
            {
                rows.Add(new Row(RowKind.MenuTargetObject, Line));
                var targetObj = property.FindPropertyRelative("targetObject").objectReferenceValue as GameObject;
                if (targetObj == null)
                {
                    rows.Add(new Row(RowKind.MenuTargetNullInfo, Line * 2));
                }
                else
                {
                    var mi = targetObj.GetComponent<ModularAvatarMenuItem>();
                    if (mi == null)
                    {
                        rows.Add(new Row(RowKind.MenuTargetNoMenuItemError, Line * 2));
                        // 토글이 없는 일반 오브젝트면 토글을 만들어 연결하는 버튼 제공
                        if (ClosetDriverUI.CanOfferToggleCreation(targetObj, avatarRoot))
                            rows.Add(new Row(RowKind.CreateToggleButton, Line));
                    }
                    else
                    {
                        string p = mi.Control?.parameter?.name;
                        if (string.IsNullOrEmpty(p))
                        {
                            rows.Add(new Row(RowKind.MenuTargetNoParameterError, Line * 2));
                        }
                        else
                        {
                            rows.Add(new Row(RowKind.MenuTargetPreview, Line));
                            rows.Add(new Row(RowKind.MenuTargetOnPopup, Line));
                        }
                    }
                }
            }

            return rows;
        }

        // 이름 검증(미등록 경고 + 자동 등록) 또는 Add/Random의 Bool 타입 경고를 조건부로 추가.
        private static void AddNameValidationRows(List<Row> rows, SerializedProperty property, Transform avatarRoot, bool allowBoolWarn)
        {
            if (avatarRoot == null) return; // 아바타 컨텍스트가 없으면 검증 불가 — 조용히 넘어감
            var name = property.FindPropertyRelative("name").stringValue;
            if (string.IsNullOrEmpty(name)) return;

            if (!needon.Editor.Util.ClosetParameterCatalog.TryGetParameter(avatarRoot, name, out var info))
            {
                rows.Add(new Row(RowKind.NotFoundWarning, Line * 2));
                rows.Add(new Row(RowKind.AutoRegisterToggle, Line));
                if (property.FindPropertyRelative("autoRegister").boolValue)
                {
                    rows.Add(new Row(RowKind.AutoRegisterSyncedToggle, Line));
                    if (property.FindPropertyRelative("autoRegisterSynced").boolValue)
                        rows.Add(new Row(RowKind.SyncedBudgetWarning, Line * 2));
                }
            }
            else if (allowBoolWarn && info.valueType == needon.Editor.Util.ClosetParameterValueType.Bool)
            {
                rows.Add(new Row(RowKind.BoolValueTypeWarning, Line * 2));
            }
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            GUI.Box(position, GUIContent.none, GUI.skin.box);

            var ctx = property.serializedObject?.targetObject as UnityEngine.Object;
            var avatarRoot = needon.Editor.Util.ClosetParameterCatalog.FindAvatarRoot(property.serializedObject?.targetObject as Component);
            var rows = BuildRows(property, avatarRoot);

            var targetModeProp = property.FindPropertyRelative("targetMode");
            var typeProp = property.FindPropertyRelative("type");
            var nameProp = property.FindPropertyRelative("name");
            var valueProp = property.FindPropertyRelative("value");
            var valueMinProp = property.FindPropertyRelative("valueMin");
            var valueMaxProp = property.FindPropertyRelative("valueMax");
            var chanceProp = property.FindPropertyRelative("chance");
            var sourceProp = property.FindPropertyRelative("source");
            var destNameProp = property.FindPropertyRelative("destName");
            var targetObjectProp = property.FindPropertyRelative("targetObject");
            var menuTargetOnProp = property.FindPropertyRelative("menuTargetOn");
            var autoRegisterProp = property.FindPropertyRelative("autoRegister");
            var autoRegisterSyncedProp = property.FindPropertyRelative("autoRegisterSynced");

            float width = position.width - 2 * Pad;
            float x = position.x + Pad;
            float y = position.y + Pad;

            foreach (var row in rows)
            {
                var rect = new Rect(x, y, width, row.Height);
                DrawRow(row.Kind, rect, ctx, avatarRoot,
                    targetModeProp, typeProp, nameProp, valueProp, valueMinProp, valueMaxProp,
                    chanceProp, sourceProp, destNameProp, targetObjectProp, menuTargetOnProp,
                    autoRegisterProp, autoRegisterSyncedProp);
                y += row.Height + Space;
            }

            EditorGUI.EndProperty();
        }

        private void DrawRow(RowKind kind, Rect rect, UnityEngine.Object ctx, Transform avatarRoot,
            SerializedProperty targetModeProp, SerializedProperty typeProp, SerializedProperty nameProp,
            SerializedProperty valueProp, SerializedProperty valueMinProp, SerializedProperty valueMaxProp,
            SerializedProperty chanceProp, SerializedProperty sourceProp, SerializedProperty destNameProp,
            SerializedProperty targetObjectProp, SerializedProperty menuTargetOnProp,
            SerializedProperty autoRegisterProp, SerializedProperty autoRegisterSyncedProp)
        {
            switch (kind)
            {
                case RowKind.FieldsMissing:
                    EditorGUI.HelpBox(rect, needon.Editor.Util.ClosetLocalization.Get(ctx, "Error.ParameterDriver.FieldsMissing"), MessageType.Error);
                    break;

                case RowKind.TargetModePopup:
                {
                    var label = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.TargetMode");
                    string[] options =
                    {
                        needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.TargetMode.Parameter"),
                        needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.TargetMode.MenuTarget"),
                    };
                    int cur = targetModeProp.enumValueIndex;
                    int nw = EditorGUI.Popup(rect, label, cur, options);
                    if (nw != cur) targetModeProp.enumValueIndex = nw;
                    break;
                }

                case RowKind.TypePopup:
                    EditorGUI.PropertyField(rect, typeProp, new GUIContent(needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.Type")));
                    break;

                case RowKind.ParamName:
                    ClosetDriverUI.DrawParamNameField(rect, nameProp, avatarRoot,
                        new GUIContent(needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.ParameterName")));
                    break;

                case RowKind.Value:
                    ClosetDriverUI.DrawTypeAwareValue(rect, valueProp, avatarRoot, nameProp.stringValue,
                        new GUIContent(needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.Common.Value")));
                    break;

                case RowKind.ValueMin:
                    EditorGUI.PropertyField(rect, valueMinProp, new GUIContent(needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.ValueMin")));
                    break;

                case RowKind.ValueMax:
                    EditorGUI.PropertyField(rect, valueMaxProp, new GUIContent(needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.ValueMax")));
                    break;

                case RowKind.Chance:
                    EditorGUI.Slider(rect, chanceProp, 0f, 1f, new GUIContent(needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.Chance")));
                    break;

                case RowKind.Source:
                    ClosetDriverUI.DrawParamNameField(rect, sourceProp, avatarRoot,
                        new GUIContent(needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.Source")));
                    break;

                case RowKind.Destination:
                    ClosetDriverUI.DrawParamNameField(rect, destNameProp, avatarRoot,
                        new GUIContent(needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.Destination")));
                    break;

                case RowKind.NotFoundWarning:
                    EditorGUI.HelpBox(rect, needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.NotFound"), MessageType.Warning);
                    break;

                case RowKind.AutoRegisterToggle:
                    EditorGUI.PropertyField(rect, autoRegisterProp, new GUIContent(needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.AutoRegister")));
                    break;

                case RowKind.AutoRegisterSyncedToggle:
                    EditorGUI.PropertyField(rect, autoRegisterSyncedProp, new GUIContent(needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.AutoRegisterSynced")));
                    break;

                case RowKind.SyncedBudgetWarning:
                    EditorGUI.HelpBox(rect, needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.SyncedBudgetWarning"), MessageType.Warning);
                    break;

                case RowKind.BoolValueTypeWarning:
                    EditorGUI.HelpBox(rect, needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.BoolTypeWarning"), MessageType.Warning);
                    break;

                case RowKind.MenuTargetObject:
                    ClosetDriverUI.DrawMenuTargetObjectField(rect, targetObjectProp,
                        new GUIContent(needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.MenuTarget")), ctx, avatarRoot);
                    break;

                case RowKind.CreateToggleButton:
                    if (GUI.Button(rect, needon.Editor.Util.ClosetLocalization.Get(ctx, "ClosetConfig.Drivers.CreateToggle"), EditorStyles.miniButton))
                        ClosetDriverUI.CreateAndAssignToggle(targetObjectProp, ctx);
                    break;

                case RowKind.MenuTargetNullInfo:
                    EditorGUI.HelpBox(rect, needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.MenuTargetHint"), MessageType.Info);
                    break;

                case RowKind.MenuTargetNoMenuItemError:
                    EditorGUI.HelpBox(rect, needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.MenuTargetNoMenuItem"), MessageType.Error);
                    break;

                case RowKind.MenuTargetNoParameterError:
                    EditorGUI.HelpBox(rect, needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.MenuTargetNoParameter"), MessageType.Error);
                    break;

                case RowKind.MenuTargetPreview:
                {
                    var targetObj = targetObjectProp.objectReferenceValue as GameObject;
                    if (ClosetDriverUI.TryResolveMenuTarget(targetObj, menuTargetOnProp.boolValue, avatarRoot, out var pn, out var pv, out _))
                    {
                        var text = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.MenuTargetPreview", pn, FormatValue(pv));
                        EditorGUI.LabelField(rect, text, EditorStyles.miniLabel);
                    }
                    break;
                }

                case RowKind.MenuTargetOnPopup:
                {
                    var label = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.MenuTargetOn");
                    string[] options =
                    {
                        needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.Toggle.On"),
                        needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.Toggle.Off"),
                    };
                    int cur = menuTargetOnProp.boolValue ? 0 : 1;
                    int nw = EditorGUI.Popup(rect, label, cur, options);
                    menuTargetOnProp.boolValue = (nw == 0);
                    break;
                }
            }
        }

        // 정수는 소수점 없이, 소수는 최대 3자리로 표시.
        private static string FormatValue(float v)
        {
            return Mathf.Approximately(v, Mathf.Round(v))
                ? Mathf.RoundToInt(v).ToString()
                : v.ToString("0.###");
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var avatarRoot = needon.Editor.Util.ClosetParameterCatalog.FindAvatarRoot(property.serializedObject?.targetObject as Component);
            var rows = BuildRows(property, avatarRoot);
            float total = Pad + Pad;
            foreach (var row in rows) total += row.Height + Space;
            return total;
        }
    }

    /// <summary>
    /// 파라미터 카탈로그 기반 검색 드롭다운. 각 항목에 값 타입과 synced 뱃지를 표시한다.
    /// </summary>
    internal class ClosetParameterDropdown : AdvancedDropdown
    {
        private readonly IReadOnlyList<needon.Editor.Util.ClosetParameterInfo> _parameters;
        private readonly Action<needon.Editor.Util.ClosetParameterInfo> _onSelected;
        private readonly string _title;

        public ClosetParameterDropdown(AdvancedDropdownState state,
            IReadOnlyList<needon.Editor.Util.ClosetParameterInfo> parameters,
            string title,
            Action<needon.Editor.Util.ClosetParameterInfo> onSelected) : base(state)
        {
            _parameters = parameters;
            _onSelected = onSelected;
            _title = title;
            minimumSize = new Vector2(260f, 320f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem(string.IsNullOrEmpty(_title) ? "Parameters" : _title);
            if (_parameters != null)
            {
                for (int i = 0; i < _parameters.Count; i++)
                {
                    var p = _parameters[i];
                    string badge = "[" + p.valueType + "]";
                    string synced = p.synced ? "  (synced)" : "";
                    root.AddChild(new AdvancedDropdownItem($"{p.name}    {badge}{synced}") { id = i });
                }
            }
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (_parameters != null && item.id >= 0 && item.id < _parameters.Count)
                _onSelected?.Invoke(_parameters[item.id]);
        }
    }

    /// <summary>
    /// 파라미터 드라이버 UI 공유 헬퍼. 고급 드로어(ClosetParameterDriverItemDrawer)와
    /// 간단 모드 인스펙터(ClosetConfigEditor)가 같은 위젯/해석 로직을 쓰도록 한 곳에 모았다.
    /// </summary>
    internal static class ClosetDriverUI
    {
        // 텍스트 필드 + 카탈로그 드롭다운 버튼(▾) 하이브리드. avatarRoot가 없거나 파라미터가 없으면 버튼 비활성.
        internal static void DrawParamNameField(Rect rect, SerializedProperty nameProp, Transform avatarRoot, GUIContent label)
        {
            const float btnW = 20f;
            Rect textRect = new Rect(rect.x, rect.y, Mathf.Max(0f, rect.width - btnW - 2f), rect.height);
            Rect btnRect = new Rect(rect.xMax - btnW, rect.y, btnW, rect.height);

            EditorGUI.BeginChangeCheck();
            string s = (label == null || label == GUIContent.none)
                ? EditorGUI.TextField(textRect, nameProp.stringValue)
                : EditorGUI.TextField(textRect, label, nameProp.stringValue);
            if (EditorGUI.EndChangeCheck()) nameProp.stringValue = s;

            var parameters = needon.Editor.Util.ClosetParameterCatalog.GetParameters(avatarRoot);
            bool hasParams = parameters != null && parameters.Count > 0;
            using (new EditorGUI.DisabledScope(!hasParams))
            {
                if (GUI.Button(btnRect, "▾", EditorStyles.miniButton))
                    ShowParamDropdown(btnRect, nameProp, parameters);
            }
        }

        private static void ShowParamDropdown(Rect rect, SerializedProperty nameProp, IReadOnlyList<needon.Editor.Util.ClosetParameterInfo> parameters)
        {
            // 드롭다운 콜백은 다음 이벤트(별도 창)에서 발화하므로, 원본 SerializedProperty 대신
            // 대상 오브젝트 + 프로퍼티 경로를 캡처해 새 SerializedObject로 안전하게 기록한다.
            var target = nameProp.serializedObject.targetObject;
            var path = nameProp.propertyPath;
            var title = needon.Editor.Util.ClosetLocalization.Get(target, "Drawer.ParameterDriver.SelectParameter");
            var dd = new ClosetParameterDropdown(new AdvancedDropdownState(), parameters, title, info =>
            {
                if (target == null) return;
                var fresh = new SerializedObject(target);
                var p = fresh.FindProperty(path);
                if (p != null)
                {
                    p.stringValue = info.name;
                    fresh.ApplyModifiedProperties();
                }
            });
            dd.Show(rect);
        }

        // 카탈로그에 파라미터가 있으면 타입별 위젯(Bool→체크박스/Int→정수/Float→float), 없으면 기존 float 필드.
        internal static void DrawTypeAwareValue(Rect rect, SerializedProperty valueProp, Transform avatarRoot, string paramName, GUIContent label)
        {
            bool hasLabel = !(label == null || label == GUIContent.none);

            if (avatarRoot != null && !string.IsNullOrEmpty(paramName)
                && needon.Editor.Util.ClosetParameterCatalog.TryGetParameter(avatarRoot, paramName, out var info))
            {
                switch (info.valueType)
                {
                    case needon.Editor.Util.ClosetParameterValueType.Bool:
                    {
                        EditorGUI.BeginChangeCheck();
                        bool cur = valueProp.floatValue != 0f;
                        bool nw = hasLabel ? EditorGUI.Toggle(rect, label, cur) : EditorGUI.Toggle(rect, cur);
                        if (EditorGUI.EndChangeCheck()) valueProp.floatValue = nw ? 1f : 0f;
                        return;
                    }
                    case needon.Editor.Util.ClosetParameterValueType.Int:
                    {
                        EditorGUI.BeginChangeCheck();
                        int cur = Mathf.RoundToInt(valueProp.floatValue);
                        int nw = hasLabel ? EditorGUI.IntField(rect, label, cur) : EditorGUI.IntField(rect, cur);
                        if (EditorGUI.EndChangeCheck()) valueProp.floatValue = nw;
                        return;
                    }
                }
            }

            EditorGUI.PropertyField(rect, valueProp, hasLabel ? label : GUIContent.none);
        }

        // ===== 토글 자동 생성 (참조 대상에 MenuItem이 없을 때) =====

        // 토글 생성을 제안할 수 있는 대상인가: MenuItem이 없고, 옷장/아바타 루트가 아니며
        // (우클릭 "Add Create Toggle"의 Validate와 동일 기준), 같은 아바타 안에 있어야 한다.
        internal static bool CanOfferToggleCreation(GameObject obj, Transform avatarRoot)
        {
            if (obj == null || avatarRoot == null) return false;
            if (obj.GetComponent<ModularAvatarMenuItem>() != null) return false;
            if (obj.GetComponent<AutoCloset>() != null) return false;
            if (obj.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>() != null) return false;
            return needon.Editor.Util.ClosetParameterCatalog.FindAvatarRoot(obj.transform) == avatarRoot;
        }

        // MenuTarget 대상 오브젝트 필드. 드래그로 MenuItem 없는 오브젝트가 들어오면
        // "토글을 만들어 연결할까요?" 다이얼로그를 제안한다.
        internal static void DrawMenuTargetObjectField(Rect rect, SerializedProperty targetObjectProp, GUIContent label,
            UnityEngine.Object ctx, Transform avatarRoot)
        {
            EditorGUI.BeginChangeCheck();
            if (label == null || label == GUIContent.none)
                EditorGUI.PropertyField(rect, targetObjectProp, GUIContent.none);
            else
                EditorGUI.PropertyField(rect, targetObjectProp, label);
            if (!EditorGUI.EndChangeCheck()) return;

            // ⊙ 픽커의 실시간 선택 갱신 이벤트마다 다이얼로그가 뜨는 것을 방지
            // (픽커 경로는 에러 상태의 "토글 생성" 버튼이 커버)
            if (Event.current != null && Event.current.commandName == "ObjectSelectorUpdated") return;

            var obj = targetObjectProp.objectReferenceValue as GameObject;
            if (!CanOfferToggleCreation(obj, avatarRoot)) return;

            var title = needon.Editor.Util.ClosetLocalization.Get(ctx, "Dialog.CreateToggle.Title");
            var message = needon.Editor.Util.ClosetLocalization.Get(ctx, "Dialog.CreateToggle.Message", obj.name);
            var ok = needon.Editor.Util.ClosetLocalization.Get(ctx, "Dialog.CreateToggle.Confirm");
            var cancel = needon.Editor.Util.ClosetLocalization.Get(ctx, "Dialog.Cancel");
            if (EditorUtility.DisplayDialog(title, message, ok, cancel))
                CreateAndAssignToggle(targetObjectProp, ctx);
        }

        // 현재 targetObject를 켜고 끄는 토글을 생성하고, 참조를 새 토글 아이템으로 교체한다.
        internal static void CreateAndAssignToggle(SerializedProperty targetObjectProp, UnityEngine.Object ctx)
        {
            var obj = targetObjectProp.objectReferenceValue as GameObject;
            if (obj == null) return;

            var searchFrom = (ctx as Component)?.transform;
            var item = needon.Editor.ToggleCreator.CreateToggleForObject(obj, searchFrom);
            if (item != null)
                targetObjectProp.objectReferenceValue = item;
        }

        /// <summary>
        /// MenuTarget 참조를 (파라미터 이름, 적용 값, Int 대상 여부)로 해석한다.
        /// 값은 빌드 타임 해석(AutoClosetUtil.ResolveDriverItems)과 정확히 일치시킨다:
        /// 켜기(menuTargetOn) 시 MenuItem의 Control.value, 끄기 시 0. Int/Bool 모두 동일.
        /// isIntTarget은 값 계산이 아니라 간단 모드 UI 분기(의상 전환 요약 vs 켜기/끄기 팝업)에만 쓴다.
        /// 카탈로그로 타입을 확인하되, 미등록이면 MenuItem의 Control.value로 Int 여부를 추정한다.
        /// </summary>
        internal static bool TryResolveMenuTarget(GameObject targetObj, bool menuTargetOn, Transform avatarRoot,
            out string paramName, out float value, out bool isIntTarget)
        {
            paramName = null; value = 0f; isIntTarget = false;
            if (targetObj == null) return false;

            var mi = targetObj.GetComponent<ModularAvatarMenuItem>();
            if (mi == null || mi.Control == null || mi.Control.parameter == null) return false;

            paramName = mi.Control.parameter.name;
            if (string.IsNullOrEmpty(paramName)) return false;

            if (avatarRoot != null && needon.Editor.Util.ClosetParameterCatalog.TryGetParameter(avatarRoot, paramName, out var info))
                isIntTarget = info.valueType == needon.Editor.Util.ClosetParameterValueType.Int;
            else
                isIntTarget = mi.Control.value > 1f;

            value = menuTargetOn ? mi.Control.value : 0f;
            return true;
        }
    }
}
#endif
