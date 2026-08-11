using NUnit.Framework;

public sealed class JourneyCargoLossCleanupTests
{
    // A loss that consumes the final item must remove the row, not leave an invisible blocker.
    [Test]
    public void ApplyCargoLoss_WhenQuantityReachesZero_RemovesCargoEntry()
    {
        var caravan = new CaravanData
        {
            state = JourneyState.Traveling,
            lossLimitRate = 1f,
            runOriginalCargoCount = 1
        };
        caravan.cargo.Add(new CargoEntry
        {
            item = new imsiTradeItemData { id = "Stone" },
            quantity = 1
        });

        JourneyRunner.ApplyCargoLoss(caravan, 1);

        Assert.That(caravan.runCargoLost, Is.EqualTo(1));
        Assert.That(caravan.cargo, Is.Empty);
    }

    // Defensive coverage for legacy/corrupt data: negative rows must not alter loss counters.
    [Test]
    public void ApplyCargoLoss_NonPositiveLegacyRow_DoesNotCorruptLossCalculation()
    {
        var caravan = new CaravanData
        {
            state = JourneyState.Traveling,
            lossLimitRate = 1f,
            runOriginalCargoCount = 2
        };
        caravan.cargo.Add(new CargoEntry
        {
            item = new imsiTradeItemData { id = "LegacyInvalid" },
            quantity = -1
        });
        caravan.cargo.Add(new CargoEntry
        {
            item = new imsiTradeItemData { id = "Logs" },
            quantity = 2
        });

        JourneyRunner.ApplyCargoLoss(caravan, 1);

        Assert.That(caravan.runCargoLost, Is.EqualTo(1));
        Assert.That(caravan.cargo.Count, Is.EqualTo(1));
        Assert.That(caravan.cargo[0].item.id, Is.EqualTo("Logs"));
        Assert.That(caravan.cargo[0].quantity, Is.EqualTo(1));
    }
}
