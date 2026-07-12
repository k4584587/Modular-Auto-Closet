#if UNITY_EDITOR
using System;
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

            // 씬 어딘가에 AutoCloset 이 최소 1개 존재해야 함 (비활성 포함)
            var anyCloset = UnityEngine.Object.FindObjectsOfType<AutoCloset>(true).Length > 0;
            if (!anyCloset) return false;

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

            // 옷장 루트 찾기 (상위에서, 없으면 씬 전체)
            var closetRoot = FindClosetRoot(selectedObjects[0].transform)
                             ?? UnityEngine.Object.FindObjectOfType<AutoCloset>(true)?.transform;
            if (closetRoot == null)
                throw new Exception("AutoCloset 컴포넌트가 붙어있는 옷장 루트를 찾을 수 없습니다.");

            var toggleRootObj = EnsureToggleRoot(closetRoot, out var icon);

            // 선택 개수에 따라 분기
            if (selectedObjects.Length == 1)
            {
                CreateSingleToggle(selectedObjects[0], toggleRootObj.transform, icon);
            }
            else
            {
                CreateGroupToggle(selectedObjects, toggleRootObj.transform, icon);
            }

            needon.Editor.Util.ClosetLogger.Log(toggleRootObj, "Log.Toggle.Created");
        }

        /// <summary>
        /// 대상 오브젝트 하나를 켜고 끄는 토글 아이템을 생성하고 그 GameObject를 반환합니다.
        /// 우클릭 "Add Create Toggle"의 단일 선택 흐름과 동일한 구조를 만듭니다.
        /// searchFrom(참조를 건 컴포넌트 위치 등)에서 가까운 옷장 루트를 탐색하고, 없으면 씬 전체에서 찾습니다.
        /// AutoCloset을 찾지 못하면 null을 반환합니다.
        /// </summary>
        public static GameObject CreateToggleForObject(GameObject obj, Transform searchFrom = null)
        {
            if (obj == null) return null;

            var closetRoot = FindClosetRoot(searchFrom != null ? searchFrom : obj.transform)
                             ?? UnityEngine.Object.FindObjectOfType<AutoCloset>(true)?.transform;
            if (closetRoot == null) return null;

            var toggleRootObj = EnsureToggleRoot(closetRoot, out var icon);
            var item = CreateSingleToggle(obj, toggleRootObj.transform, icon);
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

        // 개별 오브젝트용 토글 생성 (생성/재사용된 토글 아이템 GameObject 반환)
        private static GameObject CreateSingleToggle(GameObject obj, Transform parent, Texture2D icon)
        {
            var baseName  = $"Toggle_{obj.name}";
            var suffix    = Guid.NewGuid().ToString("N").Substring(0, 8);
            var paramName = $"{baseName}_{suffix}";

            var existingGO = parent.Find(baseName);
            GameObject itemGO;
            if (existingGO == null)
            {
                itemGO = new GameObject(baseName);
                itemGO.transform.SetParent(parent, false);

                var toggleComp = itemGO.AddComponent<AutoClosetObjectToggle>();
                toggleComp.targets = new[]
                {
                    new AutoClosetToggleTarget { target = obj, active = true }
                };

                // BlendshapeToggle 컴포넌트 자동 추가
                itemGO.AddComponent<BlendshapeToggle>();

                var menuItem = itemGO.AddComponent<ModularAvatarMenuItem>();
                menuItem.Control    ??= new VRCExpressionsMenu.Control();
                menuItem.Control.type      = VRCExpressionsMenu.Control.ControlType.Toggle;
                menuItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter { name = paramName };
                menuItem.Control.icon      = icon;
            }
            else
            {
                itemGO = existingGO.gameObject;
                // 기존 토글에 BlendshapeToggle 컴포넌트가 없으면 추가
                if (itemGO.GetComponent<BlendshapeToggle>() == null)
                {
                    itemGO.AddComponent<BlendshapeToggle>();
                }
            }

            var parameters = itemGO.GetComponent<ModularAvatarParameters>()
                             ?? itemGO.AddComponent<ModularAvatarParameters>();
            if (parameters.parameters.All(p => p.nameOrPrefix != paramName))
            {
                parameters.parameters.Add(new ParameterConfig
                {
                    nameOrPrefix  = paramName,
                    syncType      = ParameterSyncType.Bool,
                    defaultValue  = 1,
                    saved         = true
                });
            }

            return itemGO;
        }

        // 다중 선택 시 그룹 토글 생성 (파라미터는 최초 생성 시에만 UUID 부여, 이후 재사용)
        private static void CreateGroupToggle(GameObject[] objects, Transform parent, Texture2D icon)
        {
            const string groupName = "Toggle_Group";
            var existingGO = parent.Find(groupName);

            GameObject groupGO;
            string paramName;
            ModularAvatarMenuItem menuItem;

            if (existingGO == null)
            {
                // 아직 그룹이 없으면 생성하고 UUID 붙인 파라미터 하나만 설정
                groupGO = new GameObject(groupName);
                groupGO.transform.SetParent(parent, false);

                // targets 설정
                var toggleComp = groupGO.AddComponent<AutoClosetObjectToggle>();
                toggleComp.targets = objects
                    .Select(o => new AutoClosetToggleTarget { target = o, active = true })
                    .ToArray();

                // BlendshapeToggle 컴포넌트 자동 추가
                groupGO.AddComponent<BlendshapeToggle>();

                // 메뉴 아이템 & 파라미터 이름(UUID 포함) 설정
                menuItem = groupGO.AddComponent<ModularAvatarMenuItem>();
                menuItem.Control    ??= new VRCExpressionsMenu.Control();
                menuItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
                var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
                paramName = $"{groupName}_{suffix}";
                menuItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter { name = paramName };
                menuItem.Control.icon      = icon;
            }
            else
            {
                // 이미 생성된 그룹이면 기존 오브젝트와 파라미터 이름 그대로 재사용
                groupGO = existingGO.gameObject;
                menuItem = groupGO.GetComponent<ModularAvatarMenuItem>();
                paramName = menuItem.Control.parameter?.name ?? groupName;

                // 기존 그룹에 BlendshapeToggle 컴포넌트가 없으면 추가
                if (groupGO.GetComponent<BlendshapeToggle>() == null)
                {
                    groupGO.AddComponent<BlendshapeToggle>();
                }

                // targets 업데이트
                var toggleComp = groupGO.GetComponent<AutoClosetObjectToggle>();
                toggleComp.targets = objects
                    .Select(o => new AutoClosetToggleTarget { target = o, active = true })
                    .ToArray();
            }

            // 파라미터 컴포넌트는 오직 하나만 유지
            var parameters = groupGO.GetComponent<ModularAvatarParameters>() ?? groupGO.AddComponent<ModularAvatarParameters>();
            parameters.parameters.Clear();
            parameters.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = paramName,
                syncType     = ParameterSyncType.Bool,
                defaultValue = 1,
                saved        = true
            });
        }

        // 상위 트랜스폼 순회하여 AutoCloset 찾기
        private static Transform FindClosetRoot(Transform t)
        {
            while (t != null)
            {
                if (t.GetComponent<AutoCloset>() != null)
                    return t;
                t = t.parent;
            }
            return null;
        }
    }
}
#endif
