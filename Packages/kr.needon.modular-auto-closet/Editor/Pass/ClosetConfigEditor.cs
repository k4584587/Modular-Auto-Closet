#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace needon.Editor.Pass
{
    [CustomEditor(typeof(ClosetConfig))]
    [CanEditMultipleObjects]
    public class ClosetConfigEditor : UnityEditor.Editor
    {
        private ClosetConfig _component;

        public void OnEnable()
        {
            _component = (ClosetConfig)target;

            // Ensure arrays are non-null for clean drawing
            if (_component.toggles == null) _component.toggles = new ClosetToggleItem[0];
            if (_component.shapes  == null) _component.shapes  = new ClosetBlendshapeItem[0];
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

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Determine language from nearest AutoCloset (if any)
            var lang = AutoCloset.ClosetLanguage.English;
            var closet = (_component != null) ? _component.GetComponentInParent<AutoCloset>() : null;
            if (closet != null) lang = closet.language;

            // Draw a simple header separation
            string labelToggles = lang switch
            {
                AutoCloset.ClosetLanguage.Korean   => "오브젝트 토글",
                AutoCloset.ClosetLanguage.Japanese => "オブジェクト トグル",
                _                                  => "Object Toggles"
            };
            EditorGUILayout.LabelField(labelToggles, EditorStyles.boldLabel);
            string helpToggles = lang switch
            {
                AutoCloset.ClosetLanguage.Korean   => "이 옷을 토글할 때 켜거나 끌 오브젝트 목록입니다.",
                AutoCloset.ClosetLanguage.Japanese => "この衣装をトグルした際にON/OFFするオブジェクトです。",
                _                                  => "Turn objects ON/OFF when this clothing is toggled."
            };
            EditorGUILayout.HelpBox(helpToggles, MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("toggles"), includeChildren: true);
            EditorGUILayout.Space(6);
            string labelShapes = lang switch
            {
                AutoCloset.ClosetLanguage.Korean   => "블렌드셰이프",
                AutoCloset.ClosetLanguage.Japanese => "ブレンドシェイプ",
                _                                  => "Blendshapes"
            };
            EditorGUILayout.LabelField(labelShapes, EditorStyles.boldLabel);
            string helpShapes = lang switch
            {
                AutoCloset.ClosetLanguage.Korean   => "이 옷을 토글할 때 적용할 블렌드셰이프 값입니다.",
                AutoCloset.ClosetLanguage.Japanese => "この衣装をトグルした際に適用するブレンドシェイプ値です。",
                _                                  => "Apply these blendshape values when this clothing is toggled."
            };
            EditorGUILayout.HelpBox(helpShapes, MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shapes"), includeChildren: true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
