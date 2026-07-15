#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class TradePrepareRuntimeContextPrefabGenerator
{
    private const string PrefabPath = "Assets/_Project/08.Prefabs/UI/Trade/TradePrepareRuntimeContext.prefab";

    [InitializeOnLoadMethod]
    private static void GenerateOnceAfterImport()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            EditorApplication.delayCall += Generate;
    }

    [MenuItem("ND/UI/Generate Trade Prepare Runtime Context")]
    public static void Generate()
    {
        GameObject root = new GameObject("TradePrepareRuntimeContext");
        root.AddComponent<TradePrepareRuntimeContextProvider>();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Debug.Log($"[UI] Generated {PrefabPath}");
    }
}
#endif
