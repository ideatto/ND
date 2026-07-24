/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - CoreServices가 JSON으로 저장하는 runtime 저장 데이터 schema를 정의한다.
 * - 플레이어, ID 기반 다중 caravan, 무역 진행, 월드, 튜토리얼 상태를 하나의 SaveData 객체 그래프로 묶는다.
 *
 * Main Features
 * - 현재 저장 schema version을 제공한다.
 * - Core caravan runtime data(M2 포함)를 직렬화 가능한 DTO 형태로 보관한다.
 * - caravan에 배치된 마차·동물·용병의 보유 개체 식별자를 저장한다.
 * - 무역 진행 상태와 UTC tick 기반 시작/종료 예정 시간을 저장한다.
 * - Economy M1 연동을 위한 long 화폐·growth level·월드 unlock 목록을 저장한다.
 * - caravan별 SettlementPending 대기 정산 결과(PendingSettlementSaveData)를 저장한다.
 * - 구조 대출 원금, 잔액, 활성 여부와 출발 전 제한 상태를 저장한다.
 * - 상점 재고(marketInventories)와 구매 준비(marketPurchasePreparation)를 WorldSaveData에 저장한다.
 * - 거점 창고(homeInventory)와 마을 건물 진행(villageBuildings)을 PlayerSaveData에 저장한다.
 *
 * Usage for Team Members
 * - JsonSaveService가 SaveData를 생성, 로드, 저장한다.
 * - runtime caravan 객체와의 변환은 CaravanSaveDataMapper를 통해 수행한다.
 * - 대기 정산 결과 변환은 PendingSettlementSaveDataMapper를 통해 수행한다.
 * - 상점 적재 초안·확정 화물은 caravan.cargo(CargoEntrySaveData)에 매핑하며 loadedLines를 별도 저장하지 않는다.
 * - 거점 창고는 caravan.cargo와 동일한 CargoEntrySaveData 목록으로 저장한다.
 * - 마을 건물은 displayName + level로 저장한다(종류 한정·표시명 고정 전제).
 * - 새 저장 필드를 추가할 때는 CurrentVersion과 NormalizeData 정책을 함께 검토한다.
 *
 * Main Public APIs
 * - SaveData.CurrentVersion: 현재 지원하는 저장 schema version.
 * - TradeProgressState: 저장 데이터의 무역 진행 상태 enum.
 *
 * Important Notes
 * - Unity JsonUtility 직렬화를 위해 DTO는 public field 중심으로 구성되어 있다.
 * - 시간 값은 UTC DateTime.Ticks 기준으로 저장된다.
 * - version 4부터 Core M2 caravan 필드와 long 화폐를 포함한다.
 * - version 6부터 caravans, tradeProgressEntries, pendingSettlements와 selectedCaravanId를 사용한다.
 * - 상점 재고·구매 준비 필드는 version 5를 유지한 채 추가되며, 구 세이브의 null은 JsonSaveService.NormalizeData가 보정한다.
 * - 거점 창고·마을 건물 필드도 version 5를 유지한 채 추가되며, 구 세이브의 null은 NormalizeData가 보정한다.
 * - Related Documentation: Docs/Personal_Documents/CSU/0712_m3-pending-settlement-persist.md
 * - Related Documentation: Docs/Personal_Documents/JJH/0714_Progression MarketInventory_Change_Request.md
 * - Related Documentation: Docs/Personal_Documents/CSU/0716_save_data_base_camp_schema.md
 */
using System;
using System.Collections.Generic;

namespace ND.Framework
{
    /// <summary>
    /// 게임 전체 저장 데이터를 구성하는 최상위 DTO이다.
    /// </summary>
    /// <remarks>
    /// JsonSaveService가 version을 확인하고 null 하위 데이터를 정규화한 뒤 runtime에서 공유한다.
    /// </remarks>
    [Serializable]
    public sealed class SaveData
    {
        public SaveData()
        {
            var defaultCaravan = new CaravanSaveData { caravanId = SaveDataLookup.NewCaravanId() };
            caravans.Add(defaultCaravan);
            selectedCaravanId = defaultCaravan.caravanId;
        }

        /// <summary>
        /// 현재 코드가 지원하는 저장 데이터 schema version이다.
        /// </summary>
        public const int CurrentVersion = 6;

