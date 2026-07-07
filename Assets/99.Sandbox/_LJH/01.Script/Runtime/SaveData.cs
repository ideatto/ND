using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class SaveData
{
    public const int CurrentVersion = 1;

    public int Version = CurrentVersion;
    public long LastSavedUtcTicks;
    
    public PlayerSaveData Player = new PlayerSaveData();
    public CaravanSaveData Caravan = new CaravanSaveData();
    public TradeProgressSaveData TradeProgress = new TradeProgressSaveData();
    public WorldSaveData World = new WorldSaveData();
    public TutorialSaveData Tutorial = new TutorialSaveData();
}

[System.Serializable]
public sealed class  PlayerSaveData
{
    public string CurrentTownId;

    public int TradingCurrency = 1000;
    public int DevelopmentCurrency;
}

[System.Serializable]
public sealed class CaravanSaveData
{
    public float MaxLoad;
    public float CurrentLoad;

    public float CurrentDurability;

    public List<TradeItemBundle> Inventory = new List<TradeItemBundle>();
}

[System.Serializable]
public sealed class TradeProgressSaveData
{
    public string ActiveTradeId = string.Empty;
    public string ActiveRouteId = string.Empty;
    public TradeProgressState State;
    public long TradeStartUtcTick;
    public long ExpectedTradeEndUtcTick;
}

[System.Serializable]
public sealed class WorldSaveData
{
    public string CurrentSeason = "summer";
    public string CurrentDisaster = string.Empty;

    public List<string> UnlockedTownIds = new List<string>();
    public List<string> UnlockedRouteIds = new List<string>();
    public List<string> CompletedRouteIds = new List<string>();
}

[System.Serializable]
public sealed class TutorialSaveData
{
    public bool IsCompleted;
    public bool IsSkipped;
    public int StepIndex;
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
