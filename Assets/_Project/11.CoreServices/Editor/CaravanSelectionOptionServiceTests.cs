using System.Reflection;
using System.Collections.Generic;
using ND.Framework;
using ND.UI.Market;
using ND.UI.InGame.Warehouse;
using NUnit.Framework;
using UnityEngine;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;
using FrameworkSaveData = ND.Framework.SaveData;

public sealed class CaravanSelectionOptionServiceTests
{
    [Test]
    public void WarehouseCargoTitle_UsesSavedNameAndSlotFallback()
    {
        MethodInfo resolve = typeof(WarehouseInventoryPopupController).GetMethod(
            "ResolveCargoTitle",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(resolve, Is.Not.Null);

        var named = new FrameworkCaravanSaveData
        {
            slotIndex = 1,
            displayName = "  Sunset Company  "
        };
        var legacy = new FrameworkCaravanSaveData
        {
            slotIndex = 1,
            displayName = " "
        };

        Assert.That(resolve.Invoke(null, new object[] { named }), Is.EqualTo("Sunset Company Cargo"));
        Assert.That(resolve.Invoke(null, new object[] { legacy }), Is.EqualTo("Caravan 2 Cargo"));
        Assert.That(resolve.Invoke(null, new object[] { null }), Is.EqualTo("Cargo"));
    }

    [Test]
    public void CreateOptions_ReturnsEmptyForMissingSave()
    {
        var service = new CaravanSelectionOptionService();

        TradePrepareCaravanOptionViewData[] options = service.CreateOptions(null);

        Assert.That(options, Is.Not.Null);
        Assert.That(options, Is.Empty);
    }

    [Test]
    public void CreateOptions_AllowsPreparedCaravanWithCurrentTown()
    {
        FrameworkSaveData save = CreateSave(
            new FrameworkCaravanSaveData
            {
                caravanId = " caravan-a ",
                currentTownId = " town-a ",
                state = JourneyState.Prepare
            });

        TradePrepareCaravanOptionViewData[] options =
            new CaravanSelectionOptionService().CreateOptions(save);

        Assert.That(options, Has.Length.EqualTo(1));
        Assert.That(options[0].caravanId, Is.EqualTo("caravan-a"));
        Assert.That(options[0].currentTownId, Is.EqualTo("town-a"));
        Assert.That(options[0].canSelect, Is.True);
        Assert.That(options[0].disabledReason, Is.Empty);
    }

    [Test]
    public void CreateOptions_UsesSavedDisplayNameAndFallsBackForLegacySave()
    {
        FrameworkSaveData save = CreateSave(
            new FrameworkCaravanSaveData
            {
                caravanId = "named",
                displayName = " 북부 교역대 ",
                currentTownId = "town-a"
            },
            new FrameworkCaravanSaveData
            {
                caravanId = "legacy",
                displayName = string.Empty,
                currentTownId = "town-a"
            });

        TradePrepareCaravanOptionViewData[] options =
            new CaravanSelectionOptionService().CreateOptions(save);

        Assert.That(options[0].displayName, Is.EqualTo("북부 교역대"));
        Assert.That(options[1].displayName, Is.EqualTo("Caravan 2"));
    }

    [Test]
    public void MapperRoundTrip_PreservesDisplayName()
    {
        var saved = new FrameworkCaravanSaveData
        {
            caravanId = "caravan-a",
            displayName = "서부 상단"
        };

        CaravanData runtime = CaravanSaveDataMapper.ToRuntime(saved);
        var copied = new FrameworkCaravanSaveData();
        CaravanSaveDataMapper.CopyToSave(runtime, copied);

        Assert.That(runtime.displayName, Is.EqualTo("서부 상단"));
        Assert.That(copied.displayName, Is.EqualTo("서부 상단"));
    }

    [Test]
    public void MapperCopyToSave_DoesNotOverwriteSavedNameFromStaleRuntime()
    {
        var runtime = new CaravanData
        {
            caravanId = "caravan-a",
            displayName = "Caravan 1",
            state = JourneyState.Traveling
        };
        var saved = new FrameworkCaravanSaveData
        {
            caravanId = "caravan-a",
            displayName = "Northern Traders",
            state = JourneyState.Prepare
        };

        CaravanSaveDataMapper.CopyToSave(runtime, saved);

        Assert.That(saved.displayName, Is.EqualTo("Northern Traders"));
        Assert.That(saved.state, Is.EqualTo(JourneyState.Traveling));
    }

    [Test]
    public void SaveQuery_UsesSavedNameAndSlotFallback()
    {
        FrameworkSaveData save = CreateSave(
            new FrameworkCaravanSaveData
            {
                caravanId = "named",
                slotIndex = 2,
                displayName = "  Northern Traders  "
            },
            new FrameworkCaravanSaveData
            {
                caravanId = "legacy",
                slotIndex = 3,
                displayName = string.Empty
            });
        var service = new CaravanSaveQueryService();

        Assert.That(service.TryGet(save, "named", out CaravanSaveQueryResult named), Is.True);
        Assert.That(service.TryGet(save, "legacy", out CaravanSaveQueryResult legacy), Is.True);
        Assert.That(named.DisplayName, Is.EqualTo("Northern Traders"));
        Assert.That(legacy.DisplayName, Is.EqualTo("Caravan 4"));
    }

    [Test]
    public void ArrivalSaleDisplayName_UsesSavedNameAndNeverCaravanId()
    {
        MethodInfo resolve = typeof(CaravanArrivalSaleController).GetMethod(
            "ResolveCaravanDisplayName",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(resolve, Is.Not.Null);

        var named = new FrameworkCaravanSaveData
        {
            caravanId = "guid-like-id",
            slotIndex = 1,
            displayName = "  Sunset Company  "
        };
        var legacy = new FrameworkCaravanSaveData
        {
            caravanId = "another-guid",
            slotIndex = 2,
            displayName = string.Empty
        };

        Assert.That(resolve.Invoke(null, new object[] { named }), Is.EqualTo("Sunset Company"));
        Assert.That(resolve.Invoke(null, new object[] { legacy }), Is.EqualTo("Caravan 3"));
    }

    [Test]
    public void OverviewProvider_UsesSavedDisplayName()
    {
        FrameworkSaveData save = CreateSave(new FrameworkCaravanSaveData
        {
            caravanId = "caravan-a",
            slotIndex = 0,
            displayName = "남부 교역대"
        });
        var go = new GameObject("provider-test");
        try
        {
            var provider = go.AddComponent<SaveDataCaravanOverviewProviderBehaviour>();
            provider.SetSaveDataForTests(save);
            Assert.That(provider.GetOverview().caravans[0].displayName, Is.EqualTo("남부 교역대"));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void OverviewProvider_UsesContentIdsForTransportIconsAndAnimalGrouping()
    {
        FrameworkSaveData save = CreateSave(new FrameworkCaravanSaveData
        {
            caravanId = "caravan-a",
            slotIndex = 0,
            wagon = new WagonSaveData
            {
                contentId = "wagon-basic",
                wagonName = "Localized Wagon Name"
            },
            animals = new List<AnimalSaveData>
            {
                new AnimalSaveData { contentId = "horse", animalName = "Brown Horse" },
                new AnimalSaveData { contentId = "horse", animalName = "White Horse" }
            }
        });
        var go = new GameObject("provider-test");
        try
        {
            var provider = go.AddComponent<SaveDataCaravanOverviewProviderBehaviour>();
            provider.SetSaveDataForTests(save);

            CaravanBlockViewData result = provider.GetOverview().caravans[0];

            Assert.That(result.wagonContentId, Is.EqualTo("wagon-basic"));
            Assert.That(result.animalIcons, Has.Length.EqualTo(1));
            Assert.That(result.animalIcons[0].animalContentId, Is.EqualTo("horse"));
            Assert.That(result.animalIcons[0].quantity, Is.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void RenameService_SavesOnlyTargetAndRollsBackOnFailure()
    {
        FrameworkSaveData save = CreateSave(
            new FrameworkCaravanSaveData { caravanId = "target", displayName = "기존" },
            new FrameworkCaravanSaveData { caravanId = "other", displayName = "다른 캐러반" });
        var failing = new RecordingSaveService(false);

        CaravanRenameResult failed =
            new CaravanRenameService(() => save, failing).Execute("target", "변경 이름");

        Assert.That(failed.Succeeded, Is.False);
        Assert.That(save.caravans[0].displayName, Is.EqualTo("기존"));
        Assert.That(save.caravans[1].displayName, Is.EqualTo("다른 캐러반"));

        CaravanRenameResult succeeded =
            new CaravanRenameService(() => save, new RecordingSaveService(true))
                .Execute("target", " 변경 이름 ");
        Assert.That(succeeded.Succeeded, Is.True);
        Assert.That(save.caravans[0].displayName, Is.EqualTo("변경 이름"));
        Assert.That(save.caravans[1].displayName, Is.EqualTo("다른 캐러반"));
    }

    [Test]
    public void CreateOptions_BlocksPreparedCaravanWithoutCurrentTown()
    {
        FrameworkSaveData save = CreateSave(
            new FrameworkCaravanSaveData
            {
                caravanId = "caravan-a",
                currentTownId = " ",
                state = JourneyState.Prepare
            });

        TradePrepareCaravanOptionViewData option =
            new CaravanSelectionOptionService().CreateOptions(save)[0];

        Assert.That(option.canSelect, Is.False);
        Assert.That(option.disabledReason, Does.Contain("departure town"));
    }

    [Test]
    public void CreateOptions_PreservesValidSaveOrderAndBlocksTravelingCaravan()
    {
        FrameworkSaveData save = CreateSave(
            null,
            new FrameworkCaravanSaveData
            {
                caravanId = "caravan-b",
                currentTownId = "town-b",
                state = JourneyState.Traveling
            },
            new FrameworkCaravanSaveData
            {
                caravanId = "caravan-c",
                currentTownId = "town-c",
                state = JourneyState.Prepare
            });

        TradePrepareCaravanOptionViewData[] options =
            new CaravanSelectionOptionService().CreateOptions(save);

        Assert.That(options, Has.Length.EqualTo(2));
        Assert.That(options[0].caravanId, Is.EqualTo("caravan-b"));
        Assert.That(options[0].displayName, Is.EqualTo("Caravan 2"));
        Assert.That(options[0].canSelect, Is.False);
        Assert.That(options[0].disabledReason, Does.Contain("traveling"));
        Assert.That(options[1].caravanId, Is.EqualTo("caravan-c"));
        Assert.That(options[1].canSelect, Is.True);
    }

    private static FrameworkSaveData CreateSave(params FrameworkCaravanSaveData[] caravans)
    {
        var save = new FrameworkSaveData();
        save.caravans.Clear();
        save.caravans.AddRange(caravans);
        return save;
    }

    private sealed class RecordingSaveService : ISaveService
    {
        private readonly bool succeeds;
        public RecordingSaveService(bool succeeds) => this.succeeds = succeeds;
        public bool HasSaveData() => true;
        public FrameworkSaveData CreateNewGameData() => new FrameworkSaveData();
        public FrameworkSaveData Load() => new FrameworkSaveData();
        public SaveResult Save(FrameworkSaveData data) => succeeds
            ? SaveResult.Success()
            : SaveResult.Failure(SaveFailureReason.WriteFailed, "test");
        public void ResetSaveData() { }
    }
}
