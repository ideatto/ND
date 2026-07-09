using UnityEngine;

namespace ND.Framework
{
    public sealed class TradeStartDebugHarness : MonoBehaviour
    {
        [SerializeField] private string tradeId = "debug_trade_001";
        [SerializeField] private string routeId = "debug_route_001";
        [SerializeField] private float distanceKm = 100f;
        [SerializeField] private CaravanData caravan = new CaravanData();

        [ContextMenu("Framework/Fill Sample Caravan")]
        public void FillSampleCaravan()
        {
            caravan = new CaravanData
            {
                wagon = new imsiWagonData
                {
                    wagonName = "Debug Wagon",
                    overLoad = 30f,
                    maxLoad = 60f,
                    minAnimals = 1,
                    maxAnimals = 5
                },
                foodAmount = 30
            };

            caravan.animals.Add(new imsiAnimalData { animalName = "Debug Horse", foodPerKm = 0.1f });
            caravan.animals.Add(new imsiAnimalData { animalName = "Debug Horse", foodPerKm = 0.1f });

            var item = new imsiTradeItemData
            {
                id = "debug_item_wheat",
                itemName = "Debug Wheat",
                weight = 5f,
                basePrice = 10
            };
            caravan.cargo.Add(new CargoEntry { item = item, quantity = 5 });
            caravan.currentDurability = caravan.wagon.maxDurability;

            FrameworkLog.Info("Sample caravan filled for trade start debug.");
        }

        [ContextMenu("Framework/Start Trade And Record Time")]
        public void StartTradeAndRecordTime()
        {
            if (FrameworkRoot.Instance == null || FrameworkRoot.Instance.TradeStart == null)
            {
                FrameworkLog.Warning("Trade start debug skipped because FrameworkRoot is not ready.");
                return;
            }

            var tradeStart = FrameworkRoot.Instance.TradeStart;
            var result = tradeStart.TryStartTrade(caravan, distanceKm, tradeId, routeId);
            if (!result.canDepart)
            {
                FrameworkLog.Warning($"Debug trade could not depart. Reasons: {string.Join(", ", result.reasons)}");
                return;
            }

            if (tradeStart.LastRecordSucceeded)
            {
                FrameworkRoot.Instance.TradeProgressCoordinator?.SetActiveCaravan(caravan);
                FrameworkLog.Info($"Debug trade started and recorded. TradeId: {tradeId}, RouteId: {routeId}");
                return;
            }

            FrameworkLog.Warning($"Debug trade departed, but start time was not recorded. TradeId: {tradeId}, RouteId: {routeId}");
        }

        [ContextMenu("Framework/Print Save Data")]
        public void PrintSaveData()
        {
            if (FrameworkRoot.Instance == null || FrameworkRoot.Instance.CurrentSaveData == null)
            {
                FrameworkLog.Warning("Save data print skipped because FrameworkRoot is not ready.");
                return;
            }

            FrameworkLog.Info($"Current save data:\n{JsonUtility.ToJson(FrameworkRoot.Instance.CurrentSaveData, true)}");
        }

        [ContextMenu("Framework/Check Trade Progress And Completion")]
        public void CheckTradeProgressAndCompletion()
        {
            var coordinator = GetCoordinator();
            if (coordinator == null)
            {
                return;
            }

            var settlementReady = coordinator.CheckProgressAndCompletion();
            var activeCaravan = coordinator.ActiveCaravan;
            if (activeCaravan != null)
            {
                caravan = activeCaravan;
                FrameworkLog.Info(
                    $"Trade progress checked. State: {caravan.state}, Progress: {caravan.progress01:0.###}, SettlementReady: {settlementReady}");
            }

            if (coordinator.LastSettlementResult != null)
            {
                FrameworkLog.Info($"Settlement result: {coordinator.LastSettlementResult.grade}");
            }
        }

