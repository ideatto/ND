/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - ND.Framework.SaveData와 SharedGameData를 EconomyM1LoopInput으로 조립한다.
 * - M1 단계에서는 첫 번째 유효 cargo entry를 단일 품목 정산 입력으로 사용한다.
 *
 * Main Features
 * - route 비용, season/disaster, growth level, cargo quantity를 Economy 입력으로 변환한다.
 * - JourneyResultData의 durability 손실을 CartRepairCost 임시 규칙으로 반영한다.
 *
 * Important Notes
 * - 다품목 정산은 후속 작업이며, 입력 조립 실패 시 null을 반환한다.
 * - CartRepairCost는 durability 1당 1 trade money 임시 규칙을 사용한다.
 */
using ND.Economy;

namespace ND.Framework
{
    /// <summary>
    /// Framework 저장 데이터를 Economy M1 loop 입력으로 변환하는 builder이다.
    /// </summary>
    public static class FrameworkEconomyM1InputBuilder
    {
        /// <summary>
        /// 내구도 1당 수리 비용(trade money) 임시 배율이다.
        /// </summary>
        public const long DurabilityRepairCostPerPoint = 1L;

        /// <summary>
        /// SaveData와 Core 정산 결과를 기반으로 Economy M1 입력을 조립한다.
        /// </summary>
        /// <returns>필수 참조가 준비되었으면 입력 객체, 아니면 null.</returns>
        public static EconomyM1LoopInput TryBuild(
            SaveData saveData,
            CaravanData caravan,
            JourneyResultData journeyResult,
            ISharedGameDataProvider sharedGameData)
        {
            if (saveData == null || caravan == null || journeyResult == null || sharedGameData == null || !sharedGameData.IsLoaded)
            {
                return null;
            }

            if (saveData.tradeProgress == null || saveData.player == null)
            {
                return null;
            }

            if (!TryGetPrimaryCargo(caravan, out var cargoEntry, out var itemId))
            {
                FrameworkLog.Warning("Economy M1 input build skipped because caravan has no valid cargo entry.");
                return null;
            }

            SharedTradeItemDefinition tradeItemDefinition;
            if (!sharedGameData.TryGetTradeItem(itemId, out tradeItemDefinition))
            {
                FrameworkLog.Warning($"Economy M1 input build skipped because trade item '{itemId}' was not found in shared game data.");
                return null;
            }

            var routeId = saveData.tradeProgress.activeRouteId ?? string.Empty;
            SharedRouteDefinition routeDefinition;
            if (string.IsNullOrEmpty(routeId) || !sharedGameData.TryGetRoute(routeId, out routeDefinition))
            {
                FrameworkLog.Warning($"Economy M1 input build skipped because route '{routeId}' was not found in shared game data.");
                return null;
            }

            var sellQuantity = cargoEntry.quantity - journeyResult.cargoLost;
            if (sellQuantity < 0)
            {
                sellQuantity = 0;
            }

            var cartRepairCost = journeyResult.durabilityLost > 0f
                ? (long)journeyResult.durabilityLost * DurabilityRepairCostPerPoint
                : 0L;

            var world = saveData.world;
            var seasonId = world != null ? world.currentSeasonId ?? string.Empty : string.Empty;
            var disasterId = world != null ? world.currentDisasterId ?? string.Empty : string.Empty;

            return new EconomyM1LoopInput
            {
                PriceInput = new ND.Economy.PriceCalculationInput
                {
                    TradeItemId = tradeItemDefinition.Id,
                    FromTownId = routeDefinition.FromTownId ?? string.Empty,
                    ToTownId = routeDefinition.ToTownId ?? string.Empty,
                    RouteId = routeDefinition.Id ?? string.Empty,
                    Quantity = sellQuantity,
                    BaseBuyPrice = tradeItemDefinition.BaseBuyPrice,
                    BaseSellPrice = tradeItemDefinition.BaseSellPrice,
                    SeasonId = seasonId,
                    DisasterId = disasterId,
                    PlayerGrowthLevel = saveData.player.playerGrowthLevel,
                    CaravanGrowthLevel = saveData.player.caravanGrowthLevel,
                    Modifiers = tradeItemDefinition.PriceModifiers != null
                        ? new System.Collections.Generic.List<PriceModifierInput>(tradeItemDefinition.PriceModifiers)
                        : new System.Collections.Generic.List<PriceModifierInput>()
                },
                CurrencyState = new CurrencyState
                {
                    TradeMoney = saveData.player.tradingCurrency,
                    DevelopmentCurrency = saveData.player.developmentCurrency
                },
                TradeId = saveData.tradeProgress.activeTradeId ?? string.Empty,
                FoodCost = routeDefinition.BaseFoodCost,
                MercenaryCost = routeDefinition.BaseMercenaryCost,
                CartRepairCost = cartRepairCost,
                DevelopmentCurrencyReward = 0L,
                PurchaseGrowth = false,
                PlayerGrowthLevel = saveData.player.playerGrowthLevel,
                CaravanGrowthLevel = saveData.player.caravanGrowthLevel
            };
        }

        private static bool TryGetPrimaryCargo(CaravanData caravan, out CargoEntry cargoEntry, out string itemId)
        {
            cargoEntry = null;
            itemId = string.Empty;

            if (caravan.cargo == null)
            {
                return false;
            }

            for (var index = 0; index < caravan.cargo.Count; index++)
            {
                var entry = caravan.cargo[index];
                if (entry == null || entry.item == null || entry.quantity <= 0)
                {
                    continue;
                }

                var candidateId = entry.item.id;
                if (string.IsNullOrEmpty(candidateId))
                {
                    continue;
                }

                cargoEntry = entry;
                itemId = candidateId;
                return true;
            }

            return false;
        }
    }
}
