using System;

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
        public WorldSaveData world = new WorldSaveData();
        public TutorialSaveData tutorial = new TutorialSaveData();
    }

    [Serializable]
    public sealed class PlayerSaveData
    {
        public int tradingCurrency = 1000;
        public int developmentCurrency;
    }

    [Serializable]
    public sealed class CaravanSaveData
    {
        public string activeTradeId = string.Empty;
        public long tradeStartUtcTicks;
        public long expectedTradeEndUtcTicks;
        public bool hasPendingSettlement;
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
}
