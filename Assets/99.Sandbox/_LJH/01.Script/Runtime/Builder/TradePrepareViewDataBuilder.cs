using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TradePrepareViewDataBuilder
{
    private readonly TradePrepareConditionEvaluator conditionEvaluator = new TradePrepareConditionEvaluator();

    public TradePrepareViewData Build(TradePrepareDraft draft, TradePrepareBuildContext context)
    {
        draft = draft ?? new TradePrepareDraft();
        context = context ?? new TradePrepareBuildContext();

        ND.Framework.SaveData saveData = context.saveData;
        string currentTownId = FirstNotEmpty(
            draft.currentTownId,
            saveData != null && saveData.player != null ? saveData.player.currentTownId : string.Empty);

        TownData currentTown = FindTown(context.towns, currentTownId);
        RouteData[] availableRoutes = MergeUnique(
            context.routes,
            currentTown != null ? currentTown.AvailableRoutes : null,
            route => route != null ? route.RouteId : string.Empty);
        RouteData selectedRoute = FindRoute(availableRoutes, draft.selectedRouteId);
        if (!IsRouteValidForDraft(selectedRoute, currentTownId, draft.selectedDestinationTownId))
        {
            selectedRoute = null;
        }

        TradeItemData[] availableItems = MergeUnique(
            context.tradeItems,
            currentTown != null && currentTown.Market != null ? currentTown.Market.TradeItems : null,
            item => item != null ? item.ItemId : string.Empty);
        WagonData[] availableWagons = MergeUnique(
            context.wagons,
            currentTown != null && currentTown.Market != null ? currentTown.Market.WagonItems : null,
            wagon => wagon != null ? wagon.WagonId : string.Empty);
        WagonData selectedWagon = FindWagon(availableWagons, draft.selectedWagonId);
        DraftAnimalData[] availableAnimals = MergeUnique(
            context.draftAnimals,
            currentTown != null && currentTown.Market != null ? currentTown.Market.DraftAnimalItems : null,
            animal => animal != null ? animal.DraftAnimalId : string.Empty);

        Dictionary<string, int> finalCargoQuantities =
            TradePrepareCaravanFactory.CreateFinalCargoQuantities(draft, saveData);
        CaravanData previewCaravan = TradePrepareCaravanFactory.Create(draft, context);

        float currentLoad = CaravanCalculator.GetCurrentLoad(previewCaravan);
        float overloadLimit = CaravanCalculator.GetFinalEfficientLoad(previewCaravan);
        float maxLoad = CaravanCalculator.GetMaxLoad(previewCaravan);
        int usedSlots = CalculateUsedSlots(finalCargoQuantities, availableItems);
        int maxSlots = CaravanCalculator.GetMaxSlots(previewCaravan);
        float finalTravelTime = selectedRoute != null
            ? CaravanCalculator.GetTravelSeconds(previewCaravan, selectedRoute.Distance)
            : 0f;
        int calculatedDraftAnimalFood = Mathf.CeilToInt(CaravanCalculator.GetRequiredFood(previewCaravan, finalTravelTime));
        int requiredDraftAnimalFood = selectedRoute != null
            ? Mathf.Max(selectedRoute.BaseRequiredDraftAnimalFoodQuantity, calculatedDraftAnimalFood)
            : 0;
        int loadedDraftAnimalFood = GetCategoryQuantity(
            finalCargoQuantities,
            availableItems,
            TradeItemCategory.DraftAnimalsFood);

        long totalPurchaseCost;
        long draftAnimalFoodCost;
        long sellRevenue;
        TradeItemViewData[] itemViewData = BuildTradeItems(
            availableItems,
            selectedRoute,
            saveData,
            draft,
            finalCargoQuantities,
            out totalPurchaseCost,
            out draftAnimalFoodCost,
            out sellRevenue);

        int selectedMercenaryPower;
        long selectedMercenaryHireCost;
        MercenaryViewData[] mercenaryViewData = BuildMercenaries(
            context.mercenaries,
            draft,
            out selectedMercenaryPower,
            out selectedMercenaryHireCost);

        // Mercenary cost comes from the actually selected mercenaries, not the legacy route cost.
        long mercenaryCost = selectedMercenaryHireCost;
        long totalPreparationCost = AddClamped(totalPurchaseCost, mercenaryCost);
        long currentTradingCurrency = ReadTradingCurrency(saveData);
        CalculateCurrencyProjection(
            currentTradingCurrency,
            totalPurchaseCost,
            mercenaryCost,
            out long estimatedCurrencyAfterPurchase,
            out long estimatedCurrencyAfterHire,
            out bool canPurchaseCargo,
            out bool canHireSelectedMercenaries);
        long estimatedNetProfit = sellRevenue - totalPreparationCost;
        bool routeUnlocked = IsRouteUnlocked(selectedRoute, saveData);
        DraftAnimalType[] selectedAnimalTypes = GetSelectedAnimalTypes(draft, availableAnimals);

        TradePrepareConditionInput conditionInput = new TradePrepareConditionInput
        {
            isTradeAlreadyActive = IsTradeAlreadyActive(saveData),
            isRouteSelected = selectedRoute != null,
            isRouteUnlocked = routeUnlocked,
            isWagonRequired = true,
            isWagonSelected = selectedWagon != null,
            // Walking (WagonType.None) is a travel method, not an inventory-owned wagon.
            // Treat it as satisfying ownership here just as BuildWagons allows selecting it;
            // otherwise the final departure check contradicts S3 and blocks every walk attempt.
            isSelectedWagonOwned = selectedWagon != null &&
                (selectedWagon.WagonType == WagonType.None || IsSavedWagon(saveData, selectedWagon)),
            currentWagonDurability = GetCurrentDurability(saveData, selectedWagon),
            selectedWagonType = selectedWagon != null ? selectedWagon.WagonType : WagonType.None,
            selectedDraftAnimalCount = selectedAnimalTypes.Length,
            minRequiredDraftAnimalCount = selectedWagon != null ? selectedWagon.MinRequireAnimals : 0,
            maxAllowedDraftAnimalCount = selectedWagon != null ? selectedWagon.MaxPullAnimals : 0,
            selectedDraftAnimalTypes = selectedAnimalTypes,
            eligibleDraftAnimalTypes = selectedWagon != null ? selectedWagon.EligibleAnimalTypes : new DraftAnimalType[0],
            hasInvalidDraftAnimalSelection = HasInvalidDraftAnimalSelection(saveData, draft, availableAnimals),
            hasCargo = HasKnownPositiveCargo(finalCargoQuantities, availableItems),
            // Cargo is no longer purchased or edited during preparation. SaveData cargo is authoritative.
            hasInvalidCargoSelection = false,
            usedInventorySlotCount = usedSlots,
            maxInventorySlotCount = maxSlots,
            currentTradingCurrency = currentTradingCurrency,
            totalPurchaseCost = totalPurchaseCost,
            totalPreparationCost = totalPreparationCost,
            currentLoad = currentLoad,
            overloadLimit = overloadLimit,
            maxLoad = maxLoad,
            loadedDraftAnimalFoodQuantity = loadedDraftAnimalFood,
            requiredDraftAnimalFoodQuantity = requiredDraftAnimalFood,
            selectedMercenaryPower = selectedMercenaryPower,
            requiredMercenaryPower = selectedRoute != null ? selectedRoute.BaseRequiredMercenaryPower : 0,
            hasInvalidMercenarySelection = HasInvalidMercenarySelection(draft, context.mercenaries)
        };

        return new TradePrepareViewData
        {
            // The selected Caravan identity must survive ViewData projection so every later UI request
            // remains scoped to the same Caravan chosen in the overview flow.
            selectedCaravanId = draft.selectedCaravanId ?? string.Empty,
            currentTownId = currentTownId,
            currentTownName = currentTown != null ? currentTown.DisplayName : string.Empty,
            currentTradingCurrency = currentTradingCurrency,
            currentDevelopmentCurrency = ReadDevelopmentCurrency(saveData),
            towns = BuildTowns(context.towns, currentTownId, saveData),
            routes = BuildRoutes(availableRoutes, currentTownId, saveData),
            tradeItems = itemViewData,
            selectedRouteId = draft.selectedRouteId ?? string.Empty,
            currentLoad = currentLoad,
            overloadLimit = overloadLimit,
            maxLoad = maxLoad,
            usedInventorySlotCount = usedSlots,
            maxInventorySlotCount = maxSlots,
            totalPurchaseCost = totalPurchaseCost,
            estimatedCurrencyAfterPurchase = estimatedCurrencyAfterPurchase,
            canPurchaseCargo = canPurchaseCargo,
            loadedDraftAnimalFoodQuantity = loadedDraftAnimalFood,
            requiredDraftAnimalFoodQuantity = requiredDraftAnimalFood,
            selectedMercenaryPower = selectedMercenaryPower,
            requiredMercenaryPower = selectedRoute != null ? selectedRoute.BaseRequiredMercenaryPower : 0,
            startCondition = conditionEvaluator.Evaluate(conditionInput),
            wagons = BuildWagons(availableWagons, saveData),
            draftAnimals = BuildDraftAnimals(availableAnimals, selectedWagon, draft, saveData),
            loadedItems = BuildLoadedItems(finalCargoQuantities, availableItems, selectedRoute, saveData),
            mercenaries = mercenaryViewData,
            selectedWagonId = draft.selectedWagonId ?? string.Empty,
            draftAnimalFoodCost = draftAnimalFoodCost,
            mercenaryCost = mercenaryCost,
            totalPreparationCost = totalPreparationCost,
            estimatedCurrencyAfterHire = estimatedCurrencyAfterHire,
            canHireSelectedMercenaries = canHireSelectedMercenaries,
            estimatedSellRevenue = sellRevenue,
            estimatedNetProfit = estimatedNetProfit,
            baseExpectedTravelTime = selectedRoute != null ? selectedRoute.DefaultElapsedTime : 0f,
            finalExpectedTravelTime = finalTravelTime,
            selectedMoveSpeed = selectedRoute != null && finalTravelTime > 0f
                ? selectedRoute.Distance / finalTravelTime
                : 0f
        };
    }

    internal static CaravanData CreatePreviewCaravan(
        Dictionary<string, int> cargoQuantities,
        TradeItemData[] items,
        ND.Framework.SaveData saveData,
        WagonData wagon,
        DraftAnimalData[] animals,
        MercenaryData[] mercenaries,
        TradePrepareDraft draft)
    {
        var caravan = new CaravanData
        {
            wagon = wagon != null ? new imsiWagonData
            {
                wagonName = wagon.WagonId,
                overLoad = wagon.Overload,
                maxLoad = wagon.MaxLoad,
                minAnimals = wagon.MinRequireAnimals,
                maxAnimals = wagon.MaxPullAnimals,
                speedModifier = wagon.BaseMoveSpeed,
                maxDurability = wagon.MaxDurability,
                inventorySlotCount = wagon.InventorySlotCount
            } : null,
            currentDurability = GetCurrentDurability(saveData, wagon)
        };

        float draftAnimalFoodWeight = 0f;

        foreach (KeyValuePair<string, int> pair in cargoQuantities)
        {
            if (pair.Value <= 0)
            {
                continue;
            }

            TradeItemData item = FindItem(items, pair.Key);
            if (item == null)
            {
                ND.Framework.TradeItemSaveData savedItem = FindSavedCargoItem(saveData, pair.Key);
                if (savedItem != null)
                {
                    caravan.cargo.Add(new CargoEntry
                    {
                        item = new imsiTradeItemData
                        {
                            id = pair.Key,
                            itemName = savedItem.itemName,
                            weight = savedItem.weight,
                            basePrice = savedItem.basePrice,
                            maxCount = savedItem.maxCount
                        },
                        quantity = pair.Value
                    });
                }

                continue;
            }

            if (item.Category == TradeItemCategory.DraftAnimalsFood)
            {
                caravan.foodAmount = AddClampedInt(caravan.foodAmount, pair.Value);
                draftAnimalFoodWeight += item.Weight * pair.Value;
                continue;
            }

            caravan.cargo.Add(new CargoEntry
            {
                item = new imsiTradeItemData
                {
                    id = pair.Key,
                    itemName = item.DisplayName,
                    weight = item.Weight,
                    basePrice = item.BaseBuyPrice,
                    maxCount = item.MaxCount
                },
                quantity = pair.Value
            });
        }

        caravan.foodUnitWeight = caravan.foodAmount > 0
            ? draftAnimalFoodWeight / caravan.foodAmount
            : 0f;

        if (draft.selectedAnimals != null)
        {
            for (int selectionIndex = 0; selectionIndex < draft.selectedAnimals.Count; selectionIndex++)
            {
                DraftAnimalSelectionData selection = draft.selectedAnimals[selectionIndex];
                DraftAnimalData animal = selection != null ? FindAnimal(animals, selection.draftAnimalId) : null;
                int quantity = selection != null ? Mathf.Max(0, selection.quantity) : 0;
                for (int count = 0; animal != null && count < quantity; count++)
                {
                    caravan.animals.Add(new imsiAnimalData
                    {
                        animalName = animal.DraftAnimalId,
                        speed = animal.BaseMoveSpeed,
                        foodPerKm = animal.FeedConsumption,
                        increaseOverLoad = animal.IncreaseOverLoad,
                        // The M1 UI contract ignores max-load increases from animals.
                        increaseMaxLoad = 0f,
                        animalType = animal.AnimalType
                    });
                }
            }
        }

        mercenaries = mercenaries ?? new MercenaryData[0];
        for (int index = 0; index < mercenaries.Length; index++)
        {
            MercenaryData mercenary = mercenaries[index];
            if (mercenary != null && draft.IsMercenarySelected(mercenary.MercenaryId))
            {
                caravan.mercenaries.Add(new imsiMercenaryData
                {
                    mercName = mercenary.MercenaryId,
                    combatPower = mercenary.CombatCapability,
                    contractCount = 1
                });
            }
        }

        return caravan;
    }

    internal static Dictionary<string, int> CreateFinalCargoQuantities(TradePrepareDraft draft)
    {
        return CreateFinalCargoQuantities(draft, null);
    }

    internal static Dictionary<string, int> CreateFinalCargoQuantities(
        TradePrepareDraft draft,
        ND.Framework.SaveData saveData)
    {
        var quantities = new Dictionary<string, int>(StringComparer.Ordinal);
        if (saveData != null && saveData.caravan != null && saveData.caravan.cargo != null)
        {
            foreach (ND.Framework.CargoEntrySaveData cargo in saveData.caravan.cargo)
            {
                if (cargo != null && cargo.item != null && cargo.quantity > 0)
                    AddQuantity(quantities, cargo.item.itemId, cargo.quantity);
            }
        }

        return quantities;
    }

    private static void AddQuantity(Dictionary<string, int> quantities, string itemId, int amount)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return;
        }

        int current;
        quantities.TryGetValue(itemId, out current);
        quantities[itemId] = Mathf.Max(0, current + amount);
    }

    private static TradeItemViewData[] BuildTradeItems(
        TradeItemData[] items,
        RouteData route,
        ND.Framework.SaveData saveData,
        TradePrepareDraft draft,
        Dictionary<string, int> finalCargoQuantities,
        out long totalPurchaseCost,
        out long draftAnimalFoodCost,
        out long totalSellRevenue)
    {
        var result = new List<TradeItemViewData>();
        totalPurchaseCost = 0L;
        draftAnimalFoodCost = 0L;
        totalSellRevenue = 0L;

        for (int index = 0; index < items.Length; index++)
        {
            TradeItemData item = items[index];
            if (item == null)
            {
                continue;
            }

            const int buyAmount = 0;
            ND.Economy.PriceCalculationResult price = CalculatePrice(item, route, saveData, 1);
            long purchasePrice = price.IsValid ? price.UnitBuyPrice : 0L;
            long sellPrice = price.IsValid ? price.UnitSellPrice : 0L;
            long linePurchaseCost = MultiplyClamped(purchasePrice, buyAmount);
            totalPurchaseCost = AddClamped(totalPurchaseCost, linePurchaseCost);
            if (item.Category == TradeItemCategory.DraftAnimalsFood)
            {
                draftAnimalFoodCost = AddClamped(draftAnimalFoodCost, linePurchaseCost);
            }
            int finalCargoQuantity;
            finalCargoQuantities.TryGetValue(item.ItemId, out finalCargoQuantity);

            result.Add(new TradeItemViewData
            {
                itemId = item.ItemId,
                displayName = item.DisplayName,
                icon = item.Icon,
                description = item.Description,
                rarity = item.Rarity,
                category = item.Category,
                purchasePrice = purchasePrice,
                sellPrice = sellPrice,
                ownedAmount = Mathf.Max(0, finalCargoQuantity),
                selectedBuyAmount = 0,
                selectedSellAmount = 0,
                // TradeItemData.MaxCount is only a temporary ceiling until market stock is provided.
                contentQuantityLimit = item.MaxCount,
                hasAuthoritativeStock = false,
                unitWeight = item.Weight,
                selectedWeight = item.Weight * Mathf.Max(0, finalCargoQuantity),
                canBuy = false,
                canSell = false,
                buyDisabledReason = "Cargo purchases are handled by the town market.",
                sellDisabledReason = string.Empty
            });
        }

        return result.ToArray();
    }

    private static ND.Economy.PriceCalculationResult CalculatePrice(
        TradeItemData item,
        RouteData route,
        ND.Framework.SaveData saveData,
        int quantity)
    {
        return ND.Economy.PriceCalculator.Calculate(new ND.Economy.PriceCalculationInput
        {
            TradeItemId = item != null ? item.ItemId : string.Empty,
            FromTownId = route != null ? route.FromTownId : string.Empty,
            ToTownId = route != null ? route.ToTownId : string.Empty,
            RouteId = route != null ? route.RouteId : string.Empty,
            Quantity = Mathf.Max(1, quantity),
            BaseBuyPrice = item != null ? item.BaseBuyPrice : 0L,
            BaseSellPrice = item != null ? item.BaseSellPrice : 0L,
            SeasonId = saveData != null && saveData.world != null ? saveData.world.currentSeasonId : string.Empty,
            DisasterId = saveData != null && saveData.world != null ? saveData.world.currentDisasterId : string.Empty,
            PlayerGrowthLevel = saveData != null && saveData.player != null ? saveData.player.playerGrowthLevel : 0,
            CaravanGrowthLevel = saveData != null && saveData.player != null ? saveData.player.caravanGrowthLevel : 0,
            Modifiers = ND.Economy.LjhEconomyM1InputAdapter.ToPriceModifierInputs(item != null ? item.Modifiers : null)
        });
    }

    private static CargoItemViewData[] BuildLoadedItems(
        Dictionary<string, int> quantities,
        TradeItemData[] items,
        RouteData route,
        ND.Framework.SaveData saveData)
    {
        var result = new List<CargoItemViewData>();
        foreach (KeyValuePair<string, int> pair in quantities)
        {
            if (pair.Value <= 0)
            {
                continue;
            }

            TradeItemData item = FindItem(items, pair.Key);
            if (item == null)
            {
                continue;
            }

            ND.Economy.PriceCalculationResult price = CalculatePrice(item, route, saveData, 1);
            long unitPrice = price.IsValid ? price.UnitBuyPrice : 0L;
            long estimatedSellUnitPrice = price.IsValid ? price.UnitSellPrice : 0L;
            float unitWeight = item.Weight;
            result.Add(new CargoItemViewData
            {
                itemId = pair.Key,
                displayName = item.DisplayName,
                icon = item.Icon,
                category = item.Category,
                quantity = pair.Value,
                unitWeight = unitWeight,
                totalWeight = unitWeight * pair.Value,
                purchaseUnitPrice = unitPrice,
                estimatedSellUnitPrice = estimatedSellUnitPrice,
                totalPurchasePrice = MultiplyClamped(unitPrice, pair.Value)
            });
        }

        return result.ToArray();
    }

    private static MercenaryViewData[] BuildMercenaries(
        MercenaryData[] mercenaries,
        TradePrepareDraft draft,
        out int selectedPower,
        out long selectedCost)
    {
        var result = new List<MercenaryViewData>();
        selectedPower = 0;
        selectedCost = 0L;
        mercenaries = mercenaries ?? new MercenaryData[0];

        for (int index = 0; index < mercenaries.Length; index++)
        {
            MercenaryData mercenary = mercenaries[index];
            if (mercenary == null)
            {
                continue;
            }

            bool selected = draft.IsMercenarySelected(mercenary.MercenaryId);
            if (selected)
            {
                selectedPower += Mathf.Max(0, mercenary.CombatCapability);
                selectedCost = AddClamped(selectedCost, Math.Max(0L, mercenary.BaseBuyPrice));
            }

            result.Add(new MercenaryViewData
            {
                mercenaryId = mercenary.MercenaryId,
                displayName = mercenary.DisplayName,
                icon = mercenary.Icon,
                description = mercenary.Description,
                combatCapability = mercenary.CombatCapability,
                baseBuyPrice = mercenary.BaseBuyPrice,
                isSelected = selected,
                canHire = mercenary.UnlockedByDefault,
                disabledReason = mercenary.UnlockedByDefault ? string.Empty : "Mercenary is locked."
            });
        }

        return result.ToArray();
    }

    private static TownViewData[] BuildTowns(TownData[] towns, string currentTownId, ND.Framework.SaveData saveData)
    {
        var result = new List<TownViewData>();
        towns = towns ?? new TownData[0];
        for (int index = 0; index < towns.Length; index++)
        {
            TownData town = towns[index];
            if (town == null)
            {
                continue;
            }

            bool unlocked = town.UnlockedByDefault || Contains(saveData != null && saveData.world != null ? saveData.world.unlockedTownIds : null, town.TownId);
            bool isCurrent = string.Equals(town.TownId, currentTownId, StringComparison.Ordinal);
            result.Add(new TownViewData
            {
                townId = town.TownId,
                displayName = town.DisplayName,
                icon = town.Icon,
                description = town.Description,
                isUnlocked = unlocked || isCurrent,
                isCurrentTown = isCurrent,
                canSelect = (unlocked || isCurrent) && !isCurrent,
                disabledReason = isCurrent ? "Current town." : unlocked ? string.Empty : "Town is locked."
            });
        }

        return result.ToArray();
    }

    private static RouteViewData[] BuildRoutes(RouteData[] routes, string currentTownId, ND.Framework.SaveData saveData)
    {
        var result = new List<RouteViewData>();
        routes = routes ?? new RouteData[0];
        for (int index = 0; index < routes.Length; index++)
        {
            RouteData route = routes[index];
            if (route == null || !string.Equals(route.FromTownId, currentTownId, StringComparison.Ordinal))
            {
                continue;
            }

            bool unlocked = IsRouteUnlocked(route, saveData);
            result.Add(new RouteViewData
            {
                routeId = route.RouteId,
                displayName = route.DisplayName,
                fromTownId = route.FromTownId,
                fromTownName = route.FromTownName,
                toTownId = route.ToTownId,
                toTownName = route.ToTownName,
                distance = route.Distance,
                estimatedTime = route.DefaultElapsedTime,
                requiredDraftAnimalFoodQuantity = route.BaseRequiredDraftAnimalFoodQuantity,
                requiredMercenaryPower = route.BaseRequiredMercenaryPower,
                riskLevel = route.BaseRiskLevel,
                isUnlocked = unlocked,
                canSelect = unlocked,
                disabledReason = unlocked ? string.Empty : "Route is locked."
            });
        }

        return result.ToArray();
    }

    private static WagonViewData[] BuildWagons(WagonData[] wagons, ND.Framework.SaveData saveData)
    {
        var result = new List<WagonViewData>();
        for (int index = 0; index < wagons.Length; index++)
        {
            WagonData wagon = wagons[index];
            if (wagon == null)
            {
                continue;
            }

            bool owned = IsSavedWagon(saveData, wagon);
            // None represents walking, which is always available and does not require ownership.
            bool canSelect = wagon.WagonType == WagonType.None || owned;
            result.Add(new WagonViewData
            {
                wagonId = wagon.WagonId,
                displayName = wagon.DisplayName,
                icon = wagon.Icon,
                description = wagon.Description,
                wagonType = wagon.WagonType,
                baseMoveSpeed = wagon.BaseMoveSpeed,
                currentDurability = GetCurrentDurability(saveData, wagon),
                maxDurability = wagon.MaxDurability,
                overLoad = wagon.Overload,
                maxLoad = wagon.MaxLoad,
                inventorySlotCount = wagon.InventorySlotCount,
                eligibleAnimalTypes = wagon.EligibleAnimalTypes,
                minRequireAnimals = wagon.MinRequireAnimals,
                maxPullAnimals = wagon.MaxPullAnimals,
                ownedAmount = owned ? 1 : 0,
                isOwned = owned,
                canSelect = canSelect,
                disabledReason = canSelect ? string.Empty : "Wagon is not owned."
            });
        }

        return result.ToArray();
    }

    private static DraftAnimalViewData[] BuildDraftAnimals(
        DraftAnimalData[] animals,
        WagonData selectedWagon,
        TradePrepareDraft draft,
        ND.Framework.SaveData saveData)
    {
        var result = new List<DraftAnimalViewData>();
        for (int index = 0; index < animals.Length; index++)
        {
            DraftAnimalData animal = animals[index];
            if (animal == null)
            {
                continue;
            }

            bool eligible = selectedWagon != null && Array.IndexOf(selectedWagon.EligibleAnimalTypes, animal.AnimalType) >= 0;
            int ownedAmount = GetOwnedAnimalCount(saveData, animal);
            result.Add(new DraftAnimalViewData
            {
                draftAnimalId = animal.DraftAnimalId,
                displayName = animal.DisplayName,
                icon = animal.Icon,
                description = animal.Description,
                animalType = animal.AnimalType,
                feedConsumption = animal.FeedConsumption,
                baseMoveSpeed = animal.BaseMoveSpeed,
                increaseOverLoad = animal.IncreaseOverLoad,
                increaseMaxLoad = animal.IncreaseMaxLoad,
                ownedAmount = ownedAmount,
                selectedAmount = GetSelectedAnimalQuantity(draft, animal.DraftAnimalId),
                maxSelectableAmount = selectedWagon != null ? Mathf.Min(selectedWagon.MaxPullAnimals, ownedAmount) : 0,
                isEligibleForSelectedWagon = eligible,
                canSelect = eligible && ownedAmount > 0,
                disabledReason = !eligible
                    ? "Animal type is not allowed for the selected wagon."
                    : ownedAmount > 0 ? string.Empty : "Draft animal is not owned."
            });
        }

        return result.ToArray();
    }

    private static DraftAnimalType[] GetSelectedAnimalTypes(TradePrepareDraft draft, DraftAnimalData[] animals)
    {
        var result = new List<DraftAnimalType>();
        if (draft.selectedAnimals == null)
        {
            return result.ToArray();
        }

        for (int index = 0; index < draft.selectedAnimals.Count; index++)
        {
            DraftAnimalSelectionData selection = draft.selectedAnimals[index];
            DraftAnimalData animal = selection != null ? FindAnimal(animals, selection.draftAnimalId) : null;
            for (int count = 0; animal != null && count < Mathf.Max(0, selection.quantity); count++)
            {
                result.Add(animal.AnimalType);
            }
        }

        return result.ToArray();
    }

    private static bool IsRouteUnlocked(RouteData route, ND.Framework.SaveData saveData)
    {
        return route != null && (route.UnlockedByDefault
            || Contains(saveData != null && saveData.world != null ? saveData.world.unlockedRouteIds : null, route.RouteId));
    }

    private static bool IsRouteValidForDraft(RouteData route, string currentTownId, string destinationTownId)
    {
        if (route == null || !string.Equals(route.FromTownId, currentTownId, StringComparison.Ordinal))
        {
            return false;
        }

        return string.IsNullOrEmpty(destinationTownId)
            || string.Equals(route.ToTownId, destinationTownId, StringComparison.Ordinal);
    }

    private static bool HasKnownPositiveCargo(
        Dictionary<string, int> quantities,
        TradeItemData[] items)
    {
        if (quantities == null)
        {
            return false;
        }

        foreach (KeyValuePair<string, int> pair in quantities)
        {
            TradeItemData item = FindItem(items, pair.Key);
            if (pair.Value > 0
                && item != null
                && item.Category != TradeItemCategory.DraftAnimalsFood)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetCategoryQuantity(
        Dictionary<string, int> quantities,
        TradeItemData[] items,
        TradeItemCategory category)
    {
        int total = 0;
        if (quantities == null)
        {
            return total;
        }

        foreach (KeyValuePair<string, int> pair in quantities)
        {
            TradeItemData item = FindItem(items, pair.Key);
            if (item != null && item.Category == category)
            {
                total = AddClampedInt(total, pair.Value);
            }
        }

        return total;
    }

    private static int CalculateUsedSlots(
        Dictionary<string, int> quantities,
        TradeItemData[] items)
    {
        int total = 0;
        if (quantities == null)
        {
            return total;
        }

        foreach (KeyValuePair<string, int> pair in quantities)
        {
            TradeItemData item = FindItem(items, pair.Key);
            int quantity = Mathf.Max(0, pair.Value);
            if (item == null || quantity == 0)
            {
                continue;
            }

            int stackSize = Mathf.Max(1, item.MaxCount);
            int slots = quantity / stackSize + (quantity % stackSize == 0 ? 0 : 1);
            total = AddClampedInt(total, slots);
        }

        return total;
    }

    private static bool HasInvalidDraftAnimalSelection(
        ND.Framework.SaveData saveData,
        TradePrepareDraft draft,
        DraftAnimalData[] animals)
    {
        if (draft.selectedAnimals == null)
        {
            return false;
        }

        for (int index = 0; index < draft.selectedAnimals.Count; index++)
        {
            DraftAnimalSelectionData selection = draft.selectedAnimals[index];
            DraftAnimalData animal = selection != null ? FindAnimal(animals, selection.draftAnimalId) : null;
            if (selection == null || selection.quantity <= 0 || animal == null
                || selection.quantity > GetOwnedAnimalCount(saveData, animal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasInvalidMercenarySelection(TradePrepareDraft draft, MercenaryData[] mercenaries)
    {
        for (int selectionIndex = 0; selectionIndex < draft.SelectedMercenaryIds.Count; selectionIndex++)
        {
            string selectedId = draft.SelectedMercenaryIds[selectionIndex];
            bool found = false;
            if (mercenaries != null)
            {
                for (int dataIndex = 0; dataIndex < mercenaries.Length; dataIndex++)
                {
                    MercenaryData mercenary = mercenaries[dataIndex];
                    if (mercenary != null && string.Equals(mercenary.MercenaryId, selectedId, StringComparison.Ordinal))
                    {
                        found = mercenary.UnlockedByDefault;
                        break;
                    }
                }
            }

            if (!found)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSavedWagon(ND.Framework.SaveData saveData, WagonData wagon)
    {
        if (saveData == null || saveData.caravan == null || saveData.caravan.wagon == null || wagon == null)
        {
            return false;
        }

        string savedName = saveData.caravan.wagon.wagonName;
        return string.Equals(savedName, wagon.WagonId, StringComparison.Ordinal)
            || string.Equals(savedName, wagon.DisplayName, StringComparison.Ordinal);
    }

    private static int GetCurrentDurability(ND.Framework.SaveData saveData, WagonData wagon)
    {
        return IsSavedWagon(saveData, wagon) ? Mathf.Max(0, saveData.caravan.currentDurability) : wagon != null ? wagon.MaxDurability : 0;
    }

    private static int GetOwnedAnimalCount(ND.Framework.SaveData saveData, DraftAnimalData animal)
    {
        int count = 0;
        if (saveData == null || saveData.caravan == null || saveData.caravan.animals == null || animal == null)
        {
            return count;
        }

        for (int index = 0; index < saveData.caravan.animals.Count; index++)
        {
            ND.Framework.AnimalSaveData saved = saveData.caravan.animals[index];
            if (saved != null && (string.Equals(saved.animalName, animal.DraftAnimalId, StringComparison.Ordinal)
                || string.Equals(saved.animalName, animal.DisplayName, StringComparison.Ordinal)))
            {
                count++;
            }
        }

        return count;
    }

    private static int GetSelectedQuantity(List<TradeItemBundle> selections, string itemId)
    {
        int total = 0;
        if (selections == null)
        {
            return total;
        }

        for (int index = 0; index < selections.Count; index++)
        {
            TradeItemBundle selection = selections[index];
            if (selection != null && string.Equals(selection.itemId, itemId, StringComparison.Ordinal))
            {
                total += Mathf.Max(0, selection.quantity);
            }
        }

        return total;
    }

    private static int GetSelectedAnimalQuantity(TradePrepareDraft draft, string animalId)
    {
        int total = 0;
        if (draft.selectedAnimals == null)
        {
            return total;
        }

        for (int index = 0; index < draft.selectedAnimals.Count; index++)
        {
            DraftAnimalSelectionData selection = draft.selectedAnimals[index];
            if (selection != null && string.Equals(selection.draftAnimalId, animalId, StringComparison.Ordinal))
            {
                total += Mathf.Max(0, selection.quantity);
            }
        }

        return total;
    }

    internal static T[] MergeUnique<T>(T[] first, T[] second, Func<T, string> getId)
    {
        var result = new List<T>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        AddUnique(result, ids, first, getId);
        AddUnique(result, ids, second, getId);
        return result.ToArray();
    }

    private static void AddUnique<T>(List<T> result, HashSet<string> ids, T[] source, Func<T, string> getId)
    {
        if (source == null)
        {
            return;
        }

        for (int index = 0; index < source.Length; index++)
        {
            T item = source[index];
            string id = !ReferenceEquals(item, null) ? getId(item) : string.Empty;
            if (!string.IsNullOrEmpty(id) && ids.Add(id))
            {
                result.Add(item);
            }
        }
    }

    internal static TownData FindTown(TownData[] towns, string id)
    {
        if (towns != null)
        {
            for (int index = 0; index < towns.Length; index++)
            {
                if (towns[index] != null && string.Equals(towns[index].TownId, id, StringComparison.Ordinal)) return towns[index];
            }
        }
        return null;
    }

    internal static RouteData FindRoute(RouteData[] routes, string id)
    {
        if (routes != null)
        {
            for (int index = 0; index < routes.Length; index++)
            {
                if (routes[index] != null && string.Equals(routes[index].RouteId, id, StringComparison.Ordinal)) return routes[index];
            }
        }
        return null;
    }

    internal static WagonData FindWagon(WagonData[] wagons, string id)
    {
        if (wagons != null)
        {
            for (int index = 0; index < wagons.Length; index++)
            {
                if (wagons[index] != null && string.Equals(wagons[index].WagonId, id, StringComparison.Ordinal)) return wagons[index];
            }
        }
        return null;
    }

    internal static DraftAnimalData FindAnimal(DraftAnimalData[] animals, string id)
    {
        if (animals != null)
        {
            for (int index = 0; index < animals.Length; index++)
            {
                if (animals[index] != null && string.Equals(animals[index].DraftAnimalId, id, StringComparison.Ordinal)) return animals[index];
            }
        }
        return null;
    }

    internal static TradeItemData FindItem(TradeItemData[] items, string id)
    {
        if (items != null)
        {
            for (int index = 0; index < items.Length; index++)
            {
                if (items[index] != null && string.Equals(items[index].ItemId, id, StringComparison.Ordinal)) return items[index];
            }
        }
        return null;
    }

    private static ND.Framework.TradeItemSaveData FindSavedCargoItem(
        ND.Framework.SaveData saveData,
        string itemId)
    {
        if (saveData == null || saveData.caravan == null || saveData.caravan.cargo == null)
            return null;

        foreach (ND.Framework.CargoEntrySaveData cargo in saveData.caravan.cargo)
        {
            if (cargo != null && cargo.item != null
                && string.Equals(cargo.item.itemId, itemId, StringComparison.Ordinal))
            {
                return cargo.item;
            }
        }

        return null;
    }

    private static bool Contains(List<string> values, string value)
    {
        return values != null && !string.IsNullOrEmpty(value) && values.Contains(value);
    }

    private static string FirstNotEmpty(string first, string second)
    {
        return !string.IsNullOrEmpty(first) ? first : second ?? string.Empty;
    }

    private static long ReadTradingCurrency(ND.Framework.SaveData saveData)
    {
        return saveData != null && saveData.player != null ? saveData.player.tradingCurrency : 0L;
    }

    private static bool IsTradeAlreadyActive(ND.Framework.SaveData saveData)
    {
        if (saveData == null || saveData.tradeProgress == null)
        {
            return false;
        }

        return saveData.tradeProgress.state == ND.Framework.TradeProgressState.Traveling
            || saveData.tradeProgress.state == ND.Framework.TradeProgressState.SettlementPending;
    }

    private static long ReadDevelopmentCurrency(ND.Framework.SaveData saveData)
    {
        return saveData != null && saveData.player != null ? saveData.player.developmentCurrency : 0L;
    }

    private static long MultiplyClamped(long value, int quantity)
    {
        if (value <= 0L || quantity <= 0) return 0L;
        return value > long.MaxValue / quantity ? long.MaxValue : value * quantity;
    }

    private static long SubtractFloorZero(long value, long cost)
    {
        if (value <= 0L || cost >= value)
        {
            return 0L;
        }

        return cost <= 0L ? value : value - cost;
    }

    internal static void CalculateCurrencyProjection(
        long currentCurrency,
        long purchaseCost,
        long hireCost,
        out long estimatedAfterPurchase,
        out long estimatedAfterHire,
        out bool canPurchase,
        out bool canHire)
    {
        currentCurrency = Math.Max(0L, currentCurrency);
        purchaseCost = Math.Max(0L, purchaseCost);
        hireCost = Math.Max(0L, hireCost);
        long totalCost = AddClamped(purchaseCost, hireCost);

        canPurchase = purchaseCost <= currentCurrency;
        estimatedAfterPurchase = SubtractFloorZero(currentCurrency, purchaseCost);
        canHire = totalCost <= currentCurrency;
        estimatedAfterHire = SubtractFloorZero(currentCurrency, totalCost);
    }

    private static int AddClampedInt(int left, int right)
    {
        left = Mathf.Max(0, left);
        right = Mathf.Max(0, right);
        return left > int.MaxValue - right ? int.MaxValue : left + right;
    }

    private static long AddClamped(long left, long right)
    {
        if (right > 0L && left > long.MaxValue - right) return long.MaxValue;
        if (right < 0L && left < long.MinValue - right) return long.MinValue;
        return left + right;
    }
}
