using System;
using System.Collections.Generic;
using ND.Economy;
using UnityEngine;

namespace ND.Framework
{
    public enum QuestPaymentMode { TradingCurrency, CaravanItems }
    public enum QuestCompletionFailureReason
    {
        None, InvalidInput, QuestUnavailable, SubmissionTownMismatch,
        CaravanNotFound, CaravanNotAtSubmissionTown, CaravanUnavailable,
        QuestNotAccepted, InvalidPaymentMode, InvalidDefinition, InsufficientTradingCurrency,
        InsufficientItems, SaveFailed
    }

    public sealed class QuestAvailabilityResult
    {
        public bool IsAvailable { get; internal set; }
        public long NextAvailableUtcTicks { get; internal set; }
        public long RemainingTicks { get; internal set; }
    }

    public sealed class QuestCompletionResult
    {
        public bool Succeeded { get; internal set; }
        public QuestCompletionFailureReason FailureReason { get; internal set; }
        public InvestmentQuestEconomicPlan Plan { get; internal set; }
        public SaveResult SaveResult { get; internal set; }
    }

    /// <summary>
    /// World-shared Quest query and transaction. One idle Caravan at the Quest town
    /// pays all item costs; rewards are stored once in WorldSaveData.
    /// </summary>
    public static class QuestRuntimeService
    {
        public static QuestAvailabilityResult QueryAvailability(
            SaveData saveData, SharedQuestDefinition quest, DateTime currentUtc)
        {
            var result = new QuestAvailabilityResult();
            if (saveData?.world == null || quest == null ||
                string.IsNullOrWhiteSpace(quest.Id)) return result;

            QuestRuntimeSaveData state =
                FindQuestState(saveData.world.questRuntimeStates, quest.Id);
            if (state == null || state.completionCount <= 0)
            {
                result.IsAvailable = true;
                return result;
            }

            result.NextAvailableUtcTicks = Math.Max(0L, state.nextAvailableUtcTicks);
            if (quest.RepeatPolicy == QuestRepeatPolicy.None) return result;
            long now = Math.Max(0L, currentUtc.ToUniversalTime().Ticks);
            result.IsAvailable = now >= result.NextAvailableUtcTicks;
            result.RemainingTicks = result.IsAvailable
                ? 0L : result.NextAvailableUtcTicks - now;
            return result;
        }

