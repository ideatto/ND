[System.Serializable]
public class ModifierInput
{
    public ModifierType modifierType;
    public string sourceId;
    public string displayName;

    public ModifierBundle[] modifierBundles;
}

[System.Serializable]
public class ModifierBundle
{
    public Target modifierTarget;
    public Operation modifierOperation;
    public float value;
}

public enum Target
{
    None,
    BuyPrice,
    SellPrice,
    BaseMoveSpeed
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
    OverSupply,
    AffectToTown
}
