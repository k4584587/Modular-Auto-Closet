using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace needon.Editor
{
    [CustomEditor(typeof(AutoCloset))]
    public class AutoClosetEditor : UnityEditor.Editor
    {
        private const string Version = "1.0.0";
        private AutoCloset _component;

        private VRCAvatarDescriptor _avatarDescriptor;
        private SerializedObject _autoCloset;

        public void OnEnable()
        {
            _autoCloset = new SerializedObject(target);

            _component = serializedObject.targetObject as AutoCloset;

            _avatarDescriptor = _component?.gameObject.GetComponentInParent<VRCAvatarDescriptor>(true);
        }

        public override void OnInspectorGUI()
        {
            _autoCloset.Update();

            var logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/kr.needon.modular-auto-closet/Resource/ClosetIcon.png");
            if (logoTexture != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(logoTexture, GUILayout.Width(128), GUILayout.Height(128));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            #region Styles

            var titleLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 20,
            };

            var descriptionLabelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };

            // 하단에 표시할 문구 스타일
            var referenceLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow
            };

            #endregion

            const string title = "Auto Closet Tool";
            var height = titleLabelStyle.CalcHeight(new GUIContent(title), EditorGUIUtility.currentViewWidth);
            var titleRect = EditorGUILayout.GetControlRect(GUILayout.Height(height));
            EditorGUI.LabelField(titleRect, title, titleLabelStyle);
            EditorGUILayout.LabelField("by Hirami\nv" + Version, descriptionLabelStyle);

            if (_component == null)
            {
                EditorGUILayout.HelpBox("?", MessageType.Error);
                return;
            }


            if (_avatarDescriptor == null)
            {
                EditorGUILayout.HelpBox("This component must be placed inside the avatar object!", MessageType.Error);
                return;
            }


            _autoCloset.ApplyModifiedProperties();

            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var linkRect = EditorGUILayout.GetControlRect(GUILayout.MaxWidth(EditorGUIUtility.currentViewWidth - 100));
            EditorGUIUtility.AddCursorRect(linkRect, MouseCursor.Link);

            EditorGUI.LabelField(
                linkRect,
                "This tool was inspired by Kamyu HipsGrab Tool",
                referenceLabelStyle
            );


            if (Event.current.type == EventType.MouseUp && linkRect.Contains(Event.current.mousePosition) && Event.current.button == 0)
            {
                Application.OpenURL("https://kamyu1537.booth.pm/items/3910550");
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);
        }
    }
}