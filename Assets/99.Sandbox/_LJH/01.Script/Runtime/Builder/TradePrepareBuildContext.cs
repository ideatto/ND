[System.Serializable]
public sealed class TradePrepareBuildContext
{
    public ND.Framework.SaveData saveData;

    // Supplies prevalidated departure choices without coupling the builder to Overview focus state.
    // A real multi-Caravan Provider will replace temporary option data at integration time.
    public TradePrepareCaravanOptionViewData[] caravanOptions = new TradePrepareCaravanOptionViewData[0];

    public TownData[] towns = new TownData[0];
    public RouteData[] routes = new RouteData[0];
    public TradeItemData[] tradeItems = new TradeItemData[0];
    public WagonData[] wagons = new WagonData[0];
    public DraftAnimalData[] draftAnimals = new DraftAnimalData[0];
    public MercenaryData[] mercenaries = new MercenaryData[0];
}
