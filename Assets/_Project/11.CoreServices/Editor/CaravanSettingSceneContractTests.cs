using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ND.UI.InGame.TransportInventory;

public sealed class CaravanSettingSceneContractTests
{
    private const string InGameScenePath = "Assets/_Project/07.Scenes/04_InGame/InGame.unity";
    private const string InGameTestScenePath = "Assets/_Project/07.Scenes/04_InGame/InGame_Test.unity";

    [TestCase(InGameScenePath)]
    [TestCase(InGameTestScenePath)]
    public void Scene_HasCompleteCaravanSettingConnectionContract(string scenePath)
    {
        Assert.That(
            AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath),
            Is.Not.Null,
            $"Scene asset is missing: {scenePath}");

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);
            Assert.That(scene.isDirty, Is.False, "Opening the scene must not mutate it.");

            List<CaravanOverviewEditBinding> bindings =
                FindComponentsInScene<CaravanOverviewEditBinding>(scene);
            Assert.That(bindings.Count, Is.EqualTo(1));

            if (scenePath == InGameScenePath)
                AssertProductionContract(scene, bindings[0]);
            else
                AssertTestContract(scene, bindings[0]);
            AssertNoMissingScripts(scene);
            Assert.That(scene.isDirty, Is.False, "Contract inspection must not dirty the scene.");
        }
        finally
        {
            if (scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void AssertProductionContract(Scene scene, CaravanOverviewEditBinding binding)
    {
        List<TestCaravanSettingService> temporaryServices =
            FindComponentsInScene<TestCaravanSettingService>(scene);
        List<CaravanSettingRuntimeBridge> bridges =
            FindComponentsInScene<CaravanSettingRuntimeBridge>(scene);
        List<TransportInventoryPopupController> popups =
            FindComponentsInScene<TransportInventoryPopupController>(scene);
        List<TransportInventoryMainUiEntry> entries =
            FindComponentsInScene<TransportInventoryMainUiEntry>(scene);
        List<TransportInventoryRewardDebugButton> rewardButtons =
            FindComponentsInScene<TransportInventoryRewardDebugButton>(scene);

        Assert.That(temporaryServices, Is.Empty);
        Assert.That(bridges.Count, Is.EqualTo(1));
        Assert.That(popups.Count, Is.EqualTo(1));
        Assert.That(entries.Count, Is.EqualTo(1));
        Assert.That(rewardButtons.Count, Is.EqualTo(1));
        Assert.That(bridges[0].gameObject, Is.EqualTo(binding.gameObject));
        AssertBindingReferences(binding, bridges[0]);
        AssertCatalogReferences(new SerializedObject(bridges[0]), "tradeItemAssets", true);

        Assert.That(popups[0].gameObject.activeSelf, Is.False, "Popup must start closed.");
        Assert.That(popups[0].GetComponentInParent<Canvas>(true), Is.Not.Null);
        Assert.That(popups[0].GetComponentInParent<Canvas>(true).name, Is.EqualTo("MainUICanvas"));

        List<CaravanOverviewPresenter> presenters =
            FindComponentsInScene<CaravanOverviewPresenter>(scene);
        List<TreadmillPanel> treadmillPanels = FindComponentsInScene<TreadmillPanel>(scene);
        Assert.That(presenters.Count, Is.EqualTo(1));
        Assert.That(treadmillPanels.Count, Is.EqualTo(1));
        AssertObjectReference(
            new SerializedObject(presenters[0]),
            "treadmillPanel",
            treadmillPanels[0]);

        var entry = new SerializedObject(entries[0]);
        AssertObjectReference(entry, "buildingListPanel");
        AssertObjectReference(entry, "popup", popups[0]);
    }

    private static void AssertTestContract(Scene scene, CaravanOverviewEditBinding binding)
    {
        List<TestCaravanSettingService> services =
            FindComponentsInScene<TestCaravanSettingService>(scene);
        Assert.That(services.Count, Is.EqualTo(1));
        Assert.That(services[0].gameObject, Is.EqualTo(binding.gameObject));
        AssertBindingReferences(binding, services[0]);
        AssertCatalogReferences(new SerializedObject(services[0]), "cargoCatalog", false);
    }

    private static void AssertBindingReferences(
        CaravanOverviewEditBinding binding,
        Object service)
    {
        var serializedBinding = new SerializedObject(binding);
        AssertObjectReference(serializedBinding, "overviewPresenter");
        AssertObjectReference(serializedBinding, "tradePrepareUi");
        AssertObjectReference(serializedBinding, "noticeUI");
        AssertObjectReference(serializedBinding, "settingProviderBehaviour", service);
        AssertObjectReference(serializedBinding, "settingCommandBehaviour", service);
        AssertObjectReference(serializedBinding, "loadSettingProviderBehaviour", service);
        AssertObjectReference(serializedBinding, "loadSettingCommandBehaviour", service);
    }

    private static void AssertCatalogReferences(
        SerializedObject serializedService,
        string propertyName,
        bool requireTransportInventoryItems)
    {
        SerializedProperty catalog = serializedService.FindProperty(propertyName);
        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.isArray, Is.True);
        Assert.That(catalog.arraySize, Is.GreaterThan(0));

        var itemIds = new HashSet<string>();
        for (int index = 0; index < catalog.arraySize; index++)
        {
            TradeItemData item = catalog.GetArrayElementAtIndex(index).objectReferenceValue
                as TradeItemData;
            Assert.That(item, Is.Not.Null, $"cargoCatalog[{index}] is missing.");
            Assert.That(item.ItemId, Is.Not.Empty, $"cargoCatalog[{index}] has no item ID.");
            Assert.That(
                itemIds.Add(item.ItemId),
                Is.True,
                $"cargoCatalog contains duplicate item ID '{item.ItemId}'.");
        }

        if (requireTransportInventoryItems)
        {
            Assert.That(itemIds, Does.Contain("Logs"));
            Assert.That(itemIds, Does.Contain("Stone"));
        }
    }

    private static void AssertNoMissingScripts(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            Transform[] transforms = roots[index].GetComponentsInChildren<Transform>(true);
            for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                GameObject target = transforms[transformIndex].gameObject;
                Assert.That(
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target),
                    Is.Zero,
                    $"Missing script found on '{GetHierarchyPath(target.transform)}'.");
            }
        }
    }

    private static List<T> FindComponentsInScene<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
            result.AddRange(roots[index].GetComponentsInChildren<T>(true));
        return result;
    }

    private static void AssertObjectReference(
        SerializedObject owner,
        string propertyName,
        Object expected = null)
    {
        SerializedProperty property = owner.FindProperty(propertyName);
        Assert.That(property, Is.Not.Null, $"Serialized property '{propertyName}' is missing.");
        Assert.That(
            property.objectReferenceValue,
            Is.Not.Null,
            $"Serialized reference '{propertyName}' is not assigned.");
        if (expected != null)
            Assert.That(property.objectReferenceValue, Is.EqualTo(expected));
    }

    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}