        public static QuestCompletionResult Complete(
            SaveData saveData,
            ISaveService saveService,
            IGameTimeProvider timeProvider,
            SharedQuestDefinition quest,
            string requestedTownId,
            string caravanId,
            QuestPaymentMode paymentMode)
        {
            if (saveData?.world == null || saveData.player == null ||
                saveService == null || timeProvider == null || quest == null ||
                string.IsNullOrWhiteSpace(requestedTownId) ||
                string.IsNullOrWhiteSpace(caravanId) ||
                string.IsNullOrWhiteSpace(quest.Id) ||
                string.IsNullOrWhiteSpace(quest.SubmissionTownId))
                return Fail(QuestCompletionFailureReason.InvalidInput);

            DateTime completedUtc = timeProvider.CurrentUtc.ToUniversalTime();
            if (!QueryAvailability(saveData, quest, completedUtc).IsAvailable)
                return Fail(QuestCompletionFailureReason.QuestUnavailable);
            QuestRuntimeSaveData runtimeState =
                FindQuestState(saveData.world.questRuntimeStates, quest.Id);
            if (runtimeState == null ||
                runtimeState.phase != QuestRuntimePhase.Accepted)
                return Fail(QuestCompletionFailureReason.QuestNotAccepted);
            if (!string.Equals(requestedTownId, quest.SubmissionTownId, StringComparison.Ordinal))
                return Fail(QuestCompletionFailureReason.SubmissionTownMismatch);
            if (!SaveDataLookup.TryGetCaravan(saveData, caravanId, out var caravan))
                return Fail(QuestCompletionFailureReason.CaravanNotFound);
            if (!string.Equals(caravan.currentTownId, quest.SubmissionTownId, StringComparison.Ordinal))
                return Fail(QuestCompletionFailureReason.CaravanNotAtSubmissionTown);
            if (caravan.state != JourneyState.Prepare)
                return Fail(QuestCompletionFailureReason.CaravanUnavailable);
            if (!IsPaymentModeAllowed(quest.PaymentPolicy, paymentMode))
                return Fail(QuestCompletionFailureReason.InvalidPaymentMode);

            InvestmentQuestPlanBuildResult build =
                InvestmentQuestEconomicPlanBuilder.Build(
                    BuildInput(saveData, caravan, quest, paymentMode));
            if (build == null || !build.Success || build.Plan == null)
                return Fail(MapFailure(build?.FailureReason));

            string snapshot = JsonUtility.ToJson(saveData);
            long tradingCurrencyBefore = saveData.player.tradingCurrency;
            QuestRewardsCommittedEvent committedEvent;
            QuestCompletionResult completionResult;
            try
            {
                var townsBefore = BuildIdSet(saveData.world.unlockedTownIds);
                var routesBefore = BuildIdSet(saveData.world.unlockedRouteIds);
                var specialtiesBefore = BuildSpecialtyKeySet(
                    saveData.world.unlockedTownSpecialties);
                ApplyPlan(saveData, caravan, quest, build.Plan, completedUtc);
                CaravanActivityLog.Add(
                    saveData,
                    CaravanActivityLogType.QuestCompleted,
                    caravanId,
                    townId: quest.SubmissionTownId,
                    occurredUtcTicks: completedUtc.Ticks);
                SaveResult saved = saveService.Save(saveData);
                if (saved == null || !saved.Succeeded)
                {
                    JsonUtility.FromJsonOverwrite(snapshot, saveData);
                    return new QuestCompletionResult
                    {
                        FailureReason = QuestCompletionFailureReason.SaveFailed,
                        Plan = build.Plan,
                        SaveResult = saved
                    };
                }
                committedEvent = new QuestRewardsCommittedEvent(
                    quest.Id,
                    GetAddedIds(saveData.world.unlockedTownIds, townsBefore),
                    GetAddedIds(saveData.world.unlockedRouteIds, routesBefore),
                    GetAddedSpecialtyIds(
                        saveData.world.unlockedTownSpecialties,
                        specialtiesBefore));
                completionResult = new QuestCompletionResult
                {
                    Succeeded = true,
                    Plan = build.Plan,
                    SaveResult = saved
                };
            }
            catch
            {
                JsonUtility.FromJsonOverwrite(snapshot, saveData);
                return Fail(QuestCompletionFailureReason.InvalidDefinition);
            }

            if (saveData.player.tradingCurrency != tradingCurrencyBefore)
                FrameworkEvents.RaiseTradingCurrencyChanged(
                    saveData.player.tradingCurrency);
            FrameworkEvents.RaiseQuestRewardsCommitted(committedEvent);
            return completionResult;
        }

        public static float ResolveBanditEncounterMultiplier(
            WorldSaveData world, SharedRouteDefinition route, DateTime currentUtc)
        {
            if (world?.townRouteBanditModifiers == null || route == null) return 1f;
            long now = currentUtc.ToUniversalTime().Ticks;
            float result = 1f;
            for (int i = 0; i < world.townRouteBanditModifiers.Count; i++)
            {
                TownRouteBanditModifierSaveData modifier =
                    world.townRouteBanditModifiers[i];
                if (modifier == null || modifier.expiresUtcTicks <= now) continue;
                bool connected =
                    string.Equals(modifier.townId, route.FromTownId, StringComparison.Ordinal) ||
                    string.Equals(modifier.townId, route.ToTownId, StringComparison.Ordinal);
                if (connected)
                    result = Math.Min(result, Mathf.Clamp01(modifier.encounterMultiplier));
            }
            return Mathf.Clamp01(result);
        }

