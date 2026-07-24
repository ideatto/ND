using System;

namespace ND.Economy
{
    [Serializable]
    public sealed class TradePreparationEconomyPreviewPolicy
    {
        public float DistanceRiskPerUnit;
        public float FoodShortageRisk = 0.2f;
        public float GrowthRiskMultiplier = 1f;
        public float LoadPenaltyPerRatio = 0.5f;
        public float MinimumLoadSpeedMultiplier = 0.1f;
    }

    public static class TradePreparationEconomyPreviewInputAdapter
    {
        public static bool TryCreate(
            global::TradePrepareViewData source,
            out TradePreparationEconomyPreviewInput input,
            TradePreparationEconomyPreviewPolicy policy = null)
        {
            input = null;
            if (source == null)
            {
                return false;
            }

            global::RouteViewData route = FindRoute(source.routes, source.selectedRouteId);
            global::WagonViewData wagon = FindWagon(source.wagons, source.selectedWagonId);
            if (route == null || source.loadedItems == null || source.mercenaries == null)
            {
                return false;
            }

            policy = policy ?? new TradePreparationEconomyPreviewPolicy();
            input = new TradePreparationEconomyPreviewInput
            {
                TradingCurrency = Math.Max(0L, source.currentTradingCurrency),
                HasWagon = wagon != null,
                RequiresDraftAnimal = wagon != null && wagon.minRequireAnimals > 0,
                DraftAnimalCount = CountSelectedAnimals(source.draftAnimals),
                EfficientLoad = Math.Max(0f, source.overloadLimit),
                MaxLoad = Math.Max(0f, source.maxLoad),
                Distance = Math.Max(0f, route.distance),
                BaseMoveSpeed = ResolveBaseMoveSpeed(source, wagon, route),
                LoadPenaltyPerRatio = Math.Max(0f, policy.LoadPenaltyPerRatio),
                MinimumLoadSpeedMultiplier = Clamp(policy.MinimumLoadSpeedMultiplier, 0.01f, 1f),
                BaseRisk = Clamp01(route.riskLevel),
                DistanceRiskPerUnit = Math.Max(0f, policy.DistanceRiskPerUnit),
                FoodShortageRisk = Math.Max(0f, policy.FoodShortageRisk),
                RequiredFoodQuantity = Math.Max(0, source.requiredDraftAnimalFoodQuantity),
                GrowthRiskMultiplier = Math.Max(0f, policy.GrowthRiskMultiplier)
            };

            for (int index = 0; index < source.loadedItems.Length; index++)
            {
                global::CargoItemViewData item = source.loadedItems[index];
                if (item == null || item.quantity <= 0)
                {
                    continue;
                }

                input.Cargo.Add(new TradePreparationCargoInput
                {
                    ItemId = item.itemId ?? string.Empty,
                    Quantity = item.quantity,
                    IsFood = item.category == global::TradeItemCategory.DraftAnimalsFood,
                    UnitWeight = Math.Max(0f, item.unitWeight),
                    UnitBuyPrice = Math.Max(0L, item.purchaseUnitPrice),
                    UnitExpectedSellPrice = Math.Max(0L, item.estimatedSellUnitPrice)
                });
            }

            for (int index = 0; index < source.mercenaries.Length; index++)
            {
                global::MercenaryViewData mercenary = source.mercenaries[index];
                if (mercenary == null || !mercenary.isSelected)
                {
                    continue;
                }

                input.Mercenaries.Add(new TradePreparationMercenaryInput
                {
                    MercenaryId = mercenary.mercenaryId ?? string.Empty,
                    HireCost = Math.Max(0L, mercenary.baseBuyPrice),
                    RiskReduction = ResolveMercenaryRiskReduction(
                        mercenary.combatCapability,
                        source.requiredMercenaryPower)
                });
            }

            return input.MaxLoad > 0f && input.BaseMoveSpeed > 0f;
        }

        private static float ResolveMercenaryRiskReduction(int power, int requiredPower)
        {
            if (power <= 0 || requiredPower <= 0)
            {
                return 0f;
            }

            return Math.Min(0.5f, 0.5f * power / requiredPower);
        }

        private static float ResolveBaseMoveSpeed(
            global::TradePrepareViewData source,
            global::WagonViewData wagon,
            global::RouteViewData route)
        {
            if (wagon != null && wagon.baseMoveSpeed > 0f)
            {
                return wagon.baseMoveSpeed;
            }
            if (source.selectedMoveSpeed > 0f)
            {
                return source.selectedMoveSpeed;
            }
            return route.estimatedTime > 0f ? route.distance / route.estimatedTime : 0f;
        }

        private static int CountSelectedAnimals(global::DraftAnimalViewData[] animals)
        {
            int total = 0;
            if (animals == null) return total;
            for (int index = 0; index < animals.Length; index++)
            {
                global::DraftAnimalViewData animal = animals[index];
                if (animal == null || animal.selectedAmount <= 0) continue;
                total = animal.selectedAmount > int.MaxValue - total ? int.MaxValue : total + animal.selectedAmount;
            }
            return total;
        }

        private static global::RouteViewData FindRoute(global::RouteViewData[] routes, string routeId)
        {
            if (routes == null) return null;
            for (int index = 0; index < routes.Length; index++)
            {
                if (routes[index] != null && string.Equals(routes[index].routeId, routeId, StringComparison.Ordinal))
                    return routes[index];
            }
            return null;
        }

        private static global::WagonViewData FindWagon(global::WagonViewData[] wagons, string wagonId)
        {
            if (wagons == null) return null;
            for (int index = 0; index < wagons.Length; index++)
            {
                if (wagons[index] != null && string.Equals(wagons[index].wagonId, wagonId, StringComparison.Ordinal))
                    return wagons[index];
            }
            return null;
        }

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }

        private static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }
    }

    public static class TradePreparationEconomyPreviewFlow
    {
        public static bool TryExecute(
            global::TradePrepareViewData viewData,
            out TradePreparationEconomyPreviewResult result,
            TradePreparationEconomyPreviewPolicy policy = null)
        {
            result = null;
            TradePreparationEconomyPreviewInput input;
            if (!TradePreparationEconomyPreviewInputAdapter.TryCreate(viewData, out input, policy))
            {
                return false;
            }

            result = TradePreparationEconomyPreviewCalculator.Calculate(input);
            global::RouteViewData route = null;
            if (viewData.routes != null)
            {
                for (int index = 0; index < viewData.routes.Length; index++)
                {
                    if (viewData.routes[index] != null &&
                        string.Equals(viewData.routes[index].routeId, viewData.selectedRouteId, StringComparison.Ordinal))
                    {
                        route = viewData.routes[index];
                        break;
                    }
                }
            }

            float distance = route != null ? route.distance : 0f;
            return TradePreparationEconomyPreviewViewAdapter.TryApply(viewData, result, distance);
        }
    }
}
