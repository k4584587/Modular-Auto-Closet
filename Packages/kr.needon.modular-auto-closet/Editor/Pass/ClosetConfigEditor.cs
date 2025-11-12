#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace needon.Editor.Pass
{
    [CustomEditor(typeof(ClosetConfig))]
    [CanEditMultipleObjects]
    public class ClosetConfigEditor : UnityEditor.Editor
    {
        private ClosetConfig _component;
        private static bool _previewEnabled = false;
        private static Dictionary<SkinnedMeshRenderer, Dictionary<int, float>> _originalBlendshapeValues = new Dictionary<SkinnedMeshRenderer, Dictionary<int, float>>();

        static ClosetConfigEditor()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Restore blendshapes before entering play mode
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                RestoreOriginalBlendshapes();
                _previewEnabled = false;
            }
        }

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

        public void OnDisable()
        {
            // Restore original values when inspector is closed
            RestoreOriginalBlendshapes();
        }

        private static void RestoreOriginalBlendshapes()
        {
            foreach (var meshEntry in _originalBlendshapeValues)
            {
                if (meshEntry.Key == null || meshEntry.Key.sharedMesh == null) continue;

                foreach (var shapeEntry in meshEntry.Value)
                {
                    meshEntry.Key.SetBlendShapeWeight(shapeEntry.Key, shapeEntry.Value);
                }
            }

            _originalBlendshapeValues.Clear();
            UnityEditor.SceneView.RepaintAll();
        }

        public static bool IsPreviewEnabled()
        {
            return _previewEnabled;
        }

        private static void SaveOriginalBlendshapeValue(SkinnedMeshRenderer mesh, int blendshapeIndex)
        {
            if (mesh == null || blendshapeIndex < 0) return;

            if (!_originalBlendshapeValues.ContainsKey(mesh))
                _originalBlendshapeValues[mesh] = new Dictionary<int, float>();

            if (!_originalBlendshapeValues[mesh].ContainsKey(blendshapeIndex))
                _originalBlendshapeValues[mesh][blendshapeIndex] = mesh.GetBlendShapeWeight(blendshapeIndex);
        }

        public static void ApplyBlendshapePreview(SkinnedMeshRenderer mesh, string shapeKey, float value)
        {
            if (!_previewEnabled || mesh == null || string.IsNullOrEmpty(shapeKey)) return;

            int index = mesh.sharedMesh.GetBlendShapeIndex(shapeKey);
            if (index < 0) return;

            // Save original value (only once per mesh/shape)
            SaveOriginalBlendshapeValue(mesh, index);

            // Apply preview value
            mesh.SetBlendShapeWeight(index, value);
            UnityEditor.SceneView.RepaintAll();
        }

        private void ApplyAllBlendshapes()
        {
            if (_component == null || _component.shapes == null) return;

            foreach (var shape in _component.shapes)
            {
                if (shape == null || shape.mesh == null || string.IsNullOrEmpty(shape.shapeKey)) continue;

                int index = shape.mesh.sharedMesh.GetBlendShapeIndex(shape.shapeKey);
                if (index < 0) continue;

                // Save original value (only once per mesh/shape)
                SaveOriginalBlendshapeValue(shape.mesh, index);

                // Apply the configured value
                shape.mesh.SetBlendShapeWeight(index, shape.value);
            }

            UnityEditor.SceneView.RepaintAll();
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

            // Shapes section header with Preview toggle on the same line
            EditorGUILayout.BeginHorizontal();
            var labelShapes = needon.Editor.Util.ClosetLocalization.Get(_component, "ClosetConfig.Section.Shapes");
            EditorGUILayout.LabelField(labelShapes, EditorStyles.boldLabel, GUILayout.Width(EditorGUIUtility.labelWidth - 20));

            GUILayout.FlexibleSpace();

            var labelPreview = needon.Editor.Util.ClosetLocalization.Get(_component, "ClosetConfig.Preview.Enable");
            bool newPreviewEnabled = GUILayout.Toggle(_previewEnabled, labelPreview, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            if (newPreviewEnabled != _previewEnabled)
            {
                _previewEnabled = newPreviewEnabled;
                if (!_previewEnabled)
                {
                    // Restore original values when disabling preview
                    RestoreOriginalBlendshapes();
                }
                else
                {
                    // Apply all current blendshape values when enabling preview
                    ApplyAllBlendshapes();
                }
            }

            var helpShapes = needon.Editor.Util.ClosetLocalization.Get(_component, "ClosetConfig.Help.Shapes");
            EditorGUILayout.HelpBox(helpShapes, MessageType.Info);

            // Show warning when preview is enabled
            if (_previewEnabled)
            {
                var warningPreview = needon.Editor.Util.ClosetLocalization.Get(_component, "ClosetConfig.Preview.Warning");
                EditorGUILayout.HelpBox(warningPreview, MessageType.Warning);
            }

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
