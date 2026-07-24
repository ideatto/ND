using System.Collections.Generic;
using NUnit.Framework;

namespace ND.Economy.Tests
{
    public sealed class CaravanProgressionFrameworkAdapterTests
    {
        [Test]
        public void TryEvaluateCreation_UsesStableBindingsAndSavedGrowth()
        {
            global::ND.Framework.SaveData save = CreateSave("caravan-a", 0, "BaseCamp");
            save.player.caravanGrowthLevel = 1;
            save.player.tradingCurrency = 1000L;
            var bindings = new List<CaravanSlotBinding>
            {
                new CaravanSlotBinding { CaravanId = "caravan-a", SlotIndex = 0 }
            };

            CaravanProgressionResult result;
            CaravanProgressionAdapterFailureReason failure;
            bool evaluated = CaravanProgressionFrameworkAdapter.TryEvaluateCreation(
                save, bindings, 1, new CaravanProgressionPolicyDefinition { CreationCost = 100L },
                0, false, "BaseCamp", out result, out failure);

            Assert.That(evaluated, Is.True);
            Assert.That(failure, Is.EqualTo(CaravanProgressionAdapterFailureReason.None));
            Assert.That(result.CanCreate, Is.True);
            Assert.That(result.UnlockedSlotCount, Is.EqualTo(2));
        }

        [Test]
        public void TryCreateCreationInput_RejectsMissingAndDuplicateBindings()
        {
            global::ND.Framework.SaveData save = CreateSave("caravan-a", 0, "BaseCamp");
            CaravanProgressionInput input;
            CaravanProgressionAdapterFailureReason failure;

            bool missing = CaravanProgressionFrameworkAdapter.TryCreateCreationInput(
                save, new List<CaravanSlotBinding>(), 1, new CaravanProgressionPolicyDefinition(),
                0, false, "BaseCamp", out input, out failure);
            Assert.That(missing, Is.False);
            Assert.That(failure, Is.EqualTo(CaravanProgressionAdapterFailureReason.MissingSlotBinding));

            var duplicate = new List<CaravanSlotBinding>
            {
                new CaravanSlotBinding { CaravanId = "caravan-a", SlotIndex = 0 },
                new CaravanSlotBinding { CaravanId = "caravan-a", SlotIndex = 1 }
            };
            bool duplicated = CaravanProgressionFrameworkAdapter.TryCreateCreationInput(
                save, duplicate, 1, new CaravanProgressionPolicyDefinition(),
                0, false, "BaseCamp", out input, out failure);
            Assert.That(duplicated, Is.False);
            Assert.That(failure, Is.EqualTo(CaravanProgressionAdapterFailureReason.DuplicateSlotBinding));
        }

        [Test]
        public void TryEvaluateAssetAvailability_MapsTravelingAndBaseTown()
        {
            global::ND.Framework.SaveData save = CreateSave("caravan-a", 0, "BaseCamp");
            save.tradeProgressEntries.Add(new global::ND.Framework.TradeProgressSaveData
            {
                caravanId = "caravan-a",
                activeTradeId = "trade-a",
                state = global::ND.Framework.TradeProgressState.Traveling
            });

            CaravanAssetAvailabilityResult result;
            CaravanProgressionAdapterFailureReason failure;
            bool evaluated = CaravanProgressionFrameworkAdapter.TryEvaluateAssetAvailability(
                save, "caravan-a", "BaseCamp", out result, out failure);

            Assert.That(evaluated, Is.True);
            Assert.That(result.CanUseCargo, Is.False);
            Assert.That(result.BlockReason, Is.EqualTo(CaravanAssetBlockReason.Traveling));
        }

        [Test]
        public void TryCreateAssetAvailabilityInput_RejectsAmbiguousProgress()
        {
            global::ND.Framework.SaveData save = CreateSave("caravan-a", 0, "BaseCamp");
            save.tradeProgressEntries.Add(new global::ND.Framework.TradeProgressSaveData { caravanId = "caravan-a" });
            save.tradeProgressEntries.Add(new global::ND.Framework.TradeProgressSaveData { caravanId = "caravan-a" });

            CaravanAssetAvailabilityInput input;
            CaravanProgressionAdapterFailureReason failure;
            bool created = CaravanProgressionFrameworkAdapter.TryCreateAssetAvailabilityInput(
                save, "caravan-a", "BaseCamp", out input, out failure);

            Assert.That(created, Is.False);
            Assert.That(failure, Is.EqualTo(CaravanProgressionAdapterFailureReason.AmbiguousTradeProgress));
        }

        private static global::ND.Framework.SaveData CreateSave(
            string caravanId,
            int ignoredSlotIndex,
            string currentTownId)
        {
            var save = new global::ND.Framework.SaveData();
            save.caravans.Clear();
            save.tradeProgressEntries.Clear();
            save.caravans.Add(new global::ND.Framework.CaravanSaveData
            {
                caravanId = caravanId,
                currentTownId = currentTownId,
                state = global::JourneyState.Prepare
            });
            save.selectedCaravanId = caravanId;
            return save;
        }
    }
}
