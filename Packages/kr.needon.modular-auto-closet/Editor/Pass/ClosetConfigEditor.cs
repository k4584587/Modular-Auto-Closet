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
            if (_component.drivers == null) _component.drivers = new ClosetParameterDriverItem[0];
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
            var labelToggles = needon.Editor.Util.ClosetLocalization.Get(_component, "ClosetConfig.Section.Toggles");
            EditorGUILayout.LabelField(labelToggles, EditorStyles.boldLabel);
            var helpToggles = needon.Editor.Util.ClosetLocalization.Get(_component, "ClosetConfig.Help.Toggles");
            EditorGUILayout.HelpBox(helpToggles, MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("toggles"), includeChildren: true);
            EditorGUILayout.Space(6);
            var labelShapes = needon.Editor.Util.ClosetLocalization.Get(_component, "ClosetConfig.Section.Shapes");
            EditorGUILayout.LabelField(labelShapes, EditorStyles.boldLabel);
            var helpShapes = needon.Editor.Util.ClosetLocalization.Get(_component, "ClosetConfig.Help.Shapes");
            EditorGUILayout.HelpBox(helpShapes, MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shapes"), includeChildren: true);
            EditorGUILayout.Space(6);

            // Parameter Drivers section
            var labelDrivers = needon.Editor.Util.ClosetLocalization.Get(_component, "ClosetConfig.Section.Drivers");
            EditorGUILayout.LabelField(labelDrivers, EditorStyles.boldLabel);
            var helpDrivers = needon.Editor.Util.ClosetLocalization.Get(_component, "ClosetConfig.Help.Drivers");
            EditorGUILayout.HelpBox(helpDrivers, MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("drivers"), includeChildren: true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
