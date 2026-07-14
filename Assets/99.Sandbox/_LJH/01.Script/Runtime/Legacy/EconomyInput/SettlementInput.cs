using System.Collections.Generic;

[System.Serializable]
public class SettlementInput
{
    public string tradeId;
    public string routeId;
    public string fromTownId;
    public string toTownId;

    public List<SoldItemInput> soldItems = new List<SoldItemInput>();

    public long tradeMoneyBefore;
    public long foodCost;
    public long mercenaryCost;
    public long cartRepairCost;
    public long lostItemValue;
    public long eventProfit;
    public long eventLoss;
    public long loanRepayment;
    public long developmentCurrencyReward;
}
