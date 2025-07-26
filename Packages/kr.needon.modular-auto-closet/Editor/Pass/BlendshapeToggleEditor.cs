#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using nadena.dev.modular_avatar.core;

namespace needon.Editor.Pass
{
    [CustomEditor(typeof(BlendshapeToggle))]
    [CanEditMultipleObjects]
    public class BlendshapeToggleEditor : UnityEditor.Editor
    {
        private BlendshapeToggle _component;

        public void OnEnable()
        {
            _component = (BlendshapeToggle)target;

            if (_component.shapes == null)
            {
                _component.shapes = new BlendshapeToggleItem[0];
                EditorUtility.SetDirty(_component);
            }

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

    [CustomPropertyDrawer(typeof(BlendshapeToggleItem))]
    public class BlendshapeToggleItemDrawer : PropertyDrawer
    {
        private const float DropdownWidth = 100f;
        private const float ValueWidth    = 50f;
        private const float PopupWidth    = 60f;
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
            var activeProp   = property.FindPropertyRelative("active");

            if (meshProp == null || shapeKeyProp == null || valueProp == null || activeProp == null)
            {
                EditorGUI.LabelField(position, "BlendshapeToggleItem fields missing");
                return;
            }

            Rect meshRect = new Rect(
                position.x,
                position.y,
                position.width - DropdownWidth - ValueWidth - PopupWidth - SpacingX * 3,
                position.height
            );
            Rect dropdownRect = new Rect(
                position.x + position.width - DropdownWidth - ValueWidth - PopupWidth - SpacingX * 2,
                position.y,
                DropdownWidth,
                position.height
            );
            Rect valueRect = new Rect(
                position.x + position.width - ValueWidth - PopupWidth - SpacingX,
                position.y,
                ValueWidth,
                position.height
            );
            Rect popupRect = new Rect(
                position.x + position.width - PopupWidth,
                position.y,
                PopupWidth,
                position.height
            );

            var currentObj = meshProp.objectReferenceValue as SkinnedMeshRenderer;
            var newObj = (SkinnedMeshRenderer)EditorGUI.ObjectField(
                meshRect,
                GUIContent.none,
                currentObj,
                typeof(SkinnedMeshRenderer),
                true
            );
            meshProp.objectReferenceValue = newObj;

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

            valueProp.floatValue = EditorGUI.Slider(valueRect, GUIContent.none, valueProp.floatValue, 0f, 100f);

            string[] popupOptions = { "ON", "OFF" };
            int currentPopup = activeProp.boolValue ? 0 : 1;
            int popupChoice = EditorGUI.Popup(popupRect, currentPopup, popupOptions);
            activeProp.boolValue = (popupChoice == 0);

            EditorGUI.indentLevel = originalIndent;
        }
    }
}
#endif
