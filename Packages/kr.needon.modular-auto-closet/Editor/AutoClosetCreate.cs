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
                string uniqueName = "AutoCloset_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);

                Texture2D componentIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/kr.needon.modular-auto-closet/Resource/ClosetIcon.png");
                ApplyComponents(selectedObject, componentIcon, uniqueName);
                ApplyToChildren(selectedObject, uniqueName);
            }
        }

        // 기존 검증 조건에 ModularAvatarMenuItem 컴포넌트가 있는 경우 false 반환 추가
        private static bool ValidateCore(GameObject obj) =>
            obj != null &&
            obj.GetComponentInChildren<AutoCloset>() == null &&
            obj.GetComponent<VRC_AvatarDescriptor>() == null &&
            obj.GetComponent<ModularAvatarMenuItem>() == null;

        private static void ApplyComponents(GameObject targetObject, Texture2D icon = null, string uniqueName = null)
        {
            // 필요한 컴포넌트 추가 (중복 방지)
            if (targetObject.GetComponent<AutoCloset>() == null)
                targetObject.AddComponent<AutoCloset>();

            if (targetObject.GetComponent<ModularAvatarMenuInstaller>() == null)
                targetObject.AddComponent<ModularAvatarMenuInstaller>();

            var maParameters = targetObject.GetComponent<ModularAvatarParameters>() ?? targetObject.AddComponent<ModularAvatarParameters>();
            var maMenuItem = targetObject.GetComponent<ModularAvatarMenuItem>() ?? targetObject.AddComponent<ModularAvatarMenuItem>();

            // Int 파라미터 생성 (고유 파라미터 이름 사용)
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
            maMenuItem.Control.icon = icon; // 메뉴 아이콘 설정
        }

        private static void ApplyToChildren(GameObject parentObject, string uniqueName)
        {
            // 모든 직계 자식 오브젝트들의 리스트를 가져옴
            var children = new List<Transform>();
            foreach (Transform child in parentObject.transform)
            {
                children.Add(child);
            }

            // 씬에서의 순서대로 정렬 (위에서 아래로)
            children = children.OrderBy(t => t.GetSiblingIndex()).ToList();

            // 순차적으로 value 값 할당 및 필요한 컴포넌트 추가
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child.GetComponent<ModularAvatarMenuItem>() == null)
                {
                    var childMenuItem = child.gameObject.AddComponent<ModularAvatarMenuItem>();

                    // MenuItem 설정 (고유 파라미터 이름 사용)
                    childMenuItem.Control ??= new VRCExpressionsMenu.Control();
                    childMenuItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
                    childMenuItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter
                    {
                        name = uniqueName
                    };
                    childMenuItem.Control.value = i; // 순차적으로 증가하는 값 할당

                    // ModularAvatarShapeChanger 컴포넌트 추가 (중복 방지)
                    if (child.gameObject.GetComponent<ModularAvatarShapeChanger>() == null)
                        child.gameObject.AddComponent<ModularAvatarShapeChanger>();
                }
            }
        }

        // ----------------------------------------------------------------
        // 옷(클로딩) 오브젝트에 대해 Add Closet 메뉴 추가 (상위에 옷장이 있어야 함)
        [MenuItem("GameObject/Hirami/Add Closet", true, ContextMenuPriority)]
        private static bool ValidateAddClosetToClothing()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
                return false;
            if (selected.GetComponent<VRC_AvatarDescriptor>() != null)
                return false;
            // 상위에 옷장(Closet) 오브젝트가 존재하는지 확인
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
                Debug.LogError("선택된 오브젝트의 상위에 Closet 오브젝트가 존재하지 않습니다.");
                return;
            }

            // 옷장(Closet) 오브젝트의 ModularAvatarParameters에서 고유 파라미터 찾기
            var maParameters = closetParent.GetComponent<ModularAvatarParameters>();
            if (maParameters == null || maParameters.parameters == null || maParameters.parameters.Count == 0)
            {
                Debug.LogError("Closet 오브젝트에 ModularAvatarParameters가 없거나 파라미터가 존재하지 않습니다.");
                return;
            }
            string uniqueName = null;
            foreach (var p in maParameters.parameters)
            {
                if (p.nameOrPrefix.StartsWith("AutoCloset_"))
                {
                    uniqueName = p.nameOrPrefix;
                    break;
                }
            }
            if (uniqueName == null)
            {
                Debug.LogError("Closet 오브젝트에서 유효한 파라미터를 찾을 수 없습니다.");
                return;
            }

            // 이미 ModularAvatarMenuItem과 ModularAvatarShapeChanger가 추가되었으면 추가하지 않고 업데이트만 수행
            var menuItem = selected.GetComponent<ModularAvatarMenuItem>();
            bool hasComponents = menuItem != null && selected.GetComponent<ModularAvatarShapeChanger>() != null;
            if (!hasComponents)
            {
                if (menuItem == null)
                    menuItem = selected.AddComponent<ModularAvatarMenuItem>();

                if (selected.GetComponent<ModularAvatarShapeChanger>() == null)
                    selected.AddComponent<ModularAvatarShapeChanger>();

                menuItem.Control ??= new VRCExpressionsMenu.Control();
                menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
                menuItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter { name = uniqueName };
            }
            // 상위 Closet의 모든 자식(옷) 오브젝트에 대해 순서를 재계산하여 Value값 자동 할당
            RecalculateClosetChildren(closetParent, uniqueName);
        }

        // 선택된 오브젝트의 상위에 존재하는 Closet(옷장) 오브젝트를 찾음
        private static GameObject FindClosetParent(GameObject obj)
        {
            Transform current = obj.transform;
            while (current != null)
            {
                if (current.GetComponent<AutoCloset>() != null)
                    return current.gameObject;
                current = current.parent;
            }
            return null;
        }

        // Closet(옷장) 오브젝트의 자식(옷) 오브젝트들에 대해 순서를 재계산하여 ModularAvatarMenuItem의 Value값을 자동 할당
        private static void RecalculateClosetChildren(GameObject closetObject, string uniqueName)
        {
            var children = new List<Transform>();
            foreach (Transform child in closetObject.transform)
            {
                var menuItem = child.GetComponent<ModularAvatarMenuItem>();
                if (menuItem != null &&
                    menuItem.Control != null &&
                    menuItem.Control.parameter != null &&
                    menuItem.Control.parameter.name == uniqueName)
                {
                    children.Add(child);
                }
            }

            children = children.OrderBy(t => t.GetSiblingIndex()).ToList();

            for (int i = 0; i < children.Count; i++)
            {
                var mi = children[i].GetComponent<ModularAvatarMenuItem>();
                if (mi != null && mi.Control != null)
                {
                    mi.Control.value = i;
                }
            }
        }
    }
}
