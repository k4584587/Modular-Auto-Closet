#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace needon.Editor.Pass
{
    [CustomEditor(typeof(StandaloneToggle))]
    [CanEditMultipleObjects]
    public class StandaloneToggleEditor : UnityEditor.Editor
    {
        private StandaloneToggle _component;

        public void OnEnable()
        {
            _component = (StandaloneToggle)target;
            var iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/kr.needon.modular-auto-closet/Resource/toggleON.png");
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
}
#endif
