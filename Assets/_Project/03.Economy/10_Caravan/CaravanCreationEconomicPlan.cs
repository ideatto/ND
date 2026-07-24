using System;

namespace ND.Economy
{
    public sealed class CaravanCreationEconomicPlan
    {
        public CaravanCreationEconomicPlan(
            string caravanId,
            string initialTownId,
            int slotIndex,
            int unlockedSlotCount,
            int caravanCountBefore,
            int caravanCountAfter,
            long tradingCurrencyBefore,
            long creationCost,
            long tradingCurrencyAfter)
        {
            CaravanId = caravanId ?? string.Empty;
            InitialTownId = initialTownId ?? string.Empty;
            SlotIndex = slotIndex;
            UnlockedSlotCount = unlockedSlotCount;
            CaravanCountBefore = caravanCountBefore;
            CaravanCountAfter = caravanCountAfter;
            TradingCurrencyBefore = tradingCurrencyBefore;
            CreationCost = creationCost;
            TradingCurrencyAfter = tradingCurrencyAfter;
        }

        public string CaravanId { get; }
        public string InitialTownId { get; }
        public int SlotIndex { get; }
        public int UnlockedSlotCount { get; }
        public int CaravanCountBefore { get; }
        public int CaravanCountAfter { get; }
        public long TradingCurrencyBefore { get; }
        public long CreationCost { get; }
        public long TradingCurrencyAfter { get; }
    }

    public sealed class CaravanCreationPlanBuildResult
    {
        public bool Success { get; internal set; }
        public CaravanCreationFailureReason FailureReason { get; internal set; }
        public CaravanCreationEconomicPlan Plan { get; internal set; }
    }

    public static class CaravanCreationEconomicPlanBuilder
    {
        public static CaravanCreationPlanBuildResult Build(
            CaravanProgressionInput input,
            string caravanId,
            string initialTownId)
        {
            if (input == null ||
                string.IsNullOrWhiteSpace(caravanId) ||
                string.IsNullOrWhiteSpace(initialTownId))
            {
                return Fail(CaravanCreationFailureReason.InvalidInput);
            }

            CaravanProgressionResult calculation =
                CaravanProgressionPolicyCalculator.Evaluate(input);
            if (calculation == null || !calculation.CanCreate)
            {
                return Fail(
                    calculation != null
                        ? calculation.FailureReason
                        : CaravanCreationFailureReason.InvalidInput);
            }

            int caravanCountAfter;
            try
            {
                caravanCountAfter = checked(input.CurrentCaravanCount + 1);
            }
            catch (OverflowException)
            {
                return Fail(CaravanCreationFailureReason.ArithmeticOverflow);
            }

            if (!input.HasInitialTown ||
                calculation.CreationCost < 0 ||
                calculation.CurrencyAfterCreation !=
                    input.TradingCurrency - calculation.CreationCost ||
                caravanCountAfter > calculation.MaximumOwnedCaravans)
            {
                return Fail(CaravanCreationFailureReason.InvalidInput);
            }

            return new CaravanCreationPlanBuildResult
            {
                Success = true,
                FailureReason = CaravanCreationFailureReason.None,
                Plan = new CaravanCreationEconomicPlan(
                    caravanId,
                    initialTownId,
                    input.RequestedSlotIndex,
                    calculation.UnlockedSlotCount,
                    input.CurrentCaravanCount,
                    caravanCountAfter,
                    input.TradingCurrency,
                    calculation.CreationCost,
                    calculation.CurrencyAfterCreation)
            };
        }

        private static CaravanCreationPlanBuildResult Fail(
            CaravanCreationFailureReason reason)
        {
            return new CaravanCreationPlanBuildResult
            {
                FailureReason = reason
            };
        }
    }
}