        /// <summary>
        /// 저장 데이터 schema version이다.
        /// </summary>
        public int version = CurrentVersion;

        /// <summary>
        /// 마지막 저장 시각의 UTC ticks 값이다.
        /// </summary>
        public long lastSavedUtcTicks;

        /// <summary>
        /// 플레이어 위치, 재화, growth level을 저장하는 데이터이다.
        /// </summary>
        public PlayerSaveData player = new PlayerSaveData();

        /// <summary>
        /// caravan 구성과 현재 여정 상태를 저장하는 데이터이다.
        /// </summary>
        public List<CaravanSaveData> caravans = new List<CaravanSaveData>();

        /// <summary>
        /// active trade ID, route ID, 진행 상태, 시간 정보를 저장하는 데이터이다.
        /// </summary>
        public List<TradeProgressSaveData> tradeProgressEntries = new List<TradeProgressSaveData>();

        /// <summary>
        /// SettlementPending 대기 정산 결과이다. 수령 전 재실행 복구에 사용한다.
        /// </summary>
        public List<PendingSettlementSaveData> pendingSettlements = new List<PendingSettlementSaveData>();

        /// <summary>
        /// 마지막으로 선택한 caravan ID이다. 기존 단일 runtime은 이 caravan을 active caravan으로 사용한다.
        /// </summary>
        public string selectedCaravanId = string.Empty;

        /// <summary>기존 단일 runtime 호출부에 선택 caravan을 제공하는 비직렬화 호환 접근자이다.</summary>
        public CaravanSaveData caravan
        {
            get
            {
                CaravanSaveData value;
                return SaveDataLookup.TryGetSelectedCaravan(this, out value) ? value : null;
            }
            set
            {
                SaveDataLookup.SetSelectedCaravan(this, value);
            }
        }

        /// <summary>기존 단일 runtime 호출부에 선택 caravan의 progress를 제공하는 비직렬화 호환 접근자이다.</summary>
        public TradeProgressSaveData tradeProgress
        {
            get
            {
                TradeProgressSaveData value;
                return SaveDataLookup.TryGetTradeProgress(this, selectedCaravanId, out value) ? value : null;
            }
            set
            {
                SaveDataLookup.SetTradeProgress(this, selectedCaravanId, value);
            }
        }

        /// <summary>기존 단일 runtime 호출부에 선택 caravan의 pending settlement를 제공하는 비직렬화 호환 접근자이다.</summary>
        public PendingSettlementSaveData pendingSettlement
        {
            get
            {
                PendingSettlementSaveData value;
                return SaveDataLookup.TryGetPendingSettlement(this, selectedCaravanId, null, out value) ? value : null;
            }
            set
            {
                SaveDataLookup.SetPendingSettlement(this, selectedCaravanId, value);
            }
        }

        /// <summary>
        /// 구조 대출의 발급·상환 및 출발 전 제한 상태이다.
        /// </summary>
        public RescueLoanSaveData rescueLoan = new RescueLoanSaveData();

        /// <summary>Active trade departure-time preparation snapshot.</summary>
        public TradePreparationCommitSaveData tradePreparationCommit = new TradePreparationCommitSaveData();

        /// <summary>
        /// 월드 계절, 재난, unlock 목록, 상점 재고·구매 준비를 저장하는 데이터이다.
        /// </summary>
        public WorldSaveData world = new WorldSaveData();

        /// <summary>
        /// 튜토리얼 진행 상태를 저장하는 데이터이다.
        /// </summary>
        public TutorialSaveData tutorial = new TutorialSaveData();
    }

    /// <summary>
    /// 구조 대출의 영속 상태를 보관하는 DTO이다.
    /// </summary>
    [Serializable]
    public sealed class RescueLoanSaveData
    {
        public string loanId = string.Empty;
        public long originalPrincipal;
        public long remainingPrincipal;
        public bool isActive;
        public long issuedUtcTicks;
        public bool isRestrictedPreparation;
    }

    /// <summary>
    /// 플레이어의 현재 위치, 재화, growth level, 거점 창고·마을 건물을 저장하는 DTO이다.
    /// </summary>
    [Serializable]
    public sealed class PlayerSaveData
    {
        /// <summary>
        /// 현재 플레이어가 위치한 마을 ID이다.
        /// </summary>
        public string currentTownId = string.Empty;

