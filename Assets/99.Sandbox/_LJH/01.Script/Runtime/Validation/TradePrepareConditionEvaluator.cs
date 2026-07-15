using System.Collections.Generic;
using System.Linq;

public class TradePrepareConditionEvaluator
{
    public TradePrepareConditionResult Create(TradePrepareConditionType conditionType)
    {
        switch (conditionType)
        {
            case TradePrepareConditionType.TradeAlreadyActive:
                return new TradePrepareConditionResult
                {
                    canStart = false,
                    disabledReason = "Another trade is already active or awaiting settlement.",
                    hasWarning = false,
                    warningMessages = new List<string>()
                };

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

            case TradePrepareConditionType.NotEnoughDraftAnimalFood:
                return new TradePrepareConditionResult
                {
                    canStart = true,
                    disabledReason = string.Empty,
                    hasWarning = true,
                    warningMessages = new List<string> { "Draft animal food is insufficient. Trade can start, but risk may increase." }
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

            case TradePrepareConditionType.WagonNotOwned:
                return new TradePrepareConditionResult
                {
                    canStart = false,
                    disabledReason = "Selected wagon is not owned.",
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

            case TradePrepareConditionType.InvalidDraftAnimalSelection:
                return new TradePrepareConditionResult
                {
                    canStart = false,
                    disabledReason = "Selected draft animal is unavailable or exceeds the owned quantity.",
                    hasWarning = false,
                    warningMessages = new List<string>()
                };

            case TradePrepareConditionType.BrokenWagon:
                return new TradePrepareConditionResult
                {
                    canStart = false,
                    disabledReason = "Wagon durability is depleted.",
                    hasWarning = false,
                    warningMessages = new List<string>()
                };

            case TradePrepareConditionType.TooManyDraftAnimals:
                return new TradePrepareConditionResult
                {
                    canStart = false,
                    disabledReason = "Too many draft animals selected for this wagon.",
                    hasWarning = false,
                    warningMessages = new List<string>()
                };

            case TradePrepareConditionType.MixedDraftAnimalType:
                return new TradePrepareConditionResult
                {
                    canStart = true,
                    disabledReason = string.Empty,
                    hasWarning = false,
                    warningMessages = new List<string>()
                };

            case TradePrepareConditionType.NoCargo:
                return new TradePrepareConditionResult
                {
                    canStart = true,
                    disabledReason = string.Empty,
                    hasWarning = true,
                    warningMessages = new List<string>
                    {
                        "No trade cargo is loaded. Trade can start, but no cargo sale revenue is expected."
                    }
                };

            case TradePrepareConditionType.InvalidCargoSelection:
                return new TradePrepareConditionResult
                {
                    canStart = false,
                    disabledReason = "Selected cargo contains an unavailable item or invalid quantity.",
                    hasWarning = false,
                    warningMessages = new List<string>()
                };

            case TradePrepareConditionType.InventorySlotExceeded:
                return new TradePrepareConditionResult
                {
                    canStart = false,
                    disabledReason = "Inventory slot limit exceeded.",
                    hasWarning = false,
                    warningMessages = new List<string>()
                };

            case TradePrepareConditionType.InvalidMercenarySelection:
                return new TradePrepareConditionResult
                {
                    canStart = false,
                    disabledReason = "Selected mercenary is unavailable or locked.",
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
        if (input.isTradeAlreadyActive)
            return Create(TradePrepareConditionType.TradeAlreadyActive);

        if (!input.isRouteSelected)
            return Create(TradePrepareConditionType.RouteNotSelected);

        if (!input.isRouteUnlocked)
            return Create(TradePrepareConditionType.RouteLocked);

        if (input.isWagonRequired && !input.isWagonSelected)
            return Create(TradePrepareConditionType.WagonNotSelected);

        if (input.isWagonSelected && !input.isSelectedWagonOwned)
            return Create(TradePrepareConditionType.WagonNotOwned);

        if (input.isWagonSelected && input.currentWagonDurability <= 0)
            return Create(TradePrepareConditionType.BrokenWagon);

        if (input.hasInvalidDraftAnimalSelection)
            return Create(TradePrepareConditionType.InvalidDraftAnimalSelection);

        if (input.hasInvalidMercenarySelection)
            return Create(TradePrepareConditionType.InvalidMercenarySelection);

        if (input.selectedWagonType == WagonType.WagonWithAnimals)
        {
            if (input.selectedDraftAnimalCount < input.minRequiredDraftAnimalCount)
                return Create(TradePrepareConditionType.NotEnoughDraftAnimals);

            if (input.maxAllowedDraftAnimalCount >= 0 && input.selectedDraftAnimalCount > input.maxAllowedDraftAnimalCount)
                return Create(TradePrepareConditionType.TooManyDraftAnimals);

            if (!AreSelectedDraftAnimalsAllowed(input.selectedDraftAnimalCount, input.selectedDraftAnimalTypes, input.eligibleDraftAnimalTypes))
                return Create(TradePrepareConditionType.InvalidDraftAnimalType);

        }

        if (input.hasInvalidCargoSelection)
            return Create(TradePrepareConditionType.InvalidCargoSelection);

        if (input.usedInventorySlotCount > input.maxInventorySlotCount)
            return Create(TradePrepareConditionType.InventorySlotExceeded);

        if (input.currentTradingCurrency < input.totalPreparationCost)
            return Create(TradePrepareConditionType.NotEnoughMoney);

        if (input.currentLoad > input.maxLoad)
            return Create(TradePrepareConditionType.LoadExceeded);

        var warningMessages = new List<string>();

        if (!input.hasCargo)
            warningMessages.Add("No trade cargo is loaded. Trade can start, but no cargo sale revenue is expected.");

        if (input.loadedDraftAnimalFoodQuantity < input.requiredDraftAnimalFoodQuantity)
            warningMessages.Add("Draft animal food is insufficient. Trade can start, but risk may increase.");

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
