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
    public void GetSetting_MapsOwnedWagonDefinitionForTransportSelection()
    {
        CaravanSettingViewData result = CreateService(CreateSave(), new RecordingSaveService(true))
            .GetSetting("caravan-a");

        Assert.That(result.wagons.Length, Is.EqualTo(1));
        WagonViewData wagon = result.wagons[0];
        Assert.That(wagon.wagonInstanceId, Is.EqualTo("wagon-instance"));
        Assert.That(wagon.wagonType, Is.EqualTo(WagonType.WagonWithAnimals));
        Assert.That(wagon.isOwned, Is.True);
        Assert.That(wagon.canSelect, Is.True);
        Assert.That(wagon.eligibleAnimalTypes, Is.EqualTo(new[] { DraftAnimalType.Horse }));
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
    public void ExecuteSetting_ContentQuantityKeepsCurrentInstancesThenFillsFromInventory()
    {
        FrameworkSaveData save = CreateSave();
        save.player.draftAnimalInventory.Add(new OwnedDraftAnimalSaveData
        {
            instanceId = "animal-spare", contentId = "horse"
        });
        var persistence = new RecordingSaveService(true);
        var service = CreateService(save, persistence);
        var draft = new CaravanSettingDraft
        {
            caravanId = "caravan-a",
            selectedWagonInstanceId = "wagon-instance"
        };
        draft.SetAnimalQuantity("horse", 3);

        CaravanSettingCommandResult result = service.Execute(draft);

        Assert.That(result.succeeded, Is.True);
        Assert.That(persistence.SaveCount, Is.EqualTo(1));
        Assert.That(save.caravans[0].animals.ConvertAll(animal => animal.instanceId),
            Is.EqualTo(new[] { "animal-b", "animal-a", "animal-spare" }));
    }

    [Test]
    public void ExecuteSetting_ContentQuantityUnavailableFailsWithoutMutationOrSave()
    {
        FrameworkSaveData save = CreateSave();
        var persistence = new RecordingSaveService(true);
        var service = CreateService(save, persistence);
        var draft = new CaravanSettingDraft
        {
            caravanId = "caravan-a",
            selectedWagonInstanceId = "wagon-instance"
        };
        draft.SetAnimalQuantity("horse", 3);

        CaravanSettingCommandResult result = service.Execute(draft);

        Assert.That(result.succeeded, Is.False);
        Assert.That(persistence.SaveCount, Is.Zero);
        Assert.That(save.caravans[0].animals.ConvertAll(animal => animal.instanceId),
            Is.EqualTo(new[] { "animal-b", "animal-a" }));
    }

    [Test]
    public void ExecuteSetting_ValidationFailureDoesNotMutateOwnedWagonDurability()
    {
        FrameworkSaveData save = CreateSave();
        save.player.wagonInventory.Find(wagon => wagon.instanceId == "wagon-instance")
            .currentDurability = 10;
        var persistence = new RecordingSaveService(true);
        var service = CreateService(save, persistence);
        var draft = new CaravanSettingDraft
        {
            caravanId = "caravan-a",
            selectedWagonInstanceId = "wagon-instance"
        };
        draft.SetAnimalQuantity("horse", 99);

        CaravanSettingCommandResult result = service.Execute(draft);

        Assert.That(result.succeeded, Is.False);
        Assert.That(persistence.SaveCount, Is.Zero);
        Assert.That(save.player.wagonInventory[0].currentDurability, Is.EqualTo(10));
    }

    [Test]
    public void ExecuteSetting_RejectsOwnedAndAssignedContentIdConflict()
    {
        FrameworkSaveData save = CreateSave();
        save.player.wagonInventory.Add(new OwnedWagonSaveData
        {
            instanceId = "wagon-instance", contentId = "wagon-large", currentDurability = 90
        });
        var persistence = new RecordingSaveService(true);
        var service = CreateService(save, persistence);
        var draft = new CaravanSettingDraft
        {
            caravanId = "caravan-a",
            selectedWagonInstanceId = "wagon-instance"
        };

        CaravanSettingCommandResult result = service.Execute(draft);

        Assert.That(result.succeeded, Is.False);
        Assert.That(persistence.SaveCount, Is.Zero);
        Assert.That(save.caravans[0].wagon.contentId, Is.EqualTo("wagon-basic"));
    }

    [Test]
    public void ExecuteSetting_RejectsAssignedAnimalContentIdConflict()
    {
        FrameworkSaveData save = CreateSave();
        save.player.draftAnimalInventory.Add(new OwnedDraftAnimalSaveData
        {
            instanceId = "animal-a", contentId = "horse-alt"
        });
        var persistence = new RecordingSaveService(true);
        var service = CreateService(save, persistence);
        var draft = new CaravanSettingDraft
        {
            caravanId = "caravan-a",
            selectedWagonInstanceId = "wagon-instance"
        };

        CaravanSettingCommandResult result = service.Execute(draft);

        Assert.That(result.succeeded, Is.False);
        Assert.That(persistence.SaveCount, Is.Zero);
        Assert.That(save.caravans[0].animals[1].contentId, Is.EqualTo("horse"));
    }

    [Test]
    public void ExecuteSetting_ContentQuantityDoesNotTakeAnimalAssignedToAnotherCaravan()
    {
        FrameworkSaveData save = CreateSave();
        save.player.draftAnimalInventory.Add(new OwnedDraftAnimalSaveData
        {
            instanceId = "animal-other", contentId = "horse"
        });
        save.caravans.Add(new FrameworkCaravanSaveData
        {
            caravanId = "caravan-b",
            animals = new List<AnimalSaveData>
            {
                new AnimalSaveData { instanceId = "animal-other", contentId = "horse" }
            }
        });
        var persistence = new RecordingSaveService(true);
        var service = CreateService(save, persistence);
        var draft = new CaravanSettingDraft
        {
            caravanId = "caravan-a",
            selectedWagonInstanceId = "wagon-instance"
        };
        draft.SetAnimalQuantity("horse", 3);

        CaravanSettingCommandResult result = service.Execute(draft);

        Assert.That(result.succeeded, Is.False);
        Assert.That(persistence.SaveCount, Is.Zero);
        Assert.That(save.caravans[1].animals[0].instanceId, Is.EqualTo("animal-other"));
    }

    [Test]
    public void ExecuteSetting_RejectsWagonThatCannotHoldExistingCargo()
    {
        FrameworkSaveData save = CreateSave();
        save.caravans[0].cargo.Add(new CargoEntrySaveData
        {
            item = new TradeItemSaveData { itemId = "ore", weight = 30f },
            quantity = 1
        });
        var persistence = new RecordingSaveService(true);
        var service = CreateService(save, persistence);
        var draft = new CaravanSettingDraft
        {
            caravanId = "caravan-a",
            selectedWagonInstanceId = "wagon-instance"
        };
        draft.SetAnimalQuantity("horse", 2);

        CaravanSettingCommandResult result = service.Execute(draft);

        Assert.That(result.succeeded, Is.False);
        Assert.That(result.errorCode, Is.EqualTo(CaravanSettingFailureCodes.CargoCapacityExceeded));
        Assert.That(persistence.SaveCount, Is.Zero);
        Assert.That(save.caravans[0].wagon.instanceId, Is.EqualTo("wagon-instance"));
    }

    [Test]
    public void ExecuteSetting_ContentQuantitySaveFailureRestoresCompositionAndInventory()
    {
        FrameworkSaveData save = CreateSave();
        save.player.draftAnimalInventory.Add(new OwnedDraftAnimalSaveData
        {
            instanceId = "animal-spare", contentId = "horse"
        });
        var service = CreateService(save, new RecordingSaveService(false));
        var draft = new CaravanSettingDraft
        {
            caravanId = "caravan-a",
            selectedWagonInstanceId = "wagon-instance"
        };
        draft.SetAnimalQuantity("horse", 3);

        CaravanSettingCommandResult result = service.Execute(draft);

        Assert.That(result.succeeded, Is.False);
        Assert.That(save.caravans[0].animals.ConvertAll(animal => animal.instanceId),
            Is.EqualTo(new[] { "animal-b", "animal-a" }));
        Assert.That(save.player.draftAnimalInventory.ConvertAll(animal => animal.instanceId),
            Is.EqualTo(new[] { "animal-b", "animal-a", "animal-spare" }));
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
    public void ExecuteSetting_ChangesAssignmentAndRetainsAllOwnedTransportAtomically()
    {
        FrameworkSaveData save = CreateSave();
        save.player.wagonInventory.Add(new OwnedWagonSaveData
        {
            instanceId = "wagon-spare", contentId = "wagon-large", currentDurability = 75
        });
        save.player.draftAnimalInventory.Add(new OwnedDraftAnimalSaveData
        {
            instanceId = "animal-spare", contentId = "horse"
        });
        var service = CreateService(save, new RecordingSaveService(true));
        var draft = new CaravanSettingDraft { caravanId = "caravan-a", selectedWagonInstanceId = "wagon-spare" };
        draft.SelectAnimal("animal-spare");

        CaravanSettingCommandResult result = service.Execute(draft);

        Assert.That(result.succeeded, Is.True);
        Assert.That(save.caravans[0].wagon.instanceId, Is.EqualTo("wagon-spare"));
        Assert.That(save.caravans[0].wagon.contentId, Is.EqualTo("wagon-large"));
        Assert.That(save.caravans[0].currentDurability, Is.EqualTo(75));
        Assert.That(save.caravans[0].animals[0].instanceId, Is.EqualTo("animal-spare"));
        Assert.That(save.player.wagonInventory.Exists(x => x.instanceId == "wagon-instance"), Is.True);
        Assert.That(save.player.draftAnimalInventory.Exists(x => x.instanceId == "animal-a"), Is.True);
        Assert.That(save.player.draftAnimalInventory.Exists(x => x.instanceId == "animal-b"), Is.True);
        Assert.That(save.player.wagonInventory.Exists(x => x.instanceId == "wagon-spare"), Is.True);
        Assert.That(save.player.draftAnimalInventory.Exists(x => x.instanceId == "animal-spare"), Is.True);
    }

    [Test]
    public void ExecuteSetting_SwapSaveFailureRestoresCaravanAndInventories()
    {
        FrameworkSaveData save = CreateSave();
        save.player.wagonInventory.Add(new OwnedWagonSaveData
        {
            instanceId = "wagon-spare", contentId = "wagon-large", currentDurability = 75
        });
        save.player.draftAnimalInventory.Add(new OwnedDraftAnimalSaveData
        {
            instanceId = "animal-spare", contentId = "horse"
        });
        var service = CreateService(save, new RecordingSaveService(false));
        var draft = new CaravanSettingDraft { caravanId = "caravan-a", selectedWagonInstanceId = "wagon-spare" };
        draft.SelectAnimal("animal-spare");

        CaravanSettingCommandResult result = service.Execute(draft);

        Assert.That(result.succeeded, Is.False);
        Assert.That(save.caravans[0].wagon.instanceId, Is.EqualTo("wagon-instance"));
        Assert.That(save.player.wagonInventory.ConvertAll(wagon => wagon.instanceId),
            Is.EqualTo(new[] { "wagon-instance", "wagon-spare" }));
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
            new Dictionary<string, SharedWagonDefinition>
            {
                ["wagon-basic"] = new SharedWagonDefinition
                {
                    Id = "wagon-basic", DisplayName = "Wagon", MaxDurability = 100,
                    WagonType = "WagonWithAnimals",
                    MaxLoad = 20f, InventorySlotCount = 4, MinRequireAnimals = 1,
                    MaxPullAnimals = 3, EligibleAnimalTypes = new[] { "Horse" }
                },
                ["wagon-large"] = new SharedWagonDefinition
                {
                    Id = "wagon-large", DisplayName = "Large Wagon", MaxDurability = 120,
                    WagonType = "WagonWithAnimals",
                    MaxLoad = 40f, InventorySlotCount = 8, MinRequireAnimals = 1,
                    MaxPullAnimals = 3, EligibleAnimalTypes = new[] { "Horse" }
                }
            },
            new Dictionary<string, SharedDraftAnimalDefinition>
            {
                ["horse"] = new SharedDraftAnimalDefinition
                {
                    Id = "horse", DisplayName = "Horse", AnimalType = "Horse", BaseMoveSpeed = 1f
                },
                ["horse-alt"] = new SharedDraftAnimalDefinition
                {
                    Id = "horse-alt", DisplayName = "Horse Alt", AnimalType = "Horse", BaseMoveSpeed = 1f
                }
            },
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
                contentId = "wagon-basic",
                wagonName = "Wagon",
                maxLoad = 20f,
                inventorySlotCount = 4,
                minAnimals = 1,
                maxAnimals = 2
            },
            animals = new List<AnimalSaveData>
            {
                new AnimalSaveData { instanceId = "animal-b", contentId = "horse", animalName = "B" },
                new AnimalSaveData { instanceId = "animal-a", contentId = "horse", animalName = "A" }
            }
        });
        save.selectedCaravanId = "caravan-a";
        save.player.wagonInventory.Add(new OwnedWagonSaveData
        {
            instanceId = "wagon-instance", contentId = "wagon-basic", currentDurability = 90
        });
        save.player.draftAnimalInventory.Add(new OwnedDraftAnimalSaveData
        {
            instanceId = "animal-b", contentId = "horse"
        });
        save.player.draftAnimalInventory.Add(new OwnedDraftAnimalSaveData
        {
            instanceId = "animal-a", contentId = "horse"
        });
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
