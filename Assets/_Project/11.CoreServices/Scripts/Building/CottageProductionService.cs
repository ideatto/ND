using System;
using UnityEngine;

namespace ND.Framework
{
    public enum CottageCollectionTarget
    {
        Wagon,
        DraftAnimal,
        All
    }

    public enum CottageCollectionFailureReason
    {
        None,
        ContextUnavailable,
        FarmUnavailable,
        NothingStored,
        InventoryFull,
        SaveFailed,
        InvalidConfiguration
    }

    public sealed class CottageCollectionResult
    {
        public bool Succeeded;
        public int CollectedWagons;
        public int CollectedDraftAnimals;
        public CottageCollectionFailureReason FailureReason;
    }

    public readonly struct CottageProductionRestoreResult
    {
        public CottageProductionRestoreResult(bool changed) { Changed = changed; }
        public bool Changed { get; }
    }

    /// <summary>
    /// UTC 기반 오두막 생산 상태의 유일한 변경 경계다.
    /// UI는 이 서비스를 조회하며 생산 시간을 직접 변경하지 않는다.
    /// </summary>
    public sealed class CottageProductionService
    {
        private readonly CottageProductionData config;
        private readonly IGameTimeProvider time;
        private readonly Func<SaveData> getSaveData;
        private readonly ISaveService saveService;

        public CottageProductionService(
            CottageProductionData config,
            IGameTimeProvider time,
            Func<SaveData> getSaveData,
            ISaveService saveService)
        {
            this.config = config;
            this.time = time;
            this.getSaveData = getSaveData;
            this.saveService = saveService;
        }

        public CottageProductionData Config => config;

        /// <summary>
        /// SO 문자열만으로 생산을 시작하면 잘못된 ID가 수령 시점까지 숨어 있을 수 있다.
        /// SharedGameData 로드 직후 실제 definition 존재 여부까지 검사한다.
        /// </summary>
        public bool ValidateCatalog(ISharedGameDataProvider catalog, out string errorCode)
        {
            errorCode = string.Empty;
            if (catalog == null || !catalog.IsLoaded)
            {
                errorCode = "COTTAGE_CATALOG_UNAVAILABLE";
                return false;
            }
            if (config == null || !config.Validate(out errorCode)) return false;
            if (!catalog.TryGetWagon(config.WagonContentId, out SharedWagonDefinition wagon)
                || wagon == null)
            {
                errorCode = "COTTAGE_WAGON_CONTENT_NOT_FOUND";
                return false;
            }
            if (!catalog.TryGetDraftAnimal(
                    config.DraftAnimalContentId,
                    out SharedDraftAnimalDefinition animal)
                || animal == null)
            {
                errorCode = "COTTAGE_DRAFT_ANIMAL_CONTENT_NOT_FOUND";
                return false;
            }
            return true;
        }

        public CottageProductionRestoreResult RestoreOffline(
            SaveData saveData,
            OfflineRestoreContext context)
        {
            if (context.ClockRollbackDetected)
                return new CottageProductionRestoreResult(false);
            return new CottageProductionRestoreResult(
                Evaluate(saveData, context.EvaluationUtc, migrateLegacy: true));
        }

        public void TickOnline()
        {
            SaveData saveData = getSaveData?.Invoke();
            if (saveData == null || saveService == null || time == null) return;
            string snapshot = JsonUtility.ToJson(saveData);
            if (!Evaluate(saveData, time.CurrentUtc, migrateLegacy: true)) return;
            SaveResult result;
            try
            {
                result = saveService.Save(saveData);
            }
            catch (Exception exception)
            {
                JsonUtility.FromJsonOverwrite(snapshot, saveData);
                FrameworkLog.Error($"Cottage production save threw an exception: {exception}");
                return;
            }
            if (result == null || !result.Succeeded)
            {
                JsonUtility.FromJsonOverwrite(snapshot, saveData);
                FrameworkLog.Warning("Cottage production tick rolled back because save failed.");
                return;
            }
            FrameworkEvents.RaiseCottageProductionChanged();
        }

