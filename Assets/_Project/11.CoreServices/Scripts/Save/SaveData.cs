/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - CoreServices가 JSON으로 저장하는 runtime 저장 데이터 schema를 정의한다.
 * - 플레이어, caravan, 무역 진행, 월드, 튜토리얼 상태를 하나의 SaveData 객체 그래프로 묶는다.
 *
 * Main Features
 * - 현재 저장 schema version을 제공한다.
 * - Core caravan runtime data를 직렬화 가능한 DTO 형태로 보관한다.
 * - 무역 진행 상태와 UTC tick 기반 시작/종료 예정 시간을 저장한다.
 *
 * Usage for Team Members
 * - JsonSaveService가 SaveData를 생성, 로드, 저장한다.
 * - runtime caravan 객체와의 변환은 CaravanSaveDataMapper를 통해 수행한다.
 * - 새 저장 필드를 추가할 때는 CurrentVersion과 NormalizeData 정책을 함께 검토한다.
 *
 * Main Public APIs
 * - SaveData.CurrentVersion: 현재 지원하는 저장 schema version.
 * - TradeProgressState: 저장 데이터의 무역 진행 상태 enum.
 *
 * Important Notes
 * - Unity JsonUtility 직렬화를 위해 DTO는 public field 중심으로 구성되어 있다.
 * - 시간 값은 UTC DateTime.Ticks 기준으로 저장된다.
 * - progress01은 0부터 1 사이의 진행률 의미로 사용되지만, 보정은 Core 계산 로직에 따른다.
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
        /// <summary>
        /// 현재 코드가 지원하는 저장 데이터 schema version이다.
        /// </summary>
        public const int CurrentVersion = 3;

        /// <summary>
        /// 저장 데이터 schema version이다.
        /// </summary>
        public int version = CurrentVersion;

        /// <summary>
        /// 마지막 저장 시각의 UTC ticks 값이다.
        /// </summary>
        public long lastSavedUtcTicks;

        /// <summary>
        /// 플레이어 위치와 보유 재화를 저장하는 데이터이다.
        /// </summary>
        public PlayerSaveData player = new PlayerSaveData();

        /// <summary>
        /// caravan 구성과 현재 여정 상태를 저장하는 데이터이다.
        /// </summary>
        public CaravanSaveData caravan = new CaravanSaveData();

        /// <summary>
        /// active trade ID, route ID, 진행 상태, 시간 정보를 저장하는 데이터이다.
        /// </summary>
        public TradeProgressSaveData tradeProgress = new TradeProgressSaveData();

        /// <summary>
        /// 월드 계절과 재난 상태를 저장하는 데이터이다.
        /// </summary>
        public WorldSaveData world = new WorldSaveData();

        /// <summary>
        /// 튜토리얼 진행 상태를 저장하는 데이터이다.
        /// </summary>
        public TutorialSaveData tutorial = new TutorialSaveData();
    }

    /// <summary>
    /// 플레이어의 현재 위치와 재화 상태를 저장하는 DTO이다.
    /// </summary>
    [Serializable]
    public sealed class PlayerSaveData
    {
        /// <summary>
        /// 현재 플레이어가 위치한 마을 ID이다.
        /// </summary>
        public string currentTownId = string.Empty;

        /// <summary>
        /// 거래에 사용하는 재화량이다.
        /// </summary>
        public int tradingCurrency = 1000;

        /// <summary>
        /// 개발 또는 성장에 사용하는 재화량이다.
        /// </summary>
        public int developmentCurrency;
    }

    /// <summary>
    /// caravan의 구성, 적재, 여정 진행 상태를 저장하는 DTO이다.
    /// </summary>
    /// <remarks>
    /// runtime CaravanData와 직접 동일하지 않으므로 CaravanSaveDataMapper를 통해 변환해야 한다.
    /// </remarks>
    [Serializable]
    public sealed class CaravanSaveData
    {
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
    }

    /// <summary>
    /// wagon 선택과 적재 제한 정보를 저장하는 DTO이다.
    /// </summary>
    [Serializable]
    public sealed class WagonSaveData
    {
        public string wagonName = string.Empty;
        public float overLoad;
        public float maxLoad;
        public int minAnimals;
        public int maxAnimals;
        public float speedModifier;
    }

    /// <summary>
    /// caravan 동물 한 개체의 이동/식량 정보를 저장하는 DTO이다.
    /// </summary>
    [Serializable]
    public sealed class AnimalSaveData
    {
        public string animalName = string.Empty;
        public float speed = 1f;
        public float foodPerKm;
    }

    /// <summary>
    /// caravan 용병 한 명의 전투력과 계약 수량을 저장하는 DTO이다.
    /// </summary>
    [Serializable]
    public sealed class MercenarySaveData
    {
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
    }

    /// <summary>
    /// active trade의 식별자, route, 진행 상태, 시간 정보를 저장하는 DTO이다.
    /// </summary>
    [Serializable]
    public sealed class TradeProgressSaveData
    {
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
    /// 월드 계절과 재난 상태를 저장하는 DTO이다.
    /// </summary>
    [Serializable]
    public sealed class WorldSaveData
    {
        public string currentSeasonId = "summer";
        public string currentDisasterId = string.Empty;
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
