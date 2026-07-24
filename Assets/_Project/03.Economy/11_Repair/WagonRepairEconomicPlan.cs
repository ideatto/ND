namespace ND.Economy
{
    public sealed class WagonRepairEconomicPlan
    {
        public WagonRepairEconomicPlan(
            string caravanId,
            int durabilityBefore,
            int durabilityAfter,
            int repairedDurability,
            long repairCost,
            long currencyBefore,
            long currencyAfter)
        {
            CaravanId = caravanId ?? string.Empty;
            DurabilityBefore = durabilityBefore;
            DurabilityAfter = durabilityAfter;
            RepairedDurability = repairedDurability;
            RepairCost = repairCost;
            CurrencyBefore = currencyBefore;
            CurrencyAfter = currencyAfter;
        }

        public string CaravanId { get; }
        public int DurabilityBefore { get; }
        public int DurabilityAfter { get; }
        public int RepairedDurability { get; }
        public long RepairCost { get; }
        public long CurrencyBefore { get; }
        public long CurrencyAfter { get; }
    }

    public sealed class WagonRepairPlanBuildResult
    {
        public bool Success { get; internal set; }
        public WagonRepairFailureReason FailureReason { get; internal set; }
        public WagonRepairEconomicPlan Plan { get; internal set; }
    }

    public static class WagonRepairEconomicPlanBuilder
    {
        public static WagonRepairPlanBuildResult Build(
            string caravanId,
            WagonRepairInput input)
        {
            if (string.IsNullOrWhiteSpace(caravanId))
            {
                return Fail(WagonRepairFailureReason.InvalidInput);
            }

            WagonRepairResult calculation = WagonRepairCalculator.Evaluate(input);
            if (calculation == null || !calculation.CanRepair)
            {
                return Fail(
                    calculation != null
                        ? calculation.FailureReason
                        : WagonRepairFailureReason.InvalidInput);
            }

            return new WagonRepairPlanBuildResult
            {
                Success = true,
                FailureReason = WagonRepairFailureReason.None,
                Plan = new WagonRepairEconomicPlan(
                    caravanId,
                    calculation.DurabilityBefore,
                    calculation.DurabilityAfter,
                    calculation.RepairedDurability,
                    calculation.RepairCost,
                    calculation.CurrencyBefore,
                    calculation.CurrencyAfter)
            };
        }

        private static WagonRepairPlanBuildResult Fail(WagonRepairFailureReason reason)
        {
            return new WagonRepairPlanBuildResult
            {
                FailureReason = reason
            };
        }
    }
}
