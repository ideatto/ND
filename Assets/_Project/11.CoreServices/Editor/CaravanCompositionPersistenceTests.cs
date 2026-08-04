using System.Collections.Generic;
using ND.Framework;
using NUnit.Framework;
using UnityEngine;
using FrameworkSaveData = ND.Framework.SaveData;

/// <summary>편성 저장이 대상 Caravan만 변경하고 기존 Cargo 및 다른 Caravan 상태를 보존하는지 검증한다.</summary>
public sealed class CaravanCompositionPersistenceTests
{
    private GameObject host;
    private TestCaravanSettingService service;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject(nameof(CaravanCompositionPersistenceTests));
        service = host.AddComponent<TestCaravanSettingService>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
    }

    [Test]
    public void Provider_ExposesActualCatalogAssets()
    {
        CaravanSettingViewData view = Prepare(new RecordingSaveService(true), out _, out _);

        Assert.That(view.wagons, Has.Some.Matches<WagonViewData>(x => x.wagonId == "Wagon_M"));
        Assert.That(view.draftAnimals, Has.Some.Matches<DraftAnimalViewData>(x => x.draftAnimalId == "Horse"));
    }

    [Test]
    public void Confirm_ChangesOnlyTargetCompositionAndPreservesCargo()
    {
        CaravanSettingViewData view = Prepare(new RecordingSaveService(true), out FrameworkSaveData save, out ND.Framework.CaravanSaveData target);
        ND.Framework.CaravanSaveData other = save.caravans[1];
        List<CargoEntrySaveData> cargoReference = target.cargo;
        WagonSaveData otherWagonReference = other.wagon;
        var draft = new CaravanSettingDraft { caravanId = target.caravanId, selectedWagonInstanceId = "Wagon_M" };
        draft.SelectAnimal("Horse");

        CaravanSettingCommandResult result = service.Execute(draft);

        Assert.That(result.succeeded, Is.True);
        Assert.That(target.wagon.wagonName, Is.EqualTo("Wagon_M"));
        Assert.That(target.wagon.maxLoad, Is.EqualTo(30f));
        Assert.That(target.wagon.inventorySlotCount, Is.EqualTo(5));
        Assert.That(target.animals, Has.Count.EqualTo(1));
        Assert.That(target.animals[0].animalName, Is.EqualTo("Horse"));
        Assert.That(target.cargo, Is.SameAs(cargoReference));
        Assert.That(other.wagon, Is.SameAs(otherWagonReference));
        Assert.That(view.selectedWagonInstanceId, Is.Not.EqualTo("Wagon_M"));
    }

    [Test]
    public void ConfirmedComposition_AllowsWarehouseHomeToCargoTransfer()
    {
        var saveService = new RecordingSaveService(true);
        Prepare(saveService, out FrameworkSaveData save, out ND.Framework.CaravanSaveData target);
        target.slotIndex = 0;
        save.caravans[1].slotIndex = 1;
        save.player.currentTownId = WarehouseFunction.BaseTownId;
        target.currentTownId = WarehouseFunction.BaseTownId;
        save.player.homeInventory.Add(new CargoEntrySaveData
        {
            item = new TradeItemSaveData
            {
                itemId = "home-item", itemName = "Home Item",
                purchaseUnitPrice = 10L, weight = 2f, maxCount = 99
            },
            quantity = 3
        });

        var draft = new CaravanSettingDraft
        {
            caravanId = target.caravanId,
            selectedWagonInstanceId = "Wagon_M"
        };
        draft.SelectAnimal("Horse");
        CaravanSettingCommandResult compositionResult = service.Execute(draft);

        var request = new WarehouseTransferRequest(
            target.caravanId,
            WarehouseFunction.BaseTownId,
            "home-item",
            10L,
            2,
            WarehouseTransferDirection.HomeToCargo,
            30,
            target.wagon.inventorySlotCount,
            target.wagon.maxLoad);
        bool transferSucceeded = WarehouseInventoryTransferService.TryTransfer(
            save, saveService, request, out WarehouseTransferFailure failure);

        Assert.That(compositionResult.succeeded, Is.True);
        Assert.That(transferSucceeded, Is.True);
        Assert.That(failure, Is.EqualTo(WarehouseTransferFailure.None));
        Assert.That(target.wagon.inventorySlotCount, Is.EqualTo(5));
        Assert.That(target.wagon.maxLoad, Is.EqualTo(30f));
        Assert.That(target.cargo.Exists(entry => entry?.item?.itemId == "home-item" && entry.quantity == 2), Is.True);
        Assert.That(save.player.homeInventory.Exists(entry => entry?.item?.itemId == "home-item" && entry.quantity == 1), Is.True);
    }

    [Test]
    public void SaveFailure_RestoresPreviousComposition()
    {
        Prepare(new RecordingSaveService(false), out _, out ND.Framework.CaravanSaveData target);
        WagonSaveData previousWagon = target.wagon;
        List<AnimalSaveData> previousAnimals = target.animals;
        var draft = new CaravanSettingDraft { caravanId = target.caravanId, selectedWagonInstanceId = "Wagon_M" };
        draft.SelectAnimal("Horse");

        CaravanSettingCommandResult result = service.Execute(draft);

        Assert.That(result.succeeded, Is.False);
        Assert.That(result.errorCode, Is.EqualTo(CaravanSettingFailureCodes.SaveFailed));
        Assert.That(target.wagon, Is.SameAs(previousWagon));
        Assert.That(target.animals, Is.SameAs(previousAnimals));
    }

    [Test]
    public void OverweightCargo_RejectsCompositionWithoutSaving()
    {
        var saveService = new RecordingSaveService(true);
        Prepare(saveService, out _, out ND.Framework.CaravanSaveData target);
        target.cargo[0].item.weight = 40f;
        var draft = new CaravanSettingDraft { caravanId = target.caravanId, selectedWagonInstanceId = "Wagon_M" };
        draft.SelectAnimal("Horse");

        CaravanSettingCommandResult result = service.Execute(draft);

        Assert.That(result.succeeded, Is.False);
        Assert.That(result.errorCode, Is.EqualTo(CaravanSettingFailureCodes.CargoCapacityExceeded));
        Assert.That(saveService.SaveCalls, Is.Zero);
    }

    private CaravanSettingViewData Prepare(
        RecordingSaveService saveService,
        out FrameworkSaveData save,
        out ND.Framework.CaravanSaveData target)
    {
        target = CreateCaravan("target", "Walk");
        var other = CreateCaravan("other", "Wagon_S");
        save = new FrameworkSaveData();
        save.caravans.Clear();
        save.caravans.Add(target);
        save.caravans.Add(other);
        service.SetSaveDataForTests(save);
        service.SetSaveServiceForTests(saveService);
        return service.GetSetting(target.caravanId);
    }

    private static ND.Framework.CaravanSaveData CreateCaravan(string id, string wagonId)
    {
        var caravan = new ND.Framework.CaravanSaveData
        {
            caravanId = id,
            currentTownId = "town-a",
            state = JourneyState.Prepare,
            wagon = new WagonSaveData { instanceId = wagonId, wagonName = wagonId, maxLoad = 100f, inventorySlotCount = 10 },
            animals = new List<AnimalSaveData>()
        };
        caravan.cargo.Add(new CargoEntrySaveData
        {
            item = new TradeItemSaveData { itemId = "cargo", itemName = "Cargo", weight = 1f },
            quantity = 2
        });
        return caravan;
    }

    private sealed class RecordingSaveService : ISaveService
    {
        private readonly bool succeed;
        public int SaveCalls { get; private set; }

        public RecordingSaveService(bool succeed) { this.succeed = succeed; }
        public bool HasSaveData() => true;
        public FrameworkSaveData CreateNewGameData() => new FrameworkSaveData();
        public FrameworkSaveData Load() => new FrameworkSaveData();
        public SaveResult Save(FrameworkSaveData data)
        {
            SaveCalls++;
            return succeed ? SaveResult.Success() : SaveResult.Failure(SaveFailureReason.WriteFailed, "test");
        }
        public void ResetSaveData() { }
    }
}