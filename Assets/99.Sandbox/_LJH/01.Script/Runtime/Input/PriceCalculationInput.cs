using System.Collections.Generic;

[System.Serializable]
public class PriceCalculationInput
{
    public string itemId;
    public string fromTownId;
    public string toTownId;

    public int quantity;
    public long baseBuyPrice;
    public long baseSellPrice;

    public Season season;
    public Disaster disaster;
    public List<string> activeEventIds = new List<string>();

    public int playerGrowthLevel;
    public int caravanGrowthLevel;
    public int oversupplyLevel;

    public List<ModifierInput> modifiers = new List<ModifierInput>();

    public int Quantity => quantity >= 0 ? quantity : 0;
    public long BaseBuyPrice => baseBuyPrice >= 0 ? baseBuyPrice : 0;
    public long BaseSellPrice => baseSellPrice >= 0 ? baseSellPrice : 0;
}
