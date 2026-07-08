using System;
using System.Collections.Generic;

namespace ND.Economy
{
    public enum PriceModifierType
    {
        Base = 0,
        Town = 10,
        Season = 20,
        Disaster = 30,
        RouteEvent = 40,
        Oversupply = 50,
        PlayerGrowth = 60,
        CaravanGrowth = 70,
        Debug = 999
    }

    public enum PriceModifierTarget
    {
        BuyPrice,
        SellPrice,
        Both
    }

    public enum PriceModifierOperation
    {
        Add,
        Multiply,
        Percent
    }

    [Serializable]
    public sealed class PriceModifierInput
    {
        public PriceModifierType ModifierType;
        public string SourceId = string.Empty;
        public string DisplayNameKey = string.Empty;
        public PriceModifierTarget Target = PriceModifierTarget.Both;
        public PriceModifierOperation Operation = PriceModifierOperation.Percent;
        public float Value;
    }

    [Serializable]
    public sealed class PriceCalculationInput
    {
        public string ItemId = string.Empty;
        public string FromTownId = string.Empty;
        public string ToTownId = string.Empty;
        public string RouteId = string.Empty;
        public int Quantity;
        public int BaseBuyPrice;
        public int BaseSellPrice;
        public string SeasonId = string.Empty;
        public string DisasterId = string.Empty;
        public List<string> ActiveEventIds = new List<string>();
        public int PlayerGrowthLevel;
        public int CaravanGrowthLevel;
        public int OversupplyLevel;
        public List<PriceModifierInput> Modifiers = new List<PriceModifierInput>();
    }

    [Serializable]
    public sealed class PriceModifierBreakdown
    {
        public PriceModifierType ModifierType;
        public string SourceId = string.Empty;
        public string DisplayNameKey = string.Empty;
        public PriceModifierTarget Target;
        public PriceModifierOperation Operation;
        public float Value;
        public int AmountDelta;
    }

    [Serializable]
    public sealed class PriceCalculationResult
    {
        public string ItemId = string.Empty;
        public int Quantity;
        public int UnitBuyPrice;
        public int UnitSellPrice;
        public int TotalBuyPrice;
        public int TotalSellPrice;
        public int ExpectedGrossProfit;
        public List<PriceModifierBreakdown> Modifiers = new List<PriceModifierBreakdown>();
        public bool IsValid;
        public string ErrorCode = string.Empty;
    }
}
