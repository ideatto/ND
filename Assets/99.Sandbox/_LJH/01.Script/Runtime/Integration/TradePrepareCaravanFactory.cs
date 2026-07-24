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
        }
        return caravan;
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

        // Departure route validation must not substitute the player's location.
        string currentTownId = draft.currentTownId ?? string.Empty;
        TownData currentTown = TradePrepareViewDataBuilder.FindTown(context.towns, currentTownId);
        RouteData[] routes = TradePrepareViewDataBuilder.MergeUnique(
            context.routes,
            currentTown != null ? currentTown.AvailableRoutes : null,
            route => route != null ? route.RouteId : string.Empty);
        RouteData selected = TradePrepareViewDataBuilder.FindRoute(routes, draft.selectedRouteId);
        if (selected == null || !string.Equals(selected.FromTownId, currentTownId, StringComparison.Ordinal))
        {
            return null;
        }

        return string.IsNullOrEmpty(draft.selectedDestinationTownId)
            || string.Equals(selected.ToTownId, draft.selectedDestinationTownId, StringComparison.Ordinal)
            ? selected
            : null;
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