        public bool TryStageBuildingLevelChange(
            SaveData saveData,
            int previousLevel,
            int targetLevel,
            out string errorCode)
        {
            errorCode = string.Empty;
            if (!TryValidateContext(saveData, targetLevel, out CottageProductionSaveData state,
                    out CottageProductionLevelSetting targetSetting, out errorCode))
                return false;

            DateTime now = time.CurrentUtc;
            if (previousLevel == 0 && targetLevel == 1 && !state.initialSupplyGranted)
            {
                state.initialized = true;
                state.initialSupplyGranted = true;
                state.storedWagonContentId = config.WagonContentId;
                state.storedDraftAnimalContentId = config.DraftAnimalContentId;
                state.storedWagonCount = config.InitialWagonCount;
                state.storedDraftAnimalCount = config.InitialDraftAnimalCount;
                state.nextWagonProductionUtcTicks =
                    state.storedWagonCount >= targetSetting.wagonCapacity
                        ? 0L
                        : now.AddSeconds(config.WagonProductionIntervalSeconds).Ticks;
                state.nextDraftAnimalProductionUtcTicks =
                    state.storedDraftAnimalCount >= targetSetting.draftAnimalCapacity
                        ? 0L
                        : now.AddSeconds(config.DraftAnimalProductionIntervalSeconds).Ticks;
                state.lastEvaluatedUtcTicks = now.Ticks;
                return true;
            }

            if (!state.initialized)
            {
                // 건설된 구버전 저장은 최초 지급을 소급하지 않는다.
                InitializeLegacyState(state, now);
            }

            StartTimerWhenCapacityOpened(
                ref state.nextWagonProductionUtcTicks,
                state.storedWagonCount,
                targetSetting.wagonCapacity,
                now,
                config.WagonProductionIntervalSeconds);
            StartTimerWhenCapacityOpened(
                ref state.nextDraftAnimalProductionUtcTicks,
                state.storedDraftAnimalCount,
                targetSetting.draftAnimalCapacity,
                now,
                config.DraftAnimalProductionIntervalSeconds);
            state.lastEvaluatedUtcTicks = Math.Max(state.lastEvaluatedUtcTicks, now.Ticks);
            return true;
        }

        public CottageCollectionResult Collect(
            CottageCollectionTarget target,
            global::PlayerMainManager player)
        {
            var failed = new CottageCollectionResult
            {
                FailureReason = CottageCollectionFailureReason.ContextUnavailable
            };
            SaveData saveData = getSaveData?.Invoke();
            if (saveData?.player?.cottageProduction == null
                || player == null
                || saveService == null
                || time == null
                || config == null
                || !config.Validate(out _))
                return failed;

            int level = ResolveBuildingLevel(saveData);
            if (!config.TryGetLevel(level, out CottageProductionLevelSetting setting))
                return failed;

            CottageProductionSaveData state = saveData.player.cottageProduction;
            bool wantsWagon = target == CottageCollectionTarget.Wagon
                || target == CottageCollectionTarget.All;
            bool wantsAnimal = target == CottageCollectionTarget.DraftAnimal
                || target == CottageCollectionTarget.All;
            if ((!wantsWagon || state.storedWagonCount <= 0)
                && (!wantsAnimal || state.storedDraftAnimalCount <= 0))
            {
                failed.FailureReason = CottageCollectionFailureReason.NothingStored;
                return failed;
            }

            string snapshot = JsonUtility.ToJson(saveData);
            TransportInventoryValidationFailure lastFailure = TransportInventoryValidationFailure.None;
            var result = new CottageCollectionResult();
            if (wantsWagon)
            {
                while (state.storedWagonCount > 0)
                {
                    if (!player.TryCreateWagon(
                            state.storedWagonContentId,
                            out _,
                            out lastFailure,
                            publishChange: false))
                        break;
                    state.storedWagonCount--;
                    result.CollectedWagons++;
                }
            }
            if (wantsAnimal)
            {
                while (state.storedDraftAnimalCount > 0)
                {
                    if (!player.TryCreateDraftAnimal(
                            state.storedDraftAnimalContentId,
                            out _,
                            out lastFailure,
                            publishChange: false))
                        break;
                    state.storedDraftAnimalCount--;
                    result.CollectedDraftAnimals++;
                }
            }

            if (result.CollectedWagons + result.CollectedDraftAnimals == 0)
            {
                result.FailureReason = MapCollectionFailure(lastFailure);
                return result;
            }

            DateTime now = time.CurrentUtc;
            StartTimerWhenCapacityOpened(
                ref state.nextWagonProductionUtcTicks,
                state.storedWagonCount,
                setting.wagonCapacity,
                now,
                config.WagonProductionIntervalSeconds);
            StartTimerWhenCapacityOpened(
                ref state.nextDraftAnimalProductionUtcTicks,
                state.storedDraftAnimalCount,
                setting.draftAnimalCapacity,
                now,
                config.DraftAnimalProductionIntervalSeconds);
            state.lastEvaluatedUtcTicks = Math.Max(state.lastEvaluatedUtcTicks, now.Ticks);

            SaveResult saveResult;
            try
            {
                saveResult = saveService.Save(saveData);
            }
            catch (Exception exception)
            {
                JsonUtility.FromJsonOverwrite(snapshot, saveData);
                player.SynchronizeTransportInventoryFromSave();
                FrameworkLog.Error($"Cottage collection save threw an exception: {exception}");
                result.Succeeded = false;
                result.CollectedWagons = 0;
                result.CollectedDraftAnimals = 0;
                result.FailureReason = CottageCollectionFailureReason.SaveFailed;
                return result;
            }
            if (saveResult == null || !saveResult.Succeeded)
            {
                JsonUtility.FromJsonOverwrite(snapshot, saveData);
                player.SynchronizeTransportInventoryFromSave();
                result.Succeeded = false;
                result.CollectedWagons = 0;
                result.CollectedDraftAnimals = 0;
                result.FailureReason = CottageCollectionFailureReason.SaveFailed;
                return result;
            }

            result.Succeeded = true;
            result.FailureReason = CottageCollectionFailureReason.None;
            // 수령 도중에는 이벤트를 억제하고 영속 저장이 확정된 뒤 한 번만 공개한다.
            FrameworkEvents.RaiseTransportInventoryChanged();
            FrameworkEvents.RaiseCottageProductionChanged();
            return result;
        }

