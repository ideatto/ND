namespace ND.Framework
{
    public sealed class InGameScreenStateRouter
    {
        private bool hasCurrentScreenState;

        public InGameScreenState CurrentScreenState { get; private set; }

        public void RefreshFromSaveData(SaveData saveData, bool forceNotify = false)
        {
            RequestScreen(MapFromSaveData(saveData), forceNotify);
        }

        public void RequestScreen(InGameScreenState screenState, bool forceNotify = false)
        {
            if (hasCurrentScreenState && CurrentScreenState == screenState && !forceNotify)
            {
                return;
            }

            hasCurrentScreenState = true;
            CurrentScreenState = screenState;
            FrameworkEvents.RaiseInGameScreenChanged(screenState);
        }

        public static InGameScreenState MapFromSaveData(SaveData saveData)
        {
            if (saveData == null || saveData.tradeProgress == null)
            {
                return InGameScreenState.Preparation;
            }

            return MapFromTradeProgressState(saveData.tradeProgress.state);
        }

        public static InGameScreenState MapFromTradeProgressState(TradeProgressState progressState)
        {
            switch (progressState)
            {
                case TradeProgressState.Traveling:
                    return InGameScreenState.Traveling;
                case TradeProgressState.SettlementPending:
                    return InGameScreenState.Settlement;
                case TradeProgressState.None:
                case TradeProgressState.Preparing:
                case TradeProgressState.Completed:
                case TradeProgressState.Failed:
                default:
                    return InGameScreenState.Preparation;
            }
        }
    }
}
