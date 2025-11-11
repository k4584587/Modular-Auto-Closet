#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace needon.Editor.Pass
{
    /// <summary>
    /// ClosetParameterDriverItem 용 PropertyDrawer
    /// 타입에 따라 다른 필드를 표시합니다.
    /// </summary>
    [CustomPropertyDrawer(typeof(ClosetParameterDriverItem))]
    public class ClosetParameterDriverItemDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // 박스 스타일로 감싸기
            GUI.Box(position, GUIContent.none, GUI.skin.box);

            // 내부 패딩
            float padding = 4f;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float y = position.y + padding;
            float fieldSpacing = 2f;

            // Properties
            var typeProp = property.FindPropertyRelative("type");
            var nameProp = property.FindPropertyRelative("name");
            var valueProp = property.FindPropertyRelative("value");
            var valueMinProp = property.FindPropertyRelative("valueMin");
            var valueMaxProp = property.FindPropertyRelative("valueMax");
            var chanceProp = property.FindPropertyRelative("chance");
            var sourceProp = property.FindPropertyRelative("source");
            var destNameProp = property.FindPropertyRelative("destName");
            var ctx = property.serializedObject?.targetObject as UnityEngine.Object;

            if (typeProp == null || nameProp == null)
            {
                var err = needon.Editor.Util.ClosetLocalization.Get(ctx, "Error.ParameterDriver.FieldsMissing");
                EditorGUI.HelpBox(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight * 2), err, MessageType.Error);
                EditorGUI.EndProperty();
                return;
            }

            // Type 드롭다운
            var labelType = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.Type");
            EditorGUI.PropertyField(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), typeProp, new GUIContent(labelType));
            y += lineHeight + fieldSpacing;

            var changeType = (ClosetParameterDriverItem.ChangeType)typeProp.enumValueIndex;

            // 타입에 따라 다른 필드 표시
            switch (changeType)
            {
                case ClosetParameterDriverItem.ChangeType.Set:
                    DrawSetFields(position, padding, lineHeight, fieldSpacing, ref y, nameProp, valueProp, ctx);
                    break;
                case ClosetParameterDriverItem.ChangeType.Add:
                    DrawAddFields(position, padding, lineHeight, fieldSpacing, ref y, nameProp, valueProp, chanceProp, ctx);
                    break;
                case ClosetParameterDriverItem.ChangeType.Random:
                    DrawRandomFields(position, padding, lineHeight, fieldSpacing, ref y, nameProp, valueMinProp, valueMaxProp, chanceProp, ctx);
                    break;
                case ClosetParameterDriverItem.ChangeType.Copy:
                    DrawCopyFields(position, padding, lineHeight, fieldSpacing, ref y, sourceProp, destNameProp, chanceProp, ctx);
                    break;
            }

            EditorGUI.EndProperty();
        }

        private void DrawSetFields(Rect position, float padding, float lineHeight, float fieldSpacing, ref float y,
            SerializedProperty nameProp, SerializedProperty valueProp, UnityEngine.Object ctx)
        {
            var labelName = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.ParameterName");
            EditorGUI.PropertyField(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), nameProp, new GUIContent(labelName));
            y += lineHeight + fieldSpacing;

            var labelValue = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.Common.Value");
            EditorGUI.PropertyField(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), valueProp, new GUIContent(labelValue));
            y += lineHeight + fieldSpacing;
        }

        private void DrawAddFields(Rect position, float padding, float lineHeight, float fieldSpacing, ref float y,
            SerializedProperty nameProp, SerializedProperty valueProp, SerializedProperty chanceProp, UnityEngine.Object ctx)
        {
            var labelName = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.ParameterName");
            EditorGUI.PropertyField(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), nameProp, new GUIContent(labelName));
            y += lineHeight + fieldSpacing;

            var labelValue = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.Common.Value");
            EditorGUI.PropertyField(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), valueProp, new GUIContent(labelValue));
            y += lineHeight + fieldSpacing;

            var labelChance = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.Chance");
            EditorGUI.Slider(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), chanceProp, 0f, 1f, new GUIContent(labelChance));
            y += lineHeight + fieldSpacing;
        }

        private void DrawRandomFields(Rect position, float padding, float lineHeight, float fieldSpacing, ref float y,
            SerializedProperty nameProp, SerializedProperty valueMinProp, SerializedProperty valueMaxProp, SerializedProperty chanceProp, UnityEngine.Object ctx)
        {
            var labelName = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.ParameterName");
            EditorGUI.PropertyField(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), nameProp, new GUIContent(labelName));
            y += lineHeight + fieldSpacing;

            var labelMin = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.ValueMin");
            EditorGUI.PropertyField(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), valueMinProp, new GUIContent(labelMin));
            y += lineHeight + fieldSpacing;

            var labelMax = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.ValueMax");
            EditorGUI.PropertyField(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), valueMaxProp, new GUIContent(labelMax));
            y += lineHeight + fieldSpacing;

            var labelChance = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.Chance");
            EditorGUI.Slider(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), chanceProp, 0f, 1f, new GUIContent(labelChance));
            y += lineHeight + fieldSpacing;
        }

        private void DrawCopyFields(Rect position, float padding, float lineHeight, float fieldSpacing, ref float y,
            SerializedProperty sourceProp, SerializedProperty destNameProp, SerializedProperty chanceProp, UnityEngine.Object ctx)
        {
            var labelSource = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.Source");
            EditorGUI.PropertyField(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), sourceProp, new GUIContent(labelSource));
            y += lineHeight + fieldSpacing;

            var labelDest = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.Destination");
            EditorGUI.PropertyField(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), destNameProp, new GUIContent(labelDest));
            y += lineHeight + fieldSpacing;

            var labelChance = needon.Editor.Util.ClosetLocalization.Get(ctx, "Drawer.ParameterDriver.Chance");
            EditorGUI.Slider(new Rect(position.x + padding, y, position.width - 2 * padding, lineHeight), chanceProp, 0f, 1f, new GUIContent(labelChance));
            y += lineHeight + fieldSpacing;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var typeProp = property.FindPropertyRelative("type");
            if (typeProp == null) return EditorGUIUtility.singleLineHeight * 3;

            var changeType = (ClosetParameterDriverItem.ChangeType)typeProp.enumValueIndex;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float fieldSpacing = 2f;
            float padding = 4f;

            int lineCount = 1; // Type 필드

            switch (changeType)
            {
                case ClosetParameterDriverItem.ChangeType.Set:
                    lineCount += 2; // name, value
                    break;
                case ClosetParameterDriverItem.ChangeType.Add:
                    lineCount += 3; // name, value, chance
                    break;
                case ClosetParameterDriverItem.ChangeType.Random:
                    lineCount += 4; // name, valueMin, valueMax, chance
                    break;
                case ClosetParameterDriverItem.ChangeType.Copy:
                    lineCount += 3; // source, destName, chance
                    break;
            }

            return padding + (lineHeight + fieldSpacing) * lineCount + padding;
        }
    }
}
#endif
