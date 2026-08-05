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
    private ND.Framework.ISaveService saveServiceOverrideForTests;
    private CaravanTransportCatalogProvider transportCatalog;
    public CaravanSettingViewData GetSetting(string caravanId)
    {
        string normalizedCaravanId = NormalizeId(caravanId);
        if (!TryResolveCaravan(normalizedCaravanId, out JourneyState state, out string displayName))
            return null;

        bool canEdit = state == JourneyState.Prepare;
        string blockedReason = canEdit
            ? string.Empty
            : "Caravan settings cannot be changed while the Caravan is traveling.";
        CaravanCompositionSnapshot composition = GetComposition(normalizedCaravanId);
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
            return CaravanSettingCommandResult.Failure(CaravanSettingFailureCodes.InvalidDraft, "The Caravan setting request is invalid.");

        string caravanId = NormalizeId(draft.caravanId);
        if (!TryGetFrameworkCaravan(caravanId, out FrameworkCaravanSaveData caravan))
            return CaravanSettingCommandResult.Failure(CaravanSettingFailureCodes.CaravanNotFound, "선택한 카라반을 찾을 수 없습니다.");
        if (caravan.state != JourneyState.Prepare)
            return CaravanSettingCommandResult.Failure(CaravanSettingFailureCodes.CaravanNotEditable, "Caravan settings can only be changed during Preparation.");

        string wagonId = NormalizeId(draft.selectedWagonInstanceId);
        var animalIds = new List<string>();
        var uniqueAnimalIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < draft.SelectedAnimalInstanceIds.Count; index++)
        {
            string animalId = NormalizeId(draft.SelectedAnimalInstanceIds[index]);
            if (!uniqueAnimalIds.Add(animalId))
                return CaravanSettingCommandResult.Failure(CaravanSettingFailureCodes.InvalidComposition, "The same animal cannot be selected more than once.");
            animalIds.Add(animalId);
        }

        EnsureCompositionServices();
        if (!transportInventory.TryGetWagon(wagonId, out OwnedWagonInstance ownedWagon)
            || !transportCatalog.TryGetWagon(ownedWagon.ContentId, out WagonData wagon))
            return CaravanSettingCommandResult.Failure(CaravanSettingFailureCodes.AssetNotOwned, "The selected wagon is not available.");
        var animalAssets = new List<DraftAnimalData>(animalIds.Count);
        for (int index = 0; index < animalIds.Count; index++)
        {
            if (!transportInventory.TryGetAnimal(animalIds[index], out OwnedDraftAnimalInstance ownedAnimal)
                || !transportCatalog.TryGetDraftAnimal(ownedAnimal.ContentId, out DraftAnimalData animal))
                return CaravanSettingCommandResult.Failure(CaravanSettingFailureCodes.AssetNotOwned, "A selected draft animal is not available.");
            animalAssets.Add(animal);
        }

        CaravanCompositionDraftFailure failure = compositionDrafts.Validate(caravanId, wagonId, animalIds);
        if (failure != CaravanCompositionDraftFailure.None) return MapCompositionFailure(failure);
        if (!AreAnimalsEligible(wagon, animalAssets))
            return CaravanSettingCommandResult.Failure(CaravanSettingFailureCodes.InvalidComposition, "A selected animal is not eligible for this wagon.");

        if (GetSavedCargoLoad(caravan) > wagon.MaxLoad
            || savedCargoService.CreateSnapshot(caravan).Items.Count > wagon.InventorySlotCount)
            return CaravanSettingCommandResult.Failure(CaravanSettingFailureCodes.CargoCapacityExceeded, "Unload cargo before changing to a Caravan setting with lower capacity.");

        FrameworkSaveData saveData = ResolveSaveData();
        ND.Framework.ISaveService saveService = saveServiceOverrideForTests ?? FrameworkRoot.Instance?.SaveService;
        if (saveData == null || saveService == null)
            return CaravanSettingCommandResult.Failure(CaravanSettingFailureCodes.SaveFailed, "Caravan setting changes could not be saved.");

        ND.Framework.WagonSaveData previousWagon = caravan.wagon;
        List<ND.Framework.AnimalSaveData> previousAnimals = caravan.animals;
        caravan.wagon = CreateWagonSaveData(wagon, wagonId);
        caravan.animals = CreateAnimalSaveData(animalAssets, animalIds);
        ND.Framework.SaveResult saveResult = null;
        try { saveResult = saveService.Save(saveData); }
        catch (Exception exception) { Debug.LogError($"Caravan composition save failed: {exception}", this); }

        if (saveResult == null || !saveResult.Succeeded)
        {
            caravan.wagon = previousWagon;
            caravan.animals = previousAnimals;
            return CaravanSettingCommandResult.Failure(CaravanSettingFailureCodes.SaveFailed, "Caravan setting changes could not be saved.");
        }

        compositionDrafts.Clear(caravanId);
        return CaravanSettingCommandResult.Success();
    }

    public CaravanLoadSettingViewData GetLoadSetting(string caravanId)
    {
        string normalizedCaravanId = NormalizeId(caravanId);
        if (!TryResolveCaravan(normalizedCaravanId, out JourneyState state, out string displayName))
            return null;

        bool canEdit = state == JourneyState.Prepare;
        string capacityWagonInstanceId = GetComposition(normalizedCaravanId).WagonInstanceId;
        GetCapacity(capacityWagonInstanceId, out float maxLoad, out int maxSlots);

        CargoItemViewData[] plannedItems;
        if (TryGetFrameworkCaravan(normalizedCaravanId, out FrameworkCaravanSaveData savedCaravan))
        {
            // Framework-backed Cargo is authoritative after an immediate Market purchase.
            // UI working state must never be restored through the provider or mixed across Caravans.
            plannedItems = CreateSavedCargoSnapshot(savedCaravan);
        }
        else if (canEdit && cargoDrafts.TryGet(
                     normalizedCaravanId,
                     out CaravanCargoDraftSnapshot cargoDraft))
        {
            // Standalone smoke fixtures have no Framework save and intentionally remain draft-only.
            plannedItems = CreatePlannedCargoSnapshot(normalizedCaravanId, cargoDraft.Items);
        }
        else
        {
            plannedItems = Array.Empty<CargoItemViewData>();
        }

        int usedSlots = plannedItems.Length;
        float currentLoad = 0f;
        for (int index = 0; index < plannedItems.Length; index++)
            currentLoad += plannedItems[index].totalWeight;

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
                "화물 적재 요청이 올바르지 않습니다.");
        }

        string caravanId = NormalizeId(draft.caravanId);
        if (!TryResolveCaravan(caravanId, out JourneyState state, out _))
        {
            return CaravanLoadSettingCommandResult.Failure(
                CaravanLoadSettingFailureCodes.CaravanNotFound,
                "선택한 카라반을 찾을 수 없습니다.");
        }

        if (state != JourneyState.Prepare)
        {
            return CaravanLoadSettingCommandResult.Failure(
                CaravanLoadSettingFailureCodes.CaravanNotEditable,
                "화물은 카라반이 준비 중일 때만 변경할 수 있습니다.");
        }

        // The S4 draft is the final Cargo plan, so it contains both the committed SaveData
        // baseline and new Market reservations. Only the quantity added above that baseline is
        // a Market selection and may therefore be rejected by the current-town catalog.
        var savedQuantityByItemId = new Dictionary<string, int>(StringComparer.Ordinal);
        if (TryGetFrameworkCaravan(caravanId, out FrameworkCaravanSaveData baselineCaravan))
        {
            CaravanSavedCargoSnapshot baseline = savedCargoService.CreateSnapshot(baselineCaravan);
            for (int index = 0; index < baseline.Items.Count; index++)
            {
                CaravanSavedCargoItem item = baseline.Items[index];
                savedQuantityByItemId[item.ItemId] = item.Quantity;
            }
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
                        "화물 적재 목록에 잘못되었거나 중복된 항목이 있습니다.");
                }

                savedQuantityByItemId.TryGetValue(itemId, out int savedQuantity);
                if (item.quantity > savedQuantity && !IsAvailableCargoItem(caravanId, itemId))
                {
                    return CaravanLoadSettingCommandResult.Failure(
                        CaravanLoadSettingFailureCodes.ItemUnavailable,
                        "추가하려는 화물을 현재 마을의 상점에서 구매할 수 없습니다.");
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
                "화물의 무게 또는 슬롯 수가 카라반의 적재 한도를 초과했습니다.");
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
                "화물 적재 초안을 저장하지 못했습니다.");
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
        transportCatalog = null;
    }

    internal void SetSaveServiceForTests(ND.Framework.ISaveService saveService)
    {
        saveServiceOverrideForTests = saveService;
    }

    private CaravanCompositionSnapshot GetComposition(string caravanId)
    {
        EnsureCompositionServices();
        string wagonId = string.Empty;
        var animalIds = new List<string>();
        bool hasSavedCaravan = TryGetFrameworkCaravan(caravanId, out FrameworkCaravanSaveData caravan);
        if (hasSavedCaravan)
        {
            wagonId = NormalizeId(caravan.wagon?.instanceId);
            if (string.IsNullOrEmpty(wagonId)) wagonId = NormalizeId(caravan.wagon?.wagonName);
            if (caravan.animals != null)
            {
                for (int index = 0; index < caravan.animals.Count; index++)
                {
                    ND.Framework.AnimalSaveData animal = caravan.animals[index];
                    string id = NormalizeId(animal?.instanceId);
                    if (string.IsNullOrEmpty(id)) id = NormalizeId(animal?.animalName);
                    if (!string.IsNullOrEmpty(id)) animalIds.Add(id);
                }
            }
        }
        return compositionDrafts.GetOrCreate(caravanId, wagonId, animalIds);
    }

    private void EnsureCompositionServices()
    {
        if (compositionDrafts != null) return;
        transportCatalog = new CaravanTransportCatalogProvider();
        // TODO(PRODUCTION-OWNERSHIP): The SO catalog is temporarily exposed as owned instances so
        // the sandbox UI remains usable. Delete this catalog-to-ownership registration when the
        // player-owned wagon/animal inventory provider supplies stable instance IDs.
        transportInventory = new OwnedTransportInventoryService();
        foreach (WagonData wagon in transportCatalog.Wagons)
            transportInventory.RegisterWagon(new OwnedWagonInstance(wagon.WagonId, wagon.WagonId, wagon.MaxLoad, wagon.InventorySlotCount, wagon.MinRequireAnimals, wagon.MaxPullAnimals));
        foreach (DraftAnimalData animal in transportCatalog.DraftAnimals)
            transportInventory.RegisterAnimal(new OwnedDraftAnimalInstance(animal.DraftAnimalId, animal.DraftAnimalId));
        // Compatibility bridge for saves created before the ownership provider exists. Keep the
        // content-ID fallback while legacy saves are supported; remove it after save migration.
        FrameworkSaveData saveData = ResolveSaveData();
        if (saveData?.caravans != null)
        {
            for (int caravanIndex = 0; caravanIndex < saveData.caravans.Count; caravanIndex++)
            {
                FrameworkCaravanSaveData saved = saveData.caravans[caravanIndex];
                if (saved == null) continue;

                string wagonInstanceId = NormalizeId(saved.wagon?.instanceId);
                string wagonContentId = NormalizeId(saved.wagon?.wagonName);
                if (string.IsNullOrEmpty(wagonContentId)) wagonContentId = wagonInstanceId;
                if (!string.IsNullOrEmpty(wagonInstanceId)
                    && transportCatalog.TryGetWagon(wagonContentId, out WagonData wagon))
                {
                    transportInventory.RegisterWagon(new OwnedWagonInstance(
                        wagonInstanceId, wagonContentId, wagon.MaxLoad, wagon.InventorySlotCount,
                        wagon.MinRequireAnimals, wagon.MaxPullAnimals));
                }

                if (saved.animals == null) continue;
                for (int animalIndex = 0; animalIndex < saved.animals.Count; animalIndex++)
                {
                    ND.Framework.AnimalSaveData animalSave = saved.animals[animalIndex];
                    string animalInstanceId = NormalizeId(animalSave?.instanceId);
                    string animalContentId = NormalizeId(animalSave?.animalName);
                    if (string.IsNullOrEmpty(animalContentId)) animalContentId = animalInstanceId;
                    if (!string.IsNullOrEmpty(animalInstanceId)
                        && transportCatalog.TryGetDraftAnimal(animalContentId, out _))
                    {
                        transportInventory.RegisterAnimal(
                            new OwnedDraftAnimalInstance(animalInstanceId, animalContentId));
                    }
                }
            }
        }
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
            case CaravanCompositionDraftFailure.MixedAnimalType:
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
        IReadOnlyList<CaravanSavedCargoPresentationItem> snapshot =
            savedCargoService.CreatePresentation(
                caravan,
                FrameworkRoot.Instance?.SharedGameData);
        if (snapshot.Count == 0)
            return Array.Empty<CargoItemViewData>();

        var result = new CargoItemViewData[snapshot.Count];
        for (int index = 0; index < snapshot.Count; index++)
        {
            CaravanSavedCargoPresentationItem item = snapshot[index];
            SharedTradeItemDefinition definition = item.Definition;
            TradeItemSaveData savedItem = item.SavedItem;
            Enum.TryParse(definition?.Category, true, out TradeItemCategory category);
            float unitWeight = definition != null
                ? Mathf.Max(0f, definition.Weight)
                : Mathf.Max(0f, savedItem.weight);
            result[index] = new CargoItemViewData
            {
                itemId = item.ItemId,
                displayName = definition?.DisplayName ?? savedItem.itemName,
                icon = definition?.Icon,
                category = category,
                quantity = item.Quantity,
                unitWeight = unitWeight,
                totalWeight = unitWeight * item.Quantity,
                purchaseUnitPrice = Math.Max(0L, savedItem.basePrice),
                estimatedSellUnitPrice = definition != null
                    ? Math.Max(0L, definition.BaseSellPrice)
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
            CaravanSavedCargoPresentationItem savedItem =
                FindSavedCargoPresentation(caravanId, item.ItemId);
            SharedTradeItemDefinition savedDefinition = savedItem?.Definition;
            TradeItemSaveData savedData = savedItem?.SavedItem;
            Enum.TryParse(savedDefinition?.Category, true, out TradeItemCategory savedCategory);
            float unitWeight = officialItem?.Definition != null
                ? Mathf.Max(0f, officialItem.Definition.Weight)
                : catalogItem != null
                    ? Mathf.Max(0f, catalogItem.Weight)
                    : savedDefinition != null
                        ? Mathf.Max(0f, savedDefinition.Weight)
                        : Mathf.Max(0f, savedData?.weight ?? 0f);
            long buyPrice = officialItem != null
                ? officialItem.BuyUnitPrice
                : catalogItem != null
                    ? Math.Max(0L, catalogItem.BaseBuyPrice)
                    : Math.Max(0L, savedData?.basePrice ?? 0L);
            long sellPrice = catalogItem != null
                ? Math.Max(0L, catalogItem.BaseSellPrice)
                : savedDefinition != null
                    ? Math.Max(0L, savedDefinition.BaseSellPrice)
                    : Math.Max(0L, savedData?.basePrice ?? 0L);
            result[index] = new CargoItemViewData
            {
                itemId = item.ItemId,
                displayName = catalogItem != null
                    ? catalogItem.DisplayName
                    : officialItem?.Definition?.DisplayName
                        ?? savedDefinition?.DisplayName
                        ?? savedData?.itemName
                        ?? item.ItemId,
                icon = catalogItem != null
                    ? catalogItem.Icon
                    : officialItem?.Definition?.Icon ?? savedDefinition?.Icon,
                category = catalogItem != null ? catalogItem.Category : savedCategory,
                quantity = item.Quantity,
                unitWeight = unitWeight,
                totalWeight = unitWeight * item.Quantity,
                purchaseUnitPrice = buyPrice,
                estimatedSellUnitPrice = sellPrice,
                totalPurchasePrice = MultiplyClamped(buyPrice, item.Quantity)
            };
        }

        return result;
    }

    private CaravanSavedCargoPresentationItem FindSavedCargoPresentation(
        string caravanId,
        string itemId)
    {
        if (!TryGetFrameworkCaravan(caravanId, out FrameworkCaravanSaveData caravan))
            return null;

        IReadOnlyList<CaravanSavedCargoPresentationItem> items =
            savedCargoService.CreatePresentation(
                caravan,
                FrameworkRoot.Instance?.SharedGameData);
        for (int index = 0; index < items.Count; index++)
        {
            if (string.Equals(items[index].ItemId, itemId, StringComparison.Ordinal))
                return items[index];
        }

        return null;
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
        string caravanId, string displayName, JourneyState state, bool canEdit,
        string blockedReason, string snapshotWagonInstanceId,
        IReadOnlyList<string> snapshotAnimalInstanceIds)
    {
        EnsureCompositionServices();
        string[] selectedAnimalIds = CopyIds(snapshotAnimalInstanceIds);
        var wagonViews = new List<WagonViewData>();
        foreach (WagonData wagon in transportCatalog.Wagons)
        {
            // SaveData stores an owned instance ID while the SO catalog stores shared content IDs.
            // Preserve the selected physical wagon identity so S3 can restore and commit it safely.
            string wagonInstanceId = wagon.WagonId;
            if (!string.IsNullOrWhiteSpace(snapshotWagonInstanceId)
                && transportInventory.TryGetWagon(snapshotWagonInstanceId, out OwnedWagonInstance selectedWagon)
                && string.Equals(selectedWagon.ContentId, wagon.WagonId, StringComparison.Ordinal))
                wagonInstanceId = selectedWagon.InstanceId;

            wagonViews.Add(new WagonViewData
            {
                wagonId = wagon.WagonId, wagonInstanceId = wagonInstanceId,
                displayName = wagon.DisplayName, icon = wagon.Icon, description = wagon.Description,
                wagonType = wagon.WagonType, baseMoveSpeed = wagon.BaseMoveSpeed,
                currentDurability = wagon.MaxDurability, maxDurability = wagon.MaxDurability,
                overLoad = wagon.Overload, maxLoad = wagon.MaxLoad,
                inventorySlotCount = wagon.InventorySlotCount,
                eligibleAnimalTypes = wagon.EligibleAnimalTypes,
                minRequireAnimals = wagon.MinRequireAnimals, maxPullAnimals = wagon.MaxPullAnimals,
                ownedAmount = 1, isOwned = true, canSelect = canEdit,
                disabledReason = canEdit ? string.Empty : blockedReason
            });
        }
        var animalViews = new List<DraftAnimalViewData>();
        var selectedAnimalContentIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < selectedAnimalIds.Length; index++)
        {
            string selectedInstanceId = selectedAnimalIds[index];
            if (!transportInventory.TryGetAnimal(selectedInstanceId, out OwnedDraftAnimalInstance ownedAnimal)
                || !transportCatalog.TryGetDraftAnimal(ownedAnimal.ContentId, out DraftAnimalData selectedAnimal))
                continue;

            selectedAnimalContentIds.Add(ownedAnimal.ContentId);
            animalViews.Add(CreateAnimalSettingView(selectedAnimal, selectedInstanceId, true, canEdit, blockedReason));
        }
        foreach (DraftAnimalData animal in transportCatalog.DraftAnimals)
        {
            // Keep a temporary catalog fallback for unselected types until the player-owned
            // transport inventory supplies every instance, without replacing saved instance IDs.
            if (!selectedAnimalContentIds.Contains(animal.DraftAnimalId))
                animalViews.Add(CreateAnimalSettingView(animal, animal.DraftAnimalId, false, canEdit, blockedReason));
        }
        return new CaravanSettingViewData
        {
            caravanId = caravanId, caravanDisplayName = displayName, state = state,
            canEdit = canEdit, editBlockedReason = canEdit ? string.Empty : blockedReason,
            selectedWagonInstanceId = snapshotWagonInstanceId,
            selectedAnimalInstanceIds = selectedAnimalIds,
            wagons = wagonViews.ToArray(), draftAnimals = animalViews.ToArray()
        };
    }



    private static DraftAnimalViewData CreateAnimalSettingView(
        DraftAnimalData animal, string instanceId, bool selected, bool canEdit, string blockedReason)
    {
        return new DraftAnimalViewData
        {
            draftAnimalId = animal.DraftAnimalId, draftAnimalInstanceId = instanceId,
            displayName = animal.DisplayName, icon = animal.Icon, description = animal.Description,
            animalType = animal.AnimalType, feedConsumption = animal.FeedConsumption,
            baseMoveSpeed = animal.BaseMoveSpeed, increaseOverLoad = animal.IncreaseOverLoad,
            increaseMaxLoad = animal.IncreaseMaxLoad, ownedAmount = 1,
            selectedAmount = selected ? 1 : 0, maxSelectableAmount = 1,
            isEligibleForSelectedWagon = true, canSelect = canEdit,
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

        // A committed Cargo item may legitimately be absent from the current-town Market.
        // Capacity validation must still use its persisted unit weight instead of treating it as 0.
        CaravanSavedCargoPresentationItem savedItem =
            FindSavedCargoPresentation(caravanId, itemId);
        if (savedItem?.Definition != null)
            return Mathf.Max(0f, savedItem.Definition.Weight);
        return Mathf.Max(0f, savedItem?.SavedItem?.weight ?? 0f);
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

    private void GetCapacity(string wagonInstanceId, out float maxLoad, out int maxSlots)
    {
        EnsureCompositionServices();
        if (transportInventory.TryGetWagon(wagonInstanceId, out OwnedWagonInstance ownedWagon)
            && transportCatalog.TryGetWagon(ownedWagon.ContentId, out WagonData wagon))
        {
            maxLoad = wagon.MaxLoad;
            maxSlots = wagon.InventorySlotCount;
            return;
        }
        maxLoad = 0f;
        maxSlots = 0;
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

    private static bool AreAnimalsEligible(WagonData wagon, IReadOnlyList<DraftAnimalData> animals)
    {
        DraftAnimalType[] eligibleTypes = wagon.EligibleAnimalTypes;
        for (int index = 0; index < animals.Count; index++)
        {
            bool eligible = eligibleTypes.Length == 0;
            for (int typeIndex = 0; typeIndex < eligibleTypes.Length; typeIndex++)
                eligible |= eligibleTypes[typeIndex] == animals[index].AnimalType;
            if (!eligible) return false;
        }
        return true;
    }

    private static float GetSavedCargoLoad(FrameworkCaravanSaveData caravan)
    {
        float total = 0f;
        if (caravan?.cargo == null) return total;
        for (int index = 0; index < caravan.cargo.Count; index++)
        {
            ND.Framework.CargoEntrySaveData entry = caravan.cargo[index];
            if (entry?.item != null && entry.quantity > 0)
                total += Mathf.Max(0f, entry.item.weight) * entry.quantity;
        }
        return total;
    }

    private static ND.Framework.WagonSaveData CreateWagonSaveData(
        WagonData wagon,
        string instanceId)
    {
        return new ND.Framework.WagonSaveData
        {
            instanceId = NormalizeId(instanceId),
            wagonName = wagon.WagonId,
            overLoad = wagon.Overload,
            maxLoad = wagon.MaxLoad,
            minAnimals = wagon.MinRequireAnimals,
            maxAnimals = wagon.MaxPullAnimals,
            speedModifier = wagon.BaseMoveSpeed,
            maxDurability = wagon.MaxDurability,
            inventorySlotCount = wagon.InventorySlotCount
        };
    }

    private static List<ND.Framework.AnimalSaveData> CreateAnimalSaveData(
        IReadOnlyList<DraftAnimalData> animals,
        IReadOnlyList<string> instanceIds)
    {
        var result = new List<ND.Framework.AnimalSaveData>(animals.Count);
        for (int index = 0; index < animals.Count; index++)
        {
            DraftAnimalData animal = animals[index];
            result.Add(new ND.Framework.AnimalSaveData
            {
                instanceId = NormalizeId(instanceIds[index]),
                animalName = animal.DraftAnimalId,
                speed = animal.BaseMoveSpeed,
                foodPerKm = animal.FeedConsumption,
                increaseOverLoad = animal.IncreaseOverLoad,
                increaseMaxLoad = animal.IncreaseMaxLoad,
                animalType = animal.AnimalType
            });
        }

        return result;
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