        private bool Evaluate(SaveData saveData, DateTime evaluationUtc, bool migrateLegacy)
        {
            if (saveData?.player == null || config == null || !config.Validate(out _))
                return false;
            if (saveData.player.cottageProduction == null)
                saveData.player.cottageProduction = new CottageProductionSaveData();

            CottageProductionSaveData state = saveData.player.cottageProduction;
            int level = ResolveBuildingLevel(saveData);
            if (level <= 0)
                return ClearForLevelZero(state);
            if (!config.TryGetLevel(level, out CottageProductionLevelSetting setting))
                return false;
            if (state.lastEvaluatedUtcTicks > 0
                && evaluationUtc.Ticks < state.lastEvaluatedUtcTicks)
                return false;

            bool changed = false;
            if (!state.initialized && migrateLegacy)
            {
                InitializeLegacyState(state, evaluationUtc);
                changed = true;
            }

            changed |= EvaluateChannel(
                ref state.storedWagonCount,
                ref state.storedWagonContentId,
                ref state.nextWagonProductionUtcTicks,
                setting.wagonCapacity,
                config.WagonContentId,
                config.WagonProductionIntervalSeconds,
                evaluationUtc);
            changed |= EvaluateChannel(
                ref state.storedDraftAnimalCount,
                ref state.storedDraftAnimalContentId,
                ref state.nextDraftAnimalProductionUtcTicks,
                setting.draftAnimalCapacity,
                config.DraftAnimalContentId,
                config.DraftAnimalProductionIntervalSeconds,
                evaluationUtc);
            if (changed)
                state.lastEvaluatedUtcTicks = evaluationUtc.Ticks;
            return changed;
        }

        private bool TryValidateContext(
            SaveData saveData,
            int targetLevel,
            out CottageProductionSaveData state,
            out CottageProductionLevelSetting setting,
            out string error)
        {
            state = null;
            setting = null;
            error = string.Empty;
            if (saveData?.player == null || config == null || time == null
                || !config.Validate(out error)
                || !config.TryGetLevel(targetLevel, out setting))
            {
                if (string.IsNullOrEmpty(error)) error = "COTTAGE_LEVEL_INVALID";
                return false;
            }
            if (saveData.player.cottageProduction == null)
                saveData.player.cottageProduction = new CottageProductionSaveData();
            state = saveData.player.cottageProduction;
            return true;
        }