        /// <summary>
        /// 거래에 사용하는 재화량이다. 단위: abstract trade money (long).
        /// </summary>
        public long tradingCurrency = 1000;

        /// <summary>
        /// 개발 또는 성장에 사용하는 재화량이다. 단위: abstract development currency (long).
        /// </summary>
        public long developmentCurrency;

        /// <summary>
        /// 플레이어 growth level이다. Economy M1 runtime stat 계산에 사용한다.
        /// </summary>
        public int playerGrowthLevel;

        /// <summary>
        /// caravan growth level이다. Economy M1 runtime stat 계산에 사용한다.
        /// </summary>
        public int caravanGrowthLevel;

        /// <summary>
        /// 거점(Base Camp) 창고 화물 목록이다. caravan.cargo와 동일한 CargoEntrySaveData 형식이다.
        /// </summary>
        public List<CargoEntrySaveData> homeInventory = new List<CargoEntrySaveData>();

        /// <summary>
        /// 거점 마을에 보유한 건물 진행 목록이다. 키는 displayName이며 level 1 이상이 보유 상태이다.
        /// </summary>
        public List<VillageBuildingSaveData> villageBuildings = new List<VillageBuildingSaveData>();
    }

    /// <summary>
    /// 거점 마을 건물 한 종류의 저장 진행도이다.
    /// </summary>
    /// <remarks>
    /// displayName은 VillageBuildingRegistry 카탈로그와 동일한 문자열을 키로 사용한다.
    /// 건물 종류가 한정되고 표시명이 고정된다는 기획 전제 하에서만 안정적이다.
    /// level 0 이하는 미건축으로 취급한다.
    /// </remarks>
    [Serializable]
    public sealed class VillageBuildingSaveData
    {
        /// <summary>
        /// 건물 종류 키이다. 카탈로그 displayName과 일치해야 한다.
        /// </summary>
        public string displayName = string.Empty;

        /// <summary>
        /// 건물 레벨이다. 0 이하는 미건축, 1 이상은 보유 레벨이다.
        /// </summary>
        public int level;
    }

    /// <summary>
    /// caravan의 구성, 적재, 여정 진행 상태(M2 포함)를 저장하는 DTO이다.
    /// </summary>
    /// <remarks>
    /// runtime CaravanData와 직접 동일하지 않으므로 CaravanSaveDataMapper를 통해 변환해야 한다.
    /// </remarks>
    [Serializable]
    public sealed class CaravanSaveData
    {
        /// <summary>배열 위치와 무관하게 caravan을 식별하는 고유 ID이다.</summary>
        public string caravanId = string.Empty;

        /// <summary>Caravan Overview의 고정 슬롯과 연결되는 영속 위치 값이다. 목록 인덱스로 대체하지 않는다.</summary>
        public int slotIndex;

        /// <summary>
        /// 이 caravan이 현재 머무는 마을 ID이다. 이동 중에는 출발 마을을 유지하고,
        /// 정산 Claim이 성공했을 때 목적지 또는 실패 복귀 거점으로 갱신한다.
        /// </summary>
        public string currentTownId = string.Empty;

        /// <summary>
        /// 선택된 wagon 정보이다.
        /// </summary>
        public WagonSaveData wagon = new WagonSaveData();

        /// <summary>
        /// caravan에 배치된 동물 목록이다.
        /// </summary>
        public List<AnimalSaveData> animals = new List<AnimalSaveData>();

        /// <summary>
        /// caravan에 고용된 용병 목록이다.
        /// </summary>
        public List<MercenarySaveData> mercenaries = new List<MercenarySaveData>();

        /// <summary>
        /// 적재된 cargo 목록이다.
        /// </summary>
        public List<CargoEntrySaveData> cargo = new List<CargoEntrySaveData>();

        /// <summary>
        /// 보유 식량 수량이다.
        /// </summary>
        public int foodAmount;

        /// <summary>
        /// 식량 1개당 무게이다. 0 이하 값은 저장 데이터 정규화 시 1로 보정된다.
        /// </summary>
        public float foodUnitWeight = 1f;

        /// <summary>
        /// 이 caravan의 산적 이벤트 기본 무사 통과 확률이다. 단위: percent (0~100).
        /// </summary>
        public float baseSafetyChancePercent;

