namespace ND.Economy
{
    public static class LjhEconomyM1InputAdapter
    {
        public static CurrencyState ToCurrencyState(global::SaveData saveData)
        {
            if (saveData == null || saveData.player == null)
            {
                return new CurrencyState();
            }

            return new CurrencyState
            {
                TradeMoney = ReadCurrencyField(saveData.player, "tradingCurrency"),
                DevelopmentCurrency = ReadCurrencyField(saveData.player, "developmentCurrency")
            };
        }

        public static void ApplyFinalCurrencyState(global::SaveData saveData, CurrencyState currencyState)
        {
            if (saveData == null || saveData.player == null || currencyState == null)
            {
                return;
            }

            WriteCurrencyField(saveData.player, "tradingCurrency", currencyState.TradeMoney);
            WriteCurrencyField(saveData.player, "developmentCurrency", currencyState.DevelopmentCurrency);
        }

        public static PriceCalculationInput ToPriceCalculationInput(
            global::TradeItemData item,
            global::RouteData route,
            global::SaveData saveData,
            int quantity,
            int playerGrowthLevel = 0,
            int caravanGrowthLevel = 0,
            int oversupplyLevel = 0)
        {
            return new PriceCalculationInput
            {
                TradeItemId = item != null ? item.ItemId : string.Empty,
                FromTownId = route != null ? route.FromTownId : string.Empty,
                ToTownId = route != null ? route.ToTownId : string.Empty,
                RouteId = route != null ? route.RouteId : string.Empty,
                Quantity = quantity,
                BaseBuyPrice = item != null ? item.BaseBuyPrice : 0,
                BaseSellPrice = item != null ? item.BaseSellPrice : 0,
                SeasonId = saveData != null && saveData.world != null ? saveData.world.currentSeason.ToString() : string.Empty,
                DisasterId = saveData != null && saveData.world != null ? saveData.world.currentDisaster.ToString() : string.Empty,
                PlayerGrowthLevel = playerGrowthLevel,
                CaravanGrowthLevel = caravanGrowthLevel,
                OversupplyLevel = oversupplyLevel
            };
        }

        public static EconomyM1LoopInput ToEconomyM1LoopInput(
            global::SaveData saveData,
            global::TradeItemData item,
            global::RouteData route,
            int quantity,
            string tradeId,
            long developmentCurrencyReward,
            bool purchaseGrowth,
            string growthId,
            int playerGrowthLevel,
            int caravanGrowthLevel,
            int growthMaxLevel = 1,
            long growthCostDevelopmentCurrency = 1L)
        {
            return new EconomyM1LoopInput
            {
                PriceInput = ToPriceCalculationInput(
                    item,
                    route,
                    saveData,
                    quantity,
                    playerGrowthLevel,
                    caravanGrowthLevel),
                CurrencyState = ToCurrencyState(saveData),
                TradeId = tradeId,
                FoodCost = route != null ? route.BaseFoodCost : 0,
                MercenaryCost = route != null ? route.BaseMercenaryCost : 0,
                DevelopmentCurrencyReward = developmentCurrencyReward,
                PurchaseGrowth = purchaseGrowth,
                GrowthPurchaseInput = new GrowthPurchaseInput
                {
                    GrowthId = growthId,
                    CurrentLevel = playerGrowthLevel,
                    MaxLevel = growthMaxLevel,
                    CostDevelopmentCurrency = growthCostDevelopmentCurrency
                },
                PlayerGrowthLevel = playerGrowthLevel,
                CaravanGrowthLevel = caravanGrowthLevel
            };
        }

        private static long ReadCurrencyField(object target, string fieldName)
        {
            if (target == null)
            {
                return 0L;
            }

            System.Reflection.FieldInfo field = target.GetType().GetField(fieldName);
            if (field == null)
            {
                return 0L;
            }

            object value = field.GetValue(target);
            return value == null ? 0L : System.Convert.ToInt64(value);
        }

        private static void WriteCurrencyField(object target, string fieldName, long value)
        {
            if (target == null)
            {
                return;
            }

            System.Reflection.FieldInfo field = target.GetType().GetField(fieldName);
            if (field == null)
            {
                return;
            }

            if (field.FieldType == typeof(long))
            {
                field.SetValue(target, value);
                return;
            }

            if (field.FieldType == typeof(int))
            {
                field.SetValue(target, value > int.MaxValue ? int.MaxValue : value < int.MinValue ? int.MinValue : (int)value);
            }
        }
    }
}
