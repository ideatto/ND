using System.Collections.Generic;

[System.Serializable]
public class SettlementInput
{
    public string tradeId;
    public string routeId;
    public string fromTownId;
    public string toTownId;

    public List<SoldItemInput> soldItems = new List<SoldItemInput>();

    public int tradeMoneyBefore;
    public int foodCost;
    public int mercenaryCost;
    public int cartRepairCost;
    public int lostItemValue;
    public int eventProfit;
    public int eventLoss;
    public int loanRepayment;
    public int developmentCurrencyReward;
}