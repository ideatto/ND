using System;
using System.Collections.Generic;
using ND.Framework;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BakeryProductionServiceTests
{
    private sealed class FakeTime : IGameTimeProvider { public DateTime CurrentUtc { get; set; } }
    private sealed class SuccessfulSaveService : ISaveService
    {
        public bool HasSaveData() => true;
        public ND.Framework.SaveData CreateNewGameData() => new ND.Framework.SaveData();
        public ND.Framework.SaveData Load() => new ND.Framework.SaveData();
        public SaveResult Save(ND.Framework.SaveData data) => SaveResult.Success();
        public void ResetSaveData() { }
    }

    private BakeryProductionData config;
    private FakeTime time;
    private ND.Framework.SaveData save;
    private BakeryProductionService service;

    [SetUp]
    public void SetUp()
    {
        config = Resources.Load<BakeryProductionData>(BakeryProductionData.ResourceName);
        Assert.That(config, Is.Not.Null);
        Assert.That(config.Validate(out string error), Is.True, error);
        time = new FakeTime { CurrentUtc = new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc) };
        save = new ND.Framework.SaveData();
        save.player.bakeryProduction = new BakeryProductionSaveData();
        service = new BakeryProductionService(config, time, () => save,
            new SuccessfulSaveService(), CreateCatalog);
    }

    [Test]
    public void FirstBuildStartsEmptyAndProducesAfterSixtySeconds()
    {
        AddBuilding(config.BuildingDisplayName, 1);
        Assert.That(service.TryStageBuildingLevelChange(save, 1, out string error), Is.True, error);
        Assert.That(save.player.bakeryProduction.storedBreadCount, Is.Zero);

        DateTime evaluation = time.CurrentUtc.AddSeconds(60);
        var context = new OfflineRestoreContext(time.CurrentUtc, evaluation, evaluation,
            evaluation - time.CurrentUtc, false, false);
        Assert.That(service.RestoreOffline(save, context).Changed, Is.True);
        Assert.That(save.player.bakeryProduction.storedBreadCount, Is.EqualTo(1));
    }

    [Test]
    public void LevelTwoOfflineProductionStopsAtTen()
    {
        AddBuilding(config.BuildingDisplayName, 2);
        service.TryStageBuildingLevelChange(save, 2, out _);
        DateTime evaluation = time.CurrentUtc.AddHours(2);
        var context = new OfflineRestoreContext(time.CurrentUtc, evaluation, evaluation,
            evaluation - time.CurrentUtc, false, false);
        service.RestoreOffline(save, context);
        Assert.That(save.player.bakeryProduction.storedBreadCount, Is.EqualTo(10));
        Assert.That(save.player.bakeryProduction.nextProductionUtcTicks, Is.Zero);
    }

    [Test]
    public void CollectionUsesExistingStackThenOneFreeSlotAndLeavesRemainder()
    {
        AddBuilding(config.BuildingDisplayName, 5);
        AddBuilding(WarehouseFunction.BuildingDisplayName, 1);
        save.player.bakeryProduction.initialized = true;
        save.player.bakeryProduction.storedBreadContentId = config.BreadContentId;
        save.player.bakeryProduction.storedBreadCount = 40;
        save.player.homeInventory.Add(Entry("Bread", 35, 0));
        for (int index = 0; index < 9; index++)
            save.player.homeInventory.Add(Entry("Other" + index, 1, 10));

        BakeryCollectionResult result = service.Collect();

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.CollectedCount, Is.EqualTo(5));
        Assert.That(save.player.bakeryProduction.storedBreadCount, Is.EqualTo(35));
        Assert.That(save.player.homeInventory.Count, Is.EqualTo(10));
        Assert.That(save.player.homeInventory.FindAll(e => e.item.itemId == "Bread")[0].quantity,
            Is.EqualTo(40));
        Assert.That(save.player.homeInventory.FindAll(e => e.item.itemId == "Bread").Count,
            Is.EqualTo(1));
    }

    [Test]
    public void CollectionRequiresWarehouse()
    {
        AddBuilding(config.BuildingDisplayName, 1);
        save.player.bakeryProduction.storedBreadCount = 1;
        BakeryCollectionResult result = service.Collect();
        Assert.That(result.FailureReason, Is.EqualTo(BakeryCollectionFailureReason.WarehouseUnavailable));
    }

    [Test]
    public void PartialCollectionMovesOnlyRequestedQuantity()
    {
        AddBuilding(config.BuildingDisplayName, 1);
        AddBuilding(WarehouseFunction.BuildingDisplayName, 1);
        save.player.bakeryProduction.initialized = true;
        save.player.bakeryProduction.storedBreadContentId = config.BreadContentId;
        save.player.bakeryProduction.storedBreadCount = 5;

        BakeryCollectionResult result = service.Collect(2);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.CollectedCount, Is.EqualTo(2));
        Assert.That(save.player.bakeryProduction.storedBreadCount, Is.EqualTo(3));
        Assert.That(save.player.homeInventory[0].quantity, Is.EqualTo(2));
    }

    [Test]
    public void PopupPrefabIsStaticAndHasRequiredReferences()
    {
        const string path = "Assets/_Project/08.Prefabs/UI/Bakery/BakeryProductionPopup.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Assert.That(root.activeSelf, Is.False);
            Assert.That(root.GetComponent<BakeryProductionPopupView>(), Is.Not.Null);
            Assert.That(root.GetComponent<BakeryProductionPopupPresenter>(), Is.Not.Null);
            var view = new SerializedObject(root.GetComponent<BakeryProductionPopupView>());
            foreach (string property in new[] { "backdropButton", "closeButton", "productIcon",
                         "productNameText", "amountText", "remainingText",
                         "partialReceiveButton", "receiveAllButton", "quantityModal",
                         "minButton", "minusButton", "quantityText", "plusButton",
                         "maxButton", "quantitySlider", "quantityCancelButton",
                         "quantityConfirmButton" })
                Assert.That(view.FindProperty(property)?.objectReferenceValue, Is.Not.Null, property);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private void AddBuilding(string name, int level) => save.player.villageBuildings.Add(
        new VillageBuildingSaveData { displayName = name, level = level });

    private static CargoEntrySaveData Entry(string id, int quantity, long price) =>
        new CargoEntrySaveData { quantity = quantity, item = new TradeItemSaveData {
            itemId = id, itemName = id, purchaseUnitPrice = price, maxCount = 40 } };

    private static ISharedGameDataProvider CreateCatalog() => new SharedGameDataView(
        new Dictionary<string, SharedTownDefinition>(), new Dictionary<string, SharedMarketDefinition>(),
        new Dictionary<string, SharedTradeItemDefinition> {
            ["Bread"] = new SharedTradeItemDefinition { Id = "Bread", DisplayName = "빵",
                CanStack = true, MaxCount = 40, Weight = .5f, BaseBuyPrice = 50 } },
        new Dictionary<string, SharedWagonDefinition>(),
        new Dictionary<string, SharedDraftAnimalDefinition>(),
        new Dictionary<string, SharedRouteDefinition>());
}