        private static InvestmentQuestInput BuildInput(
            SaveData data, CaravanSaveData caravan, SharedQuestDefinition quest,
            QuestPaymentMode paymentMode)
        {
            var definition = new InvestmentQuestDefinition
            {
                QuestId = quest.Id,
                SubmissionTownId = quest.SubmissionTownId,
                TradingCurrencyCost = paymentMode == QuestPaymentMode.TradingCurrency
                    ? quest.RequiredTradingCurrency : 0L
            };
            if (paymentMode == QuestPaymentMode.CaravanItems)
            {
                foreach (SharedQuestItemCost cost in quest.RequiredItems)
                    if (cost != null) definition.ItemCosts.Add(
                        new InvestmentItemCost
                        {
                            ItemId = cost.ItemId,
                            Quantity = cost.Quantity
                        });
            }

            foreach (SharedQuestReward reward in quest.Rewards)
            {
                if (reward == null) continue;
                switch (reward.RewardType)
                {
                    case QuestRewardType.UnlockTown:
                        definition.UnlockTownIds.Add(reward.RewardId); break;
                    case QuestRewardType.UnlockRoute:
                        definition.UnlockRouteIds.Add(reward.RewardId); break;
                    case QuestRewardType.TradeItem:
                        definition.UnlockSpecialtyItemIds.Add(reward.RewardId); break;
                    case QuestRewardType.RouteBanditEncounterReduction:
                        definition.BanditEncounterMultiplier = reward.EncounterMultiplier;
                        definition.BanditReductionDurationTicks =
                            TimeSpan.FromMinutes(reward.Value).Ticks;
                        break;
                }
            }

            var input = new InvestmentQuestInput
            {
                RequestedQuestId = quest.Id,
                CaravanId = caravan.caravanId,
                CanSubmitCaravanAssets = true,
                TradingCurrency = data.player.tradingCurrency,
                Definition = definition
            };
            if (caravan.cargo != null)
                foreach (CargoEntrySaveData cargo in caravan.cargo)
                    if (cargo?.item != null) input.CaravanInventory.Add(
                        new InvestmentInventoryEntry
                        {
                            ItemId = cargo.item.itemId,
                            Quantity = cargo.quantity
                        });
            return input;
        }

        private static void ApplyPlan(
            SaveData data, CaravanSaveData caravan, SharedQuestDefinition quest,
            InvestmentQuestEconomicPlan plan, DateTime completedUtc)
        {
            data.player.tradingCurrency = plan.TradingCurrencyAfter;
            foreach (InvestmentItemPlan item in plan.Items)
                ConsumeCargo(caravan.cargo, item.ItemId, item.RequiredQuantity);
            AddUnique(data.world.unlockedTownIds, plan.UnlockTownIds);
            AddUnique(data.world.unlockedRouteIds, plan.UnlockRouteIds);

            foreach (string itemId in plan.UnlockSpecialtyItemIds)
                if (!HasSpecialty(data.world.unlockedTownSpecialties,
                    plan.SubmissionTownId, itemId))
                    data.world.unlockedTownSpecialties.Add(
                        new TownSpecialtyUnlockSaveData
                        {
                            townId = plan.SubmissionTownId,
                            itemId = itemId
                        });

            if (plan.BanditReductionDurationTicks > 0)
                UpsertBanditModifier(data.world.townRouteBanditModifiers,
                    plan, completedUtc.Ticks);
            UpsertQuestState(data.world.questRuntimeStates,
                quest, completedUtc.Ticks);
        }

        private static void UpsertQuestState(
            List<QuestRuntimeSaveData> states, SharedQuestDefinition quest, long completed)
        {
            QuestRuntimeSaveData state = FindQuestState(states, quest.Id);
            if (state == null)
            {
                state = new QuestRuntimeSaveData { questId = quest.Id };
                states.Add(state);
            }
            state.completionCount = checked(state.completionCount + 1);
            state.phase = QuestRuntimePhase.Inactive;
            state.lastCompletedUtcTicks = completed;
            state.nextAvailableUtcTicks =
                quest.RepeatPolicy == QuestRepeatPolicy.AfterCompletionDelay
                    ? checked(completed +
                        TimeSpan.FromSeconds(quest.RegenerationSeconds).Ticks)
                    : 0L;
        }

        private static void UpsertBanditModifier(
            List<TownRouteBanditModifierSaveData> modifiers,
            InvestmentQuestEconomicPlan plan, long completed)
        {
            TownRouteBanditModifierSaveData target = modifiers.Find(
                value => value != null &&
                    string.Equals(value.sourceQuestId, plan.QuestId,
                        StringComparison.Ordinal));
            if (target == null)
            {
                target = new TownRouteBanditModifierSaveData
                    { sourceQuestId = plan.QuestId };
                modifiers.Add(target);
            }
            target.townId = plan.SubmissionTownId;
            target.encounterMultiplier =
                Mathf.Clamp01(plan.BanditEncounterMultiplier);
            target.expiresUtcTicks =
                checked(completed + plan.BanditReductionDurationTicks);
        }

