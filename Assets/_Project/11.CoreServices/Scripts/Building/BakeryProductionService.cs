using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Framework
{
    public enum BakeryCollectionFailureReason
    {
        None, ContextUnavailable, WarehouseUnavailable, NothingStored, WarehouseFull,
        InvalidConfiguration, SaveFailed
    }

    public readonly struct BakeryProductionRestoreResult
    {
        public BakeryProductionRestoreResult(bool changed) { Changed = changed; }
        public bool Changed { get; }
    }

    public sealed class BakeryCollectionResult
    {
        public bool Succeeded;
        public int CollectedCount;
        public BakeryCollectionFailureReason FailureReason;
    }

    /// <summary>
    /// UTC 기준 빵 생산과 창고 수령을 담당하는 단일 변경 경계다.
    /// 저장 전 Snapshot을 보관하여 실패 시 생산 상태와 창고 인벤토리를 함께 복원한다.
    /// </summary>
    public sealed class BakeryProductionService
    {
        private readonly BakeryProductionData config;
        private readonly IGameTimeProvider time;
        private readonly Func<SaveData> getSaveData;
        private readonly ISaveService saveService;
        private readonly Func<ISharedGameDataProvider> getCatalog;

        public BakeryProductionService(BakeryProductionData config, IGameTimeProvider time,
            Func<SaveData> getSaveData, ISaveService saveService,
            Func<ISharedGameDataProvider> getCatalog)
        {
            this.config = config;
            this.time = time;
            this.getSaveData = getSaveData;
            this.saveService = saveService;
            this.getCatalog = getCatalog;
        }

        public BakeryProductionData Config => config;

        public bool ValidateCatalog(ISharedGameDataProvider catalog, out string error)
        {
            error = string.Empty;
            if (catalog == null || !catalog.IsLoaded) { error = "BAKERY_CATALOG_UNAVAILABLE"; return false; }
            if (config == null || !config.Validate(out error)) return false;
            if (!catalog.TryGetTradeItem(config.BreadContentId, out SharedTradeItemDefinition item)
                || item == null || !item.CanStack || item.MaxCount < 1)
            { error = "BAKERY_BREAD_CONTENT_INVALID"; return false; }
            return true;
        }

        public void TickOnline()
        {
            SaveData data = getSaveData?.Invoke();
            if (data == null || saveService == null || time == null) return;
            string snapshot = JsonUtility.ToJson(data);
            if (!Evaluate(data, time.CurrentUtc, true)) return;
            if (!TrySave(data)) { JsonUtility.FromJsonOverwrite(snapshot, data); return; }
            FrameworkEvents.RaiseBakeryProductionChanged();
        }

        public BakeryProductionRestoreResult RestoreOffline(SaveData data, OfflineRestoreContext context)
        {
            if (context.ClockRollbackDetected) return new BakeryProductionRestoreResult(false);
            return new BakeryProductionRestoreResult(Evaluate(data, context.EvaluationUtc, true));
        }

        public bool TryStageBuildingLevelChange(SaveData data, int targetLevel, out string error)
        {
            error = string.Empty;
            if (data?.player == null || time == null || config == null
                || !config.Validate(out error) || !config.TryGetLevel(targetLevel, out _))
                return false;
            data.player.bakeryProduction ??= new BakeryProductionSaveData();
            BakeryProductionSaveData state = data.player.bakeryProduction;
            if (!state.initialized)
            {
                state.initialized = true;
                state.storedBreadCount = 0;
                state.storedBreadContentId = config.BreadContentId;
                state.nextProductionUtcTicks = time.CurrentUtc.AddSeconds(config.ProductionIntervalSeconds).Ticks;
            }
            state.lastEvaluatedUtcTicks = Math.Max(state.lastEvaluatedUtcTicks, time.CurrentUtc.Ticks);
            return true;
        }

        public BakeryCollectionResult Collect() => Collect(int.MaxValue);

        public int GetReceivableCount()
        {
            SaveData data = getSaveData?.Invoke();
            ISharedGameDataProvider catalog = getCatalog?.Invoke();
            if (data?.player?.bakeryProduction == null
                || !ValidateCatalog(catalog, out _)) return 0;
            WarehouseState warehouse = WarehouseFunction.Evaluate(data);
            if (!warehouse.Exists) return 0;
            catalog.TryGetTradeItem(config.BreadContentId, out SharedTradeItemDefinition definition);
            data.player.homeInventory ??= new List<CargoEntrySaveData>();
            return CalculateReceivable(data.player.homeInventory, warehouse.SlotCount,
                config.BreadContentId, definition.MaxCount,
                data.player.bakeryProduction.storedBreadCount);
        }

        public BakeryCollectionResult Collect(int requestedQuantity)
        {
            var failed = new BakeryCollectionResult { FailureReason = BakeryCollectionFailureReason.ContextUnavailable };
            SaveData data = getSaveData?.Invoke();
            ISharedGameDataProvider catalog = getCatalog?.Invoke();
            if (data?.player?.bakeryProduction == null || saveService == null || time == null
                || !ValidateCatalog(catalog, out _)) return failed;
            WarehouseState warehouse = WarehouseFunction.Evaluate(data);
            if (!warehouse.Exists) { failed.FailureReason = BakeryCollectionFailureReason.WarehouseUnavailable; return failed; }
            BakeryProductionSaveData state = data.player.bakeryProduction;
            if (state.storedBreadCount <= 0) { failed.FailureReason = BakeryCollectionFailureReason.NothingStored; return failed; }
            catalog.TryGetTradeItem(config.BreadContentId, out SharedTradeItemDefinition definition);
            data.player.homeInventory ??= new List<CargoEntrySaveData>();
            int requested = Math.Min(state.storedBreadCount, Math.Max(0, requestedQuantity));
            int receivable = CalculateReceivable(data.player.homeInventory, warehouse.SlotCount,
                config.BreadContentId, definition.MaxCount, requested);
            if (receivable <= 0) { failed.FailureReason = BakeryCollectionFailureReason.WarehouseFull; return failed; }

            string snapshot = JsonUtility.ToJson(data);
            AddBread(data.player.homeInventory, definition, receivable);
            state.storedBreadCount -= receivable;
            int level = ResolveBuildingLevel(data);
            if (config.TryGetLevel(level, out BakeryProductionLevelSetting setting)
                && state.storedBreadCount < setting.storageCapacity && state.nextProductionUtcTicks <= 0L)
                state.nextProductionUtcTicks = time.CurrentUtc.AddSeconds(config.ProductionIntervalSeconds).Ticks;
            state.lastEvaluatedUtcTicks = Math.Max(state.lastEvaluatedUtcTicks, time.CurrentUtc.Ticks);
            if (!TrySave(data))
            {
                JsonUtility.FromJsonOverwrite(snapshot, data);
                failed.FailureReason = BakeryCollectionFailureReason.SaveFailed;
                return failed;
            }
            FrameworkEvents.RaiseHomeInventoryChanged();
            FrameworkEvents.RaiseBakeryProductionChanged();
            return new BakeryCollectionResult { Succeeded = true, CollectedCount = receivable };
        }

        private bool Evaluate(SaveData data, DateTime now, bool initialize)
        {
            if (data?.player == null || config == null || !config.Validate(out _)) return false;
            data.player.bakeryProduction ??= new BakeryProductionSaveData();
            BakeryProductionSaveData state = data.player.bakeryProduction;
            int level = ResolveBuildingLevel(data);
            if (level <= 0) return Clear(state);
            if (!config.TryGetLevel(level, out BakeryProductionLevelSetting setting)) return false;
            if (state.lastEvaluatedUtcTicks > 0L && now.Ticks < state.lastEvaluatedUtcTicks) return false;
            bool changed = false;
            if (!state.initialized && initialize)
            {
                state.initialized = true;
                state.storedBreadContentId = config.BreadContentId;
                state.nextProductionUtcTicks = now.AddSeconds(config.ProductionIntervalSeconds).Ticks;
                changed = true;
            }
            state.storedBreadCount = Math.Max(0, Math.Min(state.storedBreadCount, setting.storageCapacity));
            if (state.storedBreadCount >= setting.storageCapacity)
            {
                if (state.nextProductionUtcTicks != 0L) { state.nextProductionUtcTicks = 0L; changed = true; }
            }
            else if (state.nextProductionUtcTicks <= 0L)
            {
                state.nextProductionUtcTicks = now.AddSeconds(config.ProductionIntervalSeconds).Ticks;
                changed = true;
            }
            else if (now.Ticks >= state.nextProductionUtcTicks)
            {
                long interval = TimeSpan.FromSeconds(config.ProductionIntervalSeconds).Ticks;
                long due = 1L + ((now.Ticks - state.nextProductionUtcTicks) / interval);
                int produced = (int)Math.Min(setting.storageCapacity - state.storedBreadCount, due);
                state.storedBreadCount += produced;
                state.nextProductionUtcTicks = state.storedBreadCount >= setting.storageCapacity
                    ? 0L : state.nextProductionUtcTicks + produced * interval;
                changed |= produced > 0;
            }
            if (changed) state.lastEvaluatedUtcTicks = now.Ticks;
            return changed;
        }

        private int ResolveBuildingLevel(SaveData data)
        {
            int level = 0;
            if (data?.player?.villageBuildings == null) return level;
            foreach (VillageBuildingSaveData building in data.player.villageBuildings)
                if (building != null && string.Equals(building.displayName, config.BuildingDisplayName, StringComparison.Ordinal))
                    level = Math.Max(level, building.level);
            return level;
        }

        private static int CalculateReceivable(List<CargoEntrySaveData> entries, int slots,
            string itemId, int maxCount, int requested)
        {
            int used = 0, room = 0;
            foreach (CargoEntrySaveData entry in entries)
            {
                if (entry?.item == null || entry.quantity <= 0) continue;
                used++;
                if (entry.item.itemId == itemId && entry.item.purchaseUnitPrice == 0L)
                    room += Math.Max(0, maxCount - entry.quantity);
            }
            room += Math.Max(0, slots - used) * maxCount;
            return Math.Min(requested, room);
        }

        private static void AddBread(List<CargoEntrySaveData> entries, SharedTradeItemDefinition item, int quantity)
        {
            foreach (CargoEntrySaveData entry in entries)
            {
                if (quantity <= 0) break;
                if (entry?.item == null || entry.item.itemId != item.Id || entry.item.purchaseUnitPrice != 0L) continue;
                int added = Math.Min(quantity, Math.Max(0, item.MaxCount - entry.quantity));
                entry.quantity += added; quantity -= added;
            }
            while (quantity > 0)
            {
                int added = Math.Min(quantity, item.MaxCount);
                entries.Add(new CargoEntrySaveData { quantity = added, item = new TradeItemSaveData {
                    itemId = item.Id, itemName = item.DisplayName, weight = item.Weight,
                    purchaseUnitPrice = 0L, basePrice = item.BaseBuyPrice, maxCount = item.MaxCount } });
                quantity -= added;
            }
        }

        private bool TrySave(SaveData data)
        {
            try { SaveResult result = saveService.Save(data); return result != null && result.Succeeded; }
            catch (Exception exception) { FrameworkLog.Error($"Bakery production save failed: {exception}"); return false; }
        }

        private static bool Clear(BakeryProductionSaveData state)
        {
            bool changed = state.initialized || state.storedBreadCount != 0 || state.nextProductionUtcTicks != 0L;
            state.initialized = false; state.storedBreadCount = 0; state.storedBreadContentId = string.Empty;
            state.nextProductionUtcTicks = 0L; state.lastEvaluatedUtcTicks = 0L;
            return changed;
        }
    }
}
