using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class DualGrowthInputAdapterTests
    {
        [Test]
        public void Build_PlayerAxis_MapsBothLevelsAndCurrency()
        {
            DualGrowthInputAdapterResult result = DualGrowthInputAdapter.Build(
                "player_capacity",
                GrowthAxis.Player,
                State(),
                Definition("player_capacity", GrowthAxis.Player));

            Assert.That(result.Success, Is.True);
            Assert.That(result.GrowthId, Is.EqualTo("player_capacity"));
            Assert.That(result.Axis, Is.EqualTo(GrowthAxis.Player));
            Assert.That(result.Input.PlayerGrowthLevel, Is.EqualTo(2));
            Assert.That(result.Input.CaravanGrowthLevel, Is.EqualTo(4));
            Assert.That(result.Input.DevelopmentCurrency, Is.EqualTo(100));
        }

        [Test]
        public void Build_StableIdMismatch_IsRejected()
        {
            DualGrowthInputAdapterResult result = DualGrowthInputAdapter.Build(
                "player_capacity",
                GrowthAxis.Player,
                State(),
                Definition("player_speed", GrowthAxis.Player));

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(DualGrowthInputAdapterFailureReason.GrowthIdMismatch));
            Assert.That(result.Input, Is.Null);
        }

        [Test]
        public void Build_AxisMismatch_IsRejected()
        {
            DualGrowthInputAdapterResult result = DualGrowthInputAdapter.Build(
                "player_capacity",
                GrowthAxis.Player,
                State(),
                Definition("player_capacity", GrowthAxis.Caravan));

            Assert.That(result.FailureReason,
                Is.EqualTo(DualGrowthInputAdapterFailureReason.AxisMismatch));
        }

        [Test]
        public void Build_NegativeStoredLevel_IsRejected()
        {
            DualGrowthStateSnapshot state = State();
            state.CaravanGrowthLevel = -1;

            DualGrowthInputAdapterResult result = DualGrowthInputAdapter.Build(
                "player_capacity",
                GrowthAxis.Player,
                state,
                Definition("player_capacity", GrowthAxis.Player));

            Assert.That(result.FailureReason,
                Is.EqualTo(DualGrowthInputAdapterFailureReason.InvalidState));
        }

        [Test]
        public void Build_ResultUsesSnapshotValues()
        {
            DualGrowthStateSnapshot state = State();
            DualGrowthInputAdapterResult result = DualGrowthInputAdapter.Build(
                "caravan_load",
                GrowthAxis.Caravan,
                state,
                Definition("caravan_load", GrowthAxis.Caravan));
            state.DevelopmentCurrency = 999;

            Assert.That(result.Input.DevelopmentCurrency, Is.EqualTo(100));
        }

        private static DualGrowthStateSnapshot State()
        {
            return new DualGrowthStateSnapshot
            {
                PlayerGrowthLevel = 2,
                CaravanGrowthLevel = 4,
                DevelopmentCurrency = 100
            };
        }

        private static GrowthAxisDefinition Definition(
            string growthId,
            GrowthAxis axis)
        {
            return new GrowthAxisDefinition
            {
                GrowthId = growthId,
                Axis = axis,
                MaximumLevel = 5,
                Levels =
                {
                    new GrowthLevelDefinition
                    {
                        Level = axis == GrowthAxis.Player ? 3 : 5,
                        DevelopmentCurrencyCost = 20
                    }
                }
            };
        }
    }
}
