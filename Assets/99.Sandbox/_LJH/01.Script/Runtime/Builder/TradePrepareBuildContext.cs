[System.Serializable]
public sealed class TradePrepareBuildContext
{
    public ND.Framework.SaveData saveData;

    public TownData[] towns = new TownData[0];
    public RouteData[] routes = new RouteData[0];
    public TradeItemData[] tradeItems = new TradeItemData[0];
    public WagonData[] wagons = new WagonData[0];
    public DraftAnimalData[] draftAnimals = new DraftAnimalData[0];
    public MercenaryData[] mercenaries = new MercenaryData[0];
}
