using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
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

[System.Serializable]
public sealed class  PlayerSaveData
{
    public string currentTownId;

    public int tradingCurrency = 1000;
    public int developmentCurrency;
}

[System.Serializable]
public sealed class CaravanSaveData
{
    public float maxLoad;
    public float currentLoad;

    public float currentDurability;

    public List<TradeItemBundle> inventory = new List<TradeItemBundle>();
}

[System.Serializable]
public sealed class TradeProgressSaveData
{
    public string activeTradeId = string.Empty;
    public string activeRouteId = string.Empty;
    public TradeProgressState state;
    public long tradeStartUtcTick;
    public long expectedTradeEndUtcTick;
}

[System.Serializable]
public sealed class WorldSaveData
{
    public string currentSeason = "summer";
    public string currentDisaster = string.Empty;

    public List<string> unlockedTownIds = new List<string>();
    public List<string> unlockedRouteIds = new List<string>();
    public List<string> completedRouteIds = new List<string>();
}

[System.Serializable]
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
