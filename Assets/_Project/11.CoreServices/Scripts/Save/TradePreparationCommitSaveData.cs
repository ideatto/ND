using System;
using System.Collections.Generic;

namespace ND.Framework
{
    [Serializable]
    public sealed class TradePreparationCommitSaveData
    {
        public bool hasCommit;
        public string tradeId = string.Empty;
        public string currentTownId = string.Empty;
        public string destinationTownId = string.Empty;
        public string routeId = string.Empty;
        public string wagonId = string.Empty;
        public long purchaseCost;
        public long foodCost;
        public long mercenaryCost;
        public long estimatedSellRevenue;
        public List<TradePreparationAnimalSaveData> animals = new List<TradePreparationAnimalSaveData>();
        public List<TradePreparationItemSaveData> purchasedItems = new List<TradePreparationItemSaveData>();
        public List<string> mercenaryIds = new List<string>();
    }

    [Serializable]
    public sealed class TradePreparationAnimalSaveData
    {
        public string animalId = string.Empty;
        public int quantity;
    }

    [Serializable]
    public sealed class TradePreparationItemSaveData
    {
        public string itemId = string.Empty;
        public int quantity;
        public long purchaseUnitPrice;
        public long sellUnitPrice;
    }
}
