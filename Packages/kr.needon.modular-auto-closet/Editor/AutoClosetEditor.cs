using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace needon.Editor
{
    [CustomEditor(typeof(AutoCloset))]
    public class AutoClosetEditor : UnityEditor.Editor
    {
        private string _version;
        private AutoCloset _component;

        private VRCAvatarDescriptor _avatarDescriptor;
        private SerializedObject _autoCloset;

        public void OnEnable()
        {
            _autoCloset = new SerializedObject(target);

            _component = serializedObject.targetObject as AutoCloset;

            _avatarDescriptor = _component?.gameObject.GetComponentInParent<VRCAvatarDescriptor>(true);

            // 컴포넌트 아이콘 변경 (인스펙터 상단에 표시)
            var iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/kr.needon.modular-auto-closet/Resource/ClosetIcon.png");
            if (iconTexture != null && _component != null)
            {
                EditorGUIUtility.SetIconForObject(_component, iconTexture);
            }

            // package.json 파일에서 버전 정보를 로드
            var packageTextAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/kr.needon.modular-auto-closet/package.json");
            if (packageTextAsset != null)
            {
                var packageInfo = JsonUtility.FromJson<PackageInfo>(packageTextAsset.text);
                _version = packageInfo.version;
            }
            else
            {
                _version = "N/A";
            }
        }

        public override void OnInspectorGUI()
        {
            _autoCloset.Update();

            var logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/kr.needon.modular-auto-closet/Resource/ClosetIcon.png");
            if (logoTexture)
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
            EditorGUILayout.LabelField("by Hirami\nv" + _version, descriptionLabelStyle);

            if (!_component)
            {
                EditorGUILayout.HelpBox("?", MessageType.Error);
                return;
            }

            if (!_avatarDescriptor)
            {
                EditorGUILayout.HelpBox("This component must be placed inside the avatar object!", MessageType.Error);
                return;
            }

            EditorGUILayout.PropertyField(_autoCloset.FindProperty("toggleRootName"), new GUIContent("Toggle Root Name"));

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

    [System.Serializable]
    public class PackageInfo
    {
        public string version;
    }
    
}
