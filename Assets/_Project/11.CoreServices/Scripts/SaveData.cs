using System;
using System.Collections.Generic;

namespace ND.Framework
{
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

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
        public float maxLoad;
        public float currentLoad;
        public float currentDurability;
        public List<TradeItemBundleSaveData> inventory = new List<TradeItemBundleSaveData>();
    }

    [Serializable]
    public sealed class TradeItemBundleSaveData
    {
        public string itemId = string.Empty;
        public int quantity;
        public int purchaseUnitPrice;
        public int sellUnitPrice;
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
