#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace needon.Editor.Pass
{
    [CustomEditor(typeof(ClosetConfig))]
    [CanEditMultipleObjects]
    public class ClosetConfigEditor : UnityEditor.Editor
    {
        private ClosetConfig _component;
        private static bool _previewEnabled = false;
        private const string DriversAdvancedPrefKey = "MAC.Drivers.Advanced";
        private static Dictionary<SkinnedMeshRenderer, Dictionary<int, float>> _originalBlendshapeValues = new Dictionary<SkinnedMeshRenderer, Dictionary<int, float>>();

        static ClosetConfigEditor()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Restore blendshapes before entering play mode
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                RestoreOriginalBlendshapes();
                _previewEnabled = false;
            }
        }

        public void OnEnable()
        {
            _component = (ClosetConfig)target;

            // Ensure arrays are non-null for clean drawing
            if (_component.toggles == null) _component.toggles = new ClosetToggleItem[0];
            if (_component.shapes  == null) _component.shapes  = new ClosetBlendshapeItem[0];
            if (_component.drivers == null) _component.drivers = new ClosetParameterDriverItem[0];
            EditorUtility.SetDirty(_component);

            // Set icon
            var iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/kr.needon.modular-auto-closet/Resource/ClosetIcon.png"
            );
            if (iconTexture != null)
            {
                EditorGUIUtility.SetIconForObject(_component, iconTexture);
            }
        }

        public void OnDisable()
        {
            // Restore original values when inspector is closed
            RestoreOriginalBlendshapes();
        }

        private static void RestoreOriginalBlendshapes()
        {
            foreach (var meshEntry in _originalBlendshapeValues)
            {
                if (meshEntry.Key == null || meshEntry.Key.sharedMesh == null) continue;

                foreach (var shapeEntry in meshEntry.Value)
                {
                    meshEntry.Key.SetBlendShapeWeight(shapeEntry.Key, shapeEntry.Value);
                }
            }

            _originalBlendshapeValues.Clear();
            UnityEditor.SceneView.RepaintAll();
        }

        public static bool IsPreviewEnabled()
        {
            return _previewEnabled;
        }

        private static void SaveOriginalBlendshapeValue(SkinnedMeshRenderer mesh, int blendshapeIndex)
        {
            if (mesh == null || blendshapeIndex < 0) return;

            if (!_originalBlendshapeValues.ContainsKey(mesh))
                _originalBlendshapeValues[mesh] = new Dictionary<int, float>();

            if (!_originalBlendshapeValues[mesh].ContainsKey(blendshapeIndex))
                _originalBlendshapeValues[mesh][blendshapeIndex] = mesh.GetBlendShapeWeight(blendshapeIndex);
        }

        public static void ApplyBlendshapePreview(SkinnedMeshRenderer mesh, string shapeKey, float value)
        {
            if (!_previewEnabled || mesh == null || string.IsNullOrEmpty(shapeKey)) return;

            int index = mesh.sharedMesh.GetBlendShapeIndex(shapeKey);
            if (index < 0) return;

            // Save original value (only once per mesh/shape)
            SaveOriginalBlendshapeValue(mesh, index);

            // Apply preview value
            mesh.SetBlendShapeWeight(index, value);
            UnityEditor.SceneView.RepaintAll();
        }

        private void ApplyAllBlendshapes()
        {
            if (_component == null || _component.shapes == null) return;

            foreach (var shape in _component.shapes)
            {
                if (shape == null || shape.mesh == null || string.IsNullOrEmpty(shape.shapeKey)) continue;

                int index = shape.mesh.sharedMesh.GetBlendShapeIndex(shape.shapeKey);
                if (index < 0) continue;

                // Save original value (only once per mesh/shape)
                SaveOriginalBlendshapeValue(shape.mesh, index);

                // Apply the configured value
                shape.mesh.SetBlendShapeWeight(index, shape.value);
            }

            UnityEditor.SceneView.RepaintAll();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Determine language from nearest AutoCloset (if any)
            var lang = AutoCloset.ClosetLanguage.English;
            var closet = (_component != null) ? _component.GetComponentInParent<AutoCloset>() : null;
            if (closet != null) lang = closet.language;
            
            // Draw a simple header separation
            var labelToggles = needon.Editor.Util.ClosetLocalization.Get(_component, "ClosetConfig.Section.Toggles");
            EditorGUILayout.LabelField(labelToggles, EditorStyles.boldLabel);
            var helpToggles = needon.Editor.Util.ClosetLocalization.Get(_component, "ClosetConfig.Help.Toggles");
            EditorGUILayout.HelpBox(helpToggles, MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("toggles"), includeChildren: true);
            EditorGUILayout.Space(6);

            // Shapes section header with Preview toggle on the same line
            EditorGUILayout.BeginHorizontal();
            var labelShapes = needon.Editor.Util.ClosetLocalization.Get(_component, "ClosetConfig.Section.Shapes");
            EditorGUILayout.LabelField(labelShapes, EditorStyles.boldLabel, GUILayout.Width(EditorGUIUtility.labelWidth - 20));

            GUILayout.FlexibleSpace();

            var labelPreview = needon.Editor.Util.ClosetLocalization.Get(_component, "ClosetConfig.Preview.Enable");
            bool newPreviewEnabled = GUILayout.Toggle(_previewEnabled, labelPreview, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            if (newPreviewEnabled != _previewEnabled)
            {
                _previewEnabled = newPreviewEnabled;
                if (!_previewEnabled)
                {
                    // Restore original values when disabling preview
                    RestoreOriginalBlendshapes();
                }
                else
                {
                    // Apply all current blendshape values when enabling preview
                    ApplyAllBlendshapes();
                }
            }

            var helpShapes = needon.Editor.Util.ClosetLocalization.Get(_component, "ClosetConfig.Help.Shapes");
            EditorGUILayout.HelpBox(helpShapes, MessageType.Info);

            // Show warning when preview is enabled
            if (_previewEnabled)
            {
                var warningPreview = needon.Editor.Util.ClosetLocalization.Get(_component, "ClosetConfig.Preview.Warning");
                EditorGUILayout.HelpBox(warningPreview, MessageType.Warning);
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("shapes"), includeChildren: true);
            EditorGUILayout.Space(6);

            // Parameter Drivers section — 간단(기본)/고급 모드 병행
            var driversProp = serializedObject.FindProperty("drivers");
            var avatarRoot = needon.Editor.Util.ClosetParameterCatalog.FindAvatarRoot(_component);

            // 섹션 헤더 + 오른쪽 "고급" 토글 (EditorPrefs로 상태 유지)
            EditorGUILayout.BeginHorizontal();
            var labelDrivers = needon.Editor.Util.ClosetLocalization.Get(_component, "ClosetConfig.Section.Drivers");
            EditorGUILayout.LabelField(labelDrivers, EditorStyles.boldLabel, GUILayout.Width(EditorGUIUtility.labelWidth - 20));
            GUILayout.FlexibleSpace();
            bool advanced = EditorPrefs.GetBool(DriversAdvancedPrefKey, false);
            bool newAdvanced = GUILayout.Toggle(advanced, Loc("ClosetConfig.Drivers.Advanced"), GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
            if (newAdvanced != advanced)
            {
                EditorPrefs.SetBool(DriversAdvancedPrefKey, newAdvanced);
                advanced = newAdvanced;
            }

            var helpDrivers = needon.Editor.Util.ClosetLocalization.Get(_component, "ClosetConfig.Help.Drivers");
            EditorGUILayout.HelpBox(helpDrivers, MessageType.Info);

            if (advanced)
            {
                // 고급 모드: 기존 Drawer가 각 항목을 렌더
                EditorGUILayout.PropertyField(driversProp, includeChildren: true);
            }
            else
            {
                // 간단 모드: 문장형 행 리스트 + [+ 동작 추가]
                DrawSimpleDrivers(driversProp, avatarRoot);
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ===== 간단 모드 (Parameter Drivers) =====

        private string Loc(string key) => needon.Editor.Util.ClosetLocalization.Get(_component, key);

        private void DrawSimpleDrivers(SerializedProperty driversProp, Transform avatarRoot)
        {
            int removeIndex = -1;
            for (int i = 0; i < driversProp.arraySize; i++)
            {
                var el = driversProp.GetArrayElementAtIndex(i);
                if (DrawSimpleDriverRow(el, avatarRoot))
                    removeIndex = i; // 삭제는 루프 후에 한 번만 (이터레이터 무효화 방지)
            }

            if (removeIndex >= 0)
                driversProp.DeleteArrayElementAtIndex(removeIndex);

            if (driversProp.arraySize == 0)
                EditorGUILayout.LabelField(Loc("ClosetConfig.Drivers.Empty"), EditorStyles.miniLabel);

            EditorGUILayout.Space(2);
            if (GUILayout.Button(Loc("ClosetConfig.Drivers.AddAction")))
                ShowAddDriverMenu();
        }

        // 항목 하나를 문장형 한 줄로 렌더. 삭제(−) 눌리면 true 반환.
        private bool DrawSimpleDriverRow(SerializedProperty el, Transform avatarRoot)
        {
            var modeProp = el.FindPropertyRelative("targetMode");
            var typeProp = el.FindPropertyRelative("type");
            if (modeProp == null || typeProp == null) return false;

            var mode = (ClosetParameterDriverItem.TargetMode)modeProp.enumValueIndex;
            var type = (ClosetParameterDriverItem.ChangeType)typeProp.enumValueIndex;

            bool remove = false;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            if (mode == ClosetParameterDriverItem.TargetMode.Parameter
                && type == ClosetParameterDriverItem.ChangeType.Set)
            {
                // "{name} = {값}" — 인라인 편집
                var nameProp = el.FindPropertyRelative("name");
                var valueProp = el.FindPropertyRelative("value");
                Rect nameRect = EditorGUILayout.GetControlRect(GUILayout.MinWidth(70));
                ClosetDriverUI.DrawParamNameField(nameRect, nameProp, avatarRoot, GUIContent.none);
                GUILayout.Label("=", GUILayout.Width(12));
                Rect valRect = EditorGUILayout.GetControlRect(GUILayout.Width(90));
                ClosetDriverUI.DrawTypeAwareValue(valRect, valueProp, avatarRoot, nameProp.stringValue, GUIContent.none);
            }
            else if (mode == ClosetParameterDriverItem.TargetMode.MenuTarget)
            {
                // 대상 오브젝트 + (Bool 대상) 켜기/끄기 팝업 또는 (Int 대상) "이 의상으로" 요약
                var targetObjectProp = el.FindPropertyRelative("targetObject");
                var menuOnProp = el.FindPropertyRelative("menuTargetOn");
                // MenuItem 없는 오브젝트 드래그 시 토글 생성 다이얼로그 제안 (Drawer와 공유 로직)
                Rect objRect = EditorGUILayout.GetControlRect(GUILayout.MinWidth(70));
                ClosetDriverUI.DrawMenuTargetObjectField(objRect, targetObjectProp, GUIContent.none, _component, avatarRoot);

                var targetObj = targetObjectProp.objectReferenceValue as GameObject;
                if (targetObj == null)
                {
                    GUILayout.Label(Loc("ClosetConfig.Drivers.NeedTarget"), EditorStyles.miniLabel, GUILayout.Width(110));
                }
                else if (ClosetDriverUI.TryResolveMenuTarget(targetObj, menuOnProp.boolValue, avatarRoot, out _, out _, out bool isInt))
                {
                    if (isInt)
                    {
                        GUILayout.Label(Loc("ClosetConfig.Drivers.SwitchSummary"), EditorStyles.miniLabel, GUILayout.Width(120));
                    }
                    else
                    {
                        string[] options = { Loc("Drawer.Toggle.On"), Loc("Drawer.Toggle.Off") };
                        int cur = menuOnProp.boolValue ? 0 : 1;
                        int nw = EditorGUILayout.Popup(cur, options, GUILayout.Width(70));
                        menuOnProp.boolValue = (nw == 0);
                    }
                }
                else
                {
                    GUILayout.Label(Loc("ClosetConfig.Drivers.InvalidTarget"), EditorStyles.miniLabel, GUILayout.Width(120));
                    // 토글이 없는 일반 오브젝트면 토글을 만들어 연결하는 버튼 제공
                    if (ClosetDriverUI.CanOfferToggleCreation(targetObj, avatarRoot)
                        && GUILayout.Button(Loc("ClosetConfig.Drivers.CreateToggle"), EditorStyles.miniButton, GUILayout.Width(70)))
                    {
                        ClosetDriverUI.CreateAndAssignToggle(targetObjectProp, _component);
                    }
                }
            }
            else
            {
                // Add/Random/Copy: 간단 모드에선 편집 불가, 안내만
                GUILayout.Label(Loc("ClosetConfig.Drivers.AdvancedRow"), EditorStyles.miniLabel);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("−", GUILayout.Width(22)))
                remove = true;
            EditorGUILayout.EndHorizontal();
            return remove;
        }

        private void ShowAddDriverMenu()
        {
            var menu = new GenericMenu();
            // ②③은 데이터상 동일(MenuTarget+Set) — 라벨만 시나리오로 구분, 추가 후 targetObject만 지정
            menu.AddItem(new GUIContent(Loc("ClosetConfig.Drivers.Action.SetParameter")), false,
                () => AddDriver(ClosetParameterDriverItem.TargetMode.Parameter, ClosetParameterDriverItem.ChangeType.Set));
            menu.AddItem(new GUIContent(Loc("ClosetConfig.Drivers.Action.ToggleOther")), false,
                () => AddDriver(ClosetParameterDriverItem.TargetMode.MenuTarget, ClosetParameterDriverItem.ChangeType.Set));
            menu.AddItem(new GUIContent(Loc("ClosetConfig.Drivers.Action.SwitchCloset")), false,
                () => AddDriver(ClosetParameterDriverItem.TargetMode.MenuTarget, ClosetParameterDriverItem.ChangeType.Set));
            menu.ShowAsContext();
        }

        // 새 드라이버를 기본값으로 추가. GenericMenu 콜백은 지연 발화하므로 serializedObject를 새로 갱신·적용한다.
        private void AddDriver(ClosetParameterDriverItem.TargetMode mode, ClosetParameterDriverItem.ChangeType type)
        {
            serializedObject.Update();
            var dp = serializedObject.FindProperty("drivers");
            int idx = dp.arraySize;
            dp.InsertArrayElementAtIndex(idx);
            var el = dp.GetArrayElementAtIndex(idx);

            // 배열 삽입은 이전 요소 값을 복제하므로 전 필드를 기본값으로 초기화
            el.FindPropertyRelative("targetMode").enumValueIndex = (int)mode;
            el.FindPropertyRelative("type").enumValueIndex = (int)type;
            el.FindPropertyRelative("name").stringValue = "";
            el.FindPropertyRelative("value").floatValue = 0f;
            el.FindPropertyRelative("valueMin").floatValue = 0f;
            el.FindPropertyRelative("valueMax").floatValue = 1f;
            el.FindPropertyRelative("chance").floatValue = 1f;
            el.FindPropertyRelative("source").stringValue = "";
            el.FindPropertyRelative("destName").stringValue = "";
            el.FindPropertyRelative("targetObject").objectReferenceValue = null;
            el.FindPropertyRelative("menuTargetOn").boolValue = true;
            el.FindPropertyRelative("autoRegister").boolValue = false;
            el.FindPropertyRelative("autoRegisterSynced").boolValue = false;

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
