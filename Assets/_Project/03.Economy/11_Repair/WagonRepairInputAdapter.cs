using System;

namespace ND.Economy
{
    public enum WagonRepairInputAdapterFailureReason
    {
        None = 0,
        InvalidRequest,
        InvalidSnapshot,
        CaravanIdMismatch,
        InvalidContentPolicy
    }

    [Serializable]
    public sealed class WagonRepairStateSnapshot
    {
        public string CaravanId = string.Empty;
        public bool HasWagon;
        public bool IsInJourney;
        public int CurrentDurability;
        public int MaximumDurability;
        public long TradingCurrency;
    }

    [Serializable]
    public sealed class WagonRepairContentPolicy
    {
        public long RepairCostPerDurability;
        public double RarityMultiplier = 1d;
    }

    public sealed class WagonRepairInputAdapterResult
    {
        public bool Success { get; internal set; }
        public WagonRepairInputAdapterFailureReason FailureReason { get; internal set; }
        public string CaravanId { get; internal set; }
        public WagonRepairInput Input { get; internal set; }

        public WagonRepairInputAdapterResult()
        {
            CaravanId = string.Empty;
        }
    }

    /// <summary>
    /// Converts Framework-owned state and Content-owned repair policy into an Economy input.
    /// The requested caravan ID must exactly match the resolved state snapshot.
    /// </summary>
    public static class WagonRepairInputAdapter
    {
        public static WagonRepairInputAdapterResult Build(
            string requestedCaravanId,
            int requestedRepairAmount,
            WagonRepairStateSnapshot snapshot,
            WagonRepairContentPolicy contentPolicy)
        {
            if (string.IsNullOrWhiteSpace(requestedCaravanId) || requestedRepairAmount <= 0)
                return Fail(WagonRepairInputAdapterFailureReason.InvalidRequest);
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.CaravanId))
                return Fail(WagonRepairInputAdapterFailureReason.InvalidSnapshot);
            if (!string.Equals(
                requestedCaravanId,
                snapshot.CaravanId,
                StringComparison.Ordinal))
            {
                return Fail(WagonRepairInputAdapterFailureReason.CaravanIdMismatch);
            }
            if (snapshot.MaximumDurability <= 0 || snapshot.TradingCurrency < 0)
                return Fail(WagonRepairInputAdapterFailureReason.InvalidSnapshot);
            if (contentPolicy == null ||
                contentPolicy.RepairCostPerDurability <= 0 ||
                double.IsNaN(contentPolicy.RarityMultiplier) ||
                double.IsInfinity(contentPolicy.RarityMultiplier) ||
                contentPolicy.RarityMultiplier <= 0d)
            {
                return Fail(WagonRepairInputAdapterFailureReason.InvalidContentPolicy);
            }

            return new WagonRepairInputAdapterResult
            {
                Success = true,
                FailureReason = WagonRepairInputAdapterFailureReason.None,
                CaravanId = snapshot.CaravanId,
                Input = new WagonRepairInput
                {
                    HasWagon = snapshot.HasWagon,
                    IsInJourney = snapshot.IsInJourney,
                    CurrentDurability = snapshot.CurrentDurability,
                    MaximumDurability = snapshot.MaximumDurability,
                    RequestedRepairAmount = requestedRepairAmount,
                    RepairCostPerDurability = contentPolicy.RepairCostPerDurability,
                    RarityMultiplier = contentPolicy.RarityMultiplier,
                    TradingCurrency = snapshot.TradingCurrency
                }
            };
        }

        private static WagonRepairInputAdapterResult Fail(
            WagonRepairInputAdapterFailureReason reason)
        {
            return new WagonRepairInputAdapterResult
            {
                FailureReason = reason
            };
        }
    }
}
