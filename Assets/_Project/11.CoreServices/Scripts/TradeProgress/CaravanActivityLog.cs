using System;
using System.Collections.Generic;

namespace ND.Framework
{
    /// <summary>
    /// 기존 진행 상태와 별개로 UI에 필요한 Caravan 활동 이력을 제한된 크기로 보관한다.
    /// </summary>
    public static class CaravanActivityLog
    {
        public const int DefaultMaxEntries = 100;

        public static CaravanActivityLogEntrySaveData Add(
            SaveData saveData,
            CaravanActivityLogType eventType,
            string caravanId,
            string tradeId = null,
            string routeId = null,
            string townId = null,
            string routeEventId = null,
            long occurredUtcTicks = 0L)
        {
            if (saveData == null || string.IsNullOrWhiteSpace(caravanId))
            {
                return null;
            }

            if (saveData.caravanActivityLogs == null)
            {
                saveData.caravanActivityLogs = new List<CaravanActivityLogEntrySaveData>();
            }

            var entry = new CaravanActivityLogEntrySaveData
            {
                sequence = NextSequence(saveData.caravanActivityLogs),
                occurredUtcTicks = occurredUtcTicks > 0L ? occurredUtcTicks : DateTime.UtcNow.Ticks,
                caravanId = caravanId.Trim(),
                tradeId = tradeId?.Trim() ?? string.Empty,
                routeId = routeId?.Trim() ?? string.Empty,
                townId = townId?.Trim() ?? string.Empty,
                routeEventId = routeEventId?.Trim() ?? string.Empty,
                eventType = eventType
            };
            saveData.caravanActivityLogs.Add(entry);
            TrimToLimit(saveData);
            return entry;
        }

        public static bool Remove(SaveData saveData, CaravanActivityLogEntrySaveData entry)
        {
            return saveData?.caravanActivityLogs != null
                && entry != null
                && saveData.caravanActivityLogs.Remove(entry);
        }

        public static bool TryAddAndSave(
            SaveData saveData,
            ISaveService saveService,
            CaravanActivityLogType eventType,
            string caravanId,
            string townId = null)
        {
            if (saveData == null || saveService == null)
            {
                return false;
            }

            var previousEntries = saveData.caravanActivityLogs != null
                ? new List<CaravanActivityLogEntrySaveData>(saveData.caravanActivityLogs)
                : null;
            var entry = Add(saveData, eventType, caravanId, townId: townId);
            if (entry == null)
            {
                return false;
            }

            SaveResult result = saveService.Save(saveData);
            if (result != null && result.Succeeded)
            {
                return true;
            }

            saveData.caravanActivityLogs = previousEntries
                ?? new List<CaravanActivityLogEntrySaveData>();
            return false;
        }

        public static void TrimToLimit(SaveData saveData, int maxEntries = DefaultMaxEntries)
        {
            if (saveData?.caravanActivityLogs == null)
            {
                return;
            }

            maxEntries = Math.Max(1, maxEntries);
            saveData.caravanActivityLogs.RemoveAll(entry => entry == null);
            if (saveData.caravanActivityLogs.Count <= maxEntries)
            {
                return;
            }

            saveData.caravanActivityLogs.Sort(Compare);
            saveData.caravanActivityLogs.RemoveRange(
                0,
                saveData.caravanActivityLogs.Count - maxEntries);
        }

        private static long NextSequence(List<CaravanActivityLogEntrySaveData> entries)
        {
            long greatest = 0L;
            for (var index = 0; index < entries.Count; index++)
            {
                if (entries[index] != null && entries[index].sequence > greatest)
                {
                    greatest = entries[index].sequence;
                }
            }
            return greatest == long.MaxValue ? long.MaxValue : greatest + 1L;
        }

        private static int Compare(
            CaravanActivityLogEntrySaveData left,
            CaravanActivityLogEntrySaveData right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            var sequenceComparison = left.sequence.CompareTo(right.sequence);
            return sequenceComparison != 0
                ? sequenceComparison
                : left.occurredUtcTicks.CompareTo(right.occurredUtcTicks);
        }
    }
}
