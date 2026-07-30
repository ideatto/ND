using System;
using System.Collections.Generic;

namespace ND.Framework
{
    public enum TownQuestPanelKind
    {
        None,
        Offer,
        Progress,
        Payment
    }

    public sealed class TownQuestItemView
    {
        public string ItemId { get; internal set; }
        public string DisplayName { get; internal set; }
        public int RequiredQuantity { get; internal set; }
    }

    public sealed class TownQuestCaravanView
    {
        public string CaravanId { get; internal set; }
        public int SlotIndex { get; internal set; }
        public bool CanPayTradingCurrency { get; internal set; }
        public bool CanPayAllItems { get; internal set; }
    }

    public sealed class TownQuestViewModel
    {
        public string QuestId { get; internal set; }
        public string TownId { get; internal set; }
        public string DisplayName { get; internal set; }
        public string Description { get; internal set; }
        public QuestRuntimePhase Phase { get; internal set; }
        public QuestPaymentPolicy PaymentPolicy { get; internal set; }
        public long RequiredTradingCurrency { get; internal set; }
        public bool ShowTradingCurrencyPayment { get; internal set; }
        public bool ShowItemPayment { get; internal set; }
        public bool HasEligibleCaravan { get; internal set; }
        public List<TownQuestItemView> RequiredItems { get; } =
            new List<TownQuestItemView>();
        public List<TownQuestCaravanView> EligibleCaravans { get; } =
            new List<TownQuestCaravanView>();
    }

    public sealed class TownQuestOpenResult
    {
        public TownQuestPanelKind PanelKind { get; internal set; }
        public TownQuestViewModel Quest { get; internal set; }
    }

    /// <summary>
    /// Builds UI-neutral Quest panel data. Opening location is intentionally not
    /// checked here; payment eligibility always uses the issuing town.
    /// </summary>
    public static class TownQuestPresentationService
    {
        public static IReadOnlyList<TownQuestViewModel> GetActiveQuests(
            SaveData saveData,
            ISharedGameDataProvider gameData,
            string townId)
        {
            var result = new List<TownQuestViewModel>();
            if (saveData?.world == null || gameData == null ||
                string.IsNullOrWhiteSpace(townId)) return result;

            IReadOnlyList<SharedQuestDefinition> quests =
                gameData.GetQuestsForTown(townId);
            for (int i = 0; i < quests.Count; i++)
            {
                QuestRuntimeSaveData state = TownQuestFlowService.FindState(
                    saveData.world.questRuntimeStates, quests[i]?.Id);
                if (state == null ||
                    state.phase == QuestRuntimePhase.Inactive) continue;
                result.Add(Build(saveData, gameData, quests[i], state));
            }
            return result;
        }

        public static TownQuestOpenResult OpenQuest(
            SaveData saveData,
            ISharedGameDataProvider gameData,
            string questId)
        {
            var result = new TownQuestOpenResult();
            if (saveData?.world == null || gameData == null ||
                !gameData.TryGetQuest(questId, out SharedQuestDefinition quest))
                return result;

            QuestRuntimeSaveData state = TownQuestFlowService.FindState(
                saveData.world.questRuntimeStates, questId);
            if (state == null || state.phase == QuestRuntimePhase.Inactive)
                return result;

            result.Quest = Build(saveData, gameData, quest, state);
            if (state.phase == QuestRuntimePhase.Offered)
                result.PanelKind = TownQuestPanelKind.Offer;
            else
                result.PanelKind = result.Quest.HasEligibleCaravan
                    ? TownQuestPanelKind.Payment
                    : TownQuestPanelKind.Progress;
            return result;
        }

        private static TownQuestViewModel Build(
            SaveData saveData,
            ISharedGameDataProvider gameData,
            SharedQuestDefinition quest,
            QuestRuntimeSaveData state)
        {
            var view = new TownQuestViewModel
            {
                QuestId = quest.Id,
                TownId = quest.SubmissionTownId,
                DisplayName = quest.DisplayName,
                Description = quest.Description,
                Phase = state.phase,
                PaymentPolicy = quest.PaymentPolicy,
                RequiredTradingCurrency = quest.RequiredTradingCurrency,
                ShowTradingCurrencyPayment =
                    quest.PaymentPolicy == QuestPaymentPolicy.TradingCurrencyOnly ||
                    quest.PaymentPolicy == QuestPaymentPolicy.CurrencyOrItems,
                ShowItemPayment =
                    quest.PaymentPolicy == QuestPaymentPolicy.CaravanItemsOnly ||
                    quest.PaymentPolicy == QuestPaymentPolicy.CurrencyOrItems
            };

            SharedQuestItemCost[] costs =
                quest.RequiredItems ?? new SharedQuestItemCost[0];
            for (int i = 0; i < costs.Length; i++)
            {
                SharedQuestItemCost cost = costs[i];
                if (cost == null) continue;
                gameData.TryGetTradeItem(
                    cost.ItemId, out SharedTradeItemDefinition item);
                view.RequiredItems.Add(new TownQuestItemView
                {
                    ItemId = cost.ItemId,
                    DisplayName = item?.DisplayName ?? cost.ItemId,
                    RequiredQuantity = cost.Quantity
                });
            }

            if (saveData.caravans == null) return view;
            for (int i = 0; i < saveData.caravans.Count; i++)
            {
                CaravanSaveData caravan = saveData.caravans[i];
                if (caravan == null || caravan.state != JourneyState.Prepare ||
                    !string.Equals(
                        caravan.currentTownId,
                        quest.SubmissionTownId,
                        StringComparison.Ordinal))
                    continue;

                bool canCurrency = view.ShowTradingCurrencyPayment &&
                    saveData.player != null &&
                    saveData.player.tradingCurrency >=
                        quest.RequiredTradingCurrency;
                bool canItems = view.ShowItemPayment &&
                    HasAllItems(caravan, costs);
                if (!canCurrency && !canItems) continue;
                view.EligibleCaravans.Add(new TownQuestCaravanView
                {
                    CaravanId = caravan.caravanId,
                    SlotIndex = caravan.slotIndex,
                    CanPayTradingCurrency = canCurrency,
                    CanPayAllItems = canItems
                });
            }
            view.HasEligibleCaravan = view.EligibleCaravans.Count > 0;
            return view;
        }

        private static bool HasAllItems(
            CaravanSaveData caravan,
            SharedQuestItemCost[] costs)
        {
            if (costs == null || costs.Length == 0) return false;
            for (int i = 0; i < costs.Length; i++)
            {
                SharedQuestItemCost cost = costs[i];
                if (cost == null || cost.Quantity <= 0) return false;
                int total = 0;
                if (caravan.cargo != null)
                    for (int cargoIndex = 0;
                        cargoIndex < caravan.cargo.Count;
                        cargoIndex++)
                    {
                        CargoEntrySaveData cargo = caravan.cargo[cargoIndex];
                        if (cargo?.item != null &&
                            string.Equals(
                                cargo.item.itemId,
                                cost.ItemId,
                                StringComparison.Ordinal))
                            total = checked(total + Math.Max(0, cargo.quantity));
                    }
                if (total < cost.Quantity) return false;
            }
            return true;
        }
    }
}
