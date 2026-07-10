namespace ND.Economy
{
    /// <summary>
    /// Core가 무역 완료 시 호출하는 M1 Economy 단일 진입점.
    /// 입력 조립, 계산 실행, 성공 시 SaveData 화폐 반영을 한 흐름으로 처리한다.
    /// </summary>
    public static class EconomyM1FlowService
    {
        public static EconomyM1LoopResult ExecuteTradeAndApply(
            global::SaveData saveData,
            global::TradeItemData item,
            global::RouteData route,
            int quantity,
            string tradeId,
            long developmentCurrencyReward,
            bool purchaseGrowth,
            string growthId,
            int playerGrowthLevel,
            int caravanGrowthLevel,
            int growthMaxLevel = 1,
            long growthCostDevelopmentCurrency = 1L)
        {
            EconomyM1LoopInput input = LjhEconomyM1InputAdapter.ToEconomyM1LoopInput(
                saveData,
                item,
                route,
                quantity,
                tradeId,
                developmentCurrencyReward,
                purchaseGrowth,
                growthId,
                playerGrowthLevel,
                caravanGrowthLevel,
                growthMaxLevel,
                growthCostDevelopmentCurrency);

            EconomyM1LoopResult result = EconomyM1LoopCalculator.Execute(input);
            if (result.Success)
            {
                LjhEconomyM1InputAdapter.ApplyFinalCurrencyState(
                    saveData,
                    result.FinalCurrencyState);
            }

            return result;
        }
    }
}
