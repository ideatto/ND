using System.Collections.Generic;

[System.Serializable]
public class PriceCalculationInput
{
    public string itemId;
    public string fromTownId;
    public string toTownId;

    public int quantity;
    public int baseBuyPrice;
    public int baseSellPrice;

    public Season season;
    public Disaster disaster;
    public List<string> activeEventIds = new List<string>();

    public int playerGrowthLevel;
    public int caravanGrowthLevel;
    public int oversupplyLevel;

    public List<PriceModifierInput> modifiers = new List<PriceModifierInput>();

    public int Quantity => quantity >= 0 ? quantity : 0;
    public int BaseBuyPrice => baseBuyPrice >= 0 ? baseBuyPrice : 0;
    public int BaseSellPrice => baseSellPrice >= 0 ? baseSellPrice : 0;
}
