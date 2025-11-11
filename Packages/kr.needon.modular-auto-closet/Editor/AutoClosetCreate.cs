#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;
using nadena.dev.modular_avatar.core;

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

                // 선택된 오브젝트마다 고유 파라미터 생성
                var uniqueName = "AutoCloset_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);

                // 기본 아이콘 (실패 시 대체)
                var componentIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Packages/kr.needon.modular-auto-closet/Resource/ClosetIcon.png");

                // 부모 세팅
                ApplyComponents(selectedObject, componentIcon, uniqueName);

                // 자식 세팅 + 썸네일 자동 생성/적용
                ApplyToChildren(
                    parentObject: selectedObject,
                    uniqueName: uniqueName,
                    fallbackIcon: componentIcon,
                    size: 512,
                    overwrite: false,   // 기존 파일 있으면 재사용
                    chromaTol: 0.02f,   // 마젠타 배경 판정 오차
                    tempLayer: 31       // 임시 레이어 (충돌시 다른 번호 사용)
                );
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
            maMenuItem.Control.value = 0;
            maMenuItem.MenuSource = SubmenuSource.Children;
            maMenuItem.Control.icon = icon;
        }

        // ▼ 썸네일 자동 생성/적용 통합
        private static void ApplyToChildren(
            GameObject parentObject, string uniqueName, Texture2D fallbackIcon,
            int size, bool overwrite, float chromaTol, int tempLayer)
        {
            var children = new List<Transform>();
            foreach (Transform child in parentObject.transform)
                children.Add(child);

            children = children.OrderBy(t => t.GetSiblingIndex()).ToList();

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];

                // 메뉴 아이템 처리
                var childMenuItem = child.GetComponent<ModularAvatarMenuItem>();
                bool hasExistingMenuItem = childMenuItem != null;
                bool isClosetMenuItem = false;
                bool isSubMenu = false;

                // 기존 Menu Item이 있는 경우
                if (hasExistingMenuItem && childMenuItem.Control != null)
                {
                    // SubMenu 타입인지 확인
                    isSubMenu = childMenuItem.Control.type == VRCExpressionsMenu.Control.ControlType.SubMenu;

                    // Toggle 타입이고 파라미터가 있으면 옷장 파라미터인지 확인
                    if (!isSubMenu && childMenuItem.Control.parameter is { name: not null })
                    {
                        isClosetMenuItem = childMenuItem.Control.parameter.name == uniqueName;
                    }
                }

                // 옷장 메뉴 아이템이 없으면 생성하거나 설정
                if (!hasExistingMenuItem || isClosetMenuItem)
                {
                    // Menu Item이 없거나, 이미 옷장용이면 설정
                    if (childMenuItem == null)
                        childMenuItem = child.gameObject.AddComponent<ModularAvatarMenuItem>();

                    childMenuItem.Control ??= new VRCExpressionsMenu.Control();
                    childMenuItem.Control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
                    childMenuItem.Control.parameter = new VRCExpressionsMenu.Control.Parameter { name = uniqueName };
                    childMenuItem.Control.value = 0;
                }
                // SubMenu나 다른 파라미터를 사용하는 기존 Menu Item은 보존
                // (childMenuItem을 건드리지 않음)

                // Unified Closet configuration component (replaces separate ClosetBlendshape/ClosetToggle)
                var closetConfig = child.gameObject.GetComponent<ClosetConfig>();
                if (closetConfig == null)
                {
                    closetConfig = child.gameObject.AddComponent<ClosetConfig>();

                    // Migrate from existing components if present
                    var legacyBlend = child.gameObject.GetComponent<ClosetBlendshape>();
                    if (legacyBlend != null && legacyBlend.shapes != null)
                    {
                        closetConfig.shapes = legacyBlend.shapes;
                    }

                    var legacyToggle = child.gameObject.GetComponent<ClosetToggle>();
                    if (legacyToggle != null && legacyToggle.toggles != null)
                    {
                        closetConfig.toggles = legacyToggle.toggles;
                    }
                }

                // 썸네일 생성 & 아이콘 적용 (옷장 메뉴인 경우에만)
                if (!hasExistingMenuItem || isClosetMenuItem)
                {
                    var tex = ClosetThumbnailUtil.GenerateAndAssignThumbnail(
                        child.gameObject, uniqueName, size, overwrite, chromaTol, tempLayer);

                    if (childMenuItem != null && childMenuItem.Control != null)
                        childMenuItem.Control.icon = tex != null ? tex : fallbackIcon;
                }
                // 기존 Menu Item의 아이콘은 보존
            }
        }

        // (수정 유지) Add Closet 메뉴는 비활성화
        // [MenuItem("GameObject/Hirami/Add Closet", true, ContextMenuPriority)]
        private static bool ValidateAddClosetToClothing()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
                return false;
            if (selected.GetComponent<VRC_AvatarDescriptor>() != null)
                return false;
            return FindClosetParent(selected) != null;
        }

        // [MenuItem("GameObject/Hirami/Add Closet", false, ContextMenuPriority)]
        private static void MenuAddClosetToClothing()
        {
            AddClosetToClothing(Selection.activeGameObject);
        }

        internal static void AddClosetToClothing(GameObject selected)
{
    if (selected == null)
        return;

    var closetParent = FindClosetParent(selected);
    if (closetParent == null)
    {
        needon.Editor.Util.ClosetLogger.LogError(selected, "Error.NoClosetInParent");
        return;
    }

    var maParameters = closetParent.GetComponent<ModularAvatarParameters>();
    if (maParameters == null || maParameters.parameters == null || maParameters.parameters.Count == 0)
    {
        needon.Editor.Util.ClosetLogger.LogError(selected, "Error.NoParameters");
        return;
    }

    // AutoCloset_ 파라미터 찾기
    string uniqueName = null;
    foreach (var p in maParameters.parameters)
    {
        if (!string.IsNullOrEmpty(p.nameOrPrefix) && p.nameOrPrefix.StartsWith("AutoCloset_"))
        {
            uniqueName = p.nameOrPrefix;
            break;
        }
    }

    if (uniqueName == null)
    {
        needon.Editor.Util.ClosetLogger.LogError(selected, "Error.NoValidParameter");
        return;
    }

    // 컴포넌트 보장
    var menuItem = selected.GetComponent<ModularAvatarMenuItem>();
    bool hasExistingMenuItem = menuItem != null;
    bool isClosetMenuItem = false;
    bool isSubMenu = false;

    // 기존 Menu Item이 있는 경우, 옷장용인지 확인
    if (hasExistingMenuItem && menuItem.Control != null)
    {
        // SubMenu 타입인지 확인
        isSubMenu = menuItem.Control.type == VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType.SubMenu;

        // Toggle 타입이고 파라미터가 있으면 옷장 파라미터인지 확인
        if (!isSubMenu && menuItem.Control.parameter is { name: not null })
        {
            isClosetMenuItem = menuItem.Control.parameter.name == uniqueName;
        }
    }

    // Attach unified config instead of separate components
    if (selected.GetComponent<ClosetConfig>() == null)
    {
        var cfg = selected.AddComponent<ClosetConfig>();

        // If legacy components exist, migrate their data
        var legacyBlend = selected.GetComponent<ClosetBlendshape>();
        if (legacyBlend != null && legacyBlend.shapes != null)
            cfg.shapes = legacyBlend.shapes;

        var legacyToggle = selected.GetComponent<ClosetToggle>();
        if (legacyToggle != null && legacyToggle.toggles != null)
            cfg.toggles = legacyToggle.toggles;
    }

    // 옷장 메뉴로 설정 (기존 독립 메뉴가 아닌 경우에만)
    if (!hasExistingMenuItem || isClosetMenuItem)
    {
        if (menuItem == null) menuItem = selected.AddComponent<ModularAvatarMenuItem>();

        // 기본 아이콘 (실패 시 대체)
        var fallbackIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Packages/kr.needon.modular-auto-closet/Resource/ClosetIcon.png");

        menuItem.Control ??= new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control();
        menuItem.Control.type = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType.Toggle;
        menuItem.Control.parameter = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.Parameter
        {
            name = uniqueName
        };
        menuItem.Control.value = 0;

        // >>> 여기서 썸네일 자동 생성 & 적용 <<<
        // (Auto Apply와 동일 옵션: size=512, overwrite=false, chromaTol=0.02f, tempLayer=31)
        var thumb = ClosetThumbnailUtil.GenerateAndAssignThumbnail(
            selected, uniqueName, size: 512, overwrite: false, chromaTol: 0.02f, tempLayer: 31);
        menuItem.Control.icon = thumb != null ? thumb : fallbackIcon;
    }
    // 기존 독립 메뉴 아이템은 보존 (옷의 자체 기믹 메뉴)

    // 순서 재계산 및 기본값 0으로 통일
    RecalculateClosetChildren(closetParent, uniqueName);
    foreach (Transform child in closetParent.transform)
    {
        var mi = child.GetComponent<ModularAvatarMenuItem>();
        if (mi?.Control != null) mi.Control.value = 0;
    }

    // 씬 갱신
    EditorUtility.SetDirty(selected);
    EditorUtility.SetDirty(closetParent);
    AssetDatabase.SaveAssets();
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
                if (menuItem != null && menuItem.Control is { parameter: not null } &&
                    menuItem.Control.parameter.name == uniqueName)
                    children.Add(child);
            }

            children = children.OrderBy(t => t.GetSiblingIndex()).ToList();

            for (var i = 0; i < children.Count; i++)
            {
                var mi = children[i].GetComponent<ModularAvatarMenuItem>();
                if (mi != null && mi.Control != null)
                    mi.Control.value = i + 1; // 1부터
            }
        }
    }

    // ===== 썸네일 유틸 =====
    internal static class ClosetThumbnailUtil
    {
        private static readonly Color Chroma = new Color(1f, 0f, 1f, 1f); // Magenta 배경

        public static Texture2D GenerateAndAssignThumbnail(
            GameObject target, string uniqueName, int size, bool overwrite, float chromaTol, int tempLayer)
        {
            if (target == null) return null;

            // === 저장 경로를 Assets/Hirami/AutoCloset 쪽으로 변경 ===
            var baseDir = "Assets/Hirami";
            if (!AssetDatabase.IsValidFolder(baseDir)) AssetDatabase.CreateFolder("Assets", "Hirami");

            var closetDir = baseDir + "/AutoCloset";
            if (!AssetDatabase.IsValidFolder(closetDir)) AssetDatabase.CreateFolder(baseDir, "AutoCloset");

            var uniqueDir = closetDir + "/" + uniqueName;
            if (!AssetDatabase.IsValidFolder(uniqueDir)) AssetDatabase.CreateFolder(closetDir, uniqueName);

            var path = $"{uniqueDir}/{Sanitize(target.name)}.png";

            if (!overwrite)
            {
                var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (existing != null)
                {
                    return existing;
                }
            }

            var tex = CaptureToTexture(target, size, chromaTol, tempLayer);
            if (tex == null) return null;

            var png = tex.EncodeToPNG();
            File.WriteAllBytes(path, png);
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }


        private static Texture2D CaptureToTexture(GameObject target, int size, float chromaTol, int tempLayer)
        {
            // 비활성 포함 전체 Renderer 수집 (필터 제거)
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return null;

            // === 활성/렌더/옵션 상태 백업 & 임시 활성화 ===
            var goStates = new List<(GameObject go, bool activeSelf)>();
            foreach (var t in target.GetComponentsInChildren<Transform>(true))
            {
                goStates.Add((t.gameObject, t.gameObject.activeSelf));
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            }

            var renStates = new List<(Renderer r, bool enabled)>();
            var smrStates = new List<(SkinnedMeshRenderer smr, bool updateWhenOffscreen)>();
            foreach (var r in renderers)
            {
                renStates.Add((r, r.enabled));
                if (!r.enabled) r.enabled = true;

                if (r is SkinnedMeshRenderer smr)
                {
                    smrStates.Add((smr, smr.updateWhenOffscreen));
                    smr.updateWhenOffscreen = true; // 오프스크린 업데이트 보장
                }
            }

            // 바운드 계산
            var bounds = new Bounds(renderers[0].bounds.center, Vector3.zero);
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            // 임시 레이어로 격리
            var originals = new List<(Transform t, int layer)>();
            SetLayerRecursively(target.transform, tempLayer, originals);

            // 카메라/라이트/RT
            var camGO = new GameObject("__ClosetThumbCam__") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Chroma;
            cam.cullingMask = (1 << tempLayer);
            cam.allowMSAA = true;
            // near clip은 양수의 작은 값으로 설정 (음수 사용 지양)
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;

            var ext = bounds.extents;
            var center = bounds.center;
            camGO.transform.position = center + new Vector3(0, 0, -10f);
            camGO.transform.LookAt(center, Vector3.up);
            cam.orthographicSize = Mathf.Max(ext.y, ext.x) * 1.2f;

            var lightGO = new GameObject("__ClosetThumbLight__") { hideFlags = HideFlags.HideAndDontSave };
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGO.transform.rotation = Quaternion.Euler(30f, -30f, 0f);

            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            RenderTexture.active = rt;
            cam.targetTexture = rt;

            try
            {
                cam.Render();

                var tex = new Texture2D(size, size, TextureFormat.ARGB32, false, false);
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                tex.Apply();

                // 마젠타 → 투명
                var pixels = tex.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    var c = pixels[i];
                    if (IsChroma(c, chromaTol)) pixels[i] = new Color32(0, 0, 0, 0);
                }
                tex.SetPixels32(pixels);
                tex.Apply();

                return tex;
            }
            finally
            {
                // 정리
                RenderTexture.active = null;
                if (cam) cam.targetTexture = null;
                if (rt) rt.Release();

                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(lightGO);
                Object.DestroyImmediate(camGO);

                // 레이어 복구
                RestoreLayers(originals);

                // 상태 복구 (역순)
                for (int i = renStates.Count - 1; i >= 0; i--)
                    if (renStates[i].r) renStates[i].r.enabled = renStates[i].enabled;

                for (int i = smrStates.Count - 1; i >= 0; i--)
                    if (smrStates[i].smr) smrStates[i].smr.updateWhenOffscreen = smrStates[i].updateWhenOffscreen;

                for (int i = goStates.Count - 1; i >= 0; i--)
                    if (goStates[i].go) goStates[i].go.SetActive(goStates[i].activeSelf);
            }
        }

        private static bool IsChroma(Color32 c, float tol)
        {
            // (1,0,1) 근처면 배경으로 간주
            float dr = Mathf.Abs(c.r / 255f - 1f);
            float dg = Mathf.Abs(c.g / 255f - 0f);
            float db = Mathf.Abs(c.b / 255f - 1f);
            return (dr + dg + db) / 3f <= tol;
        }

        private static void SetLayerRecursively(Transform t, int layer, List<(Transform, int)> originals)
        {
            originals.Add((t, t.gameObject.layer));
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i), layer, originals);
        }

        private static void RestoreLayers(List<(Transform t, int layer)> originals)
        {
            foreach (var (t, layer) in originals)
                if (t) t.gameObject.layer = layer;
        }

        private static string Sanitize(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
#endif
