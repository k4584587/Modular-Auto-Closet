#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using nadena.dev.modular_avatar.core;

namespace needon.Editor.Pass
{
    // -------- ClosetBlendshape 용 CustomEditor --------
    [CustomEditor(typeof(ClosetBlendshape))]
    [CanEditMultipleObjects]
    public class ClosetBlendshapeEditor : UnityEditor.Editor
    {
        private ClosetBlendshape _component;

        public void OnEnable()
        {
            _component = (ClosetBlendshape)target;

            // 배열이 null일 경우 빈 배열 할당
            if (_component.shapes == null)
            {
                _component.shapes = new ClosetBlendshapeItem[0];
                EditorUtility.SetDirty(_component);
            }

            // 아이콘 로드 후 설정
            var iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/kr.needon.modular-auto-closet/Resource/ClosetIcon.png"
            );
            if (iconTexture != null)
            {
                EditorGUIUtility.SetIconForObject(_component, iconTexture);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
        }
    }

    // -------- ClosetBlendshapeItem 용 PropertyDrawer --------
    [CustomPropertyDrawer(typeof(ClosetBlendshapeItem))]
    public class ClosetBlendshapeItemDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // 박스 스타일로 감싸기 (Rect 직접 계산)
            var boxRect = new Rect(position.x, position.y, position.width, position.height);
            GUI.Box(boxRect, GUIContent.none, GUI.skin.box);

            // 내부 패딩
            float padding = 4f;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float y = position.y + padding;
            float fieldSpacing = 2f;

            // Skinned Mesh 필드
            var meshProp = property.FindPropertyRelative("mesh");
            var shapeKeyProp = property.FindPropertyRelative("shapeKey");
            var valueProp = property.FindPropertyRelative("value");
            var ctx = property.serializedObject?.targetObject as UnityEngine.Object;

            if (meshProp == null || shapeKeyProp == null || valueProp == null)
            {
                var err = needon.Editor.Util.ClosetLocalization.Get(ctx, "Error.BlendshapeItem.FieldsMissing");
                EditorGUI.HelpBox(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight * 2), err, MessageType.Error);
                EditorGUI.EndProperty();
                return;
            }

            var labelSkinned = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.Common.SkinnedMesh");
            EditorGUI.PropertyField(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), meshProp, new GUIContent(labelSkinned));
            y += lineHeight + fieldSpacing;

            // ShapeKey 드롭다운
            SkinnedMeshRenderer smr = meshProp.objectReferenceValue as SkinnedMeshRenderer;
            if (smr != null && smr.sharedMesh != null)
            {
                var noneText = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.Common.None");
                var names = new System.Collections.Generic.List<string> { noneText };
                names.AddRange(System.Linq.Enumerable.Range(0, smr.sharedMesh.blendShapeCount)
                                         .Select(idx => smr.sharedMesh.GetBlendShapeName(idx)));

                // current selection by actual key (empty means none)
                int selIndex = 0;
                if (!string.IsNullOrEmpty(shapeKeyProp.stringValue))
                {
                    var idx = names.IndexOf(shapeKeyProp.stringValue);
                    selIndex = Mathf.Max(0, idx);
                }

                var labelName = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.Common.Name");
                selIndex = EditorGUI.Popup(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), labelName, selIndex, names.ToArray());
                shapeKeyProp.stringValue = (selIndex == 0) ? string.Empty : names[selIndex];
                y += lineHeight + fieldSpacing;

                bool selectable = !string.IsNullOrEmpty(shapeKeyProp.stringValue);
                var labelValue = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.Common.Value");
                EditorGUI.BeginDisabledGroup(!selectable);

                EditorGUI.BeginChangeCheck();
                float newValue = selectable
                    ? EditorGUI.Slider(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), labelValue, valueProp.floatValue, 0f, 100f)
                    : EditorGUI.Slider(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), labelValue, 0f, 0f, 100f);

                if (EditorGUI.EndChangeCheck() && selectable)
                {
                    valueProp.floatValue = newValue;
                    // Apply preview immediately when slider changes
                    ClosetConfigEditor.ApplyBlendshapePreview(smr, shapeKeyProp.stringValue, newValue);
                }

                EditorGUI.EndDisabledGroup();
                y += lineHeight + fieldSpacing;
            }
            else
            {
                var labelName = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.Common.Name");
                var labelSelectSMR = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.Blendshape.SelectSkinnedMesh");
                EditorGUI.LabelField(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), labelName, labelSelectSMR);
                y += lineHeight + fieldSpacing;
                shapeKeyProp.stringValue = string.Empty;
                EditorGUI.BeginDisabledGroup(true);
                var labelValue = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.Common.Value");
                EditorGUI.Slider(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), labelValue, 0f, 0f, 100f);
                EditorGUI.EndDisabledGroup();
                y += lineHeight + fieldSpacing;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Skinned Mesh + Name + Value + 패딩/스페이싱
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float fieldSpacing = 2f;
            float padding = 4f;
            // 항상 3줄 (Skinned Mesh, Name, Value)
            return padding + (lineHeight + fieldSpacing) * 3 + padding;
        }
    }
}
#endif
