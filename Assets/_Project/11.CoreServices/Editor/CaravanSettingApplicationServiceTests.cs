using System.Collections.Generic;
using ND.Framework;
using NUnit.Framework;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;
using FrameworkSaveData = ND.Framework.SaveData;

public sealed class CaravanSettingApplicationServiceTests
{
    [Test]
    public void GetSetting_ReadsPersistentCaravanIdentityAndComposition()
    {
        FrameworkSaveData save = CreateSave();
        var service = CreateService(save, new RecordingSaveService(true));

        CaravanSettingViewData result = service.GetSetting("caravan-a");

        Assert.That(result, Is.Not.Null);
        Assert.That(result.selectedWagonInstanceId, Is.EqualTo("wagon-instance"));
        Assert.That(result.selectedAnimalInstanceIds, Is.EqualTo(new[] { "animal-b", "animal-a" }));
        Assert.That(result.canEdit, Is.True);
    }

    [Test]
    public void ExecuteSetting_ReordersAssignedAnimalsAndPersistsOnce()
    {
        FrameworkSaveData save = CreateSave();
        var persistence = new RecordingSaveService(true);
        var service = CreateService(save, persistence);
        var draft = new CaravanSettingDraft
        {
            caravanId = "caravan-a",
            selectedWagonInstanceId = "wagon-instance"
        };
        draft.SelectAnimal("animal-a");
        draft.SelectAnimal("animal-b");

        CaravanSettingCommandResult result = service.Execute(draft);

        Assert.That(result.succeeded, Is.True);
        Assert.That(persistence.SaveCount, Is.EqualTo(1));
        Assert.That(save.caravans[0].animals[0].instanceId, Is.EqualTo("animal-a"));
    }

    [Test]
    public void ExecuteSetting_SaveFailureRestoresOriginalOrder()
    {
        FrameworkSaveData save = CreateSave();
        var service = CreateService(save, new RecordingSaveService(false));
        var draft = new CaravanSettingDraft
        {
            caravanId = "caravan-a",
            selectedWagonInstanceId = "wagon-instance"
        };
        draft.SelectAnimal("animal-a");
        draft.SelectAnimal("animal-b");

        CaravanSettingCommandResult result = service.Execute(draft);

        Assert.That(result.succeeded, Is.False);
        Assert.That(result.errorCode, Is.EqualTo(CaravanSettingFailureCodes.SaveFailed));
        Assert.That(save.caravans[0].animals[0].instanceId, Is.EqualTo("animal-b"));
    }

    [Test]
    public void ExecuteCargo_WritesAuthoritativeMarketValues()
    {
        FrameworkSaveData save = CreateSave();
        var persistence = new RecordingSaveService(true);
        var service = CreateService(save, persistence);
        var draft = new CaravanLoadSettingDraft { caravanId = "caravan-a" };
        draft.items.Add(new CaravanLoadItemDraft { itemId = "apple", quantity = 2 });

        CaravanLoadSettingCommandResult result = ((ICaravanLoadSettingCommand)service).Execute(draft);

        Assert.That(result.succeeded, Is.True);
        Assert.That(save.caravans[0].cargo.Count, Is.EqualTo(1));
        Assert.That(save.caravans[0].cargo[0].item.purchaseUnitPrice, Is.EqualTo(135));
        Assert.That(save.caravans[0].cargo[0].item.weight, Is.EqualTo(2.5f));
    }

    private static CaravanSettingApplicationService CreateService(FrameworkSaveData save, ISaveService saveService)
    {
        ISharedGameDataProvider shared = new SharedGameDataView(
            new Dictionary<string, SharedTownDefinition>
            {
                ["town-a"] = new SharedTownDefinition { Id = "town-a", MarketId = "market-a" }
            },
            new Dictionary<string, SharedMarketDefinition>
            {
                ["market-a"] = new SharedMarketDefinition
                {
                    Id = "market-a",
                    TradeItemIds = new[] { "apple" },
                    LocalSpecialtyItemIds = new string[0]
                }
            },
            new Dictionary<string, SharedTradeItemDefinition>
            {
                ["apple"] = new SharedTradeItemDefinition
                {
                    Id = "apple",
                    DisplayName = "Apple",
                    Weight = 2.5f,
                    BaseBuyPrice = 100,
                    BaseSellPrice = 80,
                    MaxCount = 20
                }
            },
            new Dictionary<string, SharedWagonDefinition>(),
            new Dictionary<string, SharedDraftAnimalDefinition>(),
            new Dictionary<string, SharedRouteDefinition>());
        return new CaravanSettingApplicationService(() => save, saveService, () => shared);
    }

    private static FrameworkSaveData CreateSave()
    {
        var save = new FrameworkSaveData();
        save.caravans.Clear();
        save.caravans.Add(new FrameworkCaravanSaveData
        {
            caravanId = "caravan-a",
            currentTownId = "town-a",
            state = JourneyState.Prepare,
            currentDurability = 90,
            wagon = new WagonSaveData
            {
                instanceId = "wagon-instance",
                wagonName = "Wagon",
                maxLoad = 20f,
                inventorySlotCount = 4,
                minAnimals = 1,
                maxAnimals = 2
            },
            animals = new List<AnimalSaveData>
            {
                new AnimalSaveData { instanceId = "animal-b", animalName = "B" },
                new AnimalSaveData { instanceId = "animal-a", animalName = "A" }
            }
        });
        save.selectedCaravanId = "caravan-a";
        save.world.marketInventories.Add(new MarketInventorySaveData
        {
            marketId = "market-a",
            stocks = new List<MarketStockSaveData>
            {
                new MarketStockSaveData { itemId = "apple", quantity = 10, unitPrice = 135 }
            }
        });
        return save;
    }

    private sealed class RecordingSaveService : ISaveService
    {
        private readonly bool succeeds;
        public int SaveCount { get; private set; }
        public RecordingSaveService(bool succeeds) => this.succeeds = succeeds;
        public bool HasSaveData() => true;
        public FrameworkSaveData CreateNewGameData() => new FrameworkSaveData();
        public FrameworkSaveData Load() => new FrameworkSaveData();
        public SaveResult Save(FrameworkSaveData data)
        {
            SaveCount++;
            return succeeds ? SaveResult.Success() : SaveResult.Failure(SaveFailureReason.WriteFailed, "test");
        }
        public void ResetSaveData() { }
    }
}