        [ContextMenu("Framework/Force Complete Active Trade")]
        public void ForceCompleteActiveTrade()
        {
            if (FrameworkRoot.Instance == null || FrameworkRoot.Instance.DebugCommands == null)
            {
                FrameworkLog.Warning("Immediate completion skipped because FrameworkRoot is not ready.");
                return;
            }

            FrameworkRoot.Instance.DebugCommands.CompleteTradeImmediately();
        }

        [ContextMenu("Framework/Claim Settlement And Reset")]
        public void ClaimSettlementAndReset()
        {
            var coordinator = GetCoordinator();
            if (coordinator == null)
            {
                return;
            }

            var claimed = coordinator.ClaimSettlementAndReset();
            var activeCaravan = coordinator.ActiveCaravan;
            if (activeCaravan != null)
            {
                caravan = activeCaravan;
            }

            FrameworkLog.Info($"Settlement claim and reset result: {claimed}. State: {caravan.state}");
        }

        [ContextMenu("Framework/Set Low Food Failure Case")]
        public void SetLowFoodFailureCase()
        {
            caravan.foodAmount = 1;
            FrameworkLog.Info("Debug caravan food was lowered for failure testing.");
        }

        [ContextMenu("Framework/Run M1 Loop Integrity Smoke")]
        public void RunM1LoopIntegritySmoke()
        {
            var coordinator = GetCoordinator();
            if (coordinator == null || FrameworkRoot.Instance == null || FrameworkRoot.Instance.TradeStart == null)
            {
                return;
            }

            for (var cycleIndex = 0; cycleIndex < 3; cycleIndex++)
            {
                FillSampleCaravan();

                var smokeTradeId = $"{tradeId}_smoke_{cycleIndex + 1}";
                var startResult = FrameworkRoot.Instance.TradeStart.TryStartTrade(
                    caravan,
                    distanceKm,
                    smokeTradeId,
                    routeId);
                if (!startResult.canDepart || !FrameworkRoot.Instance.TradeStart.LastRecordSucceeded)
                {
                    FrameworkLog.Warning($"M1 smoke failed to start cycle {cycleIndex + 1}.");
                    return;
                }

                coordinator.SetActiveCaravan(caravan);
                coordinator.ForceCompleteActiveTrade();
                if (coordinator.LastSettlementResult == null)
                {
                    FrameworkLog.Warning($"M1 smoke failed because settlement result was missing in cycle {cycleIndex + 1}.");
                    return;
                }

                var repeatedProgressCheck = coordinator.CheckProgressAndCompletion();
                if (repeatedProgressCheck)
                {
                    FrameworkLog.Warning($"M1 smoke failed because settlement was recreated in cycle {cycleIndex + 1}.");
                    return;
                }

                var firstClaim = coordinator.ClaimSettlementAndReset();
                var duplicateClaim = coordinator.ClaimSettlementAndReset();
                if (!firstClaim || duplicateClaim)
                {
                    FrameworkLog.Warning(
                        $"M1 smoke failed claim validation in cycle {cycleIndex + 1}. First: {firstClaim}, Duplicate: {duplicateClaim}");
                    return;
                }

                var activeCaravan = coordinator.ActiveCaravan;
                if (activeCaravan == null || activeCaravan.state != JourneyState.Prepare)
                {
                    FrameworkLog.Warning($"M1 smoke failed to return to preparation in cycle {cycleIndex + 1}.");
                    return;
                }

                if (coordinator.LastSettlementResult != null)
                {
                    FrameworkLog.Warning($"M1 smoke failed because settlement cache remained after cycle {cycleIndex + 1}.");
                    return;
                }
            }

            FrameworkLog.Info("M1 loop integrity smoke completed 3 consecutive trade cycles.");
        }

        private static TradeProgressCoordinator GetCoordinator()
        {
            if (FrameworkRoot.Instance == null || FrameworkRoot.Instance.TradeProgressCoordinator == null)
            {
                FrameworkLog.Warning("Trade progress coordinator is not ready.");
                return null;
            }

            return FrameworkRoot.Instance.TradeProgressCoordinator;
        }
    }
}
