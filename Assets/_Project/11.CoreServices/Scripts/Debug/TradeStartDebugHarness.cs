/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - 무역 출발, 진행, 정산 claim 흐름을 Unity Inspector ContextMenu로 수동 검증한다.
 * - FrameworkRoot의 TradeStartService와 TradeProgressCoordinator를 개발 중 빠르게 호출할 수 있게 한다.
 *
 * Main Features
 * - 샘플 caravan 생성, 무역 시작, 저장 데이터 출력, 진행률 확인, 강제 완료, 정산 claim을 제공한다.
 * - 낮은 식량 실패 케이스와 3회 연속 loop smoke test를 제공한다.
 *
 * Usage for Team Members
 * - debug용 GameObject에 component로 추가한 뒤 ContextMenu 항목을 실행한다.
 * - FillSampleCaravan()으로 테스트 caravan을 채운 뒤 StartTradeAndRecordTime()을 호출하는 흐름을 권장한다.
 *
 * Main Public APIs
 * - FillSampleCaravan(): debug caravan을 기본값으로 채운다.
 * - StartTradeAndRecordTime(): 무역 출발과 기록을 시도한다.
 * - CheckTradeProgressAndCompletion(): 진행률 갱신과 정산 생성을 확인한다.
 * - RunM1LoopIntegritySmoke(): 출발-정산-claim loop를 3회 검증한다.
 *
 * Important Notes
 * - 이 스크립트는 개발 검증용이며 runtime gameplay flow의 필수 구성 요소가 아니다.
 * - FrameworkRoot.Instance가 준비되지 않으면 대부분의 작업은 warning 후 중단된다.
 */
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// Inspector ContextMenu로 무역 출발부터 정산 claim까지 수동 검증하는 debug harness이다.
    /// </summary>
    public sealed class TradeStartDebugHarness : MonoBehaviour
    {
        [SerializeField] private string tradeId = "debug_trade_001";
        [SerializeField] private string routeId = "debug_route_001";
        [SerializeField] private float distanceKm = 100f;
        [SerializeField] private CaravanData caravan = new CaravanData();

        /// <summary>
        /// 테스트에 사용할 샘플 caravan 데이터를 생성한다.
        /// </summary>
        [ContextMenu("Framework/Fill Sample Caravan")]
        public void FillSampleCaravan()
        {
            // 출발 검증을 통과할 수 있는 최소 wagon, 동물, cargo 데이터를 구성한다.
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

        /// <summary>
        /// 현재 debug caravan으로 무역 출발을 시도하고 시작 시간을 저장 데이터에 기록한다.
        /// </summary>
        [ContextMenu("Framework/Start Trade And Record Time")]
        public void StartTradeAndRecordTime()
        {
            // FrameworkRoot가 없으면 debug harness가 service를 직접 만들지 않고 호출을 중단한다.
            if (FrameworkRoot.Instance == null || FrameworkRoot.Instance.TradeStart == null)
            {
                FrameworkLog.Warning("Trade start debug skipped because FrameworkRoot is not ready.");
                return;
            }

            var tradeStart = FrameworkRoot.Instance.TradeStart;
            var result = tradeStart.TryStartTrade(caravan, distanceKm, tradeId, routeId);
            // Core 검증 실패 사유를 그대로 로그에 남겨 테스트 데이터 조정을 쉽게 한다.
            if (!result.canDepart)
            {
                FrameworkLog.Warning($"Debug trade could not depart. Reasons: {string.Join(", ", result.reasons)}");
                return;
            }

            if (tradeStart.LastRecordSucceeded)
            {
                // 이후 진행률과 정산 검증이 같은 runtime caravan을 사용하도록 coordinator에 연결한다.
                FrameworkRoot.Instance.TradeProgressCoordinator?.SetActiveCaravan(caravan);
                FrameworkLog.Info($"Debug trade started and recorded. TradeId: {tradeId}, RouteId: {routeId}");
                return;
            }

            FrameworkLog.Warning($"Debug trade departed, but start time was not recorded. TradeId: {tradeId}, RouteId: {routeId}");
        }

        /// <summary>
        /// 현재 저장 데이터를 JSON으로 출력한다.
        /// </summary>
        [ContextMenu("Framework/Print Save Data")]
        public void PrintSaveData()
        {
            // root 또는 저장 데이터가 없으면 JsonUtility 출력 대상이 없으므로 중단한다.
            if (FrameworkRoot.Instance == null || FrameworkRoot.Instance.CurrentSaveData == null)
            {
                FrameworkLog.Warning("Save data print skipped because FrameworkRoot is not ready.");
                return;
            }

            FrameworkLog.Info($"Current save data:\n{JsonUtility.ToJson(FrameworkRoot.Instance.CurrentSaveData, true)}");
        }

        /// <summary>
        /// active trade 진행률을 갱신하고 도착 시 settlement 생성을 확인한다.
        /// </summary>
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
            // coordinator가 복원하거나 갱신한 caravan을 Inspector에서 이어서 확인할 수 있게 보관한다.
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

        /// <summary>
        /// 현재 active trade를 즉시 완료하도록 요청한다.
        /// </summary>
        [ContextMenu("Framework/Force Complete Active Trade")]
        public void ForceCompleteActiveTrade()
        {
            // debug command가 준비되지 않으면 이벤트를 발행할 수 없으므로 중단한다.
            if (FrameworkRoot.Instance == null || FrameworkRoot.Instance.DebugCommands == null)
            {
                FrameworkLog.Warning("Immediate completion skipped because FrameworkRoot is not ready.");
                return;
            }

            FrameworkRoot.Instance.DebugCommands.CompleteTradeImmediately();
        }

        /// <summary>
        /// pending settlement를 claim하고 caravan을 준비 상태로 되돌린다.
        /// </summary>
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
            // reset 이후 상태를 Inspector에서 확인할 수 있도록 local caravan 참조를 최신화한다.
            if (activeCaravan != null)
            {
                caravan = activeCaravan;
            }

            FrameworkLog.Info($"Settlement claim and reset result: {claimed}. State: {caravan.state}");
        }

        /// <summary>
        /// 식량 부족 실패 케이스를 만들기 위해 debug caravan의 식량을 낮춘다.
        /// </summary>
        [ContextMenu("Framework/Set Low Food Failure Case")]
        public void SetLowFoodFailureCase()
        {
            // Core 실패 조건을 빠르게 재현하기 위한 테스트 데이터만 변경한다.
            caravan.foodAmount = 1;
            FrameworkLog.Info("Debug caravan food was lowered for failure testing.");
        }

        /// <summary>
        /// 출발, 강제 완료, 중복 정산 방지, claim reset을 3회 연속 검증한다.
        /// </summary>
        /// <remarks>
        /// 실패 조건을 발견하면 즉시 warning을 남기고 smoke test를 중단한다.
        /// </remarks>
        [ContextMenu("Framework/Run M1 Loop Integrity Smoke")]
        public void RunM1LoopIntegritySmoke()
        {
            var coordinator = GetCoordinator();
            // smoke test는 시작 서비스와 coordinator가 모두 준비된 상태에서만 의미가 있다.
            if (coordinator == null || FrameworkRoot.Instance == null || FrameworkRoot.Instance.TradeStart == null)
            {
                return;
            }

            for (var cycleIndex = 0; cycleIndex < 3; cycleIndex++)
            {
                // 각 cycle은 새 caravan과 고유 trade ID로 시작해 이전 상태 잔여물을 검증한다.
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
                // 강제 완료 후 settlement result가 없으면 완료 이벤트와 정산 생성 경로가 끊긴 것이다.
                if (coordinator.LastSettlementResult == null)
                {
                    FrameworkLog.Warning($"M1 smoke failed because settlement result was missing in cycle {cycleIndex + 1}.");
                    return;
                }

                var repeatedProgressCheck = coordinator.CheckProgressAndCompletion();
                // settlement pending 상태에서 progress check가 또 true이면 중복 settlement 생성 버그로 판단한다.
                if (repeatedProgressCheck)
                {
                    FrameworkLog.Warning($"M1 smoke failed because settlement was recreated in cycle {cycleIndex + 1}.");
                    return;
                }

                var firstClaim = coordinator.ClaimSettlementAndReset();
                var duplicateClaim = coordinator.ClaimSettlementAndReset();
                // 첫 claim만 성공하고 같은 settlement의 두 번째 claim은 실패해야 한다.
                if (!firstClaim || duplicateClaim)
                {
                    FrameworkLog.Warning(
                        $"M1 smoke failed claim validation in cycle {cycleIndex + 1}. First: {firstClaim}, Duplicate: {duplicateClaim}");
                    return;
                }

                var activeCaravan = coordinator.ActiveCaravan;
                // claim 후 caravan이 준비 상태로 돌아와야 다음 무역 loop를 시작할 수 있다.
                if (activeCaravan == null || activeCaravan.state != JourneyState.Prepare)
                {
                    FrameworkLog.Warning($"M1 smoke failed to return to preparation in cycle {cycleIndex + 1}.");
                    return;
                }

                // settlement cache가 남아 있으면 다음 cycle에서 stale result가 표시될 수 있다.
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
