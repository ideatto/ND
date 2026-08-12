using System;
using System.Collections.Generic;

public static class TradePrepareCaravanFactory
{
    public const string ErrorDepartureCaravanRequired = "DEPARTURE_CARAVAN_REQUIRED";
    public const string ErrorDepartureCaravanNotFound = "DEPARTURE_CARAVAN_NOT_FOUND";
    public const string ErrorDepartureCaravanUnavailable = "DEPARTURE_CARAVAN_UNAVAILABLE";
    public const string ErrorDepartureCaravanDuplicate = "DEPARTURE_CARAVAN_DUPLICATE";

    // Builds a read-only calculation target while the player is still editing the Draft.
    // Preview creation allows an empty ID so the existing single-Caravan UI can render before migration.
    public static CaravanData CreatePreview(TradePrepareDraft draft, TradePrepareBuildContext context)
    {
        draft = draft ?? new TradePrepareDraft();
        context = context ?? new TradePrepareBuildContext();

        ND.Framework.SaveData saveData = context.saveData;
        // Preview calculations use only the selected Caravan's location.
        string currentTownId = draft.currentTownId ?? string.Empty;
        TownData currentTown = TradePrepareViewDataBuilder.FindTown(context.towns, currentTownId);

        TradeItemData[] items = TradePrepareViewDataBuilder.MergeUnique(
            context.tradeItems,
            currentTown != null && currentTown.Market != null ? currentTown.Market.TradeItems : null,
            item => item != null ? item.ItemId : string.Empty);
        WagonData[] wagons = TradePrepareViewDataBuilder.MergeUnique(
            context.wagons,
            currentTown != null && currentTown.Market != null ? currentTown.Market.WagonItems : null,
            wagon => wagon != null ? wagon.WagonId : string.Empty);
        DraftAnimalData[] animals = TradePrepareViewDataBuilder.MergeUnique(
            context.draftAnimals,
            currentTown != null && currentTown.Market != null ? currentTown.Market.DraftAnimalItems : null,
            animal => animal != null ? animal.DraftAnimalId : string.Empty);

        WagonData selectedWagon = TradePrepareViewDataBuilder.FindWagon(wagons, draft.selectedWagonId);
        Dictionary<string, int> cargo = CreateFinalCargoQuantities(draft, saveData);
        CaravanData caravan = TradePrepareViewDataBuilder.CreatePreviewCaravan(
            cargo,
            items,
            saveData,
            selectedWagon,
            animals,
            context.mercenaries,
            draft);

        // Preserves the Framework-assigned identity selected inside TradePrepareUI.
        // Departure validation rejects an empty ID once the Caravan option Provider is connected.
        caravan.caravanId = NormalizeId(draft.departureCaravanId);
        ND.Framework.CaravanSaveData savedCaravan;
        if (ND.Framework.SaveDataLookup.TryGetCaravan(saveData, caravan.caravanId, out savedCaravan))
        {
            caravan.baseSafetyChancePercent = savedCaravan.baseSafetyChancePercent;
            RestoreSavedCargoPriceGroups(caravan, savedCaravan, items);
        }
        else if (saveData != null && saveData.caravan != null)
        {
            // The legacy single-Caravan compatibility path has no explicit departure ID.
            RestoreSavedCargoPriceGroups(caravan, saveData.caravan, items);
        }
        return caravan;
    }

    /// <summary>
    /// Rebuilds departure Cargo from the selected Caravan's latest SaveData snapshot.
    /// The Draft and aggregate runtime rows are presentation/validation derivatives and may be
    /// stale after a Warehouse transfer; SaveData owns the S4 departure quantities and price groups.
    /// </summary>
    private static void RestoreSavedCargoPriceGroups(
        CaravanData runtimeCaravan,
        ND.Framework.CaravanSaveData savedCaravan,
        TradeItemData[] catalogItems)
    {
        if (runtimeCaravan == null || runtimeCaravan.cargo == null
            || savedCaravan == null || savedCaravan.cargo == null)
        {
            return;
        }

        var runtimeMetadataById = new Dictionary<string, imsiTradeItemData>(StringComparer.Ordinal);
        foreach (CargoEntry aggregate in runtimeCaravan.cargo)
        {
            string itemId = NormalizeId(aggregate?.item?.id);
            if (string.IsNullOrEmpty(itemId) || aggregate.quantity <= 0)
                continue;
            runtimeMetadataById[itemId] = aggregate.item;
        }

        var restored = new List<CargoEntry>();
        foreach (ND.Framework.CargoEntrySaveData row in savedCaravan.cargo)
        {
            ND.Framework.TradeItemSaveData savedItem = row?.item;
            string itemId = NormalizeId(savedItem?.itemId);
            if (string.IsNullOrEmpty(itemId) || row.quantity <= 0)
                continue;

            TradeItemData catalogItem = TradePrepareViewDataBuilder.FindItem(catalogItems, itemId);
            // CreatePreviewCaravan already maps feed to foodAmount. Adding the same saved row to
            // Cargo would count its weight twice and can falsely fail departure at max load.
            if (catalogItem != null && catalogItem.Category == TradeItemCategory.DraftAnimalsFood)
                continue;

            runtimeMetadataById.TryGetValue(itemId, out imsiTradeItemData runtimeItem);
            restored.Add(new CargoEntry
            {
                item = new imsiTradeItemData
                {
                    // Current catalog/runtime metadata wins when available. SaveData remains the
                    // fallback for removed definitions and owns acquisition-price identity.
                    id = runtimeItem?.id ?? catalogItem?.ItemId ?? savedItem.itemId ?? string.Empty,
                    itemName = runtimeItem?.itemName ?? catalogItem?.DisplayName
                        ?? savedItem.itemName ?? string.Empty,
                    weight = runtimeItem?.weight ?? catalogItem?.Weight ?? savedItem.weight,
                    purchaseUnitPrice = Math.Max(0L, savedItem.purchaseUnitPrice),
                    basePrice = Math.Max(0L, runtimeItem?.basePrice
                        ?? catalogItem?.BaseBuyPrice ?? savedItem.basePrice),
                    maxCount = runtimeItem != null && runtimeItem.maxCount > 0
                        ? runtimeItem.maxCount
                        : (catalogItem != null
                            ? catalogItem.MaxCount
                            : (savedItem.maxCount > 0 ? savedItem.maxCount : 1))
                },
                quantity = row.quantity
            });
        }

        runtimeCaravan.cargo = restored;
    }

