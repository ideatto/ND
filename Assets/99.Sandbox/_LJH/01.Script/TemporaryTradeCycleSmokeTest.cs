using UnityEngine;

public sealed class TemporaryTradeCycleSmokeTest : MonoBehaviour
{
    [ContextMenu("Run Temporary Trade Cycle Smoke Test")]
    public void Run()
    {
        var sink = new InMemoryTradePrepareCommitSink();
        var settlement = new TemporaryTradeSettlementService(sink);
        var commit = new TradePrepareCommitData
        {
            caravanId = "temporary-smoke-caravan",
            tradeId = "temporary-smoke-trade",
            currentTownId = "temporary-origin-town",
            selectedDestinationTownId = "temporary-destination-town",
            routeId = "temporary-smoke-route",
            selectedWagonId = "temporary-wagon",
            selectedAnimals = new[]
            {
                new DraftAnimalSelectionData
                {
                    draftAnimalId = "temporary-animal",
                    quantity = 2
                }
            },
            purchaseCost = 100L,
            foodCost = 0L,
            mercenaryCost = 1L,
            estimatedSellRevenue = 150L,
            selectedMercenaryIds = new[] { "mercenary-smoke" }
        };

        long currencyAtDeparture = 1000L;
        bool staged = sink.TryStage(commit);
        Debug.Assert(staged, "Temporary trade smoke test failed: commit should be staged.");
        commit.selectedAnimals[0].quantity = 99;
        Debug.Assert(sink.TryGet(commit.tradeId, out TradePrepareCommitData stagedSnapshot)
            && stagedSnapshot.caravanId == "temporary-smoke-caravan"
            && stagedSnapshot.currentTownId == "temporary-origin-town"
            && stagedSnapshot.selectedDestinationTownId == "temporary-destination-town"
            && stagedSnapshot.selectedWagonId == "temporary-wagon"
            && stagedSnapshot.selectedAnimals.Length == 1
            && stagedSnapshot.selectedAnimals[0].quantity == 2,
            "Temporary trade smoke test failed: staged route configuration must be deep-copied.");
        Debug.Assert(currencyAtDeparture == 1000L,
            "Temporary trade smoke test failed: departure must not deduct currency.");

        TemporaryTradeSettlementResult claimed = settlement.TryClaim(
            commit.tradeId,
            currencyAtDeparture);
        Debug.Assert(claimed.succeeded,
            "Temporary trade smoke test failed: first settlement claim should succeed.");
        Debug.Assert(claimed.currencyAfter == 1049L,
            "Temporary trade smoke test failed: settlement result should be 1000 - 100 - 1 + 150.");
        Debug.Assert(sink.Count == 0,
            "Temporary trade smoke test failed: claimed commit must be removed.");

        TemporaryTradeSettlementResult duplicateClaim = settlement.TryClaim(
            commit.tradeId,
            claimed.currencyAfter);
        Debug.Assert(!duplicateClaim.succeeded,
            "Temporary trade smoke test failed: duplicate settlement claim must fail.");

        commit.tradeId = "temporary-insufficient-trade";
        commit.purchaseCost = 2000L;
        commit.estimatedSellRevenue = 0L;
        Debug.Assert(sink.TryStage(commit),
            "Temporary trade smoke test failed: insufficient-funds case should stage.");
        TemporaryTradeSettlementResult insufficient = settlement.TryClaim(commit.tradeId, 1000L);
        Debug.Assert(!insufficient.succeeded
            && insufficient.errorCode == TemporaryTradeSettlementService.ErrorInsufficientCurrency,
            "Temporary trade smoke test failed: insufficient settlement currency should be rejected.");
        Debug.Assert(sink.Count == 1,
            "Temporary trade smoke test failed: rejected settlement must remain staged.");

        sink.Rollback(commit.tradeId);
        Debug.Assert(sink.Count == 0,
            "Temporary trade smoke test failed: rollback should remove staged data.");
        Debug.Log("Temporary trade cycle smoke test PASSED.");
    }
}
