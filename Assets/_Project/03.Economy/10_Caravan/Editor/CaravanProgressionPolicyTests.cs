using NUnit.Framework;

namespace ND.Economy.Tests
{
    public sealed class CaravanProgressionPolicyTests
    {
        [Test]
        public void Evaluate_UnlocksSlotsFromGrowthAndQuestWithinHardLimit()
        {
            var input = ValidCreationInput();
            input.CaravanGrowthLevel = 1;
            input.QuestUnlockedSlotBonus = 1;
            input.RequestedSlotIndex = 2;

            CaravanProgressionResult result = CaravanProgressionPolicyCalculator.Evaluate(input);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.UnlockedSlotCount, Is.EqualTo(3));
            Assert.That(result.MaximumOwnedCaravans, Is.EqualTo(3));
            Assert.That(result.CanCreate, Is.True);
            Assert.That(result.CurrencyAfterCreation, Is.EqualTo(900L));
        }

        [Test]
        public void Evaluate_BlocksLockedOccupiedAndMaximumSlots()
        {
            var locked = ValidCreationInput();
            locked.RequestedSlotIndex = 1;
            Assert.That(CaravanProgressionPolicyCalculator.Evaluate(locked).FailureReason,
                Is.EqualTo(CaravanCreationFailureReason.SlotLocked));

            var occupied = ValidCreationInput();
            occupied.OccupiedSlotIndices.Add(0);
            occupied.CurrentCaravanCount = 1;
            Assert.That(CaravanProgressionPolicyCalculator.Evaluate(occupied).FailureReason,
                Is.EqualTo(CaravanCreationFailureReason.SlotOccupied));

            var maximum = ValidCreationInput();
            maximum.OccupiedSlotIndices.Add(1);
            maximum.CurrentCaravanCount = 1;
            maximum.RequestedSlotIndex = 0;
            Assert.That(CaravanProgressionPolicyCalculator.Evaluate(maximum).FailureReason,
                Is.EqualTo(CaravanCreationFailureReason.MaximumCaravansReached));
        }

        [Test]
        public void Evaluate_RejectsDuplicateSlotDataAsInvalid()
        {
            var input = ValidCreationInput();
            input.CaravanGrowthLevel = 2;
            input.OccupiedSlotIndices.Add(0);
            input.OccupiedSlotIndices.Add(0);
            input.CurrentCaravanCount = 2;

            CaravanProgressionResult result = CaravanProgressionPolicyCalculator.Evaluate(input);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(CaravanCreationFailureReason.InvalidInput));
        }

        [Test]
        public void AssetAvailability_LocksTravelingAndPendingCaravans()
        {
            CaravanAssetAvailabilityResult traveling = CaravanAssetAvailabilityCalculator.Evaluate(
                new CaravanAssetAvailabilityInput { State = CaravanEconomicState.Traveling, IsAtBaseTown = true });
            CaravanAssetAvailabilityResult pending = CaravanAssetAvailabilityCalculator.Evaluate(
                new CaravanAssetAvailabilityInput { State = CaravanEconomicState.SettlementPending, IsAtBaseTown = true });

            Assert.That(traveling.CanUseCargo, Is.False);
            Assert.That(traveling.CanSubmitInvestmentAssets, Is.False);
            Assert.That(traveling.BlockReason, Is.EqualTo(CaravanAssetBlockReason.Traveling));
            Assert.That(pending.CanEditConfiguration, Is.False);
            Assert.That(pending.BlockReason, Is.EqualTo(CaravanAssetBlockReason.SettlementPending));
        }

        [Test]
        public void AssetAvailability_AllowsPreparationButRequiresBaseForTransfer()
        {
            CaravanAssetAvailabilityResult away = CaravanAssetAvailabilityCalculator.Evaluate(
                new CaravanAssetAvailabilityInput { State = CaravanEconomicState.Prepare, IsAtBaseTown = false });
            CaravanAssetAvailabilityResult home = CaravanAssetAvailabilityCalculator.Evaluate(
                new CaravanAssetAvailabilityInput { State = CaravanEconomicState.Prepare, IsAtBaseTown = true });

            Assert.That(away.CanEditConfiguration, Is.True);
            Assert.That(away.CanStartTrade, Is.True);
            Assert.That(away.CanTransferToBaseInventory, Is.False);
            Assert.That(home.CanTransferToBaseInventory, Is.True);
        }

        [Test]
        public void AssetAvailability_LocksInconsistentPrepareStateWithActiveTrade()
        {
            CaravanAssetAvailabilityResult result = CaravanAssetAvailabilityCalculator.Evaluate(
                new CaravanAssetAvailabilityInput
                {
                    State = CaravanEconomicState.Prepare,
                    IsAtBaseTown = true,
                    HasActiveTrade = true
                });

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.CanEditConfiguration, Is.False);
            Assert.That(result.CanUseCargo, Is.False);
            Assert.That(result.CanStartTrade, Is.False);
            Assert.That(result.BlockReason, Is.EqualTo(CaravanAssetBlockReason.ActiveTradeExists));
        }

        private static CaravanProgressionInput ValidCreationInput()
        {
            return new CaravanProgressionInput
            {
                Definition = new CaravanProgressionPolicyDefinition
                {
                    MaximumSupportedSlots = 4,
                    BaseUnlockedSlots = 1,
                    SlotsPerGrowthLevel = 1,
                    MaximumGrowthSlotBonus = 2,
                    CreationCost = 100L
                },
                TradingCurrency = 1000L,
                RequestedSlotIndex = 0,
                HasInitialTown = true
            };
        }
    }
}
