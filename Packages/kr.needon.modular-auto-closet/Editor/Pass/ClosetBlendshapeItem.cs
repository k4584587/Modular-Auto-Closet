#if UNITY_EDITOR
using System;
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
        private const float DropdownWidth = 100f;
        private const float ValueWidth    = 50f;
        private const float SpacingX      = 4f;
        private const float SpacingY      = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            position.y += SpacingY * 0.5f;
            position.height -= SpacingY;

            EditorGUI.BeginProperty(position, label, property);
            DrawFields(position, property);
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float baseHeight = EditorGUI.GetPropertyHeight(
                property.FindPropertyRelative("mesh"), label
            );
            return baseHeight + SpacingY;
        }

        private void DrawFields(Rect position, SerializedProperty property)
        {
            int originalIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var meshProp     = property.FindPropertyRelative("mesh");
            var shapeKeyProp = property.FindPropertyRelative("shapeKey");
            var valueProp    = property.FindPropertyRelative("value");

            if (meshProp == null || shapeKeyProp == null || valueProp == null)
            {
                EditorGUI.LabelField(position, "ClosetBlendshapeItem 필드 오류");
                return;
            }

            Rect meshRect = new Rect(
                position.x,
                position.y,
                position.width - DropdownWidth - ValueWidth - SpacingX * 2,
                position.height
            );
            Rect dropdownRect = new Rect(
                position.x + position.width - DropdownWidth - ValueWidth - SpacingX,
                position.y,
                DropdownWidth,
                position.height
            );
            Rect valueRect = new Rect(
                position.x + position.width - ValueWidth,
                position.y,
                ValueWidth,
                position.height
            );

            // SkinnedMeshRenderer 필드: 타입 라벨 없이 오브젝트만 표시
            var currentObj = meshProp.objectReferenceValue as SkinnedMeshRenderer;
            var newObj = (SkinnedMeshRenderer)EditorGUI.ObjectField(
                meshRect,
                GUIContent.none,
                currentObj,
                typeof(SkinnedMeshRenderer),
                true
            );
            meshProp.objectReferenceValue = newObj;

            // BlendShape 키 목록 구성
            SkinnedMeshRenderer smr = newObj;
            string[] options = { "(none)" };
            int current = 0;
            if (smr != null && smr.sharedMesh != null)
            {
                Mesh mesh = smr.sharedMesh;
                int count = mesh.blendShapeCount;
                options = new string[count + 1];
                options[0] = "(none)";
                for (int i = 0; i < count; i++)
                {
                    options[i + 1] = mesh.GetBlendShapeName(i);
                }
                string currentKey = shapeKeyProp.stringValue;
                current = Array.IndexOf(options, currentKey);
                if (current < 0) current = 0;
            }

            int choice = EditorGUI.Popup(dropdownRect, current, options);
            shapeKeyProp.stringValue = (choice > 0) ? options[choice] : string.Empty;

            // 값 필드
            valueProp.floatValue = EditorGUI.FloatField(valueRect, GUIContent.none, valueProp.floatValue);

            EditorGUI.indentLevel = originalIndent;
        }
    }
}
#endif
