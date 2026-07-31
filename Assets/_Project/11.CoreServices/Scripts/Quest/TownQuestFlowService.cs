using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Framework
{
    public sealed class QuestMutationResult
    {
        public bool Succeeded { get; internal set; }
        public bool Changed { get; internal set; }
        public SaveResult SaveResult { get; internal set; }
    }

    public static class TownQuestFlowService
    {
        public static QuestMutationResult RefreshOnTownVisited(
            SaveData saveData,
            ISaveService saveService,
            IGameTimeProvider timeProvider,
            ISharedGameDataProvider gameData,
            QuestGenerationSettings settings,
            string visitedTownId)
        {
            if (saveData?.world == null || saveService == null ||
                timeProvider == null || gameData == null || settings == null ||
                string.IsNullOrWhiteSpace(visitedTownId))
                return new QuestMutationResult();

            IReadOnlyList<SharedQuestDefinition> catalog =
                gameData.GetQuestsForTown(visitedTownId);
            int capacity = settings.GetMaxActiveQuests(visitedTownId);
            int activeCount = CountActive(saveData.world.questRuntimeStates, catalog);
            if (activeCount >= capacity)
                return new QuestMutationResult { Succeeded = true };

            string snapshot = JsonUtility.ToJson(saveData);
            long now = timeProvider.CurrentUtc.ToUniversalTime().Ticks;
            bool changed = false;
            var candidates = new List<SharedQuestDefinition>();
            for (int i = 0; i < catalog.Count; i++)
            {
                SharedQuestDefinition quest = catalog[i];
                if (quest == null ||
                    !QuestRuntimeService.QueryAvailability(
                        saveData, quest, timeProvider.CurrentUtc).IsAvailable)
                    continue;

                QuestRuntimeSaveData state = FindState(
                    saveData.world.questRuntimeStates, quest.Id);
                if (state == null ||
                    state.phase == QuestRuntimePhase.Inactive)
                    candidates.Add(quest);
            }
            candidates.Sort((left, right) =>
            {
                long leftActivity = GetLastActivity(
                    saveData.world.questRuntimeStates, left.Id);
                long rightActivity = GetLastActivity(
                    saveData.world.questRuntimeStates, right.Id);
                int activityCompare = leftActivity.CompareTo(rightActivity);
                return activityCompare != 0
                    ? activityCompare
                    : string.CompareOrdinal(left.Id, right.Id);
            });

            for (int i = 0; i < candidates.Count && activeCount < capacity; i++)
            {
                SharedQuestDefinition quest = candidates[i];
                QuestRuntimeSaveData state = FindState(
                    saveData.world.questRuntimeStates, quest.Id);
                if (state == null)
                {
                    state = new QuestRuntimeSaveData { questId = quest.Id };
                    saveData.world.questRuntimeStates.Add(state);
                }
                state.phase = QuestRuntimePhase.Offered;
                state.offeredUtcTicks = now;
                state.acceptedUtcTicks = 0L;
                changed = true;
                activeCount++;
            }
            return PersistOrRollback(saveData, saveService, snapshot, changed);
        }

        public static QuestMutationResult Accept(
            SaveData saveData,
            ISaveService saveService,
            IGameTimeProvider timeProvider,
            string questId)
        {
            if (saveData?.world == null || saveService == null ||
                timeProvider == null || string.IsNullOrWhiteSpace(questId))
                return new QuestMutationResult();

            QuestRuntimeSaveData state =
                FindState(saveData.world.questRuntimeStates, questId);
            if (state == null || state.phase != QuestRuntimePhase.Offered)
                return new QuestMutationResult();

            string snapshot = JsonUtility.ToJson(saveData);
            state.phase = QuestRuntimePhase.Accepted;
            state.acceptedUtcTicks =
                timeProvider.CurrentUtc.ToUniversalTime().Ticks;
            return PersistOrRollback(saveData, saveService, snapshot, true);
        }

        public static QuestRuntimeSaveData FindState(
            List<QuestRuntimeSaveData> states, string questId)
        {
            return states?.Find(value => value != null &&
                string.Equals(value.questId, questId, StringComparison.Ordinal));
        }

        private static int CountActive(
            List<QuestRuntimeSaveData> states,
            IReadOnlyList<SharedQuestDefinition> catalog)
        {
            int count = 0;
            for (int i = 0; i < catalog.Count; i++)
            {
                QuestRuntimeSaveData state =
                    FindState(states, catalog[i]?.Id);
                if (state != null &&
                    (state.phase == QuestRuntimePhase.Offered ||
                     state.phase == QuestRuntimePhase.Accepted))
                    count++;
            }
            return count;
        }

        private static long GetLastActivity(
            List<QuestRuntimeSaveData> states,
            string questId)
        {
            QuestRuntimeSaveData state = FindState(states, questId);
            if (state == null) return 0L;
            return Math.Max(
                state.lastCompletedUtcTicks,
                Math.Max(state.offeredUtcTicks, state.acceptedUtcTicks));
        }

        private static QuestMutationResult PersistOrRollback(
            SaveData saveData,
            ISaveService saveService,
            string snapshot,
            bool changed)
        {
            if (!changed)
                return new QuestMutationResult { Succeeded = true };
            SaveResult saved = saveService.Save(saveData);
            if (saved == null || !saved.Succeeded)
            {
                JsonUtility.FromJsonOverwrite(snapshot, saveData);
                return new QuestMutationResult { SaveResult = saved };
            }
            return new QuestMutationResult
            {
                Succeeded = true,
                Changed = true,
                SaveResult = saved
            };
        }
    }
}
