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

    [Test]
    public void NormalizeData_TrimsOversizedLogToDefaultLimit()
    {
        var saveData = new ND.Framework.SaveData();
        saveData.caravanActivityLogs.Clear();
        for (var index = 0; index < CaravanActivityLog.DefaultMaxEntries + 7; index++)
        {
            saveData.caravanActivityLogs.Add(new CaravanActivityLogEntrySaveData
            {
                sequence = index + 1,
                occurredUtcTicks = index + 1,
                caravanId = "caravan-a",
                tradeId = $"trade-{index}"
            });
        }

        var changed = JsonSaveService.NormalizeData(saveData);

        Assert.That(changed, Is.True);
        Assert.That(
            saveData.caravanActivityLogs,
            Has.Count.EqualTo(CaravanActivityLog.DefaultMaxEntries));
        Assert.That(saveData.caravanActivityLogs[0].tradeId, Is.EqualTo("trade-7"));
        Assert.That(JsonSaveService.NormalizeData(saveData), Is.False);
    }

    [Test]
    public void Add_TwoCaravanIdentitiesRemainIndependent()
    {
        var saveData = new ND.Framework.SaveData();
        saveData.caravanActivityLogs.Clear();

        CaravanActivityLog.Add(
            saveData,
            CaravanActivityLogType.Departure,
            "caravan-a",
            "trade-a",
            "route-a");
        CaravanActivityLog.Add(
            saveData,
            CaravanActivityLogType.Departure,
            "caravan-b",
            "trade-b",
            "route-b");

        Assert.That(saveData.caravanActivityLogs[0].caravanId, Is.EqualTo("caravan-a"));
        Assert.That(saveData.caravanActivityLogs[0].tradeId, Is.EqualTo("trade-a"));
        Assert.That(saveData.caravanActivityLogs[1].caravanId, Is.EqualTo("caravan-b"));
        Assert.That(saveData.caravanActivityLogs[1].tradeId, Is.EqualTo("trade-b"));
    }

    [Test]
    public void Add_QuestCompleted_PreservesSubmissionTown()
    {
        var saveData = new ND.Framework.SaveData();
        saveData.caravanActivityLogs.Clear();

        CaravanActivityLogEntrySaveData entry = CaravanActivityLog.Add(
            saveData,
            CaravanActivityLogType.QuestCompleted,
            "caravan-a",
            townId: "river-town");

        Assert.That(entry.eventType, Is.EqualTo(CaravanActivityLogType.QuestCompleted));
        Assert.That(entry.townId, Is.EqualTo("river-town"));
    }
}
