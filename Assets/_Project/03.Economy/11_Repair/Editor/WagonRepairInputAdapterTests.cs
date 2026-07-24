using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class WagonRepairInputAdapterTests
    {
        [Test]
        public void Build_ExactCaravanMatch_MapsAllValues()
        {
            WagonRepairInputAdapterResult result = WagonRepairInputAdapter.Build(
                "caravan-a",
                15,
                Snapshot("caravan-a"),
                Policy());

            Assert.That(result.Success, Is.True);
            Assert.That(result.CaravanId, Is.EqualTo("caravan-a"));
            Assert.That(result.Input.CurrentDurability, Is.EqualTo(60));
            Assert.That(result.Input.MaximumDurability, Is.EqualTo(100));
            Assert.That(result.Input.RequestedRepairAmount, Is.EqualTo(15));
            Assert.That(result.Input.RepairCostPerDurability, Is.EqualTo(3));
            Assert.That(result.Input.RarityMultiplier, Is.EqualTo(1.25));
            Assert.That(result.Input.TradingCurrency, Is.EqualTo(500));
        }

        [Test]
        public void Build_DifferentCaravan_DoesNotUseSelectedOrFallbackState()
        {
            WagonRepairInputAdapterResult result = WagonRepairInputAdapter.Build(
                "caravan-b",
                10,
                Snapshot("caravan-a"),
                Policy());

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(WagonRepairInputAdapterFailureReason.CaravanIdMismatch));
            Assert.That(result.Input, Is.Null);
        }

        [Test]
        public void Build_CaseDifferentCaravanId_IsRejected()
        {
            WagonRepairInputAdapterResult result = WagonRepairInputAdapter.Build(
                "CARAVAN-A",
                10,
                Snapshot("caravan-a"),
                Policy());

            Assert.That(result.FailureReason,
                Is.EqualTo(WagonRepairInputAdapterFailureReason.CaravanIdMismatch));
        }

        [Test]
        public void Build_InvalidContentPolicy_IsRejectedBeforeCalculation()
        {
            WagonRepairContentPolicy policy = Policy();
            policy.RarityMultiplier = double.NaN;

            WagonRepairInputAdapterResult result = WagonRepairInputAdapter.Build(
                "caravan-a",
                10,
                Snapshot("caravan-a"),
                policy);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(WagonRepairInputAdapterFailureReason.InvalidContentPolicy));
        }

        [Test]
        public void Build_DestroyedWagonState_IsPreservedForCalculatorReason()
        {
            WagonRepairStateSnapshot snapshot = Snapshot("caravan-a");
            snapshot.CurrentDurability = 0;

            WagonRepairInputAdapterResult adapted = WagonRepairInputAdapter.Build(
                "caravan-a",
                10,
                snapshot,
                Policy());
            WagonRepairResult calculated = WagonRepairCalculator.Evaluate(adapted.Input);

            Assert.That(adapted.Success, Is.True);
            Assert.That(calculated.FailureReason,
                Is.EqualTo(WagonRepairFailureReason.WagonDestroyed));
        }

        private static WagonRepairStateSnapshot Snapshot(string caravanId)
        {
            return new WagonRepairStateSnapshot
            {
                CaravanId = caravanId,
                HasWagon = true,
                CurrentDurability = 60,
                MaximumDurability = 100,
                TradingCurrency = 500
            };
        }

        private static WagonRepairContentPolicy Policy()
        {
            return new WagonRepairContentPolicy
            {
                RepairCostPerDurability = 3,
                RarityMultiplier = 1.25
            };
        }
    }
}
