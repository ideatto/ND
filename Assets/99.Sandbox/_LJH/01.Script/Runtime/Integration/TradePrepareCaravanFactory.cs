using System;
using System.Collections.Generic;

public static class TradePrepareCaravanFactory
{
    public static CaravanData Create(TradePrepareDraft draft, TradePrepareBuildContext context)
    {
        draft = draft ?? new TradePrepareDraft();
        context = context ?? new TradePrepareBuildContext();

        ND.Framework.SaveData saveData = context.saveData;
        string currentTownId = !string.IsNullOrEmpty(draft.currentTownId)
            ? draft.currentTownId
            : saveData != null && saveData.player != null ? saveData.player.currentTownId : string.Empty;
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
        Dictionary<string, int> cargo = CreateFinalCargoQuantities(draft);
        return TradePrepareViewDataBuilder.CreatePreviewCaravan(
            cargo,
            items,
            saveData,
            selectedWagon,
            animals,
            context.mercenaries,
            draft);
    }

    public static RouteData ResolveSelectedRoute(TradePrepareDraft draft, TradePrepareBuildContext context)
    {
        if (draft == null || context == null)
        {
            return null;
        }

        ND.Framework.SaveData saveData = context.saveData;
        string currentTownId = !string.IsNullOrEmpty(draft.currentTownId)
            ? draft.currentTownId
            : saveData != null && saveData.player != null ? saveData.player.currentTownId : string.Empty;
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
}
