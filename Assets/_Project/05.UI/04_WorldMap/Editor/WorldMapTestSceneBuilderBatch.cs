using UnityEditor;
using UnityEngine;

namespace ND.UI.WorldMap.Editor
{
    /// <summary>
    /// Batchmode에서 WorldMapTest 씬 빌더를 실행하기 위한 진입점이다.
    /// </summary>
    public static class WorldMapTestSceneBuilderBatch
    {
        public static void Run()
        {
            try
            {
                WorldMapTestSceneBuilder.Build();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WorldMap] Batch build failed: {ex}");
                EditorApplication.Exit(1);
            }
        }
    }
}
