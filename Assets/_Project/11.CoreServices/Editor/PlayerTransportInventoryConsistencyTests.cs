using ND.Framework;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public sealed class PlayerTransportInventoryConsistencyTests
{
    private GameObject managerObject;
    private PlayerMainManager manager;

    [SetUp]
    public void SetUp()
    {
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
    public void NormalAcquisition_RespectsCapacity_ButReturnAllowsOverflow()
    {
        for (int i = 0; i < PlayerMainManager.WagonInventoryCapacity; i++)
            Assert.That(manager.TryAddWagon(Wagon($"wagon-{i}", "Wagon_M")), Is.True);

        Assert.That(manager.TryAddWagon(Wagon("wagon-overflow-add", "Wagon_M")), Is.False);
        Assert.That(manager.WagonInventory, Has.Count.EqualTo(PlayerMainManager.WagonInventoryCapacity));

        OwnedWagonSaveData returned = Wagon("wagon-overflow-return", "Wagon_M");
        Assert.That(manager.ReturnWagon(returned), Is.True);
        Assert.That(manager.WagonInventory, Has.Count.EqualTo(PlayerMainManager.WagonInventoryCapacity + 1));
        Assert.That(manager.FindWagon(returned.instanceId, returned.contentId), Is.Not.Null);
        Assert.That(manager.FindWagon(returned.instanceId, returned.contentId), Is.Not.SameAs(returned));
    }

    [Test]
    public void DraftAnimalReturn_AllowsOverflow_AndRemainsIndexed()
    {
        for (int i = 0; i < PlayerMainManager.DraftAnimalInventoryCapacity; i++)
            Assert.That(manager.TryAddDraftAnimal(Animal($"animal-{i}", "Horse")), Is.True);

        Assert.That(manager.TryAddDraftAnimal(Animal("animal-overflow-add", "Horse")), Is.False);

        OwnedDraftAnimalSaveData returned = Animal("animal-overflow-return", "Horse");
        Assert.That(manager.ReturnDraftAnimal(returned), Is.True);
        Assert.That(manager.DraftAnimalInventory,
            Has.Count.EqualTo(PlayerMainManager.DraftAnimalInventoryCapacity + 1));
        Assert.That(manager.FindDraftAnimal(returned.instanceId, returned.contentId), Is.Not.Null);
        Assert.That(manager.FindDraftAnimal(returned.instanceId, returned.contentId), Is.Not.SameAs(returned));
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
                ["Wagon_M"] = new SharedWagonDefinition { Id = "Wagon_M" },
                ["Wagon_L"] = new SharedWagonDefinition { Id = "Wagon_L" }
            },
            new Dictionary<string, SharedDraftAnimalDefinition>
            {
                ["Horse"] = new SharedDraftAnimalDefinition { Id = "Horse" },
                ["Donkey"] = new SharedDraftAnimalDefinition { Id = "Donkey" }
            },
            new Dictionary<string, SharedRouteDefinition>());
    }
}
