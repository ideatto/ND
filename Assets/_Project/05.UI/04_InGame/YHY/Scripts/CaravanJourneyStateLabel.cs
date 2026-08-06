namespace ND.UI
{
    /// <summary>Shared presentation labels for Caravan journey states.</summary>
    public static class CaravanJourneyStateLabel
    {
        public static string Format(JourneyState state)
        {
            switch (state)
            {
                case JourneyState.Prepare: return "준비 중";
                case JourneyState.Traveling: return "무역 중";
                case JourneyState.Selling: return "판매 대기";
                case JourneyState.Settling: return "정산 대기";
                case JourneyState.Completed: return "무역 완료";
                default: return "알 수 없음";
            }
        }
    }
}
