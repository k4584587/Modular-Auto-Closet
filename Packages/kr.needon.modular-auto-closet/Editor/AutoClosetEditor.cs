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
        private SerializedProperty _writeDefaults;
        private SerializedProperty _resetChildMenuParameters;
        private string _version;

        // FX 전체 상태를 순회하는 WD 감지를 리페인트마다 돌리지 않도록 컨트롤러 기준으로 캐싱
        private RuntimeAnimatorController _detectedWdController;
        private bool _detectedWd;
        private bool _hasDetectedWd;

        void OnEnable()
        {
            _component = (AutoCloset)target;
            _serialObj = new SerializedObject(_component);
            _toggleRootName = _serialObj.FindProperty("toggleRootName");
            _language = _serialObj.FindProperty("language");
            _writeDefaults = _serialObj.FindProperty("writeDefaultsMode");
            _resetChildMenuParameters = _serialObj.FindProperty("resetChildMenuParameters");

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
            EditorGUILayout.LabelField(needon.Editor.Util.ClosetLocalization.Get(_component, "AutoCloset.Language"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_language, GUIContent.none);

            // Ensure within avatar
            if (_component.GetComponentInParent<VRCAvatarDescriptor>() == null)
            {
                EditorGUILayout.HelpBox(
                    needon.Editor.Util.ClosetLocalization.Get(_component, "AutoCloset.PlaceInsideAvatar"),
                    MessageType.Error);
                return;
            }

            // Settings field
            EditorGUILayout.PropertyField(
                _toggleRootName,
                new GUIContent(needon.Editor.Util.ClosetLocalization.Get(_component, "AutoCloset.ToggleRootName")));

            // Write Defaults mode
            EditorGUILayout.PropertyField(
                _writeDefaults,
                new GUIContent(
                    needon.Editor.Util.ClosetLocalization.Get(_component, "AutoCloset.WriteDefaults"),
                    needon.Editor.Util.ClosetLocalization.Get(_component, "AutoCloset.WriteDefaults.Tooltip")));

            EditorGUILayout.PropertyField(
                _resetChildMenuParameters,
                new GUIContent(
                    needon.Editor.Util.ClosetLocalization.Get(_component, "AutoCloset.ResetChildMenuParameters"),
                    needon.Editor.Util.ClosetLocalization.Get(_component, "AutoCloset.ResetChildMenuParameters.Tooltip")));

            // Auto 모드: 아바타에서 감지된 WD 값을 미리 보여줌
            if (_component.writeDefaultsMode == AutoCloset.WriteDefaultsMode.Auto)
            {
                var avatar = _component.GetComponentInParent<VRCAvatarDescriptor>();
                if (avatar != null)
                {
                    var detected = GetDetectedWriteDefaults(avatar);
                    EditorGUILayout.HelpBox(
                        string.Format(
                            needon.Editor.Util.ClosetLocalization.Get(_component, "AutoCloset.WriteDefaults.Detected"),
                            detected ? "ON" : "OFF"),
                        MessageType.Info);
                }
            }
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

        // FX 컨트롤러 참조가 바뀐 경우에만 전체 상태 다수결을 다시 계산한다.
        private bool GetDetectedWriteDefaults(VRCAvatarDescriptor avatar)
        {
            if (avatar.baseAnimationLayers == null)
                return true;

            var fxLayer = System.Array.Find(
                avatar.baseAnimationLayers,
                item => item.type == VRCAvatarDescriptor.AnimLayerType.FX);
            var controller = fxLayer.animatorController;

            if (!_hasDetectedWd || controller != _detectedWdController)
            {
                _detectedWd = needon.Editor.Pass.AutoClosetUtil.DetectAvatarWriteDefaults(avatar);
                _detectedWdController = controller;
                _hasDetectedWd = true;
            }

            return _detectedWd;
        }

        [System.Serializable]
        private class PackageInfo { public string version; }

        // Title remains English per request; other strings use localization file.
    }
}
