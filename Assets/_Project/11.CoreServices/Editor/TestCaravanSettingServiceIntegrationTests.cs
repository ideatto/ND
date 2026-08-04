using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ND.Framework;
using NUnit.Framework;
using UnityEngine;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;
using FrameworkCargoEntrySaveData = ND.Framework.CargoEntrySaveData;
using FrameworkSaveData = ND.Framework.SaveData;
using FrameworkTradeItemSaveData = ND.Framework.TradeItemSaveData;

/// <summary>임시 S3/S4 서비스가 Framework SaveData 경계를 침범하지 않고 Caravan별 초안을 격리하는지 검증한다.</summary>
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
    public void SavedCargoOutsideCurrentMarketCatalog_IsAcceptedAsDraftBaseline()
    {
        TradeItemData apple = CreateCatalogItem("apple", 1f);
        FrameworkCaravanSaveData caravan = Caravan(
            "caravan-a",
            JourneyState.Prepare,
            Entry("ore", 3, 10L));
        service.SetSaveDataForTests(CreateSave(caravan));
        service.SetCargoCatalogForTests(apple);
        var draft = new CaravanLoadSettingDraft { caravanId = "caravan-a" };
        draft.items.Add(new CaravanLoadItemDraft { itemId = "ore", quantity = 3 });
        draft.items.Add(new CaravanLoadItemDraft { itemId = "apple", quantity = 2 });

        CaravanLoadSettingCommandResult result =
            ((ICaravanLoadSettingCommand)service).Execute(draft);

        Assert.That(result.succeeded, Is.True);
        CaravanLoadSettingViewData planned = service.GetLoadSetting("caravan-a");
        Assert.That(planned.plannedItems
            .Select(item => (item.itemId, item.quantity)),
            Is.EquivalentTo(new[] { ("ore", 3), ("apple", 2) }));
        Assert.That(planned.currentLoad, Is.EqualTo(5f));
    }

    [Test]
    public void QuantityAddedToOffCatalogSavedCargo_IsRejectedInKorean()
    {
        FrameworkCaravanSaveData caravan = Caravan(
            "caravan-a",
            JourneyState.Prepare,
            Entry("ore", 3, 10L));
        service.SetSaveDataForTests(CreateSave(caravan));
        service.SetCargoCatalogForTests();

        CaravanLoadSettingCommandResult result = ExecuteCargo("caravan-a", "ore", 4);

        Assert.That(result.succeeded, Is.False);
        Assert.That(result.errorCode, Is.EqualTo(CaravanLoadSettingFailureCodes.ItemUnavailable));
        Assert.That(result.userMessage, Is.EqualTo("추가하려는 화물을 현재 마을의 상점에서 구매할 수 없습니다."));
    }

    [Test]
    public void StaleSelectedCaravanId_DoesNotChangeRequestedCaravanCargoSnapshot()
    {
        TradeItemData wood = CreateCatalogItem("wood", 0.1f);
        TradeItemData ore = CreateCatalogItem("ore", 0.42f);
        TradeItemData apple = CreateCatalogItem("apple", 1f);
        FrameworkSaveData save = CreateSave(
            Caravan(
                "caravan-1",
                JourneyState.Prepare,
                EntryWithWeight("wood", 8, 10, 0.1f),
                EntryWithWeight("ore", 69, 10, 0.42f)),
            Caravan(
                "caravan-2",
                JourneyState.Prepare,
                EntryWithWeight("apple", 4, 10, 1f)));
        save.selectedCaravanId = "caravan-2";
        service.SetSaveDataForTests(save);
        service.SetCargoCatalogForTests(wood, ore, apple);

        CaravanLoadSettingViewData first = service.GetLoadSetting("caravan-1");
        CaravanLoadSettingViewData second = service.GetLoadSetting("caravan-2");
        CaravanLoadSettingViewData firstAgain = service.GetLoadSetting("caravan-1");

        Assert.That(first.caravanId, Is.EqualTo("caravan-1"));
        Assert.That(first.plannedItems.Select(item => item.itemId),
            Is.EquivalentTo(new[] { "wood", "ore" }));
        Assert.That(first.currentLoad, Is.EqualTo(29.78f).Within(0.001f));
        Assert.That(first.usedInventorySlotCount, Is.EqualTo(2));
        Assert.That(first.maxInventorySlotCount, Is.EqualTo(5));
        Assert.That(second.plannedItems, Has.Length.EqualTo(1));
        Assert.That(second.plannedItems[0].itemId, Is.EqualTo("apple"));
        Assert.That(firstAgain.plannedItems.Select(item => item.itemId),
            Is.EquivalentTo(new[] { "wood", "ore" }));
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
    public void TravelingCaravan_ReadsCommittedCargoAndIgnoresPrepareDraft()
    {
        TradeItemData apple = CreateCatalogItem("apple", 1f);
        TradeItemData cloth = CreateCatalogItem("cloth", 2f);
        FrameworkCaravanSaveData caravan = Caravan(
            "caravan-a",
            JourneyState.Prepare,
            Entry("apple", 2, 10));
        service.SetSaveDataForTests(CreateSave(caravan));
        service.SetCargoCatalogForTests(apple, cloth);
        Assert.That(ExecuteCargo("caravan-a", "cloth", 3).succeeded, Is.True);

        caravan.state = JourneyState.Traveling;
        CaravanLoadSettingViewData traveling = service.GetLoadSetting("caravan-a");

        Assert.That(traveling.canEdit, Is.False);
        Assert.That(traveling.plannedItems, Has.Length.EqualTo(1));
        Assert.That(traveling.plannedItems[0].itemId, Is.EqualTo("apple"));
        Assert.That(traveling.plannedItems[0].quantity, Is.EqualTo(2));
        Assert.That(traveling.currentLoad, Is.EqualTo(2f));
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

    private static FrameworkCargoEntrySaveData EntryWithWeight(
        string itemId,
        int quantity,
        long price,
        float weight)
    {
        FrameworkCargoEntrySaveData entry = Entry(itemId, quantity, price);
        entry.item.weight = weight;
        return entry;
    }

}
