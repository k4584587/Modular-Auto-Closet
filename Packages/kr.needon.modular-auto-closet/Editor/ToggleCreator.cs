#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using nadena.dev.modular_avatar.core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace needon.Editor
{
    public static class ToggleCreator
    {
        private const string ToggleMenuPath = "GameObject/Hirami/Add Create Toggle";
        private const int Priority = 1; // place under the separator group in the Hirami menu

        [MenuItem(ToggleMenuPath, true, Priority)]
        private static bool ValidateCreateToggle()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length == 0) return false;

            // 선택한 오브젝트가 쓸 수 있는 옷장(상위 또는 같은 아바타 안, 비활성 포함)이 있어야 함
            if (ResolveClosetRoot(selectedObjects[0].transform) == null) return false;

            // 선택 자체가 AutoCloset 이면 비활성화 (루트에 직접 생성 방지)
            return selectedObjects.All(go =>
                go.GetComponent<AutoCloset>() == null &&
                go.GetComponent<VRCAvatarDescriptor>() == null);
        }

        [MenuItem(ToggleMenuPath, false, Priority)]
        private static void CreateToggle()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length == 0) return;

            // 옷장 루트 찾기 (상위에서, 없으면 같은 아바타 안에서)
            var closetRoot = ResolveClosetRoot(selectedObjects[0].transform);
            if (closetRoot == null)
                throw new Exception("AutoCloset 컴포넌트가 붙어있는 옷장 루트를 찾을 수 없습니다.");

            var toggleRootObj = EnsureToggleRoot(closetRoot, out var icon);

            // 1개면 개별 토글, 여러 개면 그룹 토글
            CreateToggleItem(selectedObjects, toggleRootObj.transform, icon);

            needon.Editor.Util.ClosetLogger.Log(toggleRootObj, "Log.Toggle.Created");
        }

        /// <summary>
        /// 대상 오브젝트 하나를 켜고 끄는 토글 아이템을 생성하고 그 GameObject를 반환합니다.
        /// 우클릭 "Add Create Toggle"의 단일 선택 흐름과 동일한 구조를 만듭니다.
        /// searchFrom(참조를 건 컴포넌트 위치 등)에서 가까운 옷장 루트를 탐색하고, 없으면 같은 아바타 안에서 찾습니다.
        /// AutoCloset을 찾지 못하면 null을 반환합니다.
        /// </summary>
        public static GameObject CreateToggleForObject(GameObject obj, Transform searchFrom = null)
        {
            if (obj == null) return null;

            var closetRoot = ResolveClosetRoot(searchFrom != null ? searchFrom : obj.transform);
            if (closetRoot == null) return null;

            var toggleRootObj = EnsureToggleRoot(closetRoot, out var icon);
            var item = CreateToggleItem(new[] { obj }, toggleRootObj.transform, icon);
            needon.Editor.Util.ClosetLogger.Log(toggleRootObj, "Log.Toggle.Created");
            return item;
        }

        // Toggle 루트 생성/조회 + 루트 메뉴(SubMenu) 셋업 (AutoCloset의 toggleRootName 설정 사용)
        private static GameObject EnsureToggleRoot(Transform closetRoot, out Texture2D icon)
        {
            var closetComponent = closetRoot.GetComponent<AutoCloset>();
            var rootName = closetComponent != null && !string.IsNullOrEmpty(closetComponent.toggleRootName)
                ? closetComponent.toggleRootName
                : "Toggle";

            var toggleRootObj = closetRoot.Find(rootName)?.gameObject;
            if (toggleRootObj == null)
            {
                toggleRootObj = new GameObject(rootName);
                toggleRootObj.transform.SetParent(closetRoot, false);
            }

            // 메뉴 아이콘 로드
            icon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/kr.needon.modular-auto-closet/Resource/toggleON.png");

            // Root 메뉴 항목 셋업
            var rootItem = toggleRootObj.GetComponent<ModularAvatarMenuItem>() ?? toggleRootObj.AddComponent<ModularAvatarMenuItem>();
            rootItem.Control    ??= new VRCExpressionsMenu.Control();
            rootItem.Control.type      = VRCExpressionsMenu.Control.ControlType.SubMenu;
            rootItem.MenuSource        = SubmenuSource.Children;
            rootItem.Control.icon      = icon;

            return toggleRootObj;
        }

        // 대상 오브젝트(들)를 켜고 끄는 토글 아이템을 생성해 반환한다. 1개면 개별 토글, 여러 개면 그룹 토글.
        // 같은 대상의 토글이 이미 있으면 그것을 그대로 반환한다. 이름이 아니라 대상으로 판정해야
        // 이름만 같은 다른 오브젝트의 토글을 돌려주거나, 기존 그룹의 대상을 덮어쓰지 않는다.
        private static GameObject CreateToggleItem(GameObject[] objects, Transform parent, Texture2D icon)
        {
            foreach (Transform child in parent)
            {
                var existing = child.GetComponent<AutoClosetObjectToggle>();
                if (existing == null || existing.targets == null) continue;
                if (!new HashSet<GameObject>(existing.targets.Select(t => t?.target)).SetEquals(objects)) continue;

                // 구버전 토글에 BlendshapeToggle 컴포넌트가 없으면 추가
                if (child.GetComponent<BlendshapeToggle>() == null)
                    child.gameObject.AddComponent<BlendshapeToggle>();

                return child.gameObject;
            }

            var baseName  = objects.Length == 1 ? $"Toggle_{objects[0].name}" : "Toggle_Group";
            var paramName = $"{baseName}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

            // 이름이 같은 토글이 이미 있으면 "이름 (1)"처럼 유일한 이름으로 만든다
            var itemGO = new GameObject(GameObjectUtility.GetUniqueNameForSibling(parent, baseName));
            itemGO.transform.SetParent(parent, false);

            itemGO.AddComponent<AutoClosetObjectToggle>().targets = objects
                .Select(o => new AutoClosetToggleTarget { target = o, active = true })
                .ToArray();

            // BlendshapeToggle 컴포넌트 자동 추가
            itemGO.AddComponent<BlendshapeToggle>();

            var menuItem = itemGO.AddComponent<ModularAvatarMenuItem>();
            menuItem.Control    ??= new VRCExpressionsMenu.Control();
            menuItem.Control.type      = VRCExpressionsMenu.Control.ControlType.Toggle;
            menuItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter { name = paramName };
            menuItem.Control.icon      = icon;

            // 파라미터는 아이템을 새로 만들 때만 등록한다 (재실행할 때마다 고아 파라미터가 쌓이지 않게)
            itemGO.AddComponent<ModularAvatarParameters>().parameters.Add(new ParameterConfig
            {
                nameOrPrefix = paramName,
                syncType     = ParameterSyncType.Bool,
                defaultValue = 1,
                saved        = true
            });

            return itemGO;
        }

        // 상위에서 옷장을 찾고, 없으면 같은 아바타 안의 옷장을 쓴다.
        // (씬 전체에서 찾으면 다른 아바타의 옷장에 토글이 만들어진다)
        private static Transform ResolveClosetRoot(Transform t)
        {
            var closet = t.GetComponentInParent<AutoCloset>(true);
            if (closet != null) return closet.transform;

            var avatar = t.GetComponentInParent<VRCAvatarDescriptor>(true);
            if (avatar == null) return null;

            closet = avatar.GetComponentInChildren<AutoCloset>(true);
            return closet != null ? closet.transform : null;
        }
    }
}
#endif