        private static void ConsumeCargo(
            List<CargoEntrySaveData> cargo, string itemId, int quantity)
        {
            int remaining = quantity;
            for (int i = 0; i < cargo.Count && remaining > 0; i++)
            {
                CargoEntrySaveData entry = cargo[i];
                if (entry?.item == null ||
                    !string.Equals(entry.item.itemId, itemId,
                        StringComparison.Ordinal)) continue;
                int consumed = Math.Min(entry.quantity, remaining);
                entry.quantity -= consumed;
                remaining -= consumed;
            }
            if (remaining != 0) throw new InvalidOperationException();
            cargo.RemoveAll(entry => entry == null || entry.quantity <= 0);
        }

        private static void AddUnique(
            List<string> target, IReadOnlyList<string> source)
        {
            foreach (string id in source)
                if (!target.Contains(id)) target.Add(id);
        }

        private static bool HasSpecialty(
            List<TownSpecialtyUnlockSaveData> entries,
            string townId, string itemId)
        {
            return entries.Exists(entry => entry != null &&
                string.Equals(entry.townId, townId, StringComparison.Ordinal) &&
                string.Equals(entry.itemId, itemId, StringComparison.Ordinal));
        }

        private static HashSet<string> BuildSpecialtyKeySet(
            List<TownSpecialtyUnlockSaveData> entries)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (entries == null) return result;
            foreach (TownSpecialtyUnlockSaveData entry in entries)
                if (entry != null)
                    result.Add(BuildSpecialtyKey(entry.townId, entry.itemId));
            return result;
        }

        private static HashSet<string> BuildIdSet(List<string> ids)
        {
            return ids == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(ids, StringComparer.Ordinal);
        }

        private static List<string> GetAddedIds(List<string> current, HashSet<string> previous)
        {
            var result = new List<string>();
            if (current == null) return result;
            foreach (string id in current)
                if (!string.IsNullOrWhiteSpace(id) && !previous.Contains(id)) result.Add(id);
            return result;
        }

        private static List<string> GetAddedSpecialtyIds(
            List<TownSpecialtyUnlockSaveData> current, HashSet<string> previous)
        {
            var result = new List<string>();
            if (current == null) return result;
            foreach (TownSpecialtyUnlockSaveData entry in current)
                if (entry != null && !string.IsNullOrWhiteSpace(entry.itemId) &&
                    !previous.Contains(BuildSpecialtyKey(entry.townId, entry.itemId)))
                    result.Add(entry.itemId);
            return result;
        }

        private static string BuildSpecialtyKey(string townId, string itemId)
        {
            return (townId ?? string.Empty) + "\n" + (itemId ?? string.Empty);
        }

        private static QuestRuntimeSaveData FindQuestState(
            List<QuestRuntimeSaveData> states, string questId)
        {
            return states?.Find(state => state != null &&
                string.Equals(state.questId, questId, StringComparison.Ordinal));
        }

        private static QuestCompletionFailureReason MapFailure(
            InvestmentQuestFailureReason? reason)
        {
            switch (reason)
            {
                case InvestmentQuestFailureReason.InsufficientTradingCurrency:
                    return QuestCompletionFailureReason.InsufficientTradingCurrency;
                case InvestmentQuestFailureReason.InsufficientItems:
                    return QuestCompletionFailureReason.InsufficientItems;
                case InvestmentQuestFailureReason.CaravanUnavailable:
                    return QuestCompletionFailureReason.CaravanUnavailable;
                default:
                    return QuestCompletionFailureReason.InvalidDefinition;
            }
        }

        private static bool IsPaymentModeAllowed(
            QuestPaymentPolicy policy,
            QuestPaymentMode mode)
        {
            switch (policy)
            {
                case QuestPaymentPolicy.TradingCurrencyOnly:
                    return mode == QuestPaymentMode.TradingCurrency;
                case QuestPaymentPolicy.CaravanItemsOnly:
                    return mode == QuestPaymentMode.CaravanItems;
                case QuestPaymentPolicy.CurrencyOrItems:
                    return mode == QuestPaymentMode.TradingCurrency ||
                        mode == QuestPaymentMode.CaravanItems;
                default:
                    return false;
            }
        }

        private static QuestCompletionResult Fail(
            QuestCompletionFailureReason reason)
        {
            return new QuestCompletionResult { FailureReason = reason };
        }
    }
}
