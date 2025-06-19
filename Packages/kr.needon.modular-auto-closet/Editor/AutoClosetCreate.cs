using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

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

                // 각 선택된 오브젝트마다 고유한 파라미터 이름 생성 (예: AutoCloset_8자리해시)
                var uniqueName = "AutoCloset_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);

                // Closet 아이콘 로드
                var componentIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Packages/kr.needon.modular-auto-closet/Resource/ClosetIcon.png");

                // 부모 오브젝트에 필요한 컴포넌트 및 메뉴 설정
                ApplyComponents(selectedObject, componentIcon, uniqueName);
                // 자식 오브젝트들에도 아이콘 및 설정
                ApplyToChildren(selectedObject, uniqueName, componentIcon);
            }
        }

        private static bool ValidateCore(GameObject obj) =>
            obj != null &&
            !IsUnderCloset(obj) &&
            obj.GetComponentInChildren<AutoCloset>() == null &&
            obj.GetComponent<VRC_AvatarDescriptor>() == null &&
            obj.GetComponent<ModularAvatarMenuItem>() == null &&
            obj.GetComponent<ModularAvatarMeshSettings>() == null;

        private static bool IsUnderCloset(GameObject obj)
        {
            var current = obj.transform.parent;
            while (current != null)
            {
                if (current.GetComponent<AutoCloset>() != null)
                    return true;
                current = current.parent;
            }
            return false;
        }

        private static void ApplyComponents(GameObject targetObject, Texture2D icon = null, string uniqueName = null)
        {
            if (targetObject.GetComponent<AutoCloset>() == null)
                targetObject.AddComponent<AutoCloset>();

            if (targetObject.GetComponent<ModularAvatarMenuInstaller>() == null)
                targetObject.AddComponent<ModularAvatarMenuInstaller>();

            var maParameters = targetObject.GetComponent<ModularAvatarParameters>()
                               ?? targetObject.AddComponent<ModularAvatarParameters>();
            var maMenuItem = targetObject.GetComponent<ModularAvatarMenuItem>()
                              ?? targetObject.AddComponent<ModularAvatarMenuItem>();

            if (maParameters.parameters.All(p => p.nameOrPrefix != uniqueName))
            {
                maParameters.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix = uniqueName,
                    syncType = ParameterSyncType.Int,
                    defaultValue = 0,
                    saved = true
                });
            }

            maMenuItem.Control ??= new VRCExpressionsMenu.Control();
            maMenuItem.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            maMenuItem.MenuSource = SubmenuSource.Children;
            maMenuItem.Control.icon = icon;
        }

        private static void ApplyToChildren(GameObject parentObject, string uniqueName, Texture2D icon)
        {
            var children = new List<Transform>();
            foreach (Transform child in parentObject.transform)
                children.Add(child);

            children = children.OrderBy(t => t.GetSiblingIndex()).ToList();

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child.GetComponent<ModularAvatarMenuItem>() != null) continue;
                var childMenuItem = child.gameObject.AddComponent<ModularAvatarMenuItem>();

                childMenuItem.Control ??= new VRCExpressionsMenu.Control();
                childMenuItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
                childMenuItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter
                {
                    name = uniqueName
                };
                childMenuItem.Control.value = i;
                childMenuItem.Control.icon = icon;

                // Blendshape 및 Toggle 컴포넌트 추가 (중복 방지)
                if (child.gameObject.GetComponent<ClosetBlendshape>() == null)
                    child.gameObject.AddComponent<ClosetBlendshape>();
                if (child.gameObject.GetComponent<ClosetToggle>() == null)
                    child.gameObject.AddComponent<ClosetToggle>();
            }
        }

        [MenuItem("GameObject/Hirami/Add Closet", true, ContextMenuPriority)]
        private static bool ValidateAddClosetToClothing()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
                return false;
            if (selected.GetComponent<VRC_AvatarDescriptor>() != null)
                return false;
            return FindClosetParent(selected) != null;
        }

        [MenuItem("GameObject/Hirami/Add Closet", false, ContextMenuPriority)]
        private static void AddClosetToClothing()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
                return;

            var closetParent = FindClosetParent(selected);
            if (closetParent == null)
            {
                Debug.LogError("There is no Closet object in the parent of the selected object.");
                return;
            }

            var maParameters = closetParent.GetComponent<ModularAvatarParameters>();
            if (maParameters == null || maParameters.parameters == null || maParameters.parameters.Count == 0)
            {
                Debug.LogError("The Closet object does not have ModularAvatarParameters or it has no parameters.");
                return;
            }
            string uniqueName = null;
            for (var index = 0; index < maParameters.parameters.Count; index++)
            {
                var p = maParameters.parameters[index];
                if (!p.nameOrPrefix.StartsWith("AutoCloset_")) continue;
                uniqueName = p.nameOrPrefix;
                break;
            }

            if (uniqueName == null)
            {
                Debug.LogError("Could not find a valid parameter in the Closet object.");
                return;
            }

            var menuItem = selected.GetComponent<ModularAvatarMenuItem>();
            var hasComponents = menuItem != null && selected.GetComponent<ClosetBlendshape>() != null && selected.GetComponent<ClosetToggle>() != null;

            var closetIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/kr.needon.modular-auto-closet/Resource/ClosetIcon.png");

            if (!hasComponents)
            {
                if (menuItem == null)
                    menuItem = selected.AddComponent<ModularAvatarMenuItem>();

                if (selected.GetComponent<ClosetBlendshape>() == null)
                    selected.AddComponent<ClosetBlendshape>();
                if (selected.GetComponent<ClosetToggle>() == null)
                    selected.AddComponent<ClosetToggle>();

                menuItem.Control ??= new VRCExpressionsMenu.Control();
                menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
                menuItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter { name = uniqueName };
                menuItem.Control.icon = closetIcon;
            }

            RecalculateClosetChildren(closetParent, uniqueName);
        }

        private static GameObject FindClosetParent(GameObject obj)
        {
            var current = obj.transform;
            while (current != null)
            {
                if (current.GetComponent<AutoCloset>() != null)
                    return current.gameObject;
                current = current.parent;
            }
            return null;
        }

        private static void RecalculateClosetChildren(GameObject closetObject, string uniqueName)
        {
            var children = new List<Transform>();
            foreach (Transform child in closetObject.transform)
            {
                var menuItem = child.GetComponent<ModularAvatarMenuItem>();
                if (menuItem != null && menuItem.Control is { parameter: not null } && menuItem.Control.parameter.name == uniqueName)
                    children.Add(child);
            }

            children = children.OrderBy(t => t.GetSiblingIndex()).ToList();

            for (var i = 0; i < children.Count; i++)
            {
                var mi = children[i].GetComponent<ModularAvatarMenuItem>();
                if (mi != null && mi.Control != null)
                    mi.Control.value = i;
            }
        }
    }
}