    // Creates the runtime Caravan only after confirming that the Draft refers to one selectable Provider option.
    // SaveDataLookup hydration is intentionally deferred until the multi-Caravan cutover exists on this branch.
    public static bool TryCreateDeparture(
        TradePrepareDraft draft,
        TradePrepareBuildContext context,
        out CaravanData caravan,
        out string errorCode,
        out string errorMessage)
    {
        draft = draft ?? new TradePrepareDraft();
        context = context ?? new TradePrepareBuildContext();
        caravan = null;
        errorCode = string.Empty;
        errorMessage = string.Empty;

        TradePrepareCaravanOptionViewData[] options = context.caravanOptions
            ?? new TradePrepareCaravanOptionViewData[0];
        string departureCaravanId = NormalizeId(draft.departureCaravanId);

        // An empty option array is the temporary compatibility path for the existing single-Caravan scene.
        if (options.Length > 0)
        {
            if (string.IsNullOrEmpty(departureCaravanId))
            {
                return Fail(
                    ErrorDepartureCaravanRequired,
                    "Select a departure Caravan before starting the trade.",
                    out errorCode,
                    out errorMessage);
            }

            TradePrepareCaravanOptionViewData matched = null;
            int matchCount = 0;
            for (int index = 0; index < options.Length; index++)
            {
                TradePrepareCaravanOptionViewData option = options[index];
                if (option != null && string.Equals(
                    NormalizeId(option.caravanId),
                    departureCaravanId,
                    StringComparison.Ordinal))
                {
                    matched = option;
                    matchCount++;
                }
            }

            if (matchCount == 0)
            {
                return Fail(
                    ErrorDepartureCaravanNotFound,
                    "The selected departure Caravan is not present in the latest Provider snapshot.",
                    out errorCode,
                    out errorMessage);
            }

            if (matchCount > 1)
            {
                return Fail(
                    ErrorDepartureCaravanDuplicate,
                    "The Provider returned duplicate entries for the selected departure Caravan.",
                    out errorCode,
                    out errorMessage);
            }

            if (!matched.canSelect)
            {
                return Fail(
                    ErrorDepartureCaravanUnavailable,
                    string.IsNullOrWhiteSpace(matched.disabledReason)
                        ? "The selected Caravan cannot start a new trade."
                        : matched.disabledReason,
                    out errorCode,
                    out errorMessage);
            }
        }

        caravan = CreatePreview(draft, context);
        caravan.caravanId = departureCaravanId;
        return true;
    }

    public static RouteData ResolveSelectedRoute(TradePrepareDraft draft, TradePrepareBuildContext context)
    {
        if (draft == null || context == null)
        {
            return null;
        }

        // 출발 Route 검증은 Player의 위치를 대신 사용하지 않고,
        // 선택한 Caravan Draft의 currentTownId만 사용한다.
        string currentTownId = draft.currentTownId ?? string.Empty;
        TownData currentTown = TradePrepareViewDataBuilder.FindTown(context.towns, currentTownId);

        if (currentTown == null)
        {
            return null;
        }

        // Route 후보의 권위 데이터는 현재 Town의 AvailableRoutes다.
        // 전역 context.routes를 후보에 합쳐 누락된 Town 연결을 보완하지 않는다.
        RouteData[] routes = currentTown.AvailableRoutes ?? Array.Empty<RouteData>();
        RouteData selected = TradePrepareViewDataBuilder.FindRoute(routes, draft.selectedRouteId);

        // 선택 Route는 반드시 현재 Town에서 출발해야 한다.
        if (selected == null || !string.Equals(selected.FromTownId, currentTownId, StringComparison.Ordinal))
        {
            return null;
        }

        // 목적지가 선택되어 있다면 Route의 실제 목적지와 정확히 일치해야 한다.
        if (!string.IsNullOrEmpty(draft.selectedDestinationTownId)
            && !string.Equals(
                selected.ToTownId,
                draft.selectedDestinationTownId,
                StringComparison.Ordinal))
        {
            return null;
        }

        return selected;
    }

    public static Dictionary<string, int> CreateFinalCargoQuantities(TradePrepareDraft draft)
    {
        return TradePrepareViewDataBuilder.CreateFinalCargoQuantities(
            draft ?? new TradePrepareDraft());
    }

    public static Dictionary<string, int> CreateFinalCargoQuantities(
        TradePrepareDraft draft,
        ND.Framework.SaveData saveData)
    {
        return TradePrepareViewDataBuilder.CreateFinalCargoQuantities(
            draft ?? new TradePrepareDraft(),
            saveData);
    }

    private static bool Fail(
        string failureCode,
        string failureMessage,
        out string errorCode,
        out string errorMessage)
    {
        errorCode = failureCode;
        errorMessage = failureMessage;
        return false;
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
