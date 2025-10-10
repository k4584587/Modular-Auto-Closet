#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace needon.Editor
{
    /// <summary>
    /// 구버전 토글에 BlendshapeToggle 컴포넌트를 자동으로 추가하는 마이그레이션 유틸리티
    /// </summary>
    [InitializeOnLoad]
    public static class BlendshapeToggleMigration
    {
        static BlendshapeToggleMigration()
        {
            // 에디터가 컴파일되고 로드될 때 실행
            EditorApplication.delayCall += OnEditorLoaded;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnEditorLoaded()
        {
            // 씬이 이미 열려있으면 체크
            if (SceneManager.GetActiveScene().isLoaded)
            {
                CheckAndAddBlendshapeToggle();
            }
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            // 씬이 열릴 때마다 체크
            EditorApplication.delayCall += CheckAndAddBlendshapeToggle;
        }

        private static void CheckAndAddBlendshapeToggle()
        {
            // 씬의 모든 AutoClosetObjectToggle 컴포넌트 찾기
            var toggleComponents = Object.FindObjectsOfType<AutoClosetObjectToggle>(true);

            foreach (var toggle in toggleComponents)
            {
                // BlendshapeToggle이 없으면 추가
                if (toggle.GetComponent<BlendshapeToggle>() == null)
                {
                    toggle.gameObject.AddComponent<BlendshapeToggle>();
                    EditorUtility.SetDirty(toggle.gameObject);

                    Debug.Log($"[Auto Migration] Added BlendshapeToggle to: {toggle.gameObject.name}", toggle.gameObject);
                }
            }
        }
    }
}
#endif
