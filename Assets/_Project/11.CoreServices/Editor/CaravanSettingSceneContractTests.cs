using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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

            List<TestCaravanSettingService> services =
                FindComponentsInScene<TestCaravanSettingService>(scene);
            List<CaravanOverviewEditBinding> bindings =
                FindComponentsInScene<CaravanOverviewEditBinding>(scene);

            Assert.That(services.Count, Is.EqualTo(1));
            Assert.That(bindings.Count, Is.EqualTo(1));
            Assert.That(services[0].gameObject, Is.EqualTo(bindings[0].gameObject));

            AssertBindingReferences(bindings[0], services[0]);
            AssertCatalogReferences(services[0]);
            AssertNoMissingScripts(scene);
            Assert.That(scene.isDirty, Is.False, "Contract inspection must not dirty the scene.");
        }
        finally
        {
            if (scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void AssertBindingReferences(
        CaravanOverviewEditBinding binding,
        TestCaravanSettingService service)
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

    private static void AssertCatalogReferences(TestCaravanSettingService service)
    {
        var serializedService = new SerializedObject(service);
        SerializedProperty catalog = serializedService.FindProperty("cargoCatalog");
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
