#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

internal static class TradeSettlementPrefabGenerator
{
    private const string ReceiptSpritePath = "Assets/_Project/09.Art/04_UI/아래 도장 제거 패널.png";
    private const string StampSpritePath = "Assets/_Project/09.Art/04_UI/초록 도장.png";
    private const string FontPath = "Assets/_Project/09.Art/05_Fonts/문화재돌봄체 Bold SDF.asset";
    private const string PrefabFolder = "Assets/_Project/08.Prefabs/UI/Trade";
    private const string GeneratedArtFolder = PrefabFolder + "/Resources/TradeUI";
    private const string GeneratedReceiptPath = GeneratedArtFolder + "/TradeReceipt.png";
    private const string GeneratedStampPath = GeneratedArtFolder + "/TradeStamp.png";
    private const string GeneratedFontPath = GeneratedArtFolder + "/TradeFont.asset";
    private const string SettlementPrefabPath = PrefabFolder + "/TradeSettlementPanel.prefab";
    private const string PaymentPrefabPath = PrefabFolder + "/PaymentPanel.prefab";

    [InitializeOnLoadMethod]
    private static void GenerateAfterCompile()
    {
        EditorApplication.delayCall += GenerateIfMissing;
    }

    [MenuItem("ND/UI/Generate Trade Settlement Prefabs")]
    private static void GenerateFromMenu()
    {
        Generate(true);
    }

    private static void GenerateIfMissing()
    {
        if (!File.Exists(SettlementPrefabPath) || !File.Exists(PaymentPrefabPath))
            Generate(false);
    }

    private static void Generate(bool overwrite)
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(GeneratedArtFolder);
        Sprite receiptSprite = EnsureUiSprite(
            ReceiptSpritePath,
            GeneratedReceiptPath,
            "TradeReceipt",
            new Rect(68f, 57f, 887f, 1461f));
        Sprite stampSprite = EnsureUiSprite(
            StampSpritePath,
            GeneratedStampPath,
            "TradeStamp",
            new Rect(326f, 161f, 605f, 929f));
        EnsureAssetCopy(FontPath, GeneratedFontPath);
        TMPro.TMP_FontAsset uiFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(GeneratedFontPath);
        if (receiptSprite == null || stampSprite == null || uiFont == null)
        {
            Debug.LogError("[Trade UI] Receipt or stamp sprite could not be loaded.");
            return;
        }

        if (overwrite || !File.Exists(PaymentPrefabPath))
            CreatePaymentPrefab(receiptSprite, stampSprite, uiFont);
        if (overwrite || !File.Exists(SettlementPrefabPath))
            CreateSettlementPrefab(receiptSprite, uiFont);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Trade UI] Prefabs generated in {PrefabFolder}");
    }

    private static Sprite EnsureUiSprite(
        string sourcePath,
        string generatedPath,
        string spriteName,
        Rect opaqueRect)
    {
        if (!File.Exists(generatedPath))
        {
            if (!AssetDatabase.CopyAsset(sourcePath, generatedPath))
                throw new IOException($"Could not copy UI sprite from {sourcePath} to {generatedPath}.");
        }

        TextureImporter importer = AssetImporter.GetAtPath(generatedPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            var dataProviderFactories = new SpriteDataProviderFactories();
            dataProviderFactories.Init();
            var dataProvider = dataProviderFactories.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();
            var existingSprite = dataProvider.GetSpriteRects().FirstOrDefault(rect => rect.name == spriteName);
            dataProvider.SetSpriteRects(new[]
            {
                new SpriteRect
                {
                    name = spriteName,
                    rect = opaqueRect,
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = existingSprite != null ? existingSprite.spriteID : GUID.Generate()
                }
            });
            dataProvider.Apply();
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAllAssetsAtPath(generatedPath).OfType<Sprite>().FirstOrDefault();
    }

    private static void EnsureAssetCopy(string sourcePath, string generatedPath)
    {
        if (!File.Exists(generatedPath) && !AssetDatabase.CopyAsset(sourcePath, generatedPath))
            throw new IOException($"Could not copy asset from {sourcePath} to {generatedPath}.");
    }

    private static void CreateSettlementPrefab(Sprite receiptSprite, TMPro.TMP_FontAsset uiFont)
    {
        GameObject root = CreateRoot("TradeSettlementPanel");
        try
        {
            TradeSettlementPanelController controller = root.AddComponent<TradeSettlementPanelController>();
            SetObjectReference(controller, "receiptBackgroundSprite", receiptSprite);
            SetObjectReference(controller, "uiFont", uiFont);
            InvokeBuildView(controller);
            root.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, SettlementPrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void CreatePaymentPrefab(Sprite receiptSprite, Sprite stampSprite, TMPro.TMP_FontAsset uiFont)
    {
        GameObject root = CreateRoot("PaymentPanel");
        try
        {
            PaymentPanelController controller = root.AddComponent<PaymentPanelController>();
            SetObjectReference(controller, "receiptBackgroundSprite", receiptSprite);
            SetObjectReference(controller, "stampSprite", stampSprite);
            SetObjectReference(controller, "uiFont", uiFont);
            InvokeBuildView(controller);
            root.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PaymentPrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static GameObject CreateRoot(string objectName)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(UnityEngine.UI.Image), typeof(CanvasGroup));
        root.layer = LayerMask.NameToLayer("UI");
        return root;
    }

    private static void InvokeBuildView(MonoBehaviour controller)
    {
        MethodInfo method = controller.GetType().GetMethod("BuildViewIfNeeded",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method?.Invoke(controller, null);
        EditorUtility.SetDirty(controller);
    }

    private static void SetObjectReference(MonoBehaviour target, string propertyName, UnityEngine.Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new MissingFieldException(target.GetType().Name, propertyName);
        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] segments = folderPath.Split('/');
        string current = segments[0];
        for (int index = 1; index < segments.Length; index++)
        {
            string next = current + "/" + segments[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[index]);
            current = next;
        }
    }
}
#endif
