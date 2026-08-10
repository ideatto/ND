using System;
using System.Collections.Generic;
using System.Linq;

namespace ND.Economy
{
    public enum SettlementModifierPresentationKind
    {
        Other,
        Season,
        Distance,
        Lucky
    }

    [Serializable]
    public sealed class SettlementModifierLineViewData
    {
        public string SourceId = string.Empty;
        public string DisplayNameKey = string.Empty;
        public SettlementModifierPresentationKind PresentationKind;
        public PriceModifierOperation Operation;
        public float Value;
    }

    [Serializable]
    public sealed class SettlementSaleLineViewData
    {
        public string ItemId = string.Empty;
        public int Quantity;
        public long BaseUnitPrice;
        public long FinalUnitPrice;
        public long TotalAmount;
        public List<SettlementModifierLineViewData> Modifiers = new List<SettlementModifierLineViewData>();
    }

    /// <summary>
    /// UI 정산 화면이 표시만 할 수 있도록 M1 결과를 묶은 ViewData.
    /// 금액·화폐·성장 값을 다시 계산하지 않는다.
    /// </summary>
    [Serializable]
    public sealed class EconomyM1SettlementViewData
    {
        public bool Success;
        public string ErrorCode = string.Empty;
        public PriceCalculationResult PriceResult;
        public SettlementBreakdown Settlement;
        public GrowthPurchaseResult GrowthPurchase;
        public CoreRuntimeStatModifier RuntimeStats;
        public List<SettlementSaleLineViewData> SaleLines = new List<SettlementSaleLineViewData>();
    }

    public static class EconomyM1SettlementViewAdapter
    {
        private static readonly Dictionary<string, List<SettlementSaleLineViewData>> arrivalSaleLines =
            new Dictionary<string, List<SettlementSaleLineViewData>>(StringComparer.Ordinal);

        public static void StoreArrivalSaleLines(
            string caravanId,
            string tradeId,
            IEnumerable<ND.Framework.CargoLoading.MarketTransactionItemSummary> items)
        {
            var lines = new List<SettlementSaleLineViewData>();
            foreach (ND.Framework.CargoLoading.MarketTransactionItemSummary item in items
                ?? Enumerable.Empty<ND.Framework.CargoLoading.MarketTransactionItemSummary>())
            {
                if (item == null || item.SellQuantity <= 0)
                    continue;
                var line = new SettlementSaleLineViewData
                {
                    ItemId = item.ItemId ?? string.Empty,
                    Quantity = item.SellQuantity,
                    BaseUnitPrice = item.BaseSellPrice,
                    FinalUnitPrice = item.FinalUnitSellPrice,
                    TotalAmount = item.SaleRevenue
                };
                foreach (ND.Framework.CargoLoading.MarketSaleModifierSnapshot modifier in item.SaleModifiers
                    ?? new List<ND.Framework.CargoLoading.MarketSaleModifierSnapshot>())
                {
                    line.Modifiers.Add(new SettlementModifierLineViewData
                    {
                        SourceId = modifier.SourceId ?? string.Empty,
                        DisplayNameKey = modifier.DisplayNameKey ?? string.Empty,
                        PresentationKind = Classify(modifier.ModifierType, modifier.SourceId),
                        Operation = modifier.Operation,
                        Value = modifier.Value
                    });
                }
                lines.Add(line);
            }
            arrivalSaleLines[CreateKey(caravanId, tradeId)] = lines;
        }

        public static void RemoveArrivalSaleLines(string caravanId, string tradeId)
        {
            arrivalSaleLines.Remove(CreateKey(caravanId, tradeId));
        }

        public static List<SettlementSaleLineViewData> GetArrivalSaleLines(string caravanId, string tradeId)
        {
            return arrivalSaleLines.TryGetValue(CreateKey(caravanId, tradeId), out List<SettlementSaleLineViewData> lines)
                ? lines
                : new List<SettlementSaleLineViewData>();
        }

        public static EconomyM1SettlementViewData Create(EconomyM1LoopResult result)
        {
            if (result == null)
            {
                return new EconomyM1SettlementViewData
                {
                    Success = false,
                    ErrorCode = "NULL_ECONOMY_M1_RESULT"
                };
            }

            return new EconomyM1SettlementViewData
            {
                Success = result.Success,
                ErrorCode = result.ErrorCode ?? string.Empty,
                PriceResult = result.PriceResult,
                Settlement = result.Settlement,
                GrowthPurchase = result.GrowthPurchase,
                RuntimeStats = result.RuntimeStats
            };
        }

        private static SettlementModifierPresentationKind Classify(
            PriceModifierType modifierType,
            string sourceId)
        {
            if ((sourceId ?? string.Empty).StartsWith("lucky-money:", StringComparison.Ordinal))
                return SettlementModifierPresentationKind.Lucky;
            if ((sourceId ?? string.Empty).StartsWith("distance:", StringComparison.Ordinal))
                return SettlementModifierPresentationKind.Distance;
            if ((sourceId ?? string.Empty).StartsWith("category-season:", StringComparison.Ordinal)
                || modifierType == PriceModifierType.Season)
                return SettlementModifierPresentationKind.Season;
            return SettlementModifierPresentationKind.Other;
        }

        private static string CreateKey(string caravanId, string tradeId)
        {
            return (caravanId ?? string.Empty) + "\n" + (tradeId ?? string.Empty);
        }
    }
}
