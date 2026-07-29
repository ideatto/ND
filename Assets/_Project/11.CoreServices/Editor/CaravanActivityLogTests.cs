/*
using ND.Framework;
using NUnit.Framework;

public sealed class CaravanActivityLogTests
{
    [Test]
    public void Add_AppendsOrderedEntryWithIdentity()
    {
        var saveData = new ND.Framework.SaveData();
        saveData.caravanActivityLogs.Clear();

        var first = CaravanActivityLog.Add(
            saveData,
            CaravanActivityLogType.Departure,
            "caravan-a",
            "trade-a",
            "route-a",
            "town-b");
        var second = CaravanActivityLog.Add(
            saveData,
            CaravanActivityLogType.CombatVictory,
            "caravan-a",
            "trade-a",
            "route-a",
            null,
            "event-a");

        Assert.That(saveData.caravanActivityLogs, Has.Count.EqualTo(2));
        Assert.That(first.sequence, Is.LessThan(second.sequence));
        Assert.That(first.townId, Is.EqualTo("town-b"));
        Assert.That(second.routeEventId, Is.EqualTo("event-a"));
    }

    [Test]
    public void Add_TrimsOldestEntriesAtDefaultLimit()
    {
        var saveData = new ND.Framework.SaveData();
        saveData.caravanActivityLogs.Clear();

        for (var index = 0; index < CaravanActivityLog.DefaultMaxEntries + 5; index++)
        {
            CaravanActivityLog.Add(
                saveData,
                CaravanActivityLogType.CombatEncounter,
                "caravan-a",
                $"trade-{index}");
        }

        Assert.That(
            saveData.caravanActivityLogs,
            Has.Count.EqualTo(CaravanActivityLog.DefaultMaxEntries));
        Assert.That(saveData.caravanActivityLogs[0].tradeId, Is.EqualTo("trade-5"));
    }

    [Test]
    public void NormalizeData_CreatesMissingLogContainer()
    {
        var saveData = new ND.Framework.SaveData
        {
            caravanActivityLogs = null
        };

        JsonSaveService.NormalizeData(saveData);

        Assert.That(saveData.caravanActivityLogs, Is.Not.Null);
        Assert.That(saveData.caravanActivityLogs, Is.Empty);
    }
}
*/
