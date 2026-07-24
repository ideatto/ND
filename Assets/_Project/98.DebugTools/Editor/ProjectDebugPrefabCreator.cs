/*
 * Technical Ownership
 * - Responsible Discipline: Development Tools
 *
 * Script Purpose
 * - ProjectDebugPanel이 연결된 ProjectDebugCanvas.prefab을 Unity 직렬화 API로 생성한다.
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
        private const string PrefabFolder = "Assets/_Project/98.DebugTools/Prefabs";
        private const string PrefabPath = PrefabFolder + "/ProjectDebugCanvas.prefab";

        [InitializeOnLoadMethod]
        private static void ScheduleMissingPrefabCreation()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                EditorApplication.delayCall += CreatePrefabIfMissing;
            }
        }

        [MenuItem("Tools/ND Debug/Create Project Debug Canvas Prefab")]
        public static void CreatePrefab()
        {
            CreatePrefabAsset();
        }

        private static void CreatePrefabIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                CreatePrefabAsset();
            }
        }

        private static void CreatePrefabAsset()
        {
            if (!Directory.Exists(PrefabFolder))
            {
                Directory.CreateDirectory(PrefabFolder);
            }

            var root = new GameObject("ProjectDebugCanvas");
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