        /// <summary>
        /// Core journey의 현재 상태이다.
        /// </summary>
        public JourneyState state = JourneyState.Prepare;

        /// <summary>
        /// 현재 이동한 거리(km)이다.
        /// </summary>
        public float currentDistanceKm;

        /// <summary>
        /// 현재 여정의 총 경과 또는 예정 시간(초)이다.
        /// </summary>
        public float totalSeconds;

        /// <summary>
        /// 0부터 1 사이의 여정 진행률이다.
        /// </summary>
        public float progress01;

        /// <summary>
        /// 이번 무역에서 누적된 인게임 경과 시간(초)이다.
        /// </summary>
        public float elapsedInGameSeconds;

        /// <summary>
        /// 현재 정산 결과가 claim 되었는지 여부이다.
        /// </summary>
        public bool settlementClaimed;

        /// <summary>
        /// 이번 run에서 손실된 cargo 수량이다.
        /// </summary>
        public int runCargoLost;

        /// <summary>
        /// 이번 run에서 손실된 식량 수량이다.
        /// </summary>
        public float runFoodLost;

        /// <summary>
        /// 이번 run의 치명적 실패 원인이다.
        /// </summary>
        public JourneyFailureReason runFatalReason = JourneyFailureReason.None;

        /// <summary>
        /// 현재 마차 내구도이다. 0 이하면 출발 불가(BrokenWagon)로 검증된다.
        /// </summary>
        public int currentDurability;

        /// <summary>
        /// 이번 무역 약탈 내구도 손실 누적이다.
        /// </summary>
        public int runDurabilityLost;

        /// <summary>
        /// 이번 무역 전투 횟수이다.
        /// </summary>
        public int runBattlesFought;

        /// <summary>
        /// 이번 run에서 이미 처리한 거리 기반 이벤트 판정 수이다.
        /// 온라인 갱신과 오프라인 복원에서 같은 구간을 중복 판정하지 않게 한다.
        /// </summary>
        public int runEventChecksProcessed;

        /// <summary>
        /// 이번 run에서 실제 발생한 이벤트 수이다.
        /// </summary>
        public int runEventsOccurred;

        public List<string> runLostMercenaryInstanceIds = new List<string>();

        /// <summary>
        /// 이번 무역 출발 시 내구도이다.
        /// </summary>
        public int runStartDurability;

        /// <summary>
        /// 거리 마모 소수점 이월 값이다.
        /// </summary>
        public float runWearRemainder;

        /// <summary>
        /// 이번 무역 식량 바닥 여부이다.
        /// </summary>
        public bool runFoodDepleted;

        /// <summary>
        /// 식량이 바닥난 시점의 진행도(0~1)이다.
        /// </summary>
        public float runFoodDepletedProgress;

        /// <summary>
        /// 식량 바닥 후 도착 제한 시간(초)이다.
        /// </summary>
        public float starveGraceSeconds;

        /// <summary>
        /// 손실 상한율(0~1)이다. 1이면 무제한이다.
        /// </summary>
        public float lossLimitRate = 1f;

        /// <summary>
        /// 약탈 내구도 손실에 손실 상한을 적용할지 여부이다.
        /// </summary>

        /// <summary>
        /// 출발 시 원래 무역품 개수이다.
        /// </summary>
        public int runOriginalCargoCount;

        /// <summary>
        /// 출발 시 짐무게이다.
        /// </summary>
        public float runDepartureLoad;
    }

    /// <summary>
    /// wagon 선택과 적재 제한 정보를 저장하는 DTO이다.
    /// </summary>
    [Serializable]
    public sealed class WagonSaveData
    {
        /// <summary>마차 종류와 별개로 플레이어가 보유한 한 대를 식별하는 안정 ID이다.</summary>
        public string instanceId = string.Empty;
        public string wagonName = string.Empty;
        public float overLoad;
        public float maxLoad;
        public int minAnimals;
        public int maxAnimals;
        public float speedModifier;
        public int maxDurability = 100;
        public int inventorySlotCount = 1;
    }

