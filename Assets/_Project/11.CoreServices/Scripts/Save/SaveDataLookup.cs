using System;
using System.Collections.Generic;

namespace ND.Framework
{
    /// <summary>
    /// ID 기반 caravan 저장 데이터 조회와 기존 선택-caravan 호환 쓰기를 중앙화한다.
    /// </summary>
    public static class SaveDataLookup
    {
        public static bool TryGetCaravan(SaveData data, string caravanId, out CaravanSaveData caravan)
        {
            caravan = null;
            if (data == null || data.caravans == null || string.IsNullOrEmpty(caravanId)) return false;
            for (var i = 0; i < data.caravans.Count; i++)
            {
                var candidate = data.caravans[i];
                if (candidate == null || candidate.caravanId != caravanId) continue;
                if (caravan != null)
                {
                    FrameworkLog.Error($"Duplicate caravan ID prevented lookup: {caravanId}");
                    caravan = null;
                    return false;
                }
                caravan = candidate;
            }
            return caravan != null;
        }

        public static bool TryGetSelectedCaravan(SaveData data, out CaravanSaveData caravan)
        {
            caravan = null;
            return data != null && TryGetCaravan(data, data.selectedCaravanId, out caravan);
        }

        public static bool TryGetTradeProgress(SaveData data, string caravanId, out TradeProgressSaveData progress)
        {
            progress = null;
            if (!HasCaravan(data, caravanId) || data.tradeProgressEntries == null) return false;
            for (var i = 0; i < data.tradeProgressEntries.Count; i++)
            {
                var candidate = data.tradeProgressEntries[i];
                if (candidate == null || candidate.caravanId != caravanId) continue;
                if (progress != null)
                {
                    FrameworkLog.Error($"Duplicate trade progress prevented lookup. CaravanId: {caravanId}");
                    progress = null;
                    return false;
                }
                progress = candidate;
            }
            return progress != null;
        }

        public static bool TryGetPendingSettlement(SaveData data, string caravanId, string tradeId, out PendingSettlementSaveData settlement)
        {
            settlement = null;
            if (!HasCaravan(data, caravanId) || data.pendingSettlements == null) return false;
            for (var i = 0; i < data.pendingSettlements.Count; i++)
            {
                var candidate = data.pendingSettlements[i];
                if (candidate == null || candidate.caravanId != caravanId
                    || (!string.IsNullOrEmpty(tradeId) && candidate.tradeId != tradeId)) continue;
                if (settlement != null)
                {
                    FrameworkLog.Error($"Duplicate pending settlement prevented lookup. CaravanId: {caravanId}, TradeId: {tradeId}");
                    settlement = null;
                    return false;
                }
                settlement = candidate;
            }
            return settlement != null;
        }

        public static bool TrySetSelectedCaravan(SaveData data, string caravanId)
        {
            CaravanSaveData ignored;
            if (!TryGetCaravan(data, caravanId, out ignored)) return false;
            data.selectedCaravanId = caravanId;
            return true;
        }

        internal static void SetSelectedCaravan(SaveData data, CaravanSaveData caravan)
        {
            if (data == null || caravan == null) return;
            EnsureLists(data);
            if (string.IsNullOrEmpty(caravan.caravanId)) caravan.caravanId = NewCaravanId();
            CaravanSaveData current;
            if (TryGetSelectedCaravan(data, out current)) data.caravans[data.caravans.IndexOf(current)] = caravan;
            else data.caravans.Add(caravan);
            data.selectedCaravanId = caravan.caravanId;
        }

        internal static void SetTradeProgress(SaveData data, string caravanId, TradeProgressSaveData progress)
        {
            if (data == null || string.IsNullOrEmpty(caravanId)) return;
            EnsureLists(data);
            RemoveOwned(data.tradeProgressEntries, caravanId);
            if (progress != null)
            {
                progress.caravanId = caravanId;
                data.tradeProgressEntries.Add(progress);
            }
        }

        internal static void SetPendingSettlement(SaveData data, string caravanId, PendingSettlementSaveData settlement)
        {
            if (data == null || string.IsNullOrEmpty(caravanId)) return;
            EnsureLists(data);
            RemoveOwned(data.pendingSettlements, caravanId);
            if (settlement != null && settlement.hasResult)
            {
                settlement.caravanId = caravanId;
                data.pendingSettlements.Add(settlement);
            }
        }

        internal static string NewCaravanId() => NewPersistentGuid();

        /// <summary>
        /// 플레이어가 보유한 개별 자산을 식별할 영속 GUID를 생성한다.
        /// </summary>
        /// <returns>하이픈이 없는 32자 GUID 문자열.</returns>
        public static string NewInstanceId() => NewPersistentGuid();

        private static string NewPersistentGuid() => Guid.NewGuid().ToString("N");

        private static bool HasCaravan(SaveData data, string caravanId)
        {
            CaravanSaveData ignored;
            return TryGetCaravan(data, caravanId, out ignored);
        }

        private static void EnsureLists(SaveData data)
        {
            if (data.caravans == null) data.caravans = new List<CaravanSaveData>();
            if (data.tradeProgressEntries == null) data.tradeProgressEntries = new List<TradeProgressSaveData>();
            if (data.pendingSettlements == null) data.pendingSettlements = new List<PendingSettlementSaveData>();
        }

        private static void RemoveOwned<T>(List<T> entries, string caravanId) where T : class
        {
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var progress = entries[i] as TradeProgressSaveData;
                var pending = entries[i] as PendingSettlementSaveData;
                if ((progress != null && progress.caravanId == caravanId)
                    || (pending != null && pending.caravanId == caravanId)) entries.RemoveAt(i);
            }
        }
    }
}