        private int ResolveBuildingLevel(SaveData saveData)
        {
            if (saveData?.player?.villageBuildings == null || config == null) return 0;
            int level = 0;
            foreach (VillageBuildingSaveData building in saveData.player.villageBuildings)
            {
                if (building != null && string.Equals(
                        building.displayName,
                        config.BuildingDisplayName,
                        StringComparison.Ordinal))
                    level = Math.Max(level, building.level);
            }
            return level;
        }

        private void InitializeLegacyState(CottageProductionSaveData state, DateTime now)
        {
            state.initialized = true;
            state.initialSupplyGranted = true;
            state.storedWagonCount = 0;
            state.storedDraftAnimalCount = 0;
            state.storedWagonContentId = config.WagonContentId;
            state.storedDraftAnimalContentId = config.DraftAnimalContentId;
            state.nextWagonProductionUtcTicks =
                now.AddSeconds(config.WagonProductionIntervalSeconds).Ticks;
            state.nextDraftAnimalProductionUtcTicks =
                now.AddSeconds(config.DraftAnimalProductionIntervalSeconds).Ticks;
            state.lastEvaluatedUtcTicks = now.Ticks;
        }

        private static bool EvaluateChannel(
            ref int count,
            ref string storedContentId,
            ref long nextTicks,
            int capacity,
            string configuredContentId,
            int intervalSeconds,
            DateTime now)
        {
            int normalizedCount = Math.Max(0, Math.Min(count, capacity));
            bool changed = normalizedCount != count;
            count = normalizedCount;
            if (string.IsNullOrWhiteSpace(storedContentId))
            {
                storedContentId = configuredContentId;
                changed = true;
            }
            if (count >= capacity)
            {
                if (nextTicks != 0L) { nextTicks = 0L; changed = true; }
                return changed;
            }
            if (nextTicks <= 0L)
            {
                nextTicks = now.AddSeconds(intervalSeconds).Ticks;
                return true;
            }
            if (now.Ticks < nextTicks) return changed;

            long intervalTicks = TimeSpan.FromSeconds(intervalSeconds).Ticks;
            long due = 1L + ((now.Ticks - nextTicks) / intervalTicks);
            int produced = (int)Math.Min((long)(capacity - count), due);
            count += produced;
            if (count >= capacity)
                nextTicks = 0L;
            else
                nextTicks += produced * intervalTicks;
            return produced > 0 || changed;
        }

        private static void StartTimerWhenCapacityOpened(
            ref long nextTicks,
            int count,
            int capacity,
            DateTime now,
            int intervalSeconds)
        {
            if (count < capacity && nextTicks <= 0L)
                nextTicks = now.AddSeconds(intervalSeconds).Ticks;
        }

        private static bool ClearForLevelZero(CottageProductionSaveData state)
        {
            if (state == null) return false;
            bool changed = state.initialized
                || state.storedWagonCount != 0
                || state.storedDraftAnimalCount != 0
                || state.nextWagonProductionUtcTicks != 0L
                || state.nextDraftAnimalProductionUtcTicks != 0L
                || state.lastEvaluatedUtcTicks != 0L
                || !string.IsNullOrEmpty(state.storedWagonContentId)
                || !string.IsNullOrEmpty(state.storedDraftAnimalContentId);
            state.initialized = false;
            state.storedWagonCount = 0;
            state.storedDraftAnimalCount = 0;
            state.storedWagonContentId = string.Empty;
            state.storedDraftAnimalContentId = string.Empty;
            state.nextWagonProductionUtcTicks = 0L;
            state.nextDraftAnimalProductionUtcTicks = 0L;
            state.lastEvaluatedUtcTicks = 0L;
            return changed;
        }

        private static CottageCollectionFailureReason MapCollectionFailure(
            TransportInventoryValidationFailure failure)
        {
            switch (failure)
            {
                case TransportInventoryValidationFailure.FarmUnavailable:
                    return CottageCollectionFailureReason.FarmUnavailable;
                case TransportInventoryValidationFailure.ContentUnavailable:
                case TransportInventoryValidationFailure.InvalidIdentity:
                    return CottageCollectionFailureReason.InvalidConfiguration;
                default:
                    return CottageCollectionFailureReason.InventoryFull;
            }
        }
    }
}
