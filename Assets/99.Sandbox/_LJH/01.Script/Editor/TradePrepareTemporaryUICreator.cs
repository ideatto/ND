#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TradePrepareTemporaryUICreator
{
    private const string SandboxRoot = "Assets/99.Sandbox/_LJH";

    [MenuItem("GameObject/ND/Trade Prepare Temporary UI", false, 10)]
    private static void Create(MenuCommand menuCommand)
    {
        var gameObject = new GameObject("TradePrepareTemporaryUI");
        GameObjectUtility.SetParentAndAlign(gameObject, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(gameObject, "Create Trade Prepare Temporary UI");

        TradePrepareTemporaryUI temporaryUI = Undo.AddComponent<TradePrepareTemporaryUI>(gameObject);
        temporaryUI.ConfigureData(
            LoadAll<TownData>(),
            LoadAll<RouteData>(),
            LoadAll<TradeItemData>(),
            LoadAll<WagonData>(),
            LoadAll<DraftAnimalData>(),
            LoadAll<MercenaryData>());

        EditorUtility.SetDirty(temporaryUI);
        Selection.activeGameObject = gameObject;
    }

    private static T[] LoadAll<T>() where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { SandboxRoot });
        Array.Sort(guids, StringComparer.Ordinal);

        var results = new List<T>(guids.Length);
        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                results.Add(asset);
            }
        }

        return results.ToArray();
    }
}
#endif
