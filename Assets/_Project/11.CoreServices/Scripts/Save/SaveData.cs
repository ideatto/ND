using System;
using System.Collections.Generic;

namespace ND.Framework
{
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public long lastSavedUtcTicks;
        public PlayerSaveData player = new PlayerSaveData();
        public CaravanSaveData caravan = new CaravanSaveData();
        public TradeProgressSaveData tradeProgress = new TradeProgressSaveData();
        public WorldSaveData world = new WorldSaveData();
        public TutorialSaveData tutorial = new TutorialSaveData();
    }

    [Serializable]
    public sealed class PlayerSaveData
    {
        public string currentTownId = string.Empty;
        public int tradingCurrency = 1000;
        public int developmentCurrency;
    }

    [Serializable]
    public sealed class CaravanSaveData
    {
        public WagonSaveData wagon = new WagonSaveData();
        public List<AnimalSaveData> animals = new List<AnimalSaveData>();
        public List<MercenarySaveData> mercenaries = new List<MercenarySaveData>();
        public List<CargoEntrySaveData> cargo = new List<CargoEntrySaveData>();
        public int foodAmount;
        public float foodUnitWeight = 1f;
        public JourneyState state = JourneyState.Prepare;
        public float currentDistanceKm;
        public float totalSeconds;
        public float progress01;
        public bool settlementClaimed;
        public int runCargoLost;
        public float runFoodLost;
        public JourneyFailureReason runFatalReason = JourneyFailureReason.None;
    }

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

    [Serializable]
    public sealed class AnimalSaveData
    {
        public string animalName = string.Empty;
        public float speed = 1f;
        public float foodPerKm;
    }

    [Serializable]
    public sealed class MercenarySaveData
    {
        public string mercName = string.Empty;
        public int combatPower;
        public int contractCount;
    }

    [Serializable]
    public sealed class CargoEntrySaveData
    {
        public TradeItemSaveData item = new TradeItemSaveData();
        public int quantity;
    }

    [Serializable]
    public sealed class TradeItemSaveData
    {
        public string itemId = string.Empty;
        public string itemName = string.Empty;
        public float weight;
        public int basePrice;
    }

    [Serializable]
    public sealed class TradeProgressSaveData
    {
        public string activeTradeId = string.Empty;
        public string activeRouteId = string.Empty;
        public TradeProgressState state;
        public long tradeStartUtcTick;
        public long expectedTradeEndUtcTick;
    }

    [Serializable]
    public sealed class WorldSaveData
    {
        public string currentSeasonId = "summer";
        public string currentDisasterId = string.Empty;
    }

    [Serializable]
    public sealed class TutorialSaveData
    {
        public bool isCompleted;
        public bool isSkipped;
        public int stepIndex;
    }

    public enum TradeProgressState
    {
        None,
        Preparing,
        Traveling,
        SettlementPending,
        Completed,
        Failed
    }
}
