using ND.Framework;
using NUnit.Framework;
using ND.UI.InGame.TransportInventory;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerTransportInventoryConsistencyTests
{
    private GameObject managerObject;
    private PlayerMainManager manager;

    [SetUp]
    public void SetUp()
    {
        RemoveDestroyedTransportInventorySubscribers();
        if (PlayerMainManager.Instance != null)
            Object.DestroyImmediate(PlayerMainManager.Instance.gameObject);

        managerObject = new GameObject(nameof(PlayerTransportInventoryConsistencyTests));
        manager = managerObject.AddComponent<PlayerMainManager>();
        manager.SetTransportCatalogForTests(CreateCatalog());
        SetFallbackSave(SaveWithFarm(TransportInventoryFunction.MaximumLevel));
    }

    [TearDown]
    public void TearDown()
    {
        if (managerObject != null)
            Object.DestroyImmediate(managerObject);
    }

    [Test]
    public void SaveData_NewPlayer_HasSeparateTransportInventories()
    {
        var player = new ND.Framework.PlayerSaveData();

        Assert.That(player.wagonInventory, Is.Not.Null);
        Assert.That(player.draftAnimalInventory, Is.Not.Null);
        Assert.That(player.wagonInventory, Is.Empty);
        Assert.That(player.draftAnimalInventory, Is.Empty);
        Assert.That(player.homeInventory, Is.Not.SameAs(player.wagonInventory));
        Assert.That(player.homeInventory, Is.Not.SameAs(player.draftAnimalInventory));
    }

    [Test]
    public void CreateTransport_IssuesUniqueInstanceIds_AndInitializesWagonDurability()
    {
        Assert.That(manager.TryCreateWagon("Wagon_M", out OwnedWagonSaveData wagon,
            out TransportInventoryValidationFailure wagonFailure), Is.True);
        Assert.That(wagonFailure, Is.EqualTo(TransportInventoryValidationFailure.None));
        Assert.That(wagon.instanceId, Is.Not.Empty);
        Assert.That(wagon.contentId, Is.EqualTo("Wagon_M"));
        Assert.That(wagon.currentDurability, Is.EqualTo(137));

        Assert.That(manager.TryCreateDraftAnimal("Horse", out OwnedDraftAnimalSaveData first,
            out TransportInventoryValidationFailure firstFailure), Is.True);
        Assert.That(manager.TryCreateDraftAnimal("Horse", out OwnedDraftAnimalSaveData second,
            out TransportInventoryValidationFailure secondFailure), Is.True);
        Assert.That(firstFailure, Is.EqualTo(TransportInventoryValidationFailure.None));
        Assert.That(secondFailure, Is.EqualTo(TransportInventoryValidationFailure.None));
        Assert.That(first.instanceId, Is.Not.EqualTo(second.instanceId));
        Assert.That(manager.DraftAnimalInventory, Has.Count.EqualTo(2));
    }

    [Test]
    public void RewardDebugButton_DefaultPrefab_GrantsConfiguredTransports()
    {
        const string path = "Assets/_Project/08.Prefabs/Debug/TransportInventoryRewardDebugButton.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null);
        GameObject instance = Object.Instantiate(prefab);
        try
        {
            Assert.That(instance.GetComponent<TransportInventoryRewardDebugButton>().TryGrantRewards(manager), Is.True);
            Assert.That(manager.WagonInventory, Has.Count.EqualTo(2));
            Assert.That(manager.WagonInventory[0].contentId, Is.EqualTo("Wagon_M"));
            Assert.That(manager.WagonInventory[1].contentId, Is.EqualTo("Wagon_S"));
            Assert.That(manager.DraftAnimalInventory, Has.Count.EqualTo(2));
            Assert.That(manager.DraftAnimalInventory[0].contentId, Is.EqualTo("Horse"));
            Assert.That(manager.DraftAnimalInventory[0].instanceId,
                Is.Not.EqualTo(manager.DraftAnimalInventory[1].instanceId));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void Reward_ToPopup_ShowsGrantedItemsAcrossTabsAndPositionsTooltipInsideBounds()
    {
        ND.Framework.SaveData save = SaveWithFarm(1);
        SetFallbackSave(save);
        GameObject reward = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/08.Prefabs/Debug/TransportInventoryRewardDebugButton.prefab"));
        GameObject popup = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/08.Prefabs/UI/TransportInventory/TransportInventoryPopup.prefab"));
        try
        {
            TransportInventoryPopupController popupController = popup.GetComponent<TransportInventoryPopupController>();
            typeof(TransportInventoryPopupController).GetMethod("Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(popupController, null);
            // GrantRewards is the Button listener. EditMode does not execute its PlayMode Awake hookup.
            Assert.That(reward.GetComponent<TransportInventoryRewardDebugButton>().TryGrantRewards(manager), Is.True,
                "Configured reward grant should succeed.");
            Assert.That(save.player.wagonInventory, Has.Count.EqualTo(2));
            Assert.That(save.player.draftAnimalInventory, Has.Count.EqualTo(2));

            RectTransform popupRect = popup.GetComponent<RectTransform>();
            popupRect.anchorMin = popupRect.anchorMax = new Vector2(.5f, .5f);
            popupRect.sizeDelta = new Vector2(1280f, 720f);
            var provider = new TransportInventoryDataProvider(() => save, CreateCatalog);
            Assert.That(popupController.Open(provider), Is.True,
                "Farm level 1 should allow the popup to open.");
            Canvas.ForceUpdateCanvases();

            TransportInventoryPanelView wagonPanel = FindComponent<TransportInventoryPanelView>(popup, "WagonPanel");
            TransportInventoryPanelView animalPanel = FindComponent<TransportInventoryPanelView>(popup, "DraftAnimalPanel");
            TransportInventoryTooltipView tooltip = popup.GetComponentInChildren<TransportInventoryTooltipView>(true);
            typeof(TransportInventoryTooltipView).GetMethod("Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(tooltip, null);
            wagonPanel.SetTooltip(tooltip);
            animalPanel.SetTooltip(tooltip);
            Assert.That(wagonPanel.gameObject.activeSelf, Is.True);
            Assert.That(animalPanel.gameObject.activeSelf, Is.False);

            TransportInventorySlotView wagonSlot = wagonPanel.GetComponentsInChildren<TransportInventorySlotView>(false)[0];
            Assert.That(SerializedObjectReference<GameObject>(wagonSlot, "durabilityBadge").activeSelf, Is.True);
            AssertTooltip(popup, wagonSlot, "Wagon M");

            FindComponent<Button>(popup, "DraftAnimalTab").onClick.Invoke();
            Assert.That(wagonPanel.gameObject.activeSelf, Is.False);
            Assert.That(animalPanel.gameObject.activeSelf, Is.True);
            TransportInventorySlotView animalSlot = animalPanel.GetComponentsInChildren<TransportInventorySlotView>(false)[0];
            Assert.That(SerializedObjectReference<GameObject>(animalSlot, "durabilityBadge").activeSelf, Is.False);
            AssertTooltip(popup, animalSlot, "Horse");
        }
        finally
        {
            popup.GetComponent<TransportInventoryPopupController>()?.Close();
            Object.DestroyImmediate(popup);
            Object.DestroyImmediate(reward);
        }
    }

    [Test]
    public void AddAndRemoveWagon_KeepSaveListAndInstanceIndexConsistent()
    {
        OwnedWagonSaveData wagon = Wagon("wagon-instance", "Wagon_M");

        Assert.That(manager.TryAddWagon(wagon), Is.True);
        Assert.That(manager.WagonInventory, Has.Count.EqualTo(1));
        Assert.That(manager.FindWagon("wagon-instance", "Wagon_M"), Is.Not.Null);
        Assert.That(manager.FindWagon("wagon-instance", "Wagon_M"), Is.Not.SameAs(wagon));
        Assert.That(manager.FindWagon("wagon-instance", "Wagon_L"), Is.Null);

        Assert.That(manager.RemoveWagon("wagon-instance", "Wagon_L"), Is.False);
        Assert.That(manager.WagonInventory, Has.Count.EqualTo(1));
        Assert.That(manager.RemoveWagon("wagon-instance", "Wagon_M"), Is.True);
        Assert.That(manager.WagonInventory, Is.Empty);
        Assert.That(manager.FindWagon("wagon-instance", "Wagon_M"), Is.Null);
    }

    [Test]
    public void CanAdd_DoesNotMutate_AndRejectsUnknownContent()
    {
        OwnedWagonSaveData valid = Wagon("candidate", "Wagon_M");

        Assert.That(manager.CanAddWagon(valid, out TransportInventoryValidationFailure validFailure), Is.True);
        Assert.That(validFailure, Is.EqualTo(TransportInventoryValidationFailure.None));
        Assert.That(manager.WagonInventory, Is.Empty);

        Assert.That(manager.CanAddWagon(Wagon("unknown", "Missing_Wagon"), out TransportInventoryValidationFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(TransportInventoryValidationFailure.ContentUnavailable));
        Assert.That(manager.TryAddWagon(Wagon("unknown", "Missing_Wagon")), Is.False);
        Assert.That(manager.WagonInventory, Is.Empty);
    }

    [Test]
    public void NormalAcquisition_RejectsWhenFarmIsNotBuilt()
    {
        SetFallbackSave(new ND.Framework.SaveData());

        Assert.That(manager.CanAddWagon(
            Wagon("wagon-without-farm", "Wagon_M"), out TransportInventoryValidationFailure wagonFailure), Is.False);
        Assert.That(wagonFailure, Is.EqualTo(TransportInventoryValidationFailure.FarmUnavailable));
        Assert.That(manager.CanAddDraftAnimal(
            Animal("animal-without-farm", "Horse"), out TransportInventoryValidationFailure animalFailure), Is.False);
        Assert.That(animalFailure, Is.EqualTo(TransportInventoryValidationFailure.FarmUnavailable));
    }

    [Test]
    public void CanRemove_RequiresMatchingInstanceAndContent_WithoutMutation()
    {
        Assert.That(manager.TryAddDraftAnimal(Animal("animal-instance", "Horse")), Is.True);

        Assert.That(manager.CanRemoveDraftAnimal("animal-instance", "Donkey", out TransportInventoryValidationFailure failure), Is.False);
        Assert.That(failure, Is.EqualTo(TransportInventoryValidationFailure.ItemNotFound));
        Assert.That(manager.DraftAnimalInventory, Has.Count.EqualTo(1));

        Assert.That(manager.CanRemoveDraftAnimal("animal-instance", "Horse", out failure), Is.True);
        Assert.That(failure, Is.EqualTo(TransportInventoryValidationFailure.None));
        Assert.That(manager.DraftAnimalInventory, Has.Count.EqualTo(1));
    }

    [Test]
    public void SameInstanceId_CannotExistAcrossWagonAndDraftAnimalInventories()
    {
        Assert.That(manager.TryAddWagon(Wagon("shared-instance", "Wagon_M")), Is.True);

        bool added = manager.TryAddDraftAnimal(Animal("shared-instance", "Horse"));

        Assert.That(added, Is.False);
        Assert.That(manager.WagonInventory, Has.Count.EqualTo(1));
        Assert.That(manager.DraftAnimalInventory, Is.Empty);
    }

    [Test]
    public void FailedDuplicateAdd_DoesNotMutateExistingInventory()
    {
        OwnedDraftAnimalSaveData original = Animal("animal-instance", "Horse");
        Assert.That(manager.TryAddDraftAnimal(original), Is.True);

        bool added = manager.TryAddDraftAnimal(Animal("animal-instance", "Donkey"));

        Assert.That(added, Is.False);
        Assert.That(manager.DraftAnimalInventory, Has.Count.EqualTo(1));
        Assert.That(manager.FindDraftAnimal("animal-instance", "Horse"), Is.Not.Null);
        Assert.That(manager.FindDraftAnimal("animal-instance", "Horse"), Is.Not.SameAs(original));
        Assert.That(manager.FindDraftAnimal("animal-instance", "Donkey"), Is.Null);
    }

    [Test]
    public void WagonAcquisition_RejectsCapacityOverflow()
    {
        for (int i = 0; i < PlayerMainManager.WagonInventoryCapacity; i++)
            Assert.That(manager.TryAddWagon(Wagon($"wagon-{i}", "Wagon_M")), Is.True);

        Assert.That(manager.TryAddWagon(Wagon("wagon-overflow-add", "Wagon_M")), Is.False);
        Assert.That(manager.WagonInventory, Has.Count.EqualTo(PlayerMainManager.WagonInventoryCapacity));
    }

    [Test]
    public void DraftAnimalAcquisition_RejectsCapacityOverflow()
    {
        for (int i = 0; i < PlayerMainManager.DraftAnimalInventoryCapacity; i++)
            Assert.That(manager.TryAddDraftAnimal(Animal($"animal-{i}", "Horse")), Is.True);

        Assert.That(manager.TryAddDraftAnimal(Animal("animal-overflow-add", "Horse")), Is.False);
        Assert.That(manager.DraftAnimalInventory,
            Has.Count.EqualTo(PlayerMainManager.DraftAnimalInventoryCapacity));
    }

    [Test]
    public void Add_RejectsInstanceAlreadyEquippedByCaravan()
    {
        var save = new ND.Framework.SaveData
        {
            caravans = new List<ND.Framework.CaravanSaveData>
            {
                new ND.Framework.CaravanSaveData
                {
                    wagon = new ND.Framework.WagonSaveData { instanceId = "equipped-instance", wagonName = "Wagon_M" }
                }
            }
        };
        SetFallbackSave(save);

        Assert.That(manager.TryAddWagon(Wagon("equipped-instance", "Wagon_M")), Is.False);
        Assert.That(manager.WagonInventory, Is.Empty);
    }

    [Test]
    public void Normalize_RemovesInvalidAndDuplicateTransportInventoryEntries()
    {
        var save = new ND.Framework.SaveData
        {
            caravans = new List<ND.Framework.CaravanSaveData>
            {
                new ND.Framework.CaravanSaveData
                {
                    wagon = new ND.Framework.WagonSaveData { instanceId = "equipped", wagonName = "Wagon_M" }
                }
            }
        };
        save.player.wagonInventory = new List<OwnedWagonSaveData>
        {
            Wagon("owned", "Wagon_M"),
            Wagon("owned", "Wagon_L"),
            Wagon("equipped", "Wagon_M"),
            Wagon("", "Wagon_M")
        };
        save.player.draftAnimalInventory = new List<OwnedDraftAnimalSaveData>
        {
            Animal("animal", "Horse"),
            Animal("owned", "Horse")
        };

        Assert.That(JsonSaveService.NormalizeData(save), Is.True);

        Assert.That(save.player.wagonInventory, Has.Count.EqualTo(2));
        Assert.That(save.player.wagonInventory.Exists(value => value.instanceId == "owned"), Is.True);
        Assert.That(save.player.wagonInventory.Exists(value => value.instanceId == "equipped"), Is.True);
        Assert.That(save.player.wagonInventory[0].contentId, Is.EqualTo("Wagon_M"));
        Assert.That(save.player.draftAnimalInventory, Has.Count.EqualTo(1));
        Assert.That(save.player.draftAnimalInventory[0].instanceId, Is.EqualTo("animal"));
    }

    private void SetFallbackSave(ND.Framework.SaveData save)
    {
        FieldInfo field = typeof(PlayerMainManager).GetField("fallbackSave", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(manager, save);
    }

    private static void RemoveDestroyedTransportInventorySubscribers()
    {
        FieldInfo field = typeof(FrameworkEvents).GetField("TransportInventoryChanged",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        field?.SetValue(null, null);
    }

    private static OwnedWagonSaveData Wagon(string instanceId, string contentId)
    {
        return new OwnedWagonSaveData
        {
            instanceId = instanceId,
            contentId = contentId,
            currentDurability = 100
        };
    }

    private static ND.Framework.SaveData SaveWithFarm(int level)
    {
        var save = new ND.Framework.SaveData();
        save.player.villageBuildings.Add(new VillageBuildingSaveData
        {
            displayName = TransportInventoryFunction.BuildingDisplayName,
            level = level
        });
        return save;
    }

    private static OwnedDraftAnimalSaveData Animal(string instanceId, string contentId)
    {
        return new OwnedDraftAnimalSaveData
        {
            instanceId = instanceId,
            contentId = contentId
        };
    }

    private static ISharedGameDataProvider CreateCatalog()
    {
        return new SharedGameDataView(
            new Dictionary<string, SharedTownDefinition>(),
            new Dictionary<string, SharedMarketDefinition>(),
            new Dictionary<string, SharedTradeItemDefinition>(),
            new Dictionary<string, SharedWagonDefinition>
            {
                ["Wagon_M"] = new SharedWagonDefinition
                    { Id = "Wagon_M", DisplayName = "Wagon M", Description = "Medium wagon", BaseBuyPrice = 1000, MaxDurability = 137 },
                ["Wagon_S"] = new SharedWagonDefinition
                    { Id = "Wagon_S", DisplayName = "Wagon S", Description = "Small wagon", BaseBuyPrice = 700, MaxDurability = 91 },
                ["Wagon_L"] = new SharedWagonDefinition { Id = "Wagon_L" }
            },
            new Dictionary<string, SharedDraftAnimalDefinition>
            {
                ["Horse"] = new SharedDraftAnimalDefinition
                    { Id = "Horse", DisplayName = "Horse", Description = "Draft horse", BaseBuyPrice = 500 },
                ["Donkey"] = new SharedDraftAnimalDefinition { Id = "Donkey" }
            },
            new Dictionary<string, SharedRouteDefinition>());
    }

    private static T FindComponent<T>(GameObject root, string objectName) where T : Component
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == objectName) return child.GetComponent<T>();
        return null;
    }

    private static T SerializedObjectReference<T>(Object target, string propertyName) where T : Object
    {
        return new SerializedObject(target).FindProperty(propertyName).objectReferenceValue as T;
    }

    private static void AssertTooltip(GameObject popup, TransportInventorySlotView slot, string expectedName)
    {
        TransportInventoryTooltipView tooltip = popup.GetComponentInChildren<TransportInventoryTooltipView>(true);
        slot.OnPointerEnter(null);
        Assert.That(tooltip.gameObject.activeSelf, Is.True, "Hover should show the tooltip.");
        TMP_Text nameText = SerializedObjectReference<TMP_Text>(tooltip, "displayNameText");
        Assert.That(nameText.text, Is.EqualTo(expectedName));

        RectTransform bounds = SerializedObjectReference<RectTransform>(tooltip, "bounds");
        RectTransform rect = tooltip.transform as RectTransform;
        Assert.That(rect.anchoredPosition.x, Is.InRange(bounds.rect.xMin, bounds.rect.xMax - rect.rect.width));
        Assert.That(rect.anchoredPosition.y, Is.InRange(bounds.rect.yMin + rect.rect.height, bounds.rect.yMax));
    }
}
