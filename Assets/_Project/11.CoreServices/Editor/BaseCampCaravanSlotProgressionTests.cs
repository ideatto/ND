using System.Collections.Generic;
using ND.Framework;
using NUnit.Framework;
using FrameworkSaveData = ND.Framework.SaveData;

public sealed class BaseCampCaravanSlotProgressionTests
{
    [TestCase(0, 0)]
    [TestCase(1, 1)]
    [TestCase(2, 2)]
    [TestCase(3, 3)]
    [TestCase(4, 4)]
    [TestCase(5, 4)]
    public void BaseCampLevelUnlocksOneSlotPerLevel(int level, int expected)
    {
        Assert.That(
            BaseCampProgressionPolicy.GetUnlockedCaravanSlotCount(Buildings(level)),
            Is.EqualTo(expected));
    }

    [Test]
    public void ProductNewGameStartsWithoutCaravan()
    {
        FrameworkSaveData save = new JsonSaveService().CreateNewGameData();

        Assert.That(save.caravans, Is.Empty);
        Assert.That(save.selectedCaravanId, Is.Empty);
    }

    [Test]
    public void CreateCaravanRequiresMatchingBaseCampLevel()
    {
        var save = new FrameworkSaveData();
        save.caravans.Clear();
        save.selectedCaravanId = string.Empty;
        var service = new CaravanManagementService(
            () => save,
            new SuccessfulSaveService());

        CaravanCreationResult locked = service.CreateCaravan(0);
        Assert.That(locked.FailureReason, Is.EqualTo(CaravanCreationFailureReason.SlotLocked));
        Assert.That(save.caravans, Is.Empty);

        save.player.villageBuildings.Add(new VillageBuildingSaveData
        {
            displayName = BaseCampBuildingLevelPolicy.BaseCampDisplayName,
            level = 1
        });

        CaravanCreationResult created = service.CreateCaravan(0);
        Assert.That(created.Succeeded, Is.True);
        Assert.That(save.caravans, Has.Count.EqualTo(1));
        Assert.That(save.caravans[0].slotIndex, Is.Zero);
    }

    [Test]
    public void OccupiedLegacySlotRemainsAtSavedIndexWhenCurrentlyLocked()
    {
        var caravan = new CaravanData { caravanId = "legacy" };
        CaravanOverviewViewData overview = CaravanOverviewBuilder.Build(
            new[] { caravan },
            _ => 2,
            _ => false,
            index => $"locked-{index}");

        Assert.That(overview.caravans[2].slotState, Is.EqualTo(CaravanSlotState.Occupied));
        Assert.That(overview.caravans[2].caravanId, Is.EqualTo("legacy"));
        Assert.That(overview.caravans[0].slotState, Is.EqualTo(CaravanSlotState.Locked));
    }

    private static List<VillageBuildingSaveData> Buildings(int level)
    {
        var result = new List<VillageBuildingSaveData>();
        if (level > 0)
        {
            result.Add(new VillageBuildingSaveData
            {
                displayName = BaseCampBuildingLevelPolicy.BaseCampDisplayName,
                level = level
            });
        }

        return result;
    }

    private sealed class SuccessfulSaveService : ISaveService
    {
        public bool HasSaveData() => true;
        public FrameworkSaveData CreateNewGameData() => new FrameworkSaveData();
        public FrameworkSaveData Load() => new FrameworkSaveData();
        public SaveResult Save(FrameworkSaveData data) => SaveResult.Success();
        public void ResetSaveData() { }
    }
}
