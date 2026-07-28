/*
 * Technical Ownership
 * - Responsible Discipline: Development Tools
 *
 * Script Purpose
 * - InGame 등이 참조하는 런타임 캐논 프리팹
 *   Assets/_Project/08.Prefabs/Debug/ProjectDebugPanel.prefab 이 없을 때만 생성한다.
 * - 기존 프리팹 GUID·내부 fileID·Scene 참조를 보호하기 위해 덮어쓰지 않는다.
 * - 레거시 Assets/_Project/98.DebugTools/Prefabs/ProjectDebugCanvas.prefab 은 더 이상 생성하지 않는다.
 */
#if UNITY_EDITOR
using System.IO;
using ND.DebugTools;
using UnityEditor;
using UnityEngine;

namespace ND.DebugTools.Editor
{
    internal static class ProjectDebugPrefabCreator
    {
        private const string PrefabFolder = "Assets/_Project/08.Prefabs/Debug";
        private const string PrefabPath = PrefabFolder + "/ProjectDebugPanel.prefab";
        private const string RootObjectName = "ProjectDebugPanel";

        /// <summary>
        /// Editor 로드 시 캐논 프리팹이 없을 때만 생성한다.
        /// 이미 존재하면 Scene GUID 보호를 위해 아무 작업도 하지 않는다.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void ScheduleMissingPrefabCreation()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                EditorApplication.delayCall += CreatePrefabIfMissing;
            }
        }

        /// <summary>
        /// 메뉴에서 캐논 프리팹을 확인하거나, 없을 때만 생성한다.
        /// 기존 에셋이 있으면 덮어쓰지 않고 경로만 안내한다.
        /// </summary>
        [MenuItem("Tools/ND Debug/Create Project Debug Panel Prefab")]
        public static void CreatePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                EditorUtility.DisplayDialog(
                    "Project Debug Panel Prefab",
                    $"Canonical prefab already exists:\n{PrefabPath}\n\n" +
                    "Overwrite is refused to preserve the asset GUID and Scene references.\n" +
                    "Delete the asset manually only if a missing-prefab recreate is intentional.",
                    "OK");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                Debug.Log($"Project debug prefab already exists (overwrite refused): {PrefabPath}");
                return;
            }

            CreatePrefabAsset();
        }

        private static void CreatePrefabIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                CreatePrefabAsset();
            }
        }

        /// <summary>
        /// 캐논 경로에 ProjectDebugPanel 컴포넌트가 붙은 프리팹을 새로 생성한다.
        /// 호출 전에 대상 경로에 에셋이 없음을 확인해야 한다.
        /// </summary>
        private static void CreatePrefabAsset()
        {
            if (!Directory.Exists(PrefabFolder))
            {
                Directory.CreateDirectory(PrefabFolder);
            }

            var root = new GameObject(RootObjectName);
            try
            {
                root.AddComponent<ProjectDebugPanel>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                Debug.Log($"Project debug prefab created: {PrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
#endif
