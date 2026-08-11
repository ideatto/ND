using NUnit.Framework;

public sealed class JourneyFoodDepletionTests
{
    [Test]
    public void SetProgress_ZeroConsumptionAndZeroFood_DoesNotFail()
    {
        var caravan = new CaravanData
        {
            state = JourneyState.Traveling,
            totalSeconds = 10f,
            foodAmount = 0,
            starveGraceSeconds = 0f,
            currentDurability = 1,
            runFatalReason = JourneyFailureReason.None
        };

        Assert.That(CaravanCalculator.GetConsumptionPerSec(caravan), Is.Zero);

        JourneyRunner.SetProgress(caravan, 0.5f);

        Assert.That(caravan.runFoodDepleted, Is.False);
        Assert.That(caravan.runFatalReason, Is.EqualTo(JourneyFailureReason.None));
    }
}
