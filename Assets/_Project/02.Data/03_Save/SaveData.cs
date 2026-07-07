using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public int Version = 1;

    public PlayerSaveData Player = new PlayerSaveData();
    public CaravanSaveData Caravan = new CaravanSaveData();
    public TradeProgressSaveData CurrentTrade = new TradeProgressSaveData();
    public TutorialSaveData Tutorial = new TutorialSaveData();
}

[Serializable]
public class PlayerSaveData
{
    public string CurrentTownId;
    public int Money;

    public List<string> UnlockedTownIds = new List<string>();
    public List<string> CompletedRouteIds = new List<string>();
}

[Serializable]
public class CaravanSaveData
{
    public int MaxLoad;
    public int CurrentLoad;

    public List<TradeItemStackData> Inventory = new List<TradeItemStackData>();
}

[Serializable]
public class TradeProgressSaveData : TradeRecordData
{
    public TradeProgressState State;

    public int EstimatedCompleteDay;
}

[Serializable]
public class TutorialSaveData
{
    public bool IsCompleted;
    public string CurrentStepId;
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
