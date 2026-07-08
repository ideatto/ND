[System.Serializable]
public class PriceModifierInput
{
    public ModifierType priceModifierType;
    public string sourceId;
    public string displayName;
    public Target priceModifierTarget;
    public Operation priceModifierOperation;
    public float value;
}

public enum Target
{
    None,
    BuyPrice,
    SellPrice
}

public enum Operation
{
    None,
    Add,
    Subtract,
    Percent
}

public enum ModifierType
{
    None,
    Season,
    Disaster,
    ActiveEvent,
    PlayerGrowth,
    CaravanGrowth,
    OverSupply
}
