#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Editor.Pass
{
    // -------- ClosetToggle 용 CustomEditor --------
    [CustomEditor(typeof(ClosetToggle))]
    [CanEditMultipleObjects]
    public class ClosetToggleEditor : UnityEditor.Editor
    {
        private ClosetToggle _component;
        private SerializedObject _toggle;

        public void OnEnable()
        {
            // target 컴포넌트를 가져와 초기화
            _component = (ClosetToggle)target;
            _toggle = new SerializedObject(target);

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
            _toggle.Update();
            serializedObject.Update();
            
            // 스크립트 필드(m_Script) 제외하고 기본 그리기
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
        }
    }


    // -------- ClosetToggleItem 용 PropertyDrawer --------
    [CustomPropertyDrawer(typeof(ClosetToggleItem))]
    public class ClosetToggleItemDrawer : PropertyDrawer
    {
        private const float PopupWidth     = 60f;
        private const float SpacingX       = 4f;
        private const float SpacingY       = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // 세로 간격만큼 위/아래 여백 주기
            position.y     += SpacingY * 0.5f;
            position.height -= SpacingY;

            EditorGUI.BeginProperty(position, label, property);
            DrawTargetAndActivePopup(position, property);
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // 원래 높이에 세로 간격 추가
            float baseHeight = EditorGUI.GetPropertyHeight(property.FindPropertyRelative("target"), label);
            return baseHeight + SpacingY;
        }

        private void DrawTargetAndActivePopup(Rect position, SerializedProperty property)
        {
            int originalIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            // target / popup 영역 분할 (가로 간격만큼 여유 추가)
            Rect targetRect = new Rect(
                position.x,
                position.y,
                position.width - PopupWidth - SpacingX,
                position.height
            );
            Rect popupRect = new Rect(
                position.x + position.width - PopupWidth,
                position.y,
                PopupWidth,
                position.height
            );

            // target 필드
            EditorGUI.PropertyField(targetRect, property.FindPropertyRelative("target"), GUIContent.none);

            // ON/OFF 팝업
            var activeProp = property.FindPropertyRelative("active");
            string[] options = { "ON", "OFF" };
            int current = activeProp.boolValue ? 0 : 1;
            int choice  = EditorGUI.Popup(popupRect, current, options);
            activeProp.boolValue = (choice == 0);

            EditorGUI.indentLevel = originalIndent;
        }
    }
}
#endif
