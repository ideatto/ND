using System;

public sealed class TestTradePrepareCaravanOptionProvider : ITradePrepareCaravanOptionProvider
{
    // Mirrors the agreed maximum so UI and contract tests can exercise every visible preset slot.
    public const int MaxOptionCount = 4;
    public const string PrepareCaravanId = "test-caravan-prepare";
    public const string TravelingCaravanId = "test-caravan-traveling";
    public const string SettlingCaravanId = "test-caravan-settling";
    public const string CompletedCaravanId = "test-caravan-completed";

    public TradePrepareCaravanOptionViewData[] GetOptions()
    {
        // Every call creates a fresh graph so UI-side mutations cannot corrupt later snapshots.
        return new[]
        {
            CreateOption(
                PrepareCaravanId,
                "Preparation Caravan",
                JourneyState.Prepare,
                true,
                string.Empty),
            CreateOption(
                TravelingCaravanId,
                "Traveling Caravan",
                JourneyState.Traveling,
                false,
                "A traveling Caravan cannot start another trade."),
            CreateOption(
                SettlingCaravanId,
                "Settlement Caravan",
                JourneyState.Settling,
                false,
                "Claim the pending settlement before reusing this Caravan."),
            CreateOption(
                CompletedCaravanId,
                "Completed Caravan",
                JourneyState.Completed,
                false,
                "Reset the completed journey before starting another trade.")
        };
    }

    private static TradePrepareCaravanOptionViewData CreateOption(
        string caravanId,
        string displayName,
        JourneyState state,
        bool canSelect,
        string disabledReason)
    {
        return new TradePrepareCaravanOptionViewData
        {
            caravanId = caravanId,
            displayName = displayName,
            currentTownId = "test-town",
            state = state,
            canSelect = canSelect,
            disabledReason = disabledReason
        };
    }
}
