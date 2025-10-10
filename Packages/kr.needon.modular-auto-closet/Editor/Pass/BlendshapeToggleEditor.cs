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
                "Packages/kr.needon.modular-auto-closet/Resource/toggleON.png"
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
        private const float LineHeight = 18f;
        private const float SpacingY = 2f;
        private const float LabelWidth = 95f;
        private const float LabelSpacing = 6f;
        private const float ActiveWidth = 60f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var meshProp = property.FindPropertyRelative("mesh");
            var shapeKeyProp = property.FindPropertyRelative("shapeKey");
            var valueProp = property.FindPropertyRelative("value");
            var activeProp = property.FindPropertyRelative("active");
            var ctx = property.serializedObject?.targetObject as UnityEngine.Object;

            if (meshProp == null || shapeKeyProp == null || valueProp == null || activeProp == null)
            {
                var msg = needon.Editor.Util.ClosetLocalization.Get(ctx, "Error.BlendshapeToggleItem.FieldsMissing");
                EditorGUI.LabelField(position, msg);
                EditorGUI.EndProperty();
                return;
            }

            int originalIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            float currentY = position.y;

            // 첫 번째 줄: 스키닝 메쉬 라벨 + 필드 + 개짐 드롭다운
            string meshLabel = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.Common.SkinnedMesh") ?? "스키니드 메쉬";
            Rect meshLabelRect = new Rect(position.x, currentY, LabelWidth - LabelSpacing, LineHeight);
            Rect meshFieldRect = new Rect(position.x + LabelWidth, currentY, position.width - LabelWidth - ActiveWidth - 2f, LineHeight);
            Rect activeFieldRect = new Rect(position.x + position.width - ActiveWidth, currentY, ActiveWidth, LineHeight);

            EditorGUI.LabelField(meshLabelRect, meshLabel);

            var currentObj = meshProp.objectReferenceValue as SkinnedMeshRenderer;
            var newObj = (SkinnedMeshRenderer)EditorGUI.ObjectField(
                meshFieldRect,
                GUIContent.none,
                currentObj,
                typeof(SkinnedMeshRenderer),
                true
            );
            meshProp.objectReferenceValue = newObj;

            // ON/OFF 드롭다운
            string[] activeOptions = {
                needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.Toggle.On") ?? "켜짐",
                needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.Toggle.Off") ?? "꺼짐"
            };
            int currentActive = activeProp.boolValue ? 0 : 1;
            int activeChoice = EditorGUI.Popup(activeFieldRect, currentActive, activeOptions);
            activeProp.boolValue = (activeChoice == 0);

            currentY += LineHeight + SpacingY;

            // 두 번째 줄: 이름 라벨 + 드롭다운
            string nameLabel = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.Common.Name") ?? "이름";
            Rect nameLabelRect = new Rect(position.x, currentY, LabelWidth - LabelSpacing, LineHeight);
            Rect nameFieldRect = new Rect(position.x + LabelWidth, currentY, position.width - LabelWidth, LineHeight);

            EditorGUI.LabelField(nameLabelRect, nameLabel);

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

            int choice = EditorGUI.Popup(nameFieldRect, current, options);
            shapeKeyProp.stringValue = (choice > 0) ? options[choice] : string.Empty;

            currentY += LineHeight + SpacingY;

            // 세 번째 줄: 값 라벨 + 슬라이더 + 입력 필드
            string valueLabel = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.Common.Value") ?? "값";
            Rect valueLabelRect = new Rect(position.x, currentY, LabelWidth - LabelSpacing, LineHeight);
            Rect valueFieldRect = new Rect(position.x + LabelWidth, currentY, position.width - LabelWidth - 50f, LineHeight);
            Rect valueNumberRect = new Rect(position.x + position.width - 45f, currentY, 45f, LineHeight);

            EditorGUI.LabelField(valueLabelRect, valueLabel);

            valueProp.floatValue = GUI.HorizontalSlider(valueFieldRect, valueProp.floatValue, 0f, 100f);

            // 소수점 한 자리로 반올림하여 표시
            float roundedValue = Mathf.Round(valueProp.floatValue * 10f) / 10f;
            float newValue = EditorGUI.FloatField(valueNumberRect, roundedValue);
            valueProp.floatValue = Mathf.Clamp(newValue, 0f, 100f);

            EditorGUI.indentLevel = originalIndent;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return LineHeight * 3 + SpacingY * 2;
        }
    }
}
#endif
