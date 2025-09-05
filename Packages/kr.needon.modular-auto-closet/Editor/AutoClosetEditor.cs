using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace needon.Editor
{
    [CustomEditor(typeof(AutoCloset))]
    public class AutoClosetEditor : UnityEditor.Editor
    {
        private AutoCloset _component;
        private SerializedObject _serialObj;
        private SerializedProperty _toggleRootName;
        private SerializedProperty _language;
        private string _version;

        void OnEnable()
        {
            _component = (AutoCloset)target;
            _serialObj = new SerializedObject(_component);
            _toggleRootName = _serialObj.FindProperty("toggleRootName");
            _language = _serialObj.FindProperty("language");

            // Load version from package.json
            var pkg = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Packages/kr.needon.modular-auto-closet/package.json");
            _version = pkg != null
                ? JsonUtility.FromJson<PackageInfo>(pkg.text).version
                : "N/A";

            // Set custom icon
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/kr.needon.modular-auto-closet/Resource/ClosetIcon.png");
            if (icon != null)
                EditorGUIUtility.SetIconForObject(_component, icon);
        }

        public override void OnInspectorGUI()
        {
            _serialObj.Update();

            // Logo
            var logo = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/kr.needon.modular-auto-closet/Resource/ClosetIcon.png");
            if (logo)
            {
                GUILayout.Space(8);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(logo, GUILayout.Width(128), GUILayout.Height(128));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(4);
            }

            // Title
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18
            };
            EditorGUILayout.LabelField("Auto Closet Tool", titleStyle);

            // Creator name under title
            var nameStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("by Hirami", nameStyle);

            // Version below name
            var versionStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField($"v{_version}", versionStyle);
            GUILayout.Space(6);

            // Language selector
            GUILayout.Space(6);
            EditorGUILayout.LabelField(T("Language", "언어", "言語"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_language, GUIContent.none);

            // Ensure within avatar
            if (_component.GetComponentInParent<VRCAvatarDescriptor>() == null)
            {
                EditorGUILayout.HelpBox(
                    T("Place this inside the avatar object.", "아바타 오브젝트 내부에 배치해 주세요.", "アバター内に配置してください。"),
                    MessageType.Error);
                return;
            }

            // Settings field
            EditorGUILayout.PropertyField(
                _toggleRootName,
                new GUIContent(T("Toggle Root Name", "토글 루트 이름", "トグル ルート名")));
            GUILayout.Space(6);

            // Footer link
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(
                "Inspired by Kamyu HipsGrab Tool",
                EditorStyles.linkLabel,
                GUILayout.Height(18)))
            {
                Application.OpenURL(
                    "https://kamyu1537.booth.pm/items/3910550");
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            _serialObj.ApplyModifiedProperties();
        }

        [System.Serializable]
        private class PackageInfo { public string version; }

        private string T(string en, string ko, string ja)
        {
            var lang = (AutoCloset.ClosetLanguage)_language.enumValueIndex;
            switch (lang)
            {
                case AutoCloset.ClosetLanguage.Korean:   return ko;
                case AutoCloset.ClosetLanguage.Japanese: return ja;
                default:                                  return en;
            }
        }
    }
}
