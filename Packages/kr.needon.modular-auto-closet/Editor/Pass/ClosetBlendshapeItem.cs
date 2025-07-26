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

            if (meshProp == null || shapeKeyProp == null || valueProp == null)
            {
                EditorGUI.HelpBox(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight * 2), "ClosetBlendshapeItem 필드 오류", MessageType.Error);
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.PropertyField(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), meshProp, new GUIContent("Skinned Mesh"));
            y += lineHeight + fieldSpacing;

            // ShapeKey 드롭다운
            SkinnedMeshRenderer smr = meshProp.objectReferenceValue as SkinnedMeshRenderer;
            if (smr != null && smr.sharedMesh != null)
            {
                var names = new System.Collections.Generic.List<string> { "Please select" };
                names.AddRange(System.Linq.Enumerable.Range(0, smr.sharedMesh.blendShapeCount)
                                         .Select(idx => smr.sharedMesh.GetBlendShapeName(idx)));

                int selIndex = Mathf.Max(0, names.IndexOf(shapeKeyProp.stringValue));
                selIndex = EditorGUI.Popup(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), "Name", selIndex, names.ToArray());
                shapeKeyProp.stringValue = names[selIndex];
                y += lineHeight + fieldSpacing;

                bool selectable = shapeKeyProp.stringValue != "Please select";
                EditorGUI.BeginDisabledGroup(!selectable);
                valueProp.floatValue = selectable
                    ? EditorGUI.Slider(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), "Value", valueProp.floatValue, 0f, 100f)
                    : EditorGUI.Slider(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), "Value", 0f, 0f, 100f);
                EditorGUI.EndDisabledGroup();
                y += lineHeight + fieldSpacing;
            }
            else
            {
                EditorGUI.LabelField(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), "Name", "Please select a Skinned Mesh");
                y += lineHeight + fieldSpacing;
                shapeKeyProp.stringValue = "Please select";
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.Slider(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), "Value", 0f, 0f, 100f);
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