    /// <summary>
    /// caravan 동물 한 개체의 이동/식량/M2 효율 정보를 저장하는 DTO이다.
    /// </summary>
    [Serializable]
    public sealed class AnimalSaveData
    {
        /// <summary>동물 종류와 별개로 플레이어가 보유한 한 개체를 식별하는 안정 ID이다.</summary>
        public string instanceId = string.Empty;
        public string animalName = string.Empty;
        public float speed = 1f;
        public float foodPerKm;
        public float increaseOverLoad;
        public float increaseMaxLoad;
        public DraftAnimalType animalType;
    }

    /// <summary>
    /// caravan 용병 한 명의 전투력과 계약 수량을 저장하는 DTO이다.
    /// </summary>
    [Serializable]
    public sealed class MercenarySaveData
    {
        /// <summary>용병 종류와 별개로 플레이어가 보유한 한 명을 식별하는 안정 ID이다.</summary>
        public string instanceId = string.Empty;
        public string mercName = string.Empty;
        public int combatPower;
        public int contractCount;
    }

    /// <summary>
    /// cargo item과 수량을 함께 저장하는 DTO이다.
    /// </summary>
    [Serializable]
    public sealed class CargoEntrySaveData
    {
        public TradeItemSaveData item = new TradeItemSaveData();
        public int quantity;
    }

    /// <summary>
    /// 거래 item의 저장 가능한 최소 정보를 담는 DTO이다.
    /// </summary>
    [Serializable]
    public sealed class TradeItemSaveData
    {
        public string itemId = string.Empty;
        public string itemName = string.Empty;
        public float weight;
        public long basePrice;
        public int maxCount = 1;
    }

    /// <summary>
    /// active trade의 식별자, route, 진행 상태, 시간 정보를 저장하는 DTO이다.
    /// </summary>
    [Serializable]
    public sealed class TradeProgressSaveData
    {
        /// <summary>이 progress를 소유한 caravan ID이다.</summary>
        public string caravanId = string.Empty;

        /// <summary>
        /// 현재 진행 또는 정산 중인 무역 ID이다.
        /// </summary>
        public string activeTradeId = string.Empty;

        /// <summary>
        /// 현재 진행 또는 정산 중인 route ID이다.
        /// </summary>
        public string activeRouteId = string.Empty;

        /// <summary>
        /// 저장 데이터 기준 무역 진행 상태이다.
        /// </summary>
        public TradeProgressState state;

        /// <summary>
        /// 무역 시작 시각의 UTC ticks 값이다.
        /// </summary>
        public long tradeStartUtcTick;

        /// <summary>
        /// 예상 도착 시각의 UTC ticks 값이다.
        /// </summary>
        public long expectedTradeEndUtcTick;

        /// <summary>
        /// 무역 출발 시점에 고정된 인게임 시간 배율이다.
        /// </summary>
        public float inGameTimeMultiplierAtStart = 1f;
    }

    /// <summary>
    /// 월드 계절, 재난, unlock 목록, 상점 재고·구매 준비를 저장하는 DTO이다.
    /// </summary>
    [Serializable]
    public sealed class WorldSaveData
    {
        /// <summary>
        /// 현재 계절 ID이다. Economy PriceCalculationInput.SeasonId와 연결된다.
        /// </summary>
        public string currentSeasonId = "summer";

        /// <summary>
        /// 현재 재난 ID이다. Economy PriceCalculationInput.DisasterId와 연결된다.
        /// </summary>
        public string currentDisasterId = string.Empty;

        /// <summary>
        /// unlock된 마을 ID 목록이다.
        /// </summary>
        public List<string> unlockedTownIds = new List<string>();

        /// <summary>
        /// unlock된 route ID 목록이다.
        /// </summary>
        public List<string> unlockedRouteIds = new List<string>();

        /// <summary>
        /// 완료된 route ID 목록이다.
        /// </summary>
        public List<string> completedRouteIds = new List<string>();

        /// <summary>
        /// 상점별 재고 스냅샷 목록이다. 동일 marketId는 하나의 항목으로 유지한다.
        /// </summary>
        public List<MarketInventorySaveData> marketInventories = new List<MarketInventorySaveData>();

        /// <summary>
        /// 상점 구매 초안·확정 준비 상태이다. 적재 품목 자체는 caravan.cargo에 저장한다.
        /// </summary>
        public MarketPurchasePreparationSaveData marketPurchasePreparation = new MarketPurchasePreparationSaveData();
    }

