using System;
using System.Collections.Generic;
using ND.Framework;
using UnityEngine;
using FrameworkSaveData = ND.Framework.SaveData;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;

/// <summary>
/// Provides temporary S3/S4 fixture content while using Framework-owned Caravan identities.
/// </summary>
/// <remarks>
/// This component reads SaveData.caravans for identity, journey state, and committed cargo, while
/// its wagon, animal, cargo catalog, and uncommitted edit results remain in memory. Replace it with Framework implementations of
/// ICaravanSettingViewDataProvider, ICaravanSettingCommand, ICaravanLoadSettingViewDataProvider,
/// ICaravanLoadSettingCommand, ICaravanCargoCatalogProvider, and ITradePrepareCaravanOptionProvider
/// before production persistence tests. The production implementations must read owned wagon and
/// draft-animal instances from player inventory, read Caravan state/cargo from SaveData, resolve the
/// current-town market catalog, and persist changes through Framework commands.
/// </remarks>
[DisallowMultipleComponent]
public sealed class TestCaravanSettingService : MonoBehaviour,
    ICaravanSettingViewDataProvider,
    ICaravanSettingCommand,
    ICaravanLoadSettingViewDataProvider,
    ICaravanLoadSettingCommand,
    ICaravanCargoCatalogProvider,
    ITradePrepareCaravanOptionProvider
{
    public const string PrepareCaravanId = "test-caravan-prepare";
    public const string TravelingCaravanId = "test-caravan-traveling";
    // Temporary instance ownership is still local, but content IDs must resolve against the
    // official SO catalog consumed by TradePrepareViewDataBuilder.
    public const string WagonContentId = "Wagon_M";
    public const string WagonInstanceId = "test-wagon-instance-01";
    public const string AnimalContentId = "Horse";
    public const string FirstAnimalInstanceId = "test-horse-instance-01";
    public const string SecondAnimalInstanceId = "test-horse-instance-02";
    public const float WagonMaxLoad = 30f;
    public const int WagonInventorySlotCount = 5;
    public const int WagonDurability = 100;

    // TODO(PRODUCTION): Remove these Inspector fixtures with this service. The replacement catalog
    // must come from the selected Caravan's current-town MarketData/SharedGameData provider.
    [Header("S4 TradeItemData catalog")]
    [SerializeField] private TradeItemData[] cargoCatalog = Array.Empty<TradeItemData>();
    [SerializeField] private int defaultCatalogStock = 20;

    private OwnedTransportInventoryService transportInventory;
    private CaravanCompositionDraftService compositionDrafts;
    private readonly CaravanCargoDraftService cargoDrafts = new CaravanCargoDraftService();
    private readonly CaravanMarketCatalogService marketCatalogService =
        new CaravanMarketCatalogService();
    private readonly CaravanSelectionOptionService selectionOptionService =
        new CaravanSelectionOptionService();
    private readonly CaravanSaveQueryService saveQueryService =
        new CaravanSaveQueryService();
    private readonly CaravanSavedCargoService savedCargoService =
        new CaravanSavedCargoService();
    private FrameworkSaveData saveDataOverrideForTests;

    public CaravanSettingViewData GetSetting(string caravanId)
    {
        string normalizedCaravanId = NormalizeId(caravanId);
        if (!TryResolveCaravan(normalizedCaravanId, out JourneyState state, out string displayName))
            return null;

        bool canEdit = state == JourneyState.Prepare;
        string blockedReason = canEdit
            ? string.Empty
            : "Caravan settings cannot be changed while the Caravan is traveling.";
        CaravanCompositionSnapshot composition = canEdit
            ? GetComposition(normalizedCaravanId)
            : CreateLegacyTravelingComposition(normalizedCaravanId);
        return CreateSettingSnapshot(
            normalizedCaravanId,
            displayName,
            state,
            canEdit,
            blockedReason,
            composition.WagonInstanceId,
            composition.AnimalInstanceIds);
    }

    public CaravanSettingCommandResult Execute(CaravanSettingDraft draft)
    {
        if (draft == null || string.IsNullOrEmpty(NormalizeId(draft.caravanId)))
        {
            return CaravanSettingCommandResult.Failure(
                CaravanSettingFailureCodes.InvalidDraft,
                "The Caravan setting request is invalid.");
        }

        string caravanId = NormalizeId(draft.caravanId);
        if (!TryResolveCaravan(caravanId, out JourneyState state, out _))
        {
            return CaravanSettingCommandResult.Failure(
                CaravanSettingFailureCodes.CaravanNotFound,
                "The selected Caravan could not be found.");
        }

        if (state != JourneyState.Prepare)
        {
            return CaravanSettingCommandResult.Failure(
                CaravanSettingFailureCodes.CaravanNotEditable,
                "Caravan settings can only be changed during Preparation.");
        }

        string wagonInstanceId = NormalizeId(draft.selectedWagonInstanceId);
        var validatedAnimalIds = new List<string>();
        IReadOnlyList<string> draftAnimalIds = draft.SelectedAnimalInstanceIds;
        for (int index = 0; index < draftAnimalIds.Count; index++)
        {
            string animalInstanceId = NormalizeId(draftAnimalIds[index]);
            if (validatedAnimalIds.Contains(animalInstanceId))
            {
                return CaravanSettingCommandResult.Failure(
                    CaravanSettingFailureCodes.InvalidComposition,
                    "The same animal instance cannot be selected more than once.");
            }

            validatedAnimalIds.Add(animalInstanceId);
        }

        EnsureCompositionServices();
        CaravanCompositionDraftFailure compositionFailure = compositionDrafts.Validate(
            caravanId,
            wagonInstanceId,
            validatedAnimalIds);
        if (compositionFailure != CaravanCompositionDraftFailure.None)
            return MapCompositionFailure(compositionFailure);

        GetCapacity(wagonInstanceId, out float nextMaxLoad, out int nextMaxSlots);
        cargoDrafts.TryGet(caravanId, out CaravanCargoDraftSnapshot cargoDraft);
        if (GetPlannedCargoLoad(caravanId, cargoDraft?.Items) > nextMaxLoad
            || (cargoDraft?.Items.Count ?? 0) > nextMaxSlots)
        {
            return CaravanSettingCommandResult.Failure(
                CaravanSettingFailureCodes.CargoCapacityExceeded,
                "Unload cargo before changing to a Caravan setting with lower capacity.");
        }

        // Apply only after every cargo validation passes so a failed command cannot leave partial state.
        compositionFailure = compositionDrafts.TrySet(
            caravanId,
            wagonInstanceId,
            validatedAnimalIds);
        if (compositionFailure != CaravanCompositionDraftFailure.None)
            return MapCompositionFailure(compositionFailure);

        return CaravanSettingCommandResult.Success();
    }

    public CaravanLoadSettingViewData GetLoadSetting(string caravanId)
    {
        string normalizedCaravanId = NormalizeId(caravanId);
        if (!TryResolveCaravan(normalizedCaravanId, out JourneyState state, out string displayName))
            return null;

        bool canEdit = state == JourneyState.Prepare;
        string capacityWagonInstanceId = canEdit
            ? GetComposition(normalizedCaravanId).WagonInstanceId
            : WagonInstanceId;
        GetCapacity(capacityWagonInstanceId, out float maxLoad, out int maxSlots);
        CargoItemViewData[] plannedItems = Array.Empty<CargoItemViewData>();
        if (canEdit)
        {
            if (TryGetFrameworkCaravan(normalizedCaravanId, out FrameworkCaravanSaveData savedCaravan))
            {
                string savedCargoSignature = savedCargoService
                    .CreateSnapshot(savedCaravan)
                    .BaselineSignature;
                bool useDraft = cargoDrafts.TryGetCompatible(
                    normalizedCaravanId,
                    savedCargoSignature,
                    out CaravanCargoDraftSnapshot cargoDraft);
                if (useDraft)
                {
                    plannedItems = CreatePlannedCargoSnapshot(normalizedCaravanId, cargoDraft.Items);
                }
                else
                {
                    plannedItems = CreateSavedCargoSnapshot(savedCaravan);
                }
            }
            else
            {
                // Standalone smoke fixtures have no FrameworkRoot and intentionally keep their
                // temporary in-memory plan.
                plannedItems = cargoDrafts.TryGet(
                        normalizedCaravanId,
                        out CaravanCargoDraftSnapshot cargoDraft)
                    ? CreatePlannedCargoSnapshot(normalizedCaravanId, cargoDraft.Items)
                    : Array.Empty<CargoItemViewData>();
            }
        }
        int usedSlots = plannedItems.Length;
        float currentLoad = 0f;
        for (int index = 0; index < plannedItems.Length; index++)
        {
            currentLoad += plannedItems[index].totalWeight;
        }

        return new CaravanLoadSettingViewData
        {
            caravanId = normalizedCaravanId,
            caravanDisplayName = displayName,
            currentTownId = ResolveCurrentTownId(normalizedCaravanId),
            state = state,
            canEdit = canEdit,
            editBlockedReason = canEdit
                ? string.Empty
                : "Caravan cargo cannot be changed while the Caravan is traveling.",
            availableItems = CreateAvailableItemSnapshot(normalizedCaravanId),
            plannedItems = plannedItems,
            currentLoad = currentLoad,
            overloadLimit = maxLoad * 0.8f,
            maxLoad = maxLoad,
            usedInventorySlotCount = usedSlots,
            maxInventorySlotCount = maxSlots,
            totalPlannedPurchaseCost = 0L,
            estimatedCurrencyAfterPurchase = 0L
        };
    }

    CaravanLoadSettingCommandResult ICaravanLoadSettingCommand.Execute(CaravanLoadSettingDraft draft)
    {
        if (draft == null || string.IsNullOrEmpty(NormalizeId(draft.caravanId)))
        {
            return CaravanLoadSettingCommandResult.Failure(
                CaravanLoadSettingFailureCodes.InvalidDraft,
                "The Caravan cargo request is invalid.");
        }

        string caravanId = NormalizeId(draft.caravanId);
        if (!TryResolveCaravan(caravanId, out JourneyState state, out _))
        {
            return CaravanLoadSettingCommandResult.Failure(
                CaravanLoadSettingFailureCodes.CaravanNotFound,
                "The selected Caravan could not be found.");
        }

        if (state != JourneyState.Prepare)
        {
            return CaravanLoadSettingCommandResult.Failure(
                CaravanLoadSettingFailureCodes.CaravanNotEditable,
                "Caravan cargo can only be changed during Preparation.");
        }

        var validatedItems = new List<CaravanLoadItemDraft>();
        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        int totalQuantity = 0;
        if (draft.items != null)
        {
            for (int index = 0; index < draft.items.Count; index++)
            {
                CaravanLoadItemDraft item = draft.items[index];
                string itemId = item != null ? NormalizeId(item.itemId) : string.Empty;
                if (string.IsNullOrEmpty(itemId) || item.quantity <= 0 || !itemIds.Add(itemId))
                {
                    return CaravanLoadSettingCommandResult.Failure(
                        CaravanLoadSettingFailureCodes.InvalidDraft,
                        "The Caravan cargo plan contains an invalid or duplicate item.");
                }

                if (!IsAvailableCargoItem(caravanId, itemId))
                {
                    return CaravanLoadSettingCommandResult.Failure(
                        CaravanLoadSettingFailureCodes.ItemUnavailable,
                        "The selected cargo item is not available in the Caravan catalog.");
                }

                totalQuantity += item.quantity;
                validatedItems.Add(new CaravanLoadItemDraft
                {
                    itemId = itemId,
                    quantity = item.quantity
                });
            }
        }

        GetCapacity(GetComposition(caravanId).WagonInstanceId, out float maxLoad, out int maxSlots);
        float totalWeight = GetDraftCargoLoad(caravanId, validatedItems);
        if (validatedItems.Count > maxSlots || totalWeight > maxLoad)
        {
            return CaravanLoadSettingCommandResult.Failure(
                CaravanLoadSettingFailureCodes.CargoCapacityExceeded,
                "The Caravan cargo plan exceeds the temporary S4 capacity.");
        }

        var nextDraftItems = new List<CaravanCargoDraftItem>(validatedItems.Count);
        for (int index = 0; index < validatedItems.Count; index++)
        {
            nextDraftItems.Add(new CaravanCargoDraftItem(
                validatedItems[index].itemId,
                validatedItems[index].quantity));
        }
        string saveBaseline = string.Empty;
        if (TryGetFrameworkCaravan(caravanId, out FrameworkCaravanSaveData savedCaravan))
        {
            saveBaseline = savedCargoService
                .CreateSnapshot(savedCaravan)
                .BaselineSignature;
        }
        if (!cargoDrafts.Set(caravanId, saveBaseline, nextDraftItems))
        {
            return CaravanLoadSettingCommandResult.Failure(
                CaravanLoadSettingFailureCodes.InvalidDraft,
                "The Caravan cargo plan could not be stored.");
        }
        return CaravanLoadSettingCommandResult.Success();
    }

    public CaravanCargoCatalogData GetCargoCatalog(string caravanId)
    {
        string normalizedCaravanId = NormalizeId(caravanId);
        if (!TryResolveCaravan(normalizedCaravanId, out _, out _))
        {
            return null;
        }

        TryResolveOfficialCatalog(normalizedCaravanId, out CaravanMarketCatalogSnapshot officialCatalog);
        var items = new List<TradeItemData>();
        var stocks = new List<int>();
        var prices = new List<long>();
        for (int index = 0; index < cargoCatalog.Length; index++)
        {
            TradeItemData item = cargoCatalog[index];
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
                continue;
            if (officialCatalog != null
                && !officialCatalog.TryGetItem(item.ItemId, out _))
                continue;

            items.Add(item);
            if (officialCatalog != null
                && officialCatalog.TryGetItem(item.ItemId, out CaravanMarketCatalogItem officialItem))
            {
                stocks.Add(officialItem.Stock);
                prices.Add(officialItem.BuyUnitPrice);
            }
            else
            {
                stocks.Add(Mathf.Max(0, defaultCatalogStock));
                prices.Add(Math.Max(0L, item.BaseBuyPrice));
            }
        }

        return new CaravanCargoCatalogData
        {
            items = items.ToArray(),
            stocks = stocks.ToArray(),
            buyUnitPrices = prices.ToArray()
        };
    }

    public TradePrepareCaravanOptionViewData[] GetOptions()
    {
        FrameworkSaveData saveData = ResolveSaveData();
        if (saveData?.caravans != null && saveData.caravans.Count > 0)
            return selectionOptionService.CreateOptions(saveData);

        // Smoke tests without FrameworkRoot retain the explicit fixture options.
        return new[]
        {
            new TradePrepareCaravanOptionViewData
            {
                caravanId = PrepareCaravanId,
                displayName = "Preparation Caravan",
                currentTownId = "test-town",
                state = JourneyState.Prepare,
                canSelect = true,
                disabledReason = string.Empty
            },
            new TradePrepareCaravanOptionViewData
            {
                caravanId = TravelingCaravanId,
                displayName = "Traveling Caravan",
                currentTownId = "test-town",
                state = JourneyState.Traveling,
                canSelect = false,
                disabledReason = "A traveling Caravan cannot start another trade."
            }
        };
    }

    internal void SetCargoCatalogForTests(params TradeItemData[] items)
    {
        cargoCatalog = items ?? Array.Empty<TradeItemData>();
    }

    internal void SetSaveDataForTests(FrameworkSaveData saveData)
    {
        saveDataOverrideForTests = saveData;
        cargoDrafts.ClearAll();
        compositionDrafts = null;
        transportInventory = null;
    }

    private CaravanCompositionSnapshot GetComposition(string caravanId)
    {
        EnsureCompositionServices();
        return compositionDrafts.GetOrCreate(
            caravanId,
            WagonInstanceId,
            new[] { FirstAnimalInstanceId, SecondAnimalInstanceId });
    }

    private static CaravanCompositionSnapshot CreateLegacyTravelingComposition(string caravanId)
    {
        return new CaravanCompositionSnapshot(
            NormalizeId(caravanId),
            WagonInstanceId,
            new[] { FirstAnimalInstanceId, SecondAnimalInstanceId });
    }

    private void EnsureCompositionServices()
    {
        if (compositionDrafts != null)
            return;

        transportInventory = new OwnedTransportInventoryService();
        transportInventory.RegisterWagon(new OwnedWagonInstance(
            WagonInstanceId,
            WagonContentId,
            WagonMaxLoad,
            WagonInventorySlotCount,
            1,
            2));
        transportInventory.RegisterAnimal(new OwnedDraftAnimalInstance(
            FirstAnimalInstanceId,
            AnimalContentId));
        transportInventory.RegisterAnimal(new OwnedDraftAnimalInstance(
            SecondAnimalInstanceId,
            AnimalContentId));
        compositionDrafts = new CaravanCompositionDraftService(transportInventory);
    }

    private static CaravanSettingCommandResult MapCompositionFailure(
        CaravanCompositionDraftFailure failure)
    {
        switch (failure)
        {
            case CaravanCompositionDraftFailure.WagonNotOwned:
            case CaravanCompositionDraftFailure.AnimalNotOwned:
            case CaravanCompositionDraftFailure.AssetAlreadyInUse:
                return CaravanSettingCommandResult.Failure(
                    CaravanSettingFailureCodes.AssetNotOwned,
                    "One or more selected transport instances are not available.");
            case CaravanCompositionDraftFailure.DuplicateAnimal:
            case CaravanCompositionDraftFailure.InvalidComposition:
                return CaravanSettingCommandResult.Failure(
                    CaravanSettingFailureCodes.InvalidComposition,
                    "The selected wagon and animal composition is invalid.");
            default:
                return CaravanSettingCommandResult.Failure(
                    CaravanSettingFailureCodes.InvalidDraft,
                    "The Caravan setting request is invalid.");
        }
    }

    private CargoItemViewData[] CreateSavedCargoSnapshot(FrameworkCaravanSaveData caravan)
    {
        CaravanSavedCargoSnapshot snapshot = savedCargoService.CreateSnapshot(caravan);
        if (snapshot.Items.Count == 0)
            return Array.Empty<CargoItemViewData>();

        var result = new CargoItemViewData[snapshot.Items.Count];
        for (int index = 0; index < snapshot.Items.Count; index++)
        {
            CaravanSavedCargoItem item = snapshot.Items[index];
            TradeItemSaveData savedItem = item.SavedItem;
            TradeItemData catalogItem = FindCatalogItem(item.ItemId);
            float unitWeight = catalogItem != null
                ? Mathf.Max(0f, catalogItem.Weight)
                : Mathf.Max(0f, savedItem.weight);
            result[index] = new CargoItemViewData
            {
                itemId = item.ItemId,
                displayName = catalogItem != null ? catalogItem.DisplayName : savedItem.itemName,
                icon = catalogItem != null ? catalogItem.Icon : null,
                category = catalogItem != null ? catalogItem.Category : default,
                quantity = item.Quantity,
                unitWeight = unitWeight,
                totalWeight = unitWeight * item.Quantity,
                purchaseUnitPrice = Math.Max(0L, savedItem.basePrice),
                estimatedSellUnitPrice = catalogItem != null
                    ? Math.Max(0L, catalogItem.BaseSellPrice)
                    : Math.Max(0L, savedItem.basePrice),
                totalPurchasePrice = Math.Max(0L, savedItem.basePrice) * item.Quantity
            };
        }

        return result;
    }

    private CargoItemViewData[] CreatePlannedCargoSnapshot(
        string caravanId,
        IReadOnlyList<CaravanCargoDraftItem> plannedCargo)
    {
        var result = new CargoItemViewData[plannedCargo.Count];
        for (int index = 0; index < plannedCargo.Count; index++)
        {
            CaravanCargoDraftItem item = plannedCargo[index];
            TradeItemData catalogItem = FindCatalogItem(item.ItemId);
            TryGetOfficialCatalogItem(caravanId, item.ItemId, out CaravanMarketCatalogItem officialItem);
            float unitWeight = officialItem?.Definition != null
                ? Mathf.Max(0f, officialItem.Definition.Weight)
                : catalogItem != null ? Mathf.Max(0f, catalogItem.Weight) : 0f;
            long buyPrice = officialItem != null
                ? officialItem.BuyUnitPrice
                : catalogItem != null ? Math.Max(0L, catalogItem.BaseBuyPrice) : 0L;
            result[index] = new CargoItemViewData
            {
                itemId = item.ItemId,
                displayName = catalogItem != null
                    ? catalogItem.DisplayName
                    : officialItem?.Definition?.DisplayName ?? item.ItemId,
                icon = catalogItem != null ? catalogItem.Icon : officialItem?.Definition?.Icon,
                category = catalogItem != null ? catalogItem.Category : default,
                quantity = item.Quantity,
                unitWeight = unitWeight,
                totalWeight = unitWeight * item.Quantity,
                purchaseUnitPrice = buyPrice,
                estimatedSellUnitPrice = catalogItem != null ? Math.Max(0L, catalogItem.BaseSellPrice) : 0L,
                totalPurchasePrice = MultiplyClamped(buyPrice, item.Quantity)
            };
        }

        return result;
    }

    private FrameworkSaveData ResolveSaveData()
    {
        return saveDataOverrideForTests
            ?? (FrameworkRoot.Instance != null ? FrameworkRoot.Instance.CurrentSaveData : null);
    }

    private bool TryGetFrameworkCaravan(
        string caravanId,
        out FrameworkCaravanSaveData caravan)
    {
        caravan = null;
        if (!saveQueryService.TryGet(
            ResolveSaveData(),
            caravanId,
            out CaravanSaveQueryResult result))
        {
            return false;
        }

        caravan = result.Caravan;
        return true;
    }

    private CaravanSettingViewData CreateSettingSnapshot(
        string caravanId,
        string displayName,
        JourneyState state,
        bool canEdit,
        string blockedReason,
        string snapshotWagonInstanceId,
        IReadOnlyList<string> snapshotAnimalInstanceIds)
    {
        string[] animalIds = CopyIds(snapshotAnimalInstanceIds);
        return new CaravanSettingViewData
        {
            caravanId = caravanId,
            caravanDisplayName = displayName,
            state = state,
            canEdit = canEdit,
            editBlockedReason = canEdit ? string.Empty : blockedReason,
            selectedWagonInstanceId = snapshotWagonInstanceId,
            selectedAnimalInstanceIds = animalIds,
            wagons = new[]
            {
                new WagonViewData
                {
                    wagonId = WagonContentId,
                    wagonInstanceId = WagonInstanceId,
                    displayName = "Test Medium Wagon",
                    wagonType = WagonType.WagonWithAnimals,
                    maxLoad = WagonMaxLoad,
                    inventorySlotCount = WagonInventorySlotCount,
                    currentDurability = WagonDurability,
                    maxDurability = WagonDurability,
                    minRequireAnimals = 1,
                    maxPullAnimals = 2,
                    eligibleAnimalTypes = new[] { DraftAnimalType.Horse },
                    ownedAmount = 1,
                    isOwned = true,
                    canSelect = canEdit,
                    disabledReason = canEdit ? string.Empty : blockedReason
                }
            },
            draftAnimals = new[]
            {
                CreateAnimalViewData(
                    FirstAnimalInstanceId,
                    ContainsId(animalIds, FirstAnimalInstanceId),
                    canEdit,
                    blockedReason),
                CreateAnimalViewData(
                    SecondAnimalInstanceId,
                    ContainsId(animalIds, SecondAnimalInstanceId),
                    canEdit,
                    blockedReason)
            }
        };
    }

    private static DraftAnimalViewData CreateAnimalViewData(
        string animalInstanceId,
        bool selected,
        bool canEdit,
        string blockedReason)
    {
        return new DraftAnimalViewData
        {
            draftAnimalId = AnimalContentId,
            draftAnimalInstanceId = animalInstanceId,
            displayName = "Test Horse",
            animalType = DraftAnimalType.Horse,
            ownedAmount = 1,
            selectedAmount = selected ? 1 : 0,
            maxSelectableAmount = 1,
            isEligibleForSelectedWagon = true,
            canSelect = canEdit,
            disabledReason = canEdit ? string.Empty : blockedReason
        };
    }

    private TradeItemViewData[] CreateAvailableItemSnapshot(string caravanId)
    {
        CaravanCargoCatalogData catalog = CreateCargoCatalogSnapshot(caravanId);
        var result = new TradeItemViewData[catalog.items.Length];
        for (int index = 0; index < catalog.items.Length; index++)
        {
            TradeItemData item = catalog.items[index];
            result[index] = new TradeItemViewData
            {
                itemId = item.ItemId,
                displayName = item.DisplayName,
                icon = item.Icon,
                description = item.Description,
                rarity = item.Rarity,
                category = item.Category,
                purchasePrice = item.BaseBuyPrice,
                sellPrice = item.BaseSellPrice,
                contentQuantityLimit = item.MaxCount,
                hasAuthoritativeStock = true,
                unitWeight = item.Weight,
                canBuy = true
            };
        }

        return result;
    }

    private CaravanCargoCatalogData CreateCargoCatalogSnapshot(string caravanId)
    {
        TryResolveOfficialCatalog(caravanId, out CaravanMarketCatalogSnapshot officialCatalog);
        var items = new List<TradeItemData>();
        var stocks = new List<int>();
        var prices = new List<long>();
        for (int index = 0; index < cargoCatalog.Length; index++)
        {
            TradeItemData item = cargoCatalog[index];
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
                continue;
            if (officialCatalog != null
                && !officialCatalog.TryGetItem(item.ItemId, out _))
                continue;
            items.Add(item);
            if (officialCatalog != null
                && officialCatalog.TryGetItem(item.ItemId, out CaravanMarketCatalogItem officialItem))
            {
                stocks.Add(officialItem.Stock);
                prices.Add(officialItem.BuyUnitPrice);
            }
            else
            {
                stocks.Add(Mathf.Max(0, defaultCatalogStock));
                prices.Add(Math.Max(0L, item.BaseBuyPrice));
            }
        }
        return new CaravanCargoCatalogData
        {
            items = items.ToArray(),
            stocks = stocks.ToArray(),
            buyUnitPrices = prices.ToArray()
        };
    }

    private bool TryResolveCaravan(
        string caravanId,
        out JourneyState state,
        out string displayName)
    {
        state = JourneyState.Prepare;
        displayName = string.Empty;
        FrameworkSaveData saveData = ResolveSaveData();
        if (saveData?.caravans != null && saveData.caravans.Count > 0)
        {
            if (saveQueryService.TryGet(
                saveData,
                caravanId,
                out CaravanSaveQueryResult result))
            {
                state = result.State;
                displayName = result.DisplayName;
                return true;
            }
            return false;
        }

        if (caravanId == PrepareCaravanId)
        {
            state = JourneyState.Prepare;
            displayName = "Preparation Caravan";
            return true;
        }
        if (caravanId == TravelingCaravanId)
        {
            state = JourneyState.Traveling;
            displayName = "Traveling Caravan";
            return true;
        }
        return false;
    }

    private string ResolveCurrentTownId(string caravanId)
    {
        FrameworkSaveData saveData = ResolveSaveData();
        if (saveQueryService.TryGet(
            saveData,
            caravanId,
            out CaravanSaveQueryResult result))
        {
            return result.CurrentTownId;
        }

        if (saveData?.caravans != null)
        {
            string normalizedCaravanId = NormalizeId(caravanId);
            for (int index = 0; index < saveData.caravans.Count; index++)
            {
                FrameworkCaravanSaveData caravan = saveData.caravans[index];
                if (caravan != null
                    && string.Equals(caravan.caravanId, normalizedCaravanId, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(caravan.currentTownId))
                {
                    // S3/S4의 위치 표시는 플레이어 전역 위치가 아니라 선택 Caravan의 위치를 따른다.
                    return caravan.currentTownId;
                }
            }
        }

        // Framework가 없는 단독 Smoke fixture만 기존 임시 위치 fallback을 사용한다.
        // Framework runtime requires a Caravan-owned location. Only isolated smoke
        // fixtures without Framework SaveData retain a deterministic test location.
        return saveData == null ? "test-town" : string.Empty;
    }

    private TradeItemData FindCatalogItem(string itemId)
    {
        string normalized = NormalizeId(itemId);
        for (int index = 0; index < cargoCatalog.Length; index++)
        {
            TradeItemData item = cargoCatalog[index];
            if (item != null && string.Equals(item.ItemId, normalized, StringComparison.Ordinal))
                return item;
        }

        return null;
    }

    private bool IsAvailableCargoItem(string caravanId, string itemId)
    {
        if (TryResolveOfficialCatalog(caravanId, out CaravanMarketCatalogSnapshot officialCatalog))
            return officialCatalog.TryGetItem(itemId, out CaravanMarketCatalogItem officialItem)
                && officialItem.Stock > 0;

        return FindCatalogItem(itemId) != null;
    }

    private float GetCargoItemWeight(string caravanId, string itemId)
    {
        if (TryGetOfficialCatalogItem(caravanId, itemId, out CaravanMarketCatalogItem officialItem)
            && officialItem.Definition != null)
        {
            return Mathf.Max(0f, officialItem.Definition.Weight);
        }

        TradeItemData catalogItem = FindCatalogItem(itemId);
        if (catalogItem != null)
            return Mathf.Max(0f, catalogItem.Weight);
        return 0f;
    }

    private bool TryResolveOfficialCatalog(
        string caravanId,
        out CaravanMarketCatalogSnapshot catalog)
    {
        catalog = null;
        FrameworkRoot root = FrameworkRoot.Instance;
        FrameworkSaveData saveData = ResolveSaveData();
        return root != null
            && marketCatalogService.TryResolve(
                saveData,
                root.SharedGameData,
                caravanId,
                out catalog);
    }

    private bool TryGetOfficialCatalogItem(
        string caravanId,
        string itemId,
        out CaravanMarketCatalogItem item)
    {
        item = null;
        return TryResolveOfficialCatalog(caravanId, out CaravanMarketCatalogSnapshot catalog)
            && catalog.TryGetItem(itemId, out item);
    }

    private static void GetCapacity(string wagonInstanceId, out float maxLoad, out int maxSlots)
    {
        bool hasWagon = NormalizeId(wagonInstanceId) == WagonInstanceId;
        maxLoad = hasWagon ? WagonMaxLoad : 0f;
        maxSlots = hasWagon ? WagonInventorySlotCount : 0;
    }

    private float GetPlannedCargoLoad(
        string caravanId,
        IReadOnlyList<CaravanCargoDraftItem> plannedCargo)
    {
        float load = 0f;
        if (plannedCargo == null)
            return load;

        for (int index = 0; index < plannedCargo.Count; index++)
        {
            load += GetCargoItemWeight(caravanId, plannedCargo[index].ItemId)
                * Mathf.Max(0, plannedCargo[index].Quantity);
        }

        return load;
    }

    private float GetDraftCargoLoad(
        string caravanId,
        IReadOnlyList<CaravanLoadItemDraft> items)
    {
        float load = 0f;
        for (int index = 0; index < items.Count; index++)
        {
            load += GetCargoItemWeight(caravanId, items[index].itemId)
                * Mathf.Max(0, items[index].quantity);
        }

        return load;
    }

    private static string[] CopyIds(IReadOnlyList<string> source)
    {
        if (source == null || source.Count == 0)
        {
            return Array.Empty<string>();
        }

        var copy = new string[source.Count];
        for (int index = 0; index < source.Count; index++)
        {
            copy[index] = source[index];
        }

        return copy;
    }

    private static bool ContainsId(IReadOnlyList<string> source, string target)
    {
        if (source == null)
        {
            return false;
        }

        for (int index = 0; index < source.Count; index++)
        {
            if (source[index] == target)
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static long MultiplyClamped(long value, int quantity)
    {
        if (value <= 0L || quantity <= 0)
            return 0L;
        return value > long.MaxValue / quantity ? long.MaxValue : value * quantity;
    }
}
