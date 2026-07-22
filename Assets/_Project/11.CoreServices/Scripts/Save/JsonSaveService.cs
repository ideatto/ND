/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - SaveData를 Unity persistentDataPath의 JSON 파일로 저장하고 로드한다.
 * - 저장 데이터가 없거나 유효하지 않은 경우 새 게임 데이터로 복구한다.
 *
 * Main Features
 * - Load normalizes persistent asset instance IDs and writes changed data once.
 * - 저장 파일 존재 확인, 새 저장 데이터 생성, JSON 로드/저장, 저장 파일 삭제를 제공한다.
 * - 로드/저장 전 하위 DTO의 null 값을 정규화한다.
 * - Save(...)는 정규화, 직렬화, 파일 쓰기 단계별 SaveResult를 반환한다.
 *
 * Usage for Team Members
 * - FrameworkRoot.SaveService를 통해 사용한다.
 * - 저장 데이터 변경 후 영속화가 필요하면 Save(...)를 호출한다.
 * - 중요 저장 흐름은 SaveResult.Succeeded를 검사해 scene 전환·성공 UI·이벤트 발행을 제어한다.
 *
 * Main Public APIs
 * - HasSaveData(): save_data.json 존재 여부를 확인한다.
 * - CreateNewGameData(): 현재 version의 기본 SaveData를 생성한다.
 * - Load(): 저장 파일을 읽고 유효하지 않으면 새 SaveData를 반환한다.
 * - Save(...): SaveData를 JSON으로 기록하고 SaveResult를 반환한다.
 * - ResetSaveData(): 저장 파일을 삭제한다.
 *
 * Important Notes
 * - Wagon, Animal, and Mercenary instance IDs share one global uniqueness set.
 * - 저장 파일 이름은 save_data.json으로 고정되어 있다.
 * - version 5 단일 caravan 저장은 version 6 ID 기반 컬렉션 구조로 마이그레이션한다.
 * - version 5 세이브에 상점 재고·구매 준비 필드가 없어도 NormalizeData가 빈 컨테이너로 보정한다.
 * - version 5 세이브에 거점 창고·마을 건물 필드가 없어도 NormalizeData가 빈 리스트로 보정한다.
 * - version 5 세이브에 구조 대출 필드가 없어도 NormalizeData가 안전한 비활성 상태로 보정한다.
 * - Save(...)는 null 입력·직렬화·쓰기 실패를 SaveResult로 반환하고 예외를 전파하지 않는다.
 * - lastSavedUtcTicks는 UTC 기준 DateTime.UtcNow.Ticks 값이다.
 *
 * Related Documentation
 * - Docs/Personal_Documents/CSU/SaveDataPolicy/Save_Result_API_Implementation_Logic.md
 */
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// SaveData를 JSON 파일로 영속화하는 ISaveService 구현체이다.
    /// </summary>
    public sealed class JsonSaveService : ISaveService
    {
        private const string FileName = "save_data.json";

        private readonly string savePath;

        /// <summary>
        /// 기본 저장 경로를 계산해 JSON 저장 서비스를 생성한다.
        /// </summary>
        public JsonSaveService()
        {
            savePath = Path.Combine(Application.persistentDataPath, FileName);
        }

        /// <summary>
        /// 저장 파일이 현재 저장 경로에 존재하는지 확인한다.
        /// </summary>
        /// <returns>파일이 존재하면 true, 없으면 false.</returns>
        public bool HasSaveData()
        {
            return File.Exists(savePath);
        }

        /// <summary>
        /// 현재 저장 schema version을 가진 새 게임 데이터를 생성한다.
        /// </summary>
        /// <returns>기본값과 생성 시각이 반영된 SaveData.</returns>
        public SaveData CreateNewGameData()
        {
            var data = new SaveData
            {
                version = SaveData.CurrentVersion,
                lastSavedUtcTicks = DateTime.UtcNow.Ticks
            };

            // New games begin at the default town used by the initial trade routes.
            data.player.currentTownId = "BaseCamp";

            NormalizeData(data);

            FrameworkLog.Info("New game save data created.");
            return data;
        }

        /// <summary>
        /// JSON 저장 파일에서 SaveData를 로드한다.
        /// </summary>
        /// <returns>유효한 저장 데이터 또는 복구용 새 게임 데이터.</returns>
        /// <remarks>
        /// 파일 없음과 역직렬화 실패는 새 게임 데이터로 복구하고 version 5는 version 6으로 마이그레이션한다.
        /// </remarks>
        public SaveData Load()
        {
            // 저장 파일이 없으면 호출자가 null을 처리하지 않아도 되도록 기본 데이터를 반환한다.
            if (!HasSaveData())
            {
                FrameworkLog.Warning("Save file was not found. Creating new game data.");
                return CreateNewGameData();
            }

            try
            {
                // JsonUtility 역직렬화 결과는 하위 DTO가 null일 수 있으므로 version 확인 후 정규화한다.
                var json = File.ReadAllText(savePath);
                var data = JsonUtility.FromJson<SaveData>(json);

                if (data != null && data.version == 5)
                {
                    data = MigrateVersion5(json);
                    if (data != null)
                    {
                        var migrationResult = Save(data);
                        if (!migrationResult.Succeeded)
                        {
                            FrameworkLog.Error($"Version 5 save was migrated in memory but could not be written: {migrationResult.Message}");
                        }
                    }
                }

                if (data == null || data.version != SaveData.CurrentVersion)
                {
                    FrameworkLog.Warning("Save data is invalid or has an unsupported version. Creating new game data.");
                    return CreateNewGameData();
                }

                var normalized = NormalizeData(data);
                if (normalized)
                {
                    var normalizationSaveResult = Save(data);
                    if (!normalizationSaveResult.Succeeded)
                    {
                        FrameworkLog.Error($"Normalized save data could not be written: {normalizationSaveResult.Message}");
                    }
                }
                FrameworkLog.Info($"Save data loaded. Version: {data.version}");
                return data;
            }
            catch (Exception exception)
            {
                // 로드 실패가 game flow를 중단하지 않도록 새 데이터로 복구하고 원인을 로그로 남긴다.
                FrameworkLog.Error($"Failed to load save data: {exception.Message}");
                return CreateNewGameData();
            }
        }

        /// <summary>
        /// 전달된 SaveData를 JSON 파일로 저장한다.
        /// </summary>
        /// <param name="data">저장할 SaveData. null이면 InvalidData 실패 결과를 반환한다.</param>
        /// <returns>
        /// 파일 쓰기까지 완료되면 Success.
        /// null 입력은 InvalidData, 직렬화 실패는 SerializationFailed, 쓰기 실패는 WriteFailed,
        /// 정규화·메타데이터 갱신 실패는 Unknown.
        /// </returns>
        /// <remarks>
        /// version과 lastSavedUtcTicks는 직렬화 전에 현재 값으로 갱신된다.
        /// 실패 시 예외를 전파하지 않고 로그와 SaveResult로 보고한다.
        /// </remarks>
        public SaveResult Save(SaveData data)
        {
            // null 저장은 기존 저장 파일을 덮어쓰면 복구가 어려우므로 무시한다.
            if (data == null)
            {
                FrameworkLog.Warning("Save was skipped because data is null.");
                return SaveResult.Failure(
                    SaveFailureReason.InvalidData,
                    "Save data is null.",
                    nameof(SaveData));
            }

            try
            {
                // 저장 직전 DTO 그래프를 정규화해 JsonUtility가 누락된 하위 데이터를 그대로 기록하지 않게 한다.
                NormalizeData(data);
                data.version = SaveData.CurrentVersion;
                data.lastSavedUtcTicks = DateTime.UtcNow.Ticks;
            }
            catch (Exception exception)
            {
                // 정규화 또는 저장 메타데이터 갱신 중 발생한 예상 밖 실패를 호출자에게 분류해 반환한다.
                FrameworkLog.Error($"Failed to save data: {exception.Message}");
                return SaveResult.Failure(
                    SaveFailureReason.Unknown,
                    exception.Message,
                    nameof(SaveData));
            }

            string json;
            try
            {
                json = JsonUtility.ToJson(data, true);
            }
            catch (Exception exception)
            {
                FrameworkLog.Error($"Failed to serialize save data: {exception.Message}");
                return SaveResult.Failure(
                    SaveFailureReason.SerializationFailed,
                    exception.Message,
                    nameof(SaveData));
            }

            try
            {
                File.WriteAllText(savePath, json);
            }
            catch (Exception exception)
            {
                FrameworkLog.Error($"Failed to write save data: {exception.Message}");
                return SaveResult.Failure(
                    SaveFailureReason.WriteFailed,
                    exception.Message,
                    nameof(SaveData));
            }

            FrameworkLog.Info($"Save data written: {savePath}");
            return SaveResult.Success();
        }

        /// <summary>
        /// 현재 저장 파일을 삭제한다.
        /// </summary>
        /// <remarks>
        /// 파일이 없으면 아무 작업도 하지 않는다.
        /// </remarks>
        public void ResetSaveData()
        {
            // 저장 파일이 있을 때만 삭제해 불필요한 IO 예외 가능성을 줄인다.
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
                FrameworkLog.Info("Save data reset.");
            }
        }

        /// <summary>
        /// 저장 DTO의 필수 컨테이너와 영속 자산 ID를 정규화한다.
        /// </summary>
        /// <param name="data">직접 수정할 저장 데이터.</param>
        /// <returns>자산 컨테이너 또는 자산 ID가 변경되었으면 true.</returns>
        public static bool NormalizeData(SaveData data)
        {
            var assetDataChanged = false;
            // 저장 파일이 구버전이거나 일부 하위 객체가 누락된 경우 runtime 서비스가 null을 직접 다루지 않게 보정한다.
            if (data.player == null)
            {
                data.player = new PlayerSaveData();
            }

            // Older saves can have no current town because the field previously defaulted to empty.
            if (string.IsNullOrWhiteSpace(data.player.currentTownId))
            {
                data.player.currentTownId = "BaseCamp";
            }

            if (data.player.homeInventory == null)
            {
                data.player.homeInventory = new System.Collections.Generic.List<CargoEntrySaveData>();
            }

            if (data.player.villageBuildings == null)
            {
                data.player.villageBuildings = new System.Collections.Generic.List<VillageBuildingSaveData>();
            }

            for (var buildingIndex = 0; buildingIndex < data.player.villageBuildings.Count; buildingIndex++)
            {
                var building = data.player.villageBuildings[buildingIndex];
                if (building == null)
                {
                    continue;
                }

                if (building.displayName == null)
                {
                    building.displayName = string.Empty;
                }

                if (building.level < 0)
                {
                    building.level = 0;
                }
            }

            if (data.caravans == null)
            {
                data.caravans = new List<CaravanSaveData>();
            }

            if (data.tradeProgressEntries == null)
            {
                data.tradeProgressEntries = new List<TradeProgressSaveData>();
            }

            if (data.pendingSettlements == null)
            {
                data.pendingSettlements = new List<PendingSettlementSaveData>();
            }

            if (data.caravans.Count == 0)
            {
                data.caravans.Add(new CaravanSaveData());
            }

            var caravanIds = new HashSet<string>();
            var usedInstanceIds = new HashSet<string>();
            for (var caravanIndex = 0; caravanIndex < data.caravans.Count; caravanIndex++)
            {
                var caravan = data.caravans[caravanIndex];
                if (caravan == null)
                {
                    caravan = new CaravanSaveData();
                    data.caravans[caravanIndex] = caravan;
                }
                if (caravan.wagon == null || caravan.animals == null || caravan.mercenaries == null)
                {
                    assetDataChanged = true;
                }

                CaravanSaveDataMapper.Normalize(caravan);
                if (!string.IsNullOrWhiteSpace(caravan.wagon.wagonName))
                {
                    assetDataChanged |= EnsureUniqueInstanceId(
                        ref caravan.wagon.instanceId,
                        usedInstanceIds,
                        "Wagon",
                        caravan.wagon.wagonName);
                }

                for (var animalIndex = 0; animalIndex < caravan.animals.Count; animalIndex++)
                {
                    var animal = caravan.animals[animalIndex];
                    if (animal == null) continue;
                    assetDataChanged |= EnsureUniqueInstanceId(
                        ref animal.instanceId,
                        usedInstanceIds,
                        "Animal",
                        animal.animalName);
                }

                for (var mercenaryIndex = 0; mercenaryIndex < caravan.mercenaries.Count; mercenaryIndex++)
                {
                    var mercenary = caravan.mercenaries[mercenaryIndex];
                    if (mercenary == null) continue;
                    assetDataChanged |= EnsureUniqueInstanceId(
                        ref mercenary.instanceId,
                        usedInstanceIds,
                        "Mercenary",
                        mercenary.mercName);
                }
                if (string.IsNullOrEmpty(caravan.caravanId) || !caravanIds.Add(caravan.caravanId))
                {
                    if (!string.IsNullOrEmpty(caravan.caravanId))
                    {
                        FrameworkLog.Warning($"Duplicate caravan ID was replaced without relinking ambiguous child data: {caravan.caravanId}");
                    }
                    caravan.caravanId = SaveDataLookup.NewCaravanId();
                    caravanIds.Add(caravan.caravanId);
                }
            }

            CaravanSaveData selected;
            if (!SaveDataLookup.TryGetCaravan(data, data.selectedCaravanId, out selected))
                data.selectedCaravanId = data.caravans[0].caravanId;

            ValidateChildData(data, caravanIds);

            NormalizeRescueLoan(data);

            FrameworkTradePrepareCommitStore.Normalize(data);

            if (data.world == null)
            {
                data.world = new WorldSaveData();
            }

            if (data.world.unlockedTownIds == null)
            {
                data.world.unlockedTownIds = new System.Collections.Generic.List<string>();
            }

            if (data.world.unlockedRouteIds == null)
            {
                data.world.unlockedRouteIds = new System.Collections.Generic.List<string>();
            }

            if (data.world.completedRouteIds == null)
            {
                data.world.completedRouteIds = new System.Collections.Generic.List<string>();
            }

            if (data.world.marketInventories == null)
            {
                data.world.marketInventories = new System.Collections.Generic.List<MarketInventorySaveData>();
            }

            for (var inventoryIndex = 0; inventoryIndex < data.world.marketInventories.Count; inventoryIndex++)
            {
                var inventory = data.world.marketInventories[inventoryIndex];
                if (inventory == null)
                {
                    continue;
                }

                if (inventory.stocks == null)
                {
                    inventory.stocks = new System.Collections.Generic.List<MarketStockSaveData>();
                }
            }

            if (data.world.marketPurchasePreparation == null)
            {
                data.world.marketPurchasePreparation = new MarketPurchasePreparationSaveData();
            }

            if (string.IsNullOrEmpty(data.world.currentSeasonId))
            {
                data.world.currentSeasonId = "summer";
            }

            if (data.tutorial == null)
            {
                data.tutorial = new TutorialSaveData();
            }

            return assetDataChanged;
        }

        private static bool EnsureUniqueInstanceId(
            ref string instanceId,
            HashSet<string> usedInstanceIds,
            string assetType,
            string assetLabel)
        {
            if (!string.IsNullOrWhiteSpace(instanceId) && usedInstanceIds.Add(instanceId))
            {
                return false;
            }

            var duplicateId = instanceId;
            do
            {
                instanceId = SaveDataLookup.NewInstanceId();
            }
            while (!usedInstanceIds.Add(instanceId));

            if (!string.IsNullOrWhiteSpace(duplicateId))
            {
                FrameworkLog.Warning(
                    $"Duplicate asset instance ID was replaced. Type: {assetType}, Label: {assetLabel}, InstanceId: {duplicateId}");
            }

            return true;
        }

        private static void ValidateChildData(SaveData data, HashSet<string> caravanIds)
        {
            var progressOwners = new HashSet<string>();
            for (var i = 0; i < data.tradeProgressEntries.Count; i++)
            {
                var progress = data.tradeProgressEntries[i];
                if (progress == null) continue;
                if (progress.inGameTimeMultiplierAtStart <= 0f) progress.inGameTimeMultiplierAtStart = 1f;
                if (!caravanIds.Contains(progress.caravanId))
                    FrameworkLog.Error($"Orphan trade progress was preserved. CaravanId: {progress.caravanId}");
                else if (!progressOwners.Add(progress.caravanId))
                    FrameworkLog.Error($"Duplicate trade progress was preserved. CaravanId: {progress.caravanId}");
            }

            var pendingKeys = new HashSet<string>();
            for (var i = 0; i < data.pendingSettlements.Count; i++)
            {
                var pending = data.pendingSettlements[i];
                if (pending == null) continue;
                if (!caravanIds.Contains(pending.caravanId))
                    FrameworkLog.Error($"Orphan pending settlement was preserved. CaravanId: {pending.caravanId}");
                var key = pending.caravanId + "\n" + pending.tradeId;
                if (!pendingKeys.Add(key))
                    FrameworkLog.Error($"Duplicate pending settlement was preserved. CaravanId: {pending.caravanId}, TradeId: {pending.tradeId}");
            }
        }

        private static SaveData MigrateVersion5(string json)
        {
            try
            {
                var legacy = JsonUtility.FromJson<Version5SaveData>(json);
                if (legacy == null || legacy.caravan == null) return null;
                var data = new SaveData
                {
                    version = SaveData.CurrentVersion,
                    lastSavedUtcTicks = legacy.lastSavedUtcTicks,
                    player = legacy.player,
                    rescueLoan = legacy.rescueLoan,
                    tradePreparationCommit = legacy.tradePreparationCommit,
                    world = legacy.world,
                    tutorial = legacy.tutorial
                };
                data.caravans.Clear();
                data.tradeProgressEntries.Clear();
                data.pendingSettlements.Clear();
                legacy.caravan.caravanId = SaveDataLookup.NewCaravanId();
                data.caravans.Add(legacy.caravan);
                data.selectedCaravanId = legacy.caravan.caravanId;
                if (legacy.tradeProgress != null && (legacy.tradeProgress.state != TradeProgressState.None
                    || !string.IsNullOrEmpty(legacy.tradeProgress.activeTradeId)))
                {
                    legacy.tradeProgress.caravanId = data.selectedCaravanId;
                    data.tradeProgressEntries.Add(legacy.tradeProgress);
                }
                if (legacy.pendingSettlement != null && legacy.pendingSettlement.hasResult)
                {
                    legacy.pendingSettlement.caravanId = data.selectedCaravanId;
                    data.pendingSettlements.Add(legacy.pendingSettlement);
                }
                NormalizeData(data);
                FrameworkLog.Info("Save data migrated from version 5 to version 6.");
                return data;
            }
            catch (Exception exception)
            {
                FrameworkLog.Error($"Failed to migrate version 5 save data: {exception.Message}");
                return null;
            }
        }

        [Serializable]
        private sealed class Version5SaveData
        {
            public int version;
            public long lastSavedUtcTicks;
            public PlayerSaveData player;
            public CaravanSaveData caravan;
            public TradeProgressSaveData tradeProgress;
            public PendingSettlementSaveData pendingSettlement;
            public RescueLoanSaveData rescueLoan;
            public TradePreparationCommitSaveData tradePreparationCommit;
            public WorldSaveData world;
            public TutorialSaveData tutorial;
        }

        internal static void NormalizeRescueLoan(SaveData data)
        {
            if (data.rescueLoan == null)
            {
                data.rescueLoan = new RescueLoanSaveData();
                return;
            }

            var loan = data.rescueLoan;
            if (loan.loanId == null)
            {
                loan.loanId = string.Empty;
            }

            if (loan.originalPrincipal < 0L)
            {
                loan.originalPrincipal = 0L;
            }

            if (loan.remainingPrincipal < 0L)
            {
                loan.remainingPrincipal = 0L;
            }

            if (loan.remainingPrincipal > loan.originalPrincipal)
            {
                FrameworkLog.Warning("Rescue loan remaining principal exceeded original principal and was clamped.");
                loan.remainingPrincipal = loan.originalPrincipal;
            }

            if (loan.issuedUtcTicks < 0L)
            {
                FrameworkLog.Warning("Rescue loan issued UTC ticks was negative and was reset to zero.");
                loan.issuedUtcTicks = 0L;
            }

            if (loan.remainingPrincipal == 0L)
            {
                loan.isActive = false;
            }

            if (loan.isActive && (string.IsNullOrWhiteSpace(loan.loanId)
                || loan.originalPrincipal <= 0L || loan.remainingPrincipal <= 0L))
            {
                FrameworkLog.Warning("Corrupted active rescue loan was normalized to a safe inactive state.");
                loan.isActive = false;
            }

            if (!loan.isActive)
            {
                loan.isRestrictedPreparation = false;
            }
        }
    }
}
