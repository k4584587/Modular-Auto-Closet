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
        private SerializedObject _toggle;

        public void OnEnable()
        {
            // target 컴포넌트를 가져와 초기화
            _component = (AutoClosetObjectToggle)target;
            _toggle = new SerializedObject(target);
            
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
            _toggle.Update();
            serializedObject.Update();

            var targetsProp = serializedObject.FindProperty("targets");
            EditorGUILayout.PropertyField(targetsProp, new GUIContent("Targets"), true);

            // 배열 요소마다 active 프로퍼티를 강제로 true 로 세팅 후 그리기에서 제외
            for (int i = 0; i < targetsProp.arraySize; i++)
            {
                var element = targetsProp.GetArrayElementAtIndex(i);
                var activeProp = element.FindPropertyRelative("active");
                activeProp.boolValue = true;
            }

            serializedObject.ApplyModifiedProperties();
        }

    }
}
#endif