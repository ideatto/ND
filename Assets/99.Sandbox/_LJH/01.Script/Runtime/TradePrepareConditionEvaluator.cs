using System.Collections.Generic;

public class TradePrepareConditionEvaluator
{
    public TradePrepareConditionResult Create(TradePrepareConditionType conditionType)
    {
        switch (conditionType)
        {
            case TradePrepareConditionType.Available:
                return new TradePrepareConditionResult
                {
                    canStart = true,
                    disabledReason = string.Empty,
                    hasWarning = false,
                    warningMessages = new List<string>()
                };

            case TradePrepareConditionType.NotEnoughMoney:
                return new TradePrepareConditionResult
                {
                    canStart = false,
                    disabledReason = "Not enough trading currency.",
                    hasWarning = false,
                    warningMessages = new List<string>()
                };

            case TradePrepareConditionType.NotEnoughFood:
                return new TradePrepareConditionResult
                {
                    canStart = true,
                    disabledReason = string.Empty,
                    hasWarning = true,
                    warningMessages = new List<string> { "Food is insufficient. Trade can start, but risk may increase." }
                };

            case TradePrepareConditionType.NotEnoughMercenaryPower:
                return new TradePrepareConditionResult
                {
                    canStart = true,
                    disabledReason = string.Empty,
                    hasWarning = true,
                    warningMessages = new List<string> { "Mercenary power is insufficient. Trade can start, but combat risk may increase." }
                };

            case TradePrepareConditionType.LoadExceeded:
                return new TradePrepareConditionResult
                {
                    canStart = true,
                    disabledReason = string.Empty,
                    hasWarning = true,
                    warningMessages = new List<string> { "Load limit exceeded. Trade can start, but penalty may occur." }
                };

            case TradePrepareConditionType.RouteLocked:
                return new TradePrepareConditionResult
                {
                    canStart = false,
                    disabledReason = "Route is not unlocked yet.",
                    hasWarning = false,
                    warningMessages = new List<string>()
                };

            case TradePrepareConditionType.RouteNotSelected:
                return new TradePrepareConditionResult
                {
                    canStart = false,
                    disabledReason = "Route is not selected.",
                    hasWarning = false,
                    warningMessages = new List<string>()
                };

            default:
                return new TradePrepareConditionResult
                {
                    canStart = false,
                    disabledReason = "Unknown start condition.",
                    hasWarning = false,
                    warningMessages = new List<string>()
                };
        }
    }

    public TradePrepareConditionResult Evaluate(TradePrepareConditionInput input)
    {
        if (!input.isRouteSelected)
            return Create(TradePrepareConditionType.RouteNotSelected);

        if (!input.isRouteUnlocked)
            return Create(TradePrepareConditionType.RouteLocked);

        if (input.currentTradingCurrency < input.totalPurchaseCost)
            return Create(TradePrepareConditionType.NotEnoughMoney);

        var warningMessages = new List<string>();

        if (input.loadedFoodQuantity < input.requiredFoodQuantity)
            warningMessages.Add("Food is insufficient. Trade can start, but risk may increase.");

        if (input.selectedMercenaryPower < input.requiredMercenaryPower)
            warningMessages.Add("Mercenary power is insufficient. Trade can start, but combat risk may increase.");

        if (input.currentLoad > input.maxLoad)
            warningMessages.Add("Load limit exceeded. Trade can start, but penalty may occur.");

        if (warningMessages.Count > 0)
        {
            return new TradePrepareConditionResult
            {
                canStart = true,
                disabledReason = string.Empty,
                hasWarning = true,
                warningMessages = warningMessages
            };
        }

        return Create(TradePrepareConditionType.Available);
    }
}
