using System;
using System.Collections.Generic;

namespace ND.Economy
{
    public static class PriceCalculator
    {
        public const string ErrorNone = "";
        public const string ErrorInvalidTradeItemId = "INVALID_TRADE_ITEM_ID";
        public const string ErrorInvalidTownId = "INVALID_TOWN_ID";
        public const string ErrorInvalidRouteId = "INVALID_ROUTE_ID";
        public const string ErrorInvalidQuantity = "INVALID_QUANTITY";
        public const string ErrorInvalidBasePrice = "INVALID_BASE_PRICE";

        public static PriceCalculationResult Calculate(PriceCalculationInput input)
        {
            PriceCalculationResult result = new PriceCalculationResult();

            string errorCode = Validate(input);
            if (!string.IsNullOrEmpty(errorCode))
            {
                result.IsValid = false;
                result.ErrorCode = errorCode;
                return result;
            }

            result.TradeItemId = input.TradeItemId;
            result.Quantity = input.Quantity;

            float buyPrice = input.BaseBuyPrice;
            float sellPrice = input.BaseSellPrice;

            ApplyModifiers(input.Modifiers, ref buyPrice, ref sellPrice, result.Modifiers);

            result.UnitBuyPrice = ClampFinalPrice(buyPrice);
            result.UnitSellPrice = ClampFinalPrice(sellPrice);
            result.TotalBuyPrice = result.UnitBuyPrice * input.Quantity;
            result.TotalSellPrice = result.UnitSellPrice * input.Quantity;
            result.ExpectedGrossProfit = result.TotalSellPrice - result.TotalBuyPrice;
            result.IsValid = true;
            result.ErrorCode = ErrorNone;

            return result;
        }

        private static string Validate(PriceCalculationInput input)
        {
            if (input == null)
            {
                return ErrorInvalidTradeItemId;
            }

            if (string.IsNullOrWhiteSpace(input.TradeItemId))
            {
                return ErrorInvalidTradeItemId;
            }

            if (string.IsNullOrWhiteSpace(input.FromTownId) || string.IsNullOrWhiteSpace(input.ToTownId))
            {
                return ErrorInvalidTownId;
            }

            if (string.IsNullOrWhiteSpace(input.RouteId))
            {
                return ErrorInvalidRouteId;
            }

            if (input.Quantity <= 0)
            {
                return ErrorInvalidQuantity;
            }

            if (input.BaseBuyPrice <= 0 || input.BaseSellPrice <= 0)
            {
                return ErrorInvalidBasePrice;
            }

            return ErrorNone;
        }

        private static void ApplyModifiers(
            List<PriceModifierInput> modifiers,
            ref float buyPrice,
            ref float sellPrice,
            List<PriceModifierBreakdown> breakdowns)
        {
            if (modifiers == null || modifiers.Count == 0)
            {
                return;
            }

            modifiers.Sort(CompareModifierOrder);

            for (int i = 0; i < modifiers.Count; i++)
            {
                PriceModifierInput modifier = modifiers[i];
                int beforeBuy = ClampFinalPrice(buyPrice);
                int beforeSell = ClampFinalPrice(sellPrice);

                if (modifier.Target == PriceModifierTarget.BuyPrice || modifier.Target == PriceModifierTarget.Both)
                {
                    buyPrice = ApplyModifierValue(buyPrice, modifier.Operation, modifier.Value);
                }

                if (modifier.Target == PriceModifierTarget.SellPrice || modifier.Target == PriceModifierTarget.Both)
                {
                    sellPrice = ApplyModifierValue(sellPrice, modifier.Operation, modifier.Value);
                }

                int afterBuy = ClampFinalPrice(buyPrice);
                int afterSell = ClampFinalPrice(sellPrice);
                int amountDelta = 0;

                if (modifier.Target == PriceModifierTarget.BuyPrice)
                {
                    amountDelta = afterBuy - beforeBuy;
                }
                else if (modifier.Target == PriceModifierTarget.SellPrice)
                {
                    amountDelta = afterSell - beforeSell;
                }
                else
                {
                    amountDelta = (afterBuy - beforeBuy) + (afterSell - beforeSell);
                }

                breakdowns.Add(new PriceModifierBreakdown
                {
                    ModifierType = modifier.ModifierType,
                    SourceId = modifier.SourceId,
                    DisplayNameKey = modifier.DisplayNameKey,
                    Target = modifier.Target,
                    Operation = modifier.Operation,
                    Value = modifier.Value,
                    AmountDelta = amountDelta
                });
            }
        }

        private static int CompareModifierOrder(PriceModifierInput left, PriceModifierInput right)
        {
            return left.ModifierType.CompareTo(right.ModifierType);
        }

        private static float ApplyModifierValue(float price, PriceModifierOperation operation, float value)
        {
            switch (operation)
            {
                case PriceModifierOperation.Add:
                    return price + value;
                case PriceModifierOperation.Multiply:
                    return price * value;
                case PriceModifierOperation.Percent:
                    return price * (1f + value);
                default:
                    return price;
            }
        }

        private static int ClampFinalPrice(float price)
        {
            return Math.Max(1, (int)Math.Round(price, MidpointRounding.AwayFromZero));
        }
    }
}
