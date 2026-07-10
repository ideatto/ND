using System.Collections.Generic;
using System.Linq;

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

            case TradePrepareConditionType.OverloadWarning:
                return new TradePrepareConditionResult
                {
                    canStart = true,
                    disabledReason = string.Empty,
                    hasWarning = true,
                    warningMessages = new List<string> { "Load is over the efficient limit. Trade can start, but speed may decrease." }
                };

            case TradePrepareConditionType.LoadExceeded:
                return new TradePrepareConditionResult
                {
                    canStart = false,
                    disabledReason = "Load limit exceeded.",
                    hasWarning = false,
                    warningMessages = new List<string>()
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

            case TradePrepareConditionType.WagonNotSelected:
                return new TradePrepareConditionResult
                {
                    canStart = false,
                    disabledReason = "Wagon is not selected.",
                    hasWarning = false,
                    warningMessages = new List<string>()
                };

            case TradePrepareConditionType.NotEnoughDraftAnimals:
                return new TradePrepareConditionResult
                {
                    canStart = false,
                    disabledReason = "Not enough draft animals selected.",
                    hasWarning = false,
                    warningMessages = new List<string>()
                };

            case TradePrepareConditionType.InvalidDraftAnimalType:
                return new TradePrepareConditionResult
                {
                    canStart = false,
                    disabledReason = "Selected draft animal type is not allowed for this wagon.",
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

        if (input.isWagonRequired && !input.isWagonSelected)
            return Create(TradePrepareConditionType.WagonNotSelected);

        if (input.selectedWagonType == WagonType.WagonWithAnimals)
        {
            if (input.selectedDraftAnimalCount < input.minRequiredDraftAnimalCount)
                return Create(TradePrepareConditionType.NotEnoughDraftAnimals);

            if (!AreSelectedDraftAnimalsAllowed(input.selectedDraftAnimalCount, input.selectedDraftAnimalTypes, input.eligibleDraftAnimalTypes))
                return Create(TradePrepareConditionType.InvalidDraftAnimalType);
        }

        if (input.currentTradingCurrency < input.totalPurchaseCost)
            return Create(TradePrepareConditionType.NotEnoughMoney);

        if (input.currentLoad > input.maxLoad)
            return Create(TradePrepareConditionType.LoadExceeded);

        var warningMessages = new List<string>();

        if (input.loadedFoodQuantity < input.requiredFoodQuantity)
            warningMessages.Add("Food is insufficient. Trade can start, but risk may increase.");

        if (input.selectedMercenaryPower < input.requiredMercenaryPower)
            warningMessages.Add("Mercenary power is insufficient. Trade can start, but combat risk may increase.");

        if (input.overloadLimit > 0f && input.currentLoad > input.overloadLimit)
            warningMessages.Add("Load is over the efficient limit. Trade can start, but speed may decrease.");

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

    private bool AreSelectedDraftAnimalsAllowed(int selectedCount, DraftAnimalType[] selectedTypes, DraftAnimalType[] eligibleTypes)
    {
        if (selectedCount <= 0)
            return true;

        if (selectedTypes == null || selectedTypes.Length < selectedCount)
            return false;

        if (eligibleTypes == null || eligibleTypes.Length == 0)
            return false;

        for (var index = 0; index < selectedCount; index++)
        {
            var selectedType = selectedTypes[index];

            if (selectedType == DraftAnimalType.None)
                return false;

            if (!eligibleTypes.Contains(selectedType))
                return false;
        }

        return true;
    }
}
