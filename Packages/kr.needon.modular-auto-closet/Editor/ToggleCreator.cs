using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using nadena.dev.modular_avatar.core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using needon.Editor.Pass;

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

            // Toggle 루트 생성/조회
            var parent = selectedObjects[0].transform.parent;
            var toggleRoot = parent != null ? parent.Find("Toggle")?.gameObject : null;
            if (toggleRoot == null)
            {
                toggleRoot = new GameObject("Toggle");
                if (parent != null) toggleRoot.transform.SetParent(parent, false);
            }

            // 메뉴 아이콘
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/kr.needon.modular-auto-closet/Resource/ClosetIcon.png");

            // Modular Avatar 세팅
            if (toggleRoot.GetComponent<ModularAvatarMenuInstaller>() == null)
                toggleRoot.AddComponent<ModularAvatarMenuInstaller>();

            var parameters = toggleRoot.GetComponent<ModularAvatarParameters>()
                             ?? toggleRoot.AddComponent<ModularAvatarParameters>();

            var rootItem = toggleRoot.GetComponent<ModularAvatarMenuItem>()
                           ?? toggleRoot.AddComponent<ModularAvatarMenuItem>();
            rootItem.Control ??= new VRCExpressionsMenu.Control();
            rootItem.Control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
            rootItem.MenuSource = SubmenuSource.Children;
            rootItem.Control.icon = icon;

            // Avatar Descriptor 가져오기
            var avatarRoot = FindAvatarRoot(toggleRoot.transform)
                ?? throw new Exception("VRCAvatarDescriptor를 찾을 수 없습니다.");
            var avatarDescriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();

            // 선택된 각 오브젝트에 대해 Toggle 생성 및 애니메이션 설정
            foreach (var obj in selectedObjects)
            {
                var paramName = $"Toggle_{obj.name}";

                // ParameterConfig 등록
                if (parameters.parameters.All(p => p.nameOrPrefix != paramName))
                {
                    parameters.parameters.Add(new ParameterConfig
                    {
                        nameOrPrefix = paramName,
                        syncType = ParameterSyncType.Bool,
                        defaultValue = 0,
                        saved = true
                    });
                }

                // 메뉴 아이템 GameObject 생성
                if (toggleRoot.transform.Find(paramName) == null)
                {
                    var itemGO = new GameObject(paramName);
                    itemGO.transform.SetParent(toggleRoot.transform, false);

                    var toggleComp = itemGO.AddComponent<ModularAvatarObjectToggle>();
                    var path = GetRelativePath(obj.transform, avatarRoot);
                    toggleComp.Objects.Add(new ToggledObject
                    {
                        Object = new AvatarObjectReference { referencePath = path },
                        Active = true
                    });

                    var childItem = itemGO.AddComponent<ModularAvatarMenuItem>();
                    childItem.Control ??= new VRCExpressionsMenu.Control();
                    childItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
                    childItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter { name = paramName };
                    childItem.Control.icon = icon;
                }

                // 빌드 파스에서 애니메이션을 생성하도록 데이터만 설정
                // (Animator 컨트롤러와 클립은 생성하지 않음)
            }

            Debug.Log("비파괴 토글 생성 완료");
        }

        private static Transform FindAvatarRoot(Transform t)
        {
            while (t != null)
            {
                if (t.GetComponent<VRCAvatarDescriptor>() != null)
                    return t;
                t = t.parent;
            }
            return null;
        }

        private static string GetRelativePath(Transform target, Transform root)
        {
            if (target == root) return string.Empty;
            var path = target.name;
            var current = target.parent;
            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }
    }
}
