using System.Collections.Generic;
using System.Reflection;
using ND.Framework;
using NUnit.Framework;
using UnityEngine;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;
using FrameworkCargoEntrySaveData = ND.Framework.CargoEntrySaveData;
using FrameworkSaveData = ND.Framework.SaveData;
using FrameworkTradeItemSaveData = ND.Framework.TradeItemSaveData;

public sealed class TestCaravanSettingServiceIntegrationTests
{
    private GameObject host;
    private TestCaravanSettingService service;
    private readonly List<TradeItemData> createdItems = new List<TradeItemData>();

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("TestCaravanSettingServiceIntegrationTests");
        service = host.AddComponent<TestCaravanSettingService>();
    }

    [TearDown]
    public void TearDown()
    {
        for (int index = 0; index < createdItems.Count; index++)
            Object.DestroyImmediate(createdItems[index]);
        createdItems.Clear();
        Object.DestroyImmediate(host);
    }

    [Test]
    public void CargoDraft_ForOneCaravan_DoesNotOverwriteAnotherCaravansSavedCargo()
    {
        TradeItemData apple = CreateCatalogItem("apple", 2f);
        TradeItemData cloth = CreateCatalogItem("cloth", 1f);
        FrameworkSaveData save = CreateSave(
            Caravan("caravan-a", JourneyState.Prepare),
            Caravan("caravan-b", JourneyState.Prepare, Entry("cloth", 3, 40L)));
        service.SetSaveDataForTests(save);
        service.SetCargoCatalogForTests(apple, cloth);

        CaravanLoadSettingCommandResult result = ExecuteCargo("caravan-a", "apple", 2);

        Assert.That(result.succeeded, Is.True);
        Assert.That(service.GetLoadSetting("caravan-a").plannedItems[0].itemId, Is.EqualTo("apple"));
        Assert.That(service.GetLoadSetting("caravan-a").plannedItems[0].quantity, Is.EqualTo(2));
        Assert.That(service.GetLoadSetting("caravan-b").plannedItems[0].itemId, Is.EqualTo("cloth"));
        Assert.That(service.GetLoadSetting("caravan-b").plannedItems[0].quantity, Is.EqualTo(3));
    }

    [Test]
    public void SavedCargoChange_InvalidatesExistingDraftAndReturnsNewSavedBaseline()
    {
        TradeItemData apple = CreateCatalogItem("apple", 2f);
        FrameworkCaravanSaveData caravan = Caravan(
            "caravan-a",
            JourneyState.Prepare,
            Entry("apple", 1, 50L));
        FrameworkSaveData save = CreateSave(caravan);
        service.SetSaveDataForTests(save);
        service.SetCargoCatalogForTests(apple);
        Assert.That(ExecuteCargo("caravan-a", "apple", 2).succeeded, Is.True);

        caravan.cargo[0].quantity = 4;
        CaravanLoadSettingViewData refreshed = service.GetLoadSetting("caravan-a");

        Assert.That(refreshed.plannedItems, Has.Length.EqualTo(1));
        Assert.That(refreshed.plannedItems[0].quantity, Is.EqualTo(4));
    }

    [Test]
    public void TravelingCaravan_RejectsCompositionAndCargoCommands()
    {
        TradeItemData apple = CreateCatalogItem("apple", 1f);
        service.SetSaveDataForTests(CreateSave(Caravan("caravan-a", JourneyState.Traveling)));
        service.SetCargoCatalogForTests(apple);
        var settingDraft = new CaravanSettingDraft
        {
            caravanId = "caravan-a",
            selectedWagonInstanceId = "Wagon_M"
        };
        settingDraft.SelectAnimal("Horse");

        CaravanSettingCommandResult settingResult = service.Execute(settingDraft);
        CaravanLoadSettingCommandResult cargoResult = ExecuteCargo("caravan-a", "apple", 1);

        Assert.That(settingResult.succeeded, Is.False);
        Assert.That(settingResult.errorCode, Is.EqualTo(CaravanSettingFailureCodes.CaravanNotEditable));
        Assert.That(cargoResult.succeeded, Is.False);
        Assert.That(cargoResult.errorCode, Is.EqualTo(CaravanLoadSettingFailureCodes.CaravanNotEditable));
    }

    [Test]
    public void CargoOverCapacity_IsRejectedWithoutReplacingPreviousDraft()
    {
        TradeItemData heavyItem = CreateCatalogItem("ore", 10f);
        service.SetSaveDataForTests(CreateSave(Caravan("caravan-a", JourneyState.Prepare)));
        service.SetCargoCatalogForTests(heavyItem);
        Assert.That(ExecuteCargo("caravan-a", "ore", 2).succeeded, Is.True);

        CaravanLoadSettingCommandResult rejected = ExecuteCargo("caravan-a", "ore", 4);
        CaravanLoadSettingViewData retained = service.GetLoadSetting("caravan-a");

        Assert.That(rejected.succeeded, Is.False);
        Assert.That(rejected.errorCode, Is.EqualTo(CaravanLoadSettingFailureCodes.CargoCapacityExceeded));
        Assert.That(retained.plannedItems, Has.Length.EqualTo(1));
        Assert.That(retained.plannedItems[0].quantity, Is.EqualTo(2));
    }

    private CaravanLoadSettingCommandResult ExecuteCargo(string caravanId, string itemId, int quantity)
    {
        var draft = new CaravanLoadSettingDraft { caravanId = caravanId };
        draft.items.Add(new CaravanLoadItemDraft { itemId = itemId, quantity = quantity });
        return ((ICaravanLoadSettingCommand)service).Execute(draft);
    }

    private TradeItemData CreateCatalogItem(string itemId, float weight)
    {
        TradeItemData item = ScriptableObject.CreateInstance<TradeItemData>();
        SetPrivateField(item, "itemId", itemId);
        SetPrivateField(item, "displayName", itemId);
        SetPrivateField(item, "weight", weight);
        SetPrivateField(item, "baseBuyPrice", 10L);
        SetPrivateField(item, "baseSellPrice", 5L);
        SetPrivateField(item, "maxCount", 99);
        createdItems.Add(item);
        return item;
    }

    private static void SetPrivateField<T>(TradeItemData item, string fieldName, T value)
    {
        typeof(TradeItemData)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(item, value);
    }

    private static FrameworkSaveData CreateSave(params FrameworkCaravanSaveData[] caravans)
    {
        var save = new FrameworkSaveData();
        save.caravans.Clear();
        save.caravans.AddRange(caravans);
        return save;
    }

    private static FrameworkCaravanSaveData Caravan(
        string caravanId,
        JourneyState state,
        params FrameworkCargoEntrySaveData[] cargo)
    {
        var caravan = new FrameworkCaravanSaveData
        {
            caravanId = caravanId,
            currentTownId = "town-a",
            state = state
        };
        caravan.cargo.Clear();
        caravan.wagon = new ND.Framework.WagonSaveData
        {
            instanceId = "Wagon_M",
            wagonName = "Wagon_M",
            maxLoad = 30f,
            minAnimals = 1,
            maxAnimals = 2,
            inventorySlotCount = 5
        };
        caravan.animals.Add(new ND.Framework.AnimalSaveData
        {
            instanceId = "Horse",
            animalName = "Horse",
            animalType = DraftAnimalType.Horse
        });
        caravan.cargo.AddRange(cargo);
        return caravan;
    }

    private static FrameworkCargoEntrySaveData Entry(string itemId, int quantity, long price)
    {
        return new FrameworkCargoEntrySaveData
        {
            item = new FrameworkTradeItemSaveData
            {
                itemId = itemId,
                itemName = itemId,
                weight = 1f,
                basePrice = price
            },
            quantity = quantity
        };
    }
}