    /// <summary>
    /// 한 상점의 재고 갱신 구간과 품목 수량을 저장하는 DTO이다.
    /// </summary>
    /// <remarks>
    /// refreshIndex와 nextRefreshUtcTicks는 UTC 시간 구간 기반 재고 갱신에 사용한다.
    /// seed는 worldSeed·marketId·refreshIndex로부터 유도된 결정적 생성 시드이다.
    /// </remarks>
    [Serializable]
    public sealed class MarketInventorySaveData
    {
        /// <summary>
        /// 상점 식별자이다.
        /// </summary>
        public string marketId = string.Empty;

        /// <summary>
        /// 현재 UTC 시간 구간에 대응하는 재고 갱신 인덱스이다.
        /// </summary>
        public long refreshIndex;

        /// <summary>
        /// 다음 재고 갱신 시각의 UTC ticks 값이다.
        /// </summary>
        public long nextRefreshUtcTicks;

        /// <summary>
        /// 해당 갱신 구간의 결정적 재고 생성 시드이다.
        /// </summary>
        public int seed;

        /// <summary>
        /// 상점에 노출된 품목 재고 목록이다.
        /// </summary>
        public List<MarketStockSaveData> stocks = new List<MarketStockSaveData>();
    }

    /// <summary>
    /// 상점 재고 한 품목의 itemId, 수량, 단가를 저장하는 DTO이다.
    /// </summary>
    [Serializable]
    public sealed class MarketStockSaveData
    {
        /// <summary>
        /// 거래 품목 ID이다.
        /// </summary>
        public string itemId = string.Empty;

        /// <summary>
        /// 현재 판매 가능 수량이다.
        /// </summary>
        public int quantity;

        /// <summary>
        /// 해당 재고 갱신 구간의 단가이다. 단위: abstract trade money (long).
        /// </summary>
        public long unitPrice;
    }

    /// <summary>
    /// 상점 구매 초안·확정 준비 상태를 저장하는 DTO이다.
    /// </summary>
    /// <remarks>
    /// isCommitted가 true이면 지갑 차감과 재고 차감이 완료된 확정 상태이다.
    /// 적재 품목 목록은 caravan.cargo에 TradeItemSaveData + quantity로 매핑한다.
    /// </remarks>
    [Serializable]
    public sealed class MarketPurchasePreparationSaveData
    {
        /// <summary>
        /// 구매 준비가 속한 상점 ID이다.
        /// </summary>
        public string marketId = string.Empty;

        /// <summary>
        /// 구매가 확정되어 지갑·재고가 반영되었는지 여부이다.
        /// </summary>
        public bool isCommitted;

        /// <summary>
        /// 확정 또는 초안 기준 총 구매 비용이다. 단위: abstract trade money (long).
        /// </summary>
        public long totalCost;

        /// <summary>
        /// 현재 적재 구성의 해시이다. 동일 구성 재확정 시 중복 차감을 방지한다.
        /// </summary>
        public int cargoHash;
    }

    /// <summary>
    /// 튜토리얼 완료, 스킵, 단계 정보를 저장하는 DTO이다.
    /// </summary>
    [Serializable]
    public sealed class TutorialSaveData
    {
        public bool isCompleted;
        public bool isSkipped;
        public int stepIndex;
    }

    /// <summary>
    /// 저장 데이터 기준 무역 진행 상태를 나타낸다.
    /// </summary>
    public enum TradeProgressState
    {
        /// <summary>
        /// 무역 진행 정보가 없는 초기 상태이다.
        /// </summary>
        None,

        /// <summary>
        /// 출발 전 준비 상태이다.
        /// </summary>
        Preparing,

        /// <summary>
        /// 무역이 이동 중이며 시간 기반 진행률 계산 대상인 상태이다.
        /// </summary>
        Traveling,

        /// <summary>
        /// 도착 또는 실패 결과가 생성되어 사용자의 정산 claim을 기다리는 상태이다.
        /// </summary>
        SettlementPending,

        /// <summary>
        /// 정산 claim 이후 성공 결과로 완료된 상태이다.
        /// </summary>
        Completed,

        /// <summary>
        /// 정산 claim 이후 실패 결과로 종료된 상태이다.
        /// </summary>
        Failed
    }
}
