using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ND.Economy
{
    public sealed class InvestmentItemPlan
    {
        public InvestmentItemPlan(
            string itemId,
            int requiredQuantity,
            int quantityBefore,
            int quantityAfter)
        {
            ItemId = itemId ?? string.Empty;
            RequiredQuantity = requiredQuantity;
            QuantityBefore = quantityBefore;
            QuantityAfter = quantityAfter;
        }

        public string ItemId { get; }
        public int RequiredQuantity { get; }
        public int QuantityBefore { get; }
        public int QuantityAfter { get; }
    }

    public sealed class InvestmentQuestEconomicPlan
    {
        private readonly ReadOnlyCollection<InvestmentItemPlan> items;
        private readonly ReadOnlyCollection<string> unlockTownIds;
        private readonly ReadOnlyCollection<string> unlockRouteIds;
        private readonly ReadOnlyCollection<string> unlockSpecialtyItemIds;

        public InvestmentQuestEconomicPlan(
            string questId,
            string caravanId,
            long tradingCurrencyBefore,
            long tradingCurrencyCost,
            long tradingCurrencyAfter,
            IEnumerable<InvestmentItemPlan> items,
            IEnumerable<string> unlockTownIds,
            IEnumerable<string> unlockRouteIds)
            : this(
                questId,
                caravanId,
                tradingCurrencyBefore,
                tradingCurrencyCost,
                tradingCurrencyAfter,
                items,
                unlockTownIds,
                unlockRouteIds,
                null,
                string.Empty,
                1f,
                0L)
        {
        }

        public InvestmentQuestEconomicPlan(
            string questId,
            string caravanId,
            long tradingCurrencyBefore,
            long tradingCurrencyCost,
            long tradingCurrencyAfter,
            IEnumerable<InvestmentItemPlan> items,
            IEnumerable<string> unlockTownIds,
            IEnumerable<string> unlockRouteIds,
            IEnumerable<string> unlockSpecialtyItemIds,
            string submissionTownId,
            float banditEncounterMultiplier,
            long banditReductionDurationTicks)
        {
            QuestId = questId ?? string.Empty;
            CaravanId = caravanId ?? string.Empty;
            TradingCurrencyBefore = tradingCurrencyBefore;
            TradingCurrencyCost = tradingCurrencyCost;
            TradingCurrencyAfter = tradingCurrencyAfter;
            this.items = new List<InvestmentItemPlan>(
                items ?? new InvestmentItemPlan[0]).AsReadOnly();
            this.unlockTownIds = new List<string>(
                unlockTownIds ?? new string[0]).AsReadOnly();
            this.unlockRouteIds = new List<string>(
                unlockRouteIds ?? new string[0]).AsReadOnly();
            this.unlockSpecialtyItemIds = new List<string>(
                unlockSpecialtyItemIds ?? new string[0]).AsReadOnly();
            SubmissionTownId = submissionTownId ?? string.Empty;
            BanditEncounterMultiplier = banditEncounterMultiplier;
            BanditReductionDurationTicks = banditReductionDurationTicks;
        }

        public string QuestId { get; }
        public string CaravanId { get; }
        public long TradingCurrencyBefore { get; }
        public long TradingCurrencyCost { get; }
        public long TradingCurrencyAfter { get; }
        public IReadOnlyList<InvestmentItemPlan> Items => items;
        public IReadOnlyList<string> UnlockTownIds => unlockTownIds;
        public IReadOnlyList<string> UnlockRouteIds => unlockRouteIds;
        public IReadOnlyList<string> UnlockSpecialtyItemIds =>
            unlockSpecialtyItemIds;
        public string SubmissionTownId { get; }
        public float BanditEncounterMultiplier { get; }
        public long BanditReductionDurationTicks { get; }
    }

    public sealed class InvestmentQuestPlanBuildResult
    {
        public bool Success { get; internal set; }
        public InvestmentQuestFailureReason FailureReason { get; internal set; }
        public InvestmentQuestEconomicPlan Plan { get; internal set; }
    }

    public static class InvestmentQuestEconomicPlanBuilder
    {
        public static InvestmentQuestPlanBuildResult Build(
            InvestmentQuestInput input)
        {
            InvestmentQuestResult calculation =
                InvestmentQuestPolicyCalculator.Evaluate(input);
            if (calculation == null || !calculation.Success)
            {
                return Fail(
                    calculation != null
                        ? calculation.FailureReason
                        : InvestmentQuestFailureReason.InvalidInput);
            }

            var items = new List<InvestmentItemPlan>(calculation.Items.Count);
            for (int i = 0; i < calculation.Items.Count; i++)
            {
                InvestmentItemDelta item = calculation.Items[i];
                if (item == null ||
                    string.IsNullOrWhiteSpace(item.ItemId) ||
                    item.RequiredQuantity <= 0 ||
                    item.MissingQuantity != 0 ||
                    item.QuantityBefore < item.RequiredQuantity ||
                    item.QuantityAfter != item.QuantityBefore - item.RequiredQuantity)
                {
                    return Fail(InvestmentQuestFailureReason.InvalidDefinition);
                }
                items.Add(new InvestmentItemPlan(
                    item.ItemId,
                    item.RequiredQuantity,
                    item.QuantityBefore,
                    item.QuantityAfter));
            }

            return new InvestmentQuestPlanBuildResult
            {
                Success = true,
                FailureReason = InvestmentQuestFailureReason.None,
                Plan = new InvestmentQuestEconomicPlan(
                    calculation.QuestId,
                    calculation.CaravanId,
                    calculation.TradingCurrencyBefore,
                    calculation.TradingCurrencyCost,
                    calculation.TradingCurrencyAfter,
                    items,
                    calculation.UnlockTownIds,
                    calculation.UnlockRouteIds,
                    calculation.UnlockSpecialtyItemIds,
                    input.Definition.SubmissionTownId,
                    calculation.BanditEncounterMultiplier,
                    calculation.BanditReductionDurationTicks)
            };
        }

        private static InvestmentQuestPlanBuildResult Fail(
            InvestmentQuestFailureReason reason)
        {
            return new InvestmentQuestPlanBuildResult
            {
                FailureReason = reason
            };
        }
    }
}
