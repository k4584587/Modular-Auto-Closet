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

            // 옷장 루트(Closet) 검색: 상위에서 찾고, 없으면 씬 전체에서 검색
            Transform closetRoot = FindClosetRoot(selectedObjects[0].transform);
            if (closetRoot == null)
            {
                var closetComponent = GameObject.FindObjectOfType<AutoCloset>();
                if (closetComponent != null)
                    closetRoot = closetComponent.transform;
            }
            if (closetRoot == null)
                throw new Exception("AutoCloset 컴포넌트가 붙어있는 옷장 루트를 찾을 수 없습니다.");

            // 토글 루트 생성/조회 (항상 옷장 하위)
            var toggleRootObj = closetRoot.Find("Toggle")?.gameObject;
            if (toggleRootObj == null)
            {
                toggleRootObj = new GameObject("Toggle");
                toggleRootObj.transform.SetParent(closetRoot, false);
            }

            // 메뉴 아이콘 로드
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/kr.needon.modular-auto-closet/Resource/toggleON.png");

            // Root 메뉴 셋업
            var rootItem = toggleRootObj.GetComponent<ModularAvatarMenuItem>()
                           ?? toggleRootObj.AddComponent<ModularAvatarMenuItem>();
            rootItem.Control ??= new VRCExpressionsMenu.Control();
            rootItem.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            rootItem.MenuSource = SubmenuSource.Children;
            rootItem.Control.icon = icon;

            // 선택된 각 오브젝트에 대해 Toggle 생성 및 파라미터/메뉴 아이템 설정
            foreach (var obj in selectedObjects)
            {
                // UUID 접미사를 통해 중복 방지
                var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
                var baseName = $"Toggle_{obj.name}";
                var paramName = $"{baseName}_{suffix}";

                // 메뉴 아이템 GameObject 생성 또는 조회 (이름에는 suffix 제외)
                var existingGO = toggleRootObj.transform.Find(baseName);
                GameObject itemGO;
                if (existingGO == null)
                {
                    itemGO = new GameObject(baseName);
                    itemGO.transform.SetParent(toggleRootObj.transform, false);

                    // AutoClosetObjectToggle 설정
                    var toggleComp = itemGO.AddComponent<AutoClosetObjectToggle>();
                    toggleComp.targets = new[] { new AutoClosetToggleTarget { target = obj, active = true } };

                    // 토글 메뉴 항목 설정
                    var childItem = itemGO.AddComponent<ModularAvatarMenuItem>();
                    childItem.Control ??= new VRCExpressionsMenu.Control();
                    childItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
                    childItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter { name = paramName };
                    childItem.Control.icon = icon;
                }
                else
                {
                    itemGO = existingGO.gameObject;
                }

                // 해당 Toggle_<이름> 오브젝트에 파라미터 컴포넌트 추가 및 등록
                var parameters = itemGO.GetComponent<ModularAvatarParameters>()
                                 ?? itemGO.AddComponent<ModularAvatarParameters>();
                if (parameters.parameters.All(p => p.nameOrPrefix != paramName))
                {
                    parameters.parameters.Add(new ParameterConfig
                    {
                        nameOrPrefix = paramName,
                        syncType = ParameterSyncType.Bool,
                        defaultValue = 1,
                        saved = true
                    });
                }
            }

            Debug.Log("비파괴 토글 생성 완료");
        }

        // 상위 트랜스폼 중 AutoCloset 컴포넌트가 붙은 옷장 루트를 반환
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