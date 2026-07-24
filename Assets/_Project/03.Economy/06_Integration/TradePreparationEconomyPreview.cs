using System;
using System.Collections.Generic;

namespace ND.Economy
{
    [Serializable]
    public sealed class TradeEventPreviewInput
    {
        public float DistanceKm;
        public float EventIntervalKm = TradeEventPreviewCalculator.DefaultEventIntervalKm;
        public float EventChancePerCheck = TradeEventPreviewCalculator.DefaultEventChancePerCheck;
        public float CaravanBaseSafetyChancePercent;
        public int MercenaryCombatPower;
        public int BanditCombatPower;
    }

    [Serializable]
    public sealed class TradeEventPreviewResult
    {
        public bool IsValid;
        public int EventCheckCount;
        public float AtLeastOneEventChance;
        public float ExpectedEventCount;
        public float BanditSafePassChancePercent;
    }

    /// <summary>
    /// Calculates distance-based event preview values and the bandit safe-pass chance.
    /// The two default tuning values intentionally live here until they move to Shared Data.
    /// </summary>
    public static class TradeEventPreviewCalculator
    {
        // Provisional second-build tuning values. Keep centralized for later data migration.
        public const float DefaultEventIntervalKm = 100f;
        public const float DefaultEventChancePerCheck = 0.2f;

        public static TradeEventPreviewResult Calculate(TradeEventPreviewInput input)
        {
            var result = new TradeEventPreviewResult();
            if (input == null || !IsFinite(input.DistanceKm) || input.DistanceKm < 0f ||
                !IsFinite(input.EventIntervalKm) || input.EventIntervalKm <= 0f ||
                !IsFinite(input.EventChancePerCheck) || input.EventChancePerCheck < 0f ||
                input.EventChancePerCheck > 1f ||
                !IsFinite(input.CaravanBaseSafetyChancePercent))
            {
                return result;
            }

            double rawChecks = Math.Floor((double)input.DistanceKm / input.EventIntervalKm);
            int checks = rawChecks >= int.MaxValue ? int.MaxValue : (int)rawChecks;
            double chance = checks == 0
                ? 0d
                : 1d - Math.Pow(1d - input.EventChancePerCheck, checks);
            double expected = checks * (double)input.EventChancePerCheck;

            result.IsValid = true;
            result.EventCheckCount = checks;
            result.AtLeastOneEventChance = Clamp01((float)chance);
            result.ExpectedEventCount = expected >= float.MaxValue ? float.MaxValue : (float)expected;
            result.BanditSafePassChancePercent = CalculateBanditSafePassChancePercent(
                input.CaravanBaseSafetyChancePercent,
                input.MercenaryCombatPower,
                input.BanditCombatPower);
            return result;
        }

        public static float CalculateBanditSafePassChancePercent(
            float caravanBaseSafetyChancePercent,
            int mercenaryCombatPower,
            int banditCombatPower)
        {
            float baseChance = Clamp(caravanBaseSafetyChancePercent, 0f, 100f);
            if (mercenaryCombatPower <= 0 || banditCombatPower <= 0)
            {
                return baseChance;
            }

            double bonus = (double)mercenaryCombatPower / banditCombatPower * 50d;
            return Clamp((float)(baseChance + bonus), 0f, 100f);
        }

        public static int CalculateCompletedEventCheckCount(
            float distanceKm,
            float progress01,
            float eventIntervalKm = DefaultEventIntervalKm)
        {
            if (!IsFinite(distanceKm) || distanceKm <= 0f ||
                !IsFinite(progress01) || progress01 <= 0f ||
                !IsFinite(eventIntervalKm) || eventIntervalKm <= 0f)
            {
                return 0;
            }

            double traveledDistance = distanceKm * (double)Clamp01(progress01);
            double rawChecks = Math.Floor(traveledDistance / eventIntervalKm);
            return rawChecks >= int.MaxValue ? int.MaxValue : (int)rawChecks;
        }

        public static bool IsEventTriggered(
            string tradeId,
            int checkIndex,
            float eventChancePerCheck = DefaultEventChancePerCheck)
        {
            if (string.IsNullOrEmpty(tradeId) || checkIndex < 0 ||
                !IsFinite(eventChancePerCheck) || eventChancePerCheck <= 0f)
            {
                return false;
            }
            if (eventChancePerCheck >= 1f) return true;

            uint seed = unchecked((uint)CalculateStableSeed(tradeId, checkIndex, 0x45564E54u));
            float roll = (NextDeterministicUInt(seed) >> 8) * (1f / 16777216f);
            return roll < eventChancePerCheck;
        }

        public static int CalculateStableSeed(string tradeId, int checkIndex, uint salt)
        {
            unchecked
            {
                uint hash = 2166136261u ^ salt;
                string value = tradeId ?? string.Empty;
                for (int i = 0; i < value.Length; i++)
                {
                    char character = value[i];
                    hash = (hash ^ (byte)character) * 16777619u;
                    hash = (hash ^ (byte)(character >> 8)) * 16777619u;
                }

                hash = (hash ^ (byte)checkIndex) * 16777619u;
                hash = (hash ^ (byte)(checkIndex >> 8)) * 16777619u;
                hash = (hash ^ (byte)(checkIndex >> 16)) * 16777619u;
                hash = (hash ^ (byte)(checkIndex >> 24)) * 16777619u;
                return (int)(hash == 0u ? 0x6D2B79F5u : hash);
            }
        }

        private static uint NextDeterministicUInt(uint state)
        {
            if (state == 0u) state = 0x6D2B79F5u;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }

    public enum TradePreparationBlockReason
    {
        None = 0,
        InvalidInput,
        MissingWagon,
        MissingDraftAnimal,
        LoadCapacityExceeded,
        InsufficientCurrency,
        ArithmeticOverflow
    }

    [Serializable]
    public sealed class TradePreparationCargoInput
    {
        public string ItemId = string.Empty;
        public int Quantity;
        public bool IsFood;
        public float UnitWeight;
        public long UnitBuyPrice;
        public long UnitExpectedSellPrice;
    }

    [Serializable]
    public sealed class TradePreparationMercenaryInput
    {
        public string MercenaryId = string.Empty;
        public long HireCost;
        public float RiskReduction;
    }

    [Serializable]
    public sealed class TradePreparationEconomyPreviewInput
    {
        public long TradingCurrency;
        public List<TradePreparationCargoInput> Cargo = new List<TradePreparationCargoInput>();
        public int FoodQuantity;
        public float FoodUnitWeight;
        public long FoodUnitPrice;
        public List<TradePreparationMercenaryInput> Mercenaries = new List<TradePreparationMercenaryInput>();
        public bool HasWagon;
        public bool RequiresDraftAnimal = true;
        public int DraftAnimalCount;
        public float EfficientLoad;
        public float MaxLoad;
        public float Distance;
        public float BaseMoveSpeed;
        public float LoadPenaltyPerRatio = 0.5f;
        public float MinimumLoadSpeedMultiplier = 0.1f;
        public float BaseRisk;
        public float DistanceRiskPerUnit;
        public float FoodShortageRisk;
        public int RequiredFoodQuantity;
        public float GrowthRiskMultiplier = 1f;
    }

    [Serializable]
    public sealed class TradePreparationEconomyPreviewResult
    {
        public bool IsValid;
        public bool CanStart;
        public TradePreparationBlockReason BlockReason;
        public long CargoPurchaseCost;
        public long FoodCost;
        public long MercenaryCost;
        public long TotalPreparationCost;
        public long CurrencyAfterPreparation;
        public long ExpectedSellRevenue;
        public long ExpectedNetProfit;
        public float CurrentLoad;
        public float OverloadAmount;
        public float LoadRatio;
        public float SpeedMultiplier;
        public float ExpectedTravelSeconds;
        public float FinalRisk;
    }

    public static class TradePreparationEconomyPreviewCalculator
    {
        public static TradePreparationEconomyPreviewResult Calculate(TradePreparationEconomyPreviewInput input)
        {
            var result = new TradePreparationEconomyPreviewResult();
            if (!HasValidScalars(input))
                return Block(result, TradePreparationBlockReason.InvalidInput, false);

            try
            {
                long purchaseCost = 0L;
                long foodCost = checked(input.FoodUnitPrice * input.FoodQuantity);
                long sellRevenue = 0L;
                double load = (double)input.FoodQuantity * input.FoodUnitWeight;
                int foodQuantity = input.FoodQuantity;

                foreach (TradePreparationCargoInput item in input.Cargo)
                {
                    if (item == null || item.Quantity < 0 || item.UnitWeight < 0f ||
                        item.UnitBuyPrice < 0L || item.UnitExpectedSellPrice < 0L)
                        return Block(result, TradePreparationBlockReason.InvalidInput, false);

                    long lineBuyCost = checked(item.UnitBuyPrice * item.Quantity);
                    if (item.IsFood)
                    {
                        foodCost = checked(foodCost + lineBuyCost);
                        foodQuantity = checked(foodQuantity + item.Quantity);
                    }
                    else
                    {
                        purchaseCost = checked(purchaseCost + lineBuyCost);
                    }
                    sellRevenue = checked(sellRevenue + checked(item.UnitExpectedSellPrice * item.Quantity));
                    load += (double)item.UnitWeight * item.Quantity;
                }

                long mercenaryCost = 0L;
                float riskReduction = 0f;
                foreach (TradePreparationMercenaryInput mercenary in input.Mercenaries)
                {
                    if (mercenary == null || mercenary.HireCost < 0L || mercenary.RiskReduction < 0f)
                        return Block(result, TradePreparationBlockReason.InvalidInput, false);

                    mercenaryCost = checked(mercenaryCost + mercenary.HireCost);
                    riskReduction += mercenary.RiskReduction;
                }

                long totalCost = checked(checked(purchaseCost + foodCost) + mercenaryCost);
                long currencyAfter = checked(input.TradingCurrency - totalCost);
                long netProfit = checked(sellRevenue - totalCost);

                float currentLoad = load >= float.MaxValue ? float.MaxValue : (float)load;
                float overloadAmount = Math.Max(0f, currentLoad - input.EfficientLoad);
                float loadRatio = input.MaxLoad > 0f ? currentLoad / input.MaxLoad : 0f;
                float overloadRatio = input.EfficientLoad > 0f ? overloadAmount / input.EfficientLoad : 0f;
                float speedMultiplier = Math.Max(
                    input.MinimumLoadSpeedMultiplier,
                    1f - overloadRatio * input.LoadPenaltyPerRatio);
                float finalSpeed = input.BaseMoveSpeed * speedMultiplier;
                float travelSeconds = input.Distance <= 0f ? 0f : input.Distance / finalSpeed;

                float foodShortage = input.RequiredFoodQuantity > 0 && foodQuantity < input.RequiredFoodQuantity
                    ? input.FoodShortageRisk * (input.RequiredFoodQuantity - foodQuantity) / input.RequiredFoodQuantity
                    : 0f;
                float risk = (input.BaseRisk + input.Distance * input.DistanceRiskPerUnit + foodShortage - riskReduction)
                    * input.GrowthRiskMultiplier;

                result.IsValid = true;
                result.CargoPurchaseCost = purchaseCost;
                result.FoodCost = foodCost;
                result.MercenaryCost = mercenaryCost;
                result.TotalPreparationCost = totalCost;
                result.CurrencyAfterPreparation = currencyAfter;
                result.ExpectedSellRevenue = sellRevenue;
                result.ExpectedNetProfit = netProfit;
                result.CurrentLoad = currentLoad;
                result.OverloadAmount = overloadAmount;
                result.LoadRatio = Math.Max(0f, loadRatio);
                result.SpeedMultiplier = speedMultiplier;
                result.ExpectedTravelSeconds = Math.Max(0f, travelSeconds);
                result.FinalRisk = Clamp01(risk);

                if (!input.HasWagon)
                    return Block(result, TradePreparationBlockReason.MissingWagon, true);
                if (input.RequiresDraftAnimal && input.DraftAnimalCount <= 0)
                    return Block(result, TradePreparationBlockReason.MissingDraftAnimal, true);
                if (currentLoad > input.MaxLoad)
                    return Block(result, TradePreparationBlockReason.LoadCapacityExceeded, true);
                if (currencyAfter < 0L)
                    return Block(result, TradePreparationBlockReason.InsufficientCurrency, true);

                result.CanStart = true;
                result.BlockReason = TradePreparationBlockReason.None;
                return result;
            }
            catch (OverflowException)
            {
                return Block(result, TradePreparationBlockReason.ArithmeticOverflow, false);
            }
        }

        private static bool HasValidScalars(TradePreparationEconomyPreviewInput input)
        {
            return input != null && input.Cargo != null && input.Mercenaries != null &&
                   input.TradingCurrency >= 0L && input.FoodQuantity >= 0 && input.FoodUnitWeight >= 0f &&
                   input.FoodUnitPrice >= 0L && input.EfficientLoad >= 0f && input.MaxLoad > 0f &&
                   input.EfficientLoad <= input.MaxLoad && input.Distance >= 0f && input.BaseMoveSpeed > 0f &&
                   input.LoadPenaltyPerRatio >= 0f && input.MinimumLoadSpeedMultiplier > 0f &&
                   input.MinimumLoadSpeedMultiplier <= 1f && input.BaseRisk >= 0f &&
                   input.DistanceRiskPerUnit >= 0f && input.FoodShortageRisk >= 0f &&
                   input.RequiredFoodQuantity >= 0 && input.GrowthRiskMultiplier >= 0f;
        }

        private static TradePreparationEconomyPreviewResult Block(
            TradePreparationEconomyPreviewResult result,
            TradePreparationBlockReason reason,
            bool valid)
        {
            result.IsValid = valid;
            result.CanStart = false;
            result.BlockReason = reason;
            return result;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            return value > 1f ? 1f : value;
        }
    }
}
