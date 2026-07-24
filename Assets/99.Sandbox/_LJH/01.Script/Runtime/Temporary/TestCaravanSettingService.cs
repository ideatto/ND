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

    private string selectedWagonInstanceId = WagonInstanceId;
    private readonly List<string> selectedAnimalInstanceIds = new List<string>
    {
        FirstAnimalInstanceId,
        SecondAnimalInstanceId
    };
    private sealed class CargoDraftState
    {
        public readonly List<CaravanLoadItemDraft> items = new List<CaravanLoadItemDraft>();
        public string saveBaseline = string.Empty;
    }

    // Detached S4 edits are independent per persistent caravanId. Switching slots must not replace
    // another Caravan's uncommitted load plan.
    private readonly Dictionary<string, CargoDraftState> cargoDraftsByCaravanId =
        new Dictionary<string, CargoDraftState>(StringComparer.Ordinal);
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
        return CreateSettingSnapshot(
            normalizedCaravanId,
            displayName,
            state,
            canEdit,
            blockedReason,
            selectedWagonInstanceId,
            selectedAnimalInstanceIds);
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
        if (!string.IsNullOrEmpty(wagonInstanceId) && wagonInstanceId != WagonInstanceId)
        {
            return CaravanSettingCommandResult.Failure(
                CaravanSettingFailureCodes.AssetNotOwned,
                "The selected wagon is not available.");
        }

        var validatedAnimalIds = new List<string>();
        IReadOnlyList<string> draftAnimalIds = draft.SelectedAnimalInstanceIds;
        for (int index = 0; index < draftAnimalIds.Count; index++)
        {
            string animalInstanceId = NormalizeId(draftAnimalIds[index]);
            if (!IsOwnedAnimal(animalInstanceId))
            {
                return CaravanSettingCommandResult.Failure(
                    CaravanSettingFailureCodes.AssetNotOwned,
                    "One or more selected animals are not available.");
            }

            if (validatedAnimalIds.Contains(animalInstanceId))
            {
                return CaravanSettingCommandResult.Failure(
                    CaravanSettingFailureCodes.InvalidComposition,
                    "The same animal instance cannot be selected more than once.");
            }

            validatedAnimalIds.Add(animalInstanceId);
        }

        bool walking = string.IsNullOrEmpty(wagonInstanceId);
        if ((walking && validatedAnimalIds.Count > 0)
            || (!walking && (validatedAnimalIds.Count < 1 || validatedAnimalIds.Count > 2)))
        {
            return CaravanSettingCommandResult.Failure(
                CaravanSettingFailureCodes.InvalidComposition,
                "The selected wagon and animal composition is invalid.");
        }


        GetCapacity(wagonInstanceId, out float nextMaxLoad, out int nextMaxSlots);
        TryGetCargoDraft(caravanId, out CargoDraftState cargoDraft);
        if (GetPlannedCargoLoad(cargoDraft?.items) > nextMaxLoad
            || (cargoDraft?.items.Count ?? 0) > nextMaxSlots)
        {
            return CaravanSettingCommandResult.Failure(
                CaravanSettingFailureCodes.CargoCapacityExceeded,
                "Unload cargo before changing to a Caravan setting with lower capacity.");
        }

        // Apply only after every validation passes so a failed test command cannot leave partial state.
        selectedWagonInstanceId = wagonInstanceId;
        selectedAnimalInstanceIds.Clear();
        selectedAnimalInstanceIds.AddRange(validatedAnimalIds);
        return CaravanSettingCommandResult.Success();
    }

    public CaravanLoadSettingViewData GetLoadSetting(string caravanId)
    {
        string normalizedCaravanId = NormalizeId(caravanId);
        if (!TryResolveCaravan(normalizedCaravanId, out JourneyState state, out string displayName))
            return null;

        bool canEdit = state == JourneyState.Prepare;
        string capacityWagonInstanceId = canEdit ? selectedWagonInstanceId : WagonInstanceId;
        GetCapacity(capacityWagonInstanceId, out float maxLoad, out int maxSlots);
        CargoItemViewData[] plannedItems = Array.Empty<CargoItemViewData>();
        if (canEdit)
        {
            if (TryGetFrameworkCaravan(normalizedCaravanId, out FrameworkCaravanSaveData savedCaravan))
            {
                string savedCargoSignature = CreateCargoSignature(savedCaravan);
                bool useDraft = TryGetCargoDraft(normalizedCaravanId, out CargoDraftState cargoDraft)
                    && string.Equals(cargoDraft.saveBaseline, savedCargoSignature, StringComparison.Ordinal);
                if (useDraft)
                {
                    plannedItems = CreatePlannedCargoSnapshot(cargoDraft.items);
                }
                else
                {
                    ClearPlannedCargoDraft(normalizedCaravanId);
                    plannedItems = CreateSavedCargoSnapshot(savedCaravan);
                }
            }
            else
            {
                // Standalone smoke fixtures have no FrameworkRoot and intentionally keep their
                // temporary in-memory plan.
                plannedItems = TryGetCargoDraft(normalizedCaravanId, out CargoDraftState cargoDraft)
                    ? CreatePlannedCargoSnapshot(cargoDraft.items)
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
            availableItems = CreateAvailableItemSnapshot(),
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

                TradeItemData catalogItem = FindCatalogItem(itemId);
                if (catalogItem == null)
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

        GetCapacity(selectedWagonInstanceId, out float maxLoad, out int maxSlots);
        float totalWeight = GetDraftCargoLoad(validatedItems);
        if (validatedItems.Count > maxSlots || totalWeight > maxLoad)
        {
            return CaravanLoadSettingCommandResult.Failure(
                CaravanLoadSettingFailureCodes.CargoCapacityExceeded,
                "The Caravan cargo plan exceeds the temporary S4 capacity.");
        }

        var nextDraft = new CargoDraftState();
        nextDraft.items.AddRange(validatedItems);
        if (TryGetFrameworkCaravan(caravanId, out FrameworkCaravanSaveData savedCaravan))
        {
            nextDraft.saveBaseline = CreateCargoSignature(savedCaravan);
        }
        cargoDraftsByCaravanId[caravanId] = nextDraft;
        return CaravanLoadSettingCommandResult.Success();
    }

    public CaravanCargoCatalogData GetCargoCatalog(string caravanId)
    {
        string normalizedCaravanId = NormalizeId(caravanId);
        if (!TryResolveCaravan(normalizedCaravanId, out _, out _))
        {
            return null;
        }

        var items = new List<TradeItemData>();
        var stocks = new List<int>();
        var prices = new List<long>();
        for (int index = 0; index < cargoCatalog.Length; index++)
        {
            TradeItemData item = cargoCatalog[index];
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
                continue;

            items.Add(item);
            stocks.Add(Mathf.Max(0, defaultCatalogStock));
            prices.Add(Math.Max(0L, item.BaseBuyPrice));
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
        FrameworkSaveData saveData = FrameworkRoot.Instance != null
            ? FrameworkRoot.Instance.CurrentSaveData
            : null;
        if (saveData?.caravans != null && saveData.caravans.Count > 0)
        {
            var options = new List<TradePrepareCaravanOptionViewData>();
            for (int index = 0; index < saveData.caravans.Count; index++)
            {
                FrameworkCaravanSaveData caravan = saveData.caravans[index];
                if (caravan == null || string.IsNullOrWhiteSpace(caravan.caravanId))
                    continue;

                bool hasCurrentTown = !string.IsNullOrWhiteSpace(caravan.currentTownId);
                bool canSelect = caravan.state == JourneyState.Prepare && hasCurrentTown;
                options.Add(new TradePrepareCaravanOptionViewData
                {
                    caravanId = caravan.caravanId,
                    displayName = $"Caravan {index + 1}",
                    currentTownId = caravan.currentTownId ?? string.Empty,
                    state = caravan.state,
                    canSelect = canSelect,
                    disabledReason = canSelect
                        ? string.Empty
                        : !hasCurrentTown
                            ? "This Caravan has no valid departure town."
                            : "This Caravan is already traveling or awaiting settlement."
                });
            }
            return options.ToArray();
        }

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
        cargoDraftsByCaravanId.Clear();
    }

    private CargoItemViewData[] CreateSavedCargoSnapshot(FrameworkCaravanSaveData caravan)
    {
        if (caravan?.cargo == null || caravan.cargo.Count == 0)
            return Array.Empty<CargoItemViewData>();

        var quantities = new Dictionary<string, int>(StringComparer.Ordinal);
        var savedItems = new Dictionary<string, TradeItemSaveData>(StringComparer.Ordinal);
        for (int index = 0; index < caravan.cargo.Count; index++)
        {
            CargoEntrySaveData entry = caravan.cargo[index];
            string itemId = NormalizeId(entry?.item?.itemId);
            if (string.IsNullOrEmpty(itemId) || entry.quantity <= 0)
                continue;

            quantities.TryGetValue(itemId, out int quantity);
            quantities[itemId] = checked(quantity + entry.quantity);
            if (!savedItems.ContainsKey(itemId))
                savedItems.Add(itemId, entry.item);
        }

        var result = new List<CargoItemViewData>(quantities.Count);
        foreach (KeyValuePair<string, int> pair in quantities)
        {
            TradeItemSaveData savedItem = savedItems[pair.Key];
            TradeItemData catalogItem = FindCatalogItem(pair.Key);
            float unitWeight = catalogItem != null
                ? Mathf.Max(0f, catalogItem.Weight)
                : Mathf.Max(0f, savedItem.weight);
            result.Add(new CargoItemViewData
            {
                itemId = pair.Key,
                displayName = catalogItem != null ? catalogItem.DisplayName : savedItem.itemName,
                icon = catalogItem != null ? catalogItem.Icon : null,
                category = catalogItem != null ? catalogItem.Category : default,
                quantity = pair.Value,
                unitWeight = unitWeight,
                totalWeight = unitWeight * pair.Value,
                purchaseUnitPrice = Math.Max(0L, savedItem.basePrice),
                estimatedSellUnitPrice = catalogItem != null
                    ? Math.Max(0L, catalogItem.BaseSellPrice)
                    : Math.Max(0L, savedItem.basePrice),
                totalPurchasePrice = Math.Max(0L, savedItem.basePrice) * pair.Value
            });
        }

        return result.ToArray();
    }

    private CargoItemViewData[] CreatePlannedCargoSnapshot(
        IReadOnlyList<CaravanLoadItemDraft> plannedCargo)
    {
        var result = new CargoItemViewData[plannedCargo.Count];
        for (int index = 0; index < plannedCargo.Count; index++)
        {
            CaravanLoadItemDraft item = plannedCargo[index];
            TradeItemData catalogItem = FindCatalogItem(item.itemId);
            result[index] = new CargoItemViewData
            {
                itemId = item.itemId,
                displayName = catalogItem != null ? catalogItem.DisplayName : item.itemId,
                icon = catalogItem != null ? catalogItem.Icon : null,
                category = catalogItem != null ? catalogItem.Category : default,
                quantity = item.quantity,
                unitWeight = catalogItem != null ? catalogItem.Weight : 0f,
                totalWeight = (catalogItem != null ? catalogItem.Weight : 0f) * item.quantity,
                purchaseUnitPrice = catalogItem != null ? Math.Max(0L, catalogItem.BaseBuyPrice) : 0L,
                estimatedSellUnitPrice = catalogItem != null ? Math.Max(0L, catalogItem.BaseSellPrice) : 0L,
                totalPurchasePrice = catalogItem != null
                    ? MultiplyClamped(Math.Max(0L, catalogItem.BaseBuyPrice), item.quantity)
                    : 0L
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
        FrameworkSaveData saveData = ResolveSaveData();
        return saveData != null
            && SaveDataLookup.TryGetCaravan(saveData, caravanId, out caravan);
    }

    private static string CreateCargoSignature(FrameworkCaravanSaveData caravan)
    {
        if (caravan?.cargo == null || caravan.cargo.Count == 0)
            return string.Empty;

        var quantities = new SortedDictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < caravan.cargo.Count; index++)
        {
            CargoEntrySaveData entry = caravan.cargo[index];
            string itemId = NormalizeId(entry?.item?.itemId);
            if (string.IsNullOrEmpty(itemId) || entry.quantity <= 0)
                continue;

            quantities.TryGetValue(itemId, out int quantity);
            quantities[itemId] = checked(quantity + entry.quantity);
        }

        var signature = new System.Text.StringBuilder();
        foreach (KeyValuePair<string, int> pair in quantities)
            signature.Append(pair.Key).Append(':').Append(pair.Value).Append(';');
        return signature.ToString();
    }

    private bool TryGetCargoDraft(string caravanId, out CargoDraftState cargoDraft)
    {
        return cargoDraftsByCaravanId.TryGetValue(NormalizeId(caravanId), out cargoDraft);
    }

    private void ClearPlannedCargoDraft(string caravanId)
    {
        cargoDraftsByCaravanId.Remove(NormalizeId(caravanId));
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

    private static bool IsOwnedAnimal(string animalInstanceId)
    {
        return animalInstanceId == FirstAnimalInstanceId
            || animalInstanceId == SecondAnimalInstanceId;
    }

    private TradeItemViewData[] CreateAvailableItemSnapshot()
    {
        CaravanCargoCatalogData catalog = CreateCargoCatalogSnapshot();
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

    private CaravanCargoCatalogData CreateCargoCatalogSnapshot()
    {
        var items = new List<TradeItemData>();
        var stocks = new List<int>();
        var prices = new List<long>();
        for (int index = 0; index < cargoCatalog.Length; index++)
        {
            TradeItemData item = cargoCatalog[index];
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
                continue;
            items.Add(item);
            stocks.Add(Mathf.Max(0, defaultCatalogStock));
            prices.Add(Math.Max(0L, item.BaseBuyPrice));
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
            for (int index = 0; index < saveData.caravans.Count; index++)
            {
                FrameworkCaravanSaveData candidate = saveData.caravans[index];
                if (candidate == null || !string.Equals(candidate.caravanId, caravanId, StringComparison.Ordinal))
                    continue;
                state = candidate.state;
                displayName = $"Caravan {index + 1}";
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

    private static void GetCapacity(string wagonInstanceId, out float maxLoad, out int maxSlots)
    {
        bool hasWagon = NormalizeId(wagonInstanceId) == WagonInstanceId;
        maxLoad = hasWagon ? WagonMaxLoad : 0f;
        maxSlots = hasWagon ? WagonInventorySlotCount : 0;
    }

    private float GetPlannedCargoLoad(IReadOnlyList<CaravanLoadItemDraft> plannedCargo)
    {
        float load = 0f;
        if (plannedCargo == null)
            return load;

        for (int index = 0; index < plannedCargo.Count; index++)
        {
            TradeItemData item = FindCatalogItem(plannedCargo[index].itemId);
            load += item != null ? item.Weight * Mathf.Max(0, plannedCargo[index].quantity) : 0f;
        }

        return load;
    }

    private float GetDraftCargoLoad(IReadOnlyList<CaravanLoadItemDraft> items)
    {
        float load = 0f;
        for (int index = 0; index < items.Count; index++)
        {
            TradeItemData item = FindCatalogItem(items[index].itemId);
            if (item != null)
                load += item.Weight * Mathf.Max(0, items[index].quantity);
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
