#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace needon.Editor.Pass
{
    [CustomEditor(typeof(AutoClosetObjectToggle))]
    [CanEditMultipleObjects]
    public class ToggleCreatorPass : UnityEditor.Editor
    {

        private AutoClosetObjectToggle _component;

        public void OnEnable()
        {
            // target 컴포넌트를 가져와 초기화
            _component = (AutoClosetObjectToggle)target;

            // 아이콘 로드 후 설정
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

            EditorGUILayout.HelpBox(
                needon.Editor.Util.ClosetLocalization.Get(_component, "ObjectToggle.Help.Targets"),
                MessageType.Info);

            // 각 대상은 ClosetToggleItemDrawer가 "대상 + ON/OFF"로 그린다.
            var targetsProp = serializedObject.FindProperty("targets");
            var wasEmpty = targetsProp.arraySize == 0;
            EditorGUILayout.PropertyField(targetsProp, new GUIContent("Targets"), true);

            // Unity는 빈 목록에 추가한 요소를 필드 초기값(true)이 아니라 false로 채우므로 기본을 ON으로 맞춘다.
            // (이미 항목이 있으면 Unity가 앞 항목을 복제하므로 그 값을 그대로 따른다)
            if (wasEmpty)
            {
                for (int i = 0; i < targetsProp.arraySize; i++)
                    targetsProp.GetArrayElementAtIndex(i).FindPropertyRelative("active").boolValue = true;
            }

            serializedObject.ApplyModifiedProperties();
        }

    }
}
#endif