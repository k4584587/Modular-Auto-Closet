#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using nadena.dev.modular_avatar.core;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace needon.Editor
{
    public static class ToggleCreator
    {
        private const string ToggleMenuPath = "GameObject/Hirami/Add Create Toggle";
        private const int Priority = 1;

        [MenuItem(ToggleMenuPath, true, Priority)]
        private static bool ValidateCreateToggle() => Selection.gameObjects.Length > 0;

        [MenuItem(ToggleMenuPath, false, Priority)]
        private static void CreateToggle()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length == 0) return;

            // 옷장 루트 찾기 (상위에서, 없으면 씬 전체)
            var closetRoot = FindClosetRoot(selectedObjects[0].transform)
                             ?? GameObject.FindObjectOfType<AutoCloset>()?.transform;
            if (closetRoot == null)
                throw new Exception("AutoCloset 컴포넌트가 붙어있는 옷장 루트를 찾을 수 없습니다.");

            // Toggle 루트 생성/조회 (AutoCloset 설정 사용)
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
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/kr.needon.modular-auto-closet/Resource/toggleON.png");

            // Root 메뉴 항목 셋업
            var rootItem = toggleRootObj.GetComponent<ModularAvatarMenuItem>()
                           ?? toggleRootObj.AddComponent<ModularAvatarMenuItem>();
            rootItem.Control    ??= new VRCExpressionsMenu.Control();
            rootItem.Control.type      = VRCExpressionsMenu.Control.ControlType.SubMenu;
            rootItem.MenuSource        = SubmenuSource.Children;
            rootItem.Control.icon      = icon;

            // 선택 개수에 따라 분기
            if (selectedObjects.Length == 1)
            {
                CreateSingleToggle(selectedObjects[0], toggleRootObj.transform, icon);
            }
            else
            {
                CreateGroupToggle(selectedObjects, toggleRootObj.transform, icon);
            }

            Debug.Log("비파괴 토글 생성 완료");
        }

        // 개별 오브젝트용 토글 생성
        private static void CreateSingleToggle(GameObject obj, Transform parent, Texture2D icon)
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

                // Blendshape toggle component for additional shapekey curves
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
                // ensure BlendshapeToggle exists when reusing
                if (itemGO.GetComponent<BlendshapeToggle>() == null)
                    itemGO.AddComponent<BlendshapeToggle>();
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

                // Blendshape toggle component for shapekey animations
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

                if (groupGO.GetComponent<BlendshapeToggle>() == null)
                    groupGO.AddComponent<BlendshapeToggle>();

                // targets 업데이트
                var toggleComp = groupGO.GetComponent<AutoClosetObjectToggle>();
                toggleComp.targets = objects
                    .Select(o => new AutoClosetToggleTarget { target = o, active = true })
                    .ToArray();
            }

            // 파라미터 컴포넌트는 오직 하나만 유지
            var parameters = groupGO.GetComponent<ModularAvatarParameters>()
                             ?? groupGO.AddComponent<ModularAvatarParameters>();
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
