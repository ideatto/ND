using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides an in-memory S3 fixture for scene wiring and SmokeTest verification.
/// </summary>
/// <remarks>
/// This component never reads or writes SaveData. Replace it with Framework implementations of
/// ICaravanSettingViewDataProvider and ICaravanSettingCommand before production persistence tests.
/// </remarks>
[DisallowMultipleComponent]
public sealed class TestCaravanSettingService : MonoBehaviour,
    ICaravanSettingViewDataProvider,
    ICaravanSettingCommand,
    ICaravanLoadSettingViewDataProvider,
    ICaravanLoadSettingCommand,
    ICaravanCargoCatalogProvider
{
    public const string PrepareCaravanId = "test-caravan-prepare";
    public const string TravelingCaravanId = "test-caravan-traveling";
    public const string WagonContentId = "test-wagon-medium";
    public const string WagonInstanceId = "test-wagon-instance-01";
    public const string AnimalContentId = "test-horse";
    public const string FirstAnimalInstanceId = "test-horse-instance-01";
    public const string SecondAnimalInstanceId = "test-horse-instance-02";
    public const float WagonMaxLoad = 100f;
    public const int WagonInventorySlotCount = 5;

    [Header("S4 TradeItemData catalog")]
    [SerializeField] private TradeItemData[] cargoCatalog = Array.Empty<TradeItemData>();
    [SerializeField] private int defaultCatalogStock = 20;

    private string selectedWagonInstanceId = WagonInstanceId;
    private readonly List<string> selectedAnimalInstanceIds = new List<string>
    {
        FirstAnimalInstanceId,
        SecondAnimalInstanceId
    };
    private readonly List<CaravanLoadItemDraft> plannedCargo = new List<CaravanLoadItemDraft>();

    public CaravanSettingViewData GetSetting(string caravanId)
    {
        string normalizedCaravanId = NormalizeId(caravanId);
        if (normalizedCaravanId == PrepareCaravanId)
        {
            return CreateSettingSnapshot(
                PrepareCaravanId,
                "Preparation Caravan",
                JourneyState.Prepare,
                true,
                string.Empty,
                selectedWagonInstanceId,
                selectedAnimalInstanceIds);
        }

        if (normalizedCaravanId == TravelingCaravanId)
        {
            return CreateSettingSnapshot(
                TravelingCaravanId,
                "Traveling Caravan",
                JourneyState.Traveling,
                false,
                "Caravan settings cannot be changed while the Caravan is traveling.",
                WagonInstanceId,
                new[] { FirstAnimalInstanceId, SecondAnimalInstanceId });
        }

        // A missing Caravan remains distinct from a valid but read-only Caravan.
        return null;
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
        if (caravanId != PrepareCaravanId && caravanId != TravelingCaravanId)
        {
            return CaravanSettingCommandResult.Failure(
                CaravanSettingFailureCodes.CaravanNotFound,
                "The selected Caravan could not be found.");
        }

        if (caravanId != PrepareCaravanId)
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
        if (GetPlannedCargoLoad() > nextMaxLoad || plannedCargo.Count > nextMaxSlots)
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
        if (normalizedCaravanId != PrepareCaravanId && normalizedCaravanId != TravelingCaravanId)
        {
            return null;
        }

        bool canEdit = normalizedCaravanId == PrepareCaravanId;
        string capacityWagonInstanceId = canEdit ? selectedWagonInstanceId : WagonInstanceId;
        GetCapacity(capacityWagonInstanceId, out float maxLoad, out int maxSlots);
        CargoItemViewData[] plannedItems = normalizedCaravanId == PrepareCaravanId
            ? CreatePlannedCargoSnapshot()
            : Array.Empty<CargoItemViewData>();
        int usedSlots = plannedItems.Length;
        float currentLoad = 0f;
        for (int index = 0; index < plannedItems.Length; index++)
        {
            currentLoad += plannedItems[index].totalWeight;
        }

        return new CaravanLoadSettingViewData
        {
            caravanId = normalizedCaravanId,
            caravanDisplayName = canEdit ? "Preparation Caravan" : "Traveling Caravan",
            currentTownId = "test-town",
            state = canEdit ? JourneyState.Prepare : JourneyState.Traveling,
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
        if (caravanId != PrepareCaravanId && caravanId != TravelingCaravanId)
        {
            return CaravanLoadSettingCommandResult.Failure(
                CaravanLoadSettingFailureCodes.CaravanNotFound,
                "The selected Caravan could not be found.");
        }

        if (caravanId != PrepareCaravanId)
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

        plannedCargo.Clear();
        plannedCargo.AddRange(validatedItems);
        return CaravanLoadSettingCommandResult.Success();
    }

    public CaravanCargoCatalogData GetCargoCatalog(string caravanId)
    {
        string normalizedCaravanId = NormalizeId(caravanId);
        if (normalizedCaravanId != PrepareCaravanId && normalizedCaravanId != TravelingCaravanId)
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

    internal void SetCargoCatalogForTests(params TradeItemData[] items)
    {
        cargoCatalog = items ?? Array.Empty<TradeItemData>();
    }

    private CargoItemViewData[] CreatePlannedCargoSnapshot()
    {
        var result = new CargoItemViewData[plannedCargo.Count];
        for (int index = 0; index < plannedCargo.Count; index++)
        {
            CaravanLoadItemDraft item = plannedCargo[index];
            result[index] = new CargoItemViewData
            {
                itemId = item.itemId,
                displayName = item.itemId,
                quantity = item.quantity,
                unitWeight = FindCatalogItem(item.itemId)?.Weight ?? 0f,
                totalWeight = (FindCatalogItem(item.itemId)?.Weight ?? 0f) * item.quantity
            };
        }

        return result;
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
        CaravanCargoCatalogData catalog = GetCargoCatalog(PrepareCaravanId);
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

    private float GetPlannedCargoLoad()
    {
        float load = 0f;
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
}
