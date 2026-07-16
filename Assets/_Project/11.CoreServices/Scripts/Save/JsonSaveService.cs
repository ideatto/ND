/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - SaveData를 Unity persistentDataPath의 JSON 파일로 저장하고 로드한다.
 * - 저장 데이터가 없거나 유효하지 않은 경우 새 게임 데이터로 복구한다.
 *
 * Main Features
 * - 저장 파일 존재 확인, 새 저장 데이터 생성, JSON 로드/저장, 저장 파일 삭제를 제공한다.
 * - 로드/저장 전 하위 DTO의 null 값을 정규화한다.
 *
 * Usage for Team Members
 * - FrameworkRoot.SaveService를 통해 사용한다.
 * - 저장 데이터 변경 후 영속화가 필요하면 Save(...)를 호출한다.
 *
 * Main Public APIs
 * - HasSaveData(): save_data.json 존재 여부를 확인한다.
 * - CreateNewGameData(): 현재 version의 기본 SaveData를 생성한다.
 * - Load(): 저장 파일을 읽고 유효하지 않으면 새 SaveData를 반환한다.
 * - Save(...): SaveData를 JSON으로 기록한다.
 * - ResetSaveData(): 저장 파일을 삭제한다.
 *
 * Important Notes
 * - 저장 파일 이름은 save_data.json으로 고정되어 있다.
 * - version이 CurrentVersion과 다르면 migration 없이 새 데이터로 복구한다(version 5: pendingSettlement 포함).
 * - version 5 세이브에 상점 재고·구매 준비 필드가 없어도 NormalizeData가 빈 컨테이너로 보정한다.
 * - version 5 세이브에 거점 창고·마을 건물 필드가 없어도 NormalizeData가 빈 리스트로 보정한다.
 * - Save(...)는 null 입력이나 IO 예외를 로그로 남기고 외부로 예외를 던지지 않는다.
 */
using System;
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

            FrameworkLog.Info("New game save data created.");
            return data;
        }

        /// <summary>
        /// JSON 저장 파일에서 SaveData를 로드한다.
        /// </summary>
        /// <returns>유효한 저장 데이터 또는 복구용 새 게임 데이터.</returns>
        /// <remarks>
        /// 파일 없음, 역직렬화 실패, version 불일치는 새 게임 데이터 반환으로 복구한다.
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

                if (data == null || data.version != SaveData.CurrentVersion)
                {
                    FrameworkLog.Warning("Save data is invalid or has an unsupported version. Creating new game data.");
                    return CreateNewGameData();
                }

                NormalizeData(data);
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
        /// <param name="data">저장할 SaveData. null이면 저장을 건너뛴다.</param>
        /// <remarks>
        /// 성공 시 version과 lastSavedUtcTicks가 현재 값으로 갱신된다.
        /// </remarks>
        public void Save(SaveData data)
        {
            // null 저장은 기존 저장 파일을 덮어쓰면 복구가 어려우므로 무시한다.
            if (data == null)
            {
                FrameworkLog.Warning("Save was skipped because data is null.");
                return;
            }

            try
            {
                // 저장 직전 DTO 그래프를 정규화해 JsonUtility가 누락된 하위 데이터를 그대로 기록하지 않게 한다.
                NormalizeData(data);
                data.version = SaveData.CurrentVersion;
                data.lastSavedUtcTicks = DateTime.UtcNow.Ticks;

                var json = JsonUtility.ToJson(data, true);
                File.WriteAllText(savePath, json);
                FrameworkLog.Info($"Save data written: {savePath}");
            }
            catch (Exception exception)
            {
                // 저장 실패는 호출자 흐름을 끊지 않고 로그로 남긴다. 재시도 정책은 상위 flow가 결정한다.
                FrameworkLog.Error($"Failed to save data: {exception.Message}");
            }
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

        private static void NormalizeData(SaveData data)
        {
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

            if (data.caravan == null)
            {
                data.caravan = new CaravanSaveData();
            }

            CaravanSaveDataMapper.Normalize(data.caravan);

            if (data.tradeProgress == null)
            {
                data.tradeProgress = new TradeProgressSaveData();
            }

            if (data.tradeProgress.inGameTimeMultiplierAtStart <= 0f)
            {
                data.tradeProgress.inGameTimeMultiplierAtStart = 1f;
            }

            if (data.pendingSettlement == null)
            {
                data.pendingSettlement = PendingSettlementSaveDataMapper.CreateEmpty();
            }

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
        }
    }
}
