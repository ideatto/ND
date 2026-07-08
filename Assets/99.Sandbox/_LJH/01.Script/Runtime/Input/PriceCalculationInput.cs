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

    public int seasonId;
    public int disasterId;
    public List<string> activeEventIds = new List<string>();

    public int playerGrowthLevel;
    public int caravanGrowthLevel;
    public int oversupplyLevel;

    public List<PriceModifierInput> modifiers = new List<PriceModifierInput>();
}