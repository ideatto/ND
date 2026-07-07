using UnityEngine;

[System.Serializable]
public class PriceModifierInput
{
    [SerializeField] private ModifierType priceModifierType;
    [SerializeField] private string sourceId;
    [SerializeField] private Target priceModifierTarget;
    [SerializeField] private Operation priceModifierOperation;
    [SerializeField] private float value;

    #region
    public ModifierType PriceModifierType => priceModifierType;
    public string SourceId => sourceId;
    public Target PriceModifierTarget => priceModifierTarget;
    public Operation PriceModifierOperation => priceModifierOperation;
    public float Value => value;
    #endregion
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
    Subtraction,
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
