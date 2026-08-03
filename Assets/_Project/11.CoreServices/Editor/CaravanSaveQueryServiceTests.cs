using ND.Framework;
using NUnit.Framework;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;
using FrameworkSaveData = ND.Framework.SaveData;

public sealed class CaravanSaveQueryServiceTests
{
    [Test]
    public void TryGet_FailsForMissingSaveOrId()
    {
        var service = new CaravanSaveQueryService();
        Assert.That(service.TryGet(null, "caravan-a", out _), Is.False);
        Assert.That(service.TryGet(new FrameworkSaveData(), " ", out _), Is.False);
    }

    [Test]
    public void TryGet_NormalizesSavedAndRequestedIds()
    {
        FrameworkSaveData save = CreateSave(new FrameworkCaravanSaveData
        {
            caravanId = " caravan-a ",
            currentTownId = " town-a ",
            state = JourneyState.Prepare
        });

        bool found = new CaravanSaveQueryService().TryGet(save, " caravan-a ", out CaravanSaveQueryResult result);

        Assert.That(found, Is.True);
        Assert.That(result.CaravanId, Is.EqualTo("caravan-a"));
        Assert.That(result.CurrentTownId, Is.EqualTo("town-a"));
        Assert.That(result.State, Is.EqualTo(JourneyState.Prepare));
    }

    [Test]
    public void TryGet_PreservesSaveIndexForDisplayName()
    {
        FrameworkSaveData save = CreateSave(null, new FrameworkCaravanSaveData
        {
            caravanId = "caravan-b",
            currentTownId = "town-b",
            state = JourneyState.Traveling
        });

        new CaravanSaveQueryService().TryGet(save, "caravan-b", out CaravanSaveQueryResult result);

        Assert.That(result.SaveIndex, Is.EqualTo(1));
        Assert.That(result.DisplayName, Is.EqualTo("Caravan 2"));
        Assert.That(result.State, Is.EqualTo(JourneyState.Traveling));
    }

    [Test]
    public void TryGet_ReturnsFalseForUnknownCaravan()
    {
        FrameworkSaveData save = CreateSave(new FrameworkCaravanSaveData { caravanId = "caravan-a" });

        bool found = new CaravanSaveQueryService().TryGet(save, "caravan-b", out CaravanSaveQueryResult result);

        Assert.That(found, Is.False);
        Assert.That(result, Is.Null);
    }

    private static FrameworkSaveData CreateSave(params FrameworkCaravanSaveData[] caravans)
    {
        var save = new FrameworkSaveData();
        save.caravans.Clear();
        save.caravans.AddRange(caravans);
        return save;
    }
}
