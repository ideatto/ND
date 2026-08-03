using ND.Framework;
using NUnit.Framework;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;
using FrameworkSaveData = ND.Framework.SaveData;

public sealed class CaravanSelectionOptionServiceTests
{
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
}
