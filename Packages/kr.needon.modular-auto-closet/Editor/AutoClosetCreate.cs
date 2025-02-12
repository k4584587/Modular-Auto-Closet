using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace needon.Editor
{
    public abstract class AutoClosetCreate : UnityEditor.Editor
    {
        private const string ContextMenuPath = "GameObject/Hirami/Auto Apply Closet";
        private const int ContextMenuPriority = 0;

        [MenuItem(ContextMenuPath, true, ContextMenuPriority)]
        public static bool ValidateApplyToAvatar() => Selection.gameObjects.Any(ValidateCore);

        [MenuItem(ContextMenuPath, false, ContextMenuPriority)]
        public static void ApplyToAvatar()
        {
            foreach (var selectedObject in Selection.gameObjects)
            {
                if (!ValidateCore(selectedObject))
                    continue;

                Texture2D componentIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.hirami.needon.modular-auto-closet/Resource/ClosetIcon.png");
                ApplyComponents(selectedObject, componentIcon);
                ApplyToChildren(selectedObject);
            }
        }

        private static bool ValidateCore(GameObject obj) => obj != null && obj.GetComponentInChildren<AutoCloset>() == null;

        private static void ApplyComponents(GameObject targetObject, Texture2D icon = null)
        {
            // 필요한 컴포넌트 추가 (중복 방지)
            if (targetObject.GetComponent<AutoCloset>() == null)
                targetObject.AddComponent<AutoCloset>();

            if (targetObject.GetComponent<ModularAvatarMenuInstaller>() == null)
                targetObject.AddComponent<ModularAvatarMenuInstaller>();

            var maParameters = targetObject.GetComponent<ModularAvatarParameters>() ?? targetObject.AddComponent<ModularAvatarParameters>();
            var maMenuItem = targetObject.GetComponent<ModularAvatarMenuItem>() ?? targetObject.AddComponent<ModularAvatarMenuItem>();

            // Int 파라미터 생성
            if (maParameters.parameters.All(p => p.nameOrPrefix != "AutoCloset"))
            {
                maParameters.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = "AutoCloset",
                    syncType = ParameterSyncType.Int,
                    defaultValue = 0,
                    saved = true
                });
            }

            maMenuItem.Control ??= new VRCExpressionsMenu.Control();
            maMenuItem.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            maMenuItem.MenuSource = SubmenuSource.Children;
            maMenuItem.Control.icon = icon; // 메뉴 아이콘 설정
        }

        private static void ApplyToChildren(GameObject parentObject)
        {
            // 모든 직계 자식 오브젝트들의 리스트를 가져옴
            var children = new List<Transform>();
            foreach (Transform child in parentObject.transform)
            {
                children.Add(child);
            }

            // 씬에서의 순서대로 정렬 (위에서 아래로)
            children = children.OrderBy(t => t.GetSiblingIndex()).ToList();

            // 순차적으로 value 값 할당
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child.GetComponent<ModularAvatarMenuItem>() == null)
                {
                    var childMenuItem = child.gameObject.AddComponent<ModularAvatarMenuItem>();

                    // MenuItem 설정
                    childMenuItem.Control ??= new VRCExpressionsMenu.Control();
                    childMenuItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
                    childMenuItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter
                    {
                        name = "AutoCloset"
                    };
                    childMenuItem.Control.value = i; // 순차적으로 증가하는 값 할당

                    // ModularAvatarShapeChanger 컴포넌트 추가 (중복 방지)
                    if (child.gameObject.GetComponent<ModularAvatarShapeChanger>() == null)
                        child.gameObject.AddComponent<ModularAvatarShapeChanger>();
                }
            }
        }
    }
}
