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
 * - 낮은 식량 실패 케이스, 3회 연속 loop smoke test, Economy E2E smoke test, 인게임 식량 소모 smoke test를 제공한다.
 * - Pause 중 식량 elapsed 정지 smoke, Failed 정산 화면 smoke, Force* World debug smoke를 제공한다.
 * - PendingSettlementSaveData 저장 후 세션 캐시 소실·복구·claim smoke를 제공한다.
 * - Offline Traveling 복구·완료·역행 smoke를 제공한다.
 * - ForceSeason / ForceDisaster / ForceRouteEvent ContextMenu로 M2 월드 debug API를 호출한다.
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
 * - RunEconomyE2ESmoke(): settle preview 후 currency 불변, claim 후 currency 변화를 3회 검증한다.
 * - RunInGameFoodConsumptionSmoke(): 인게임 배율에 따른 식량 소모 연동을 검증한다.
 * - RunPauseFoodFreezeSmoke(): Pause 중 elapsed/식량이 증가하지 않는지 검증한다.
 * - RunFailedSettlementScreenSmoke(): Failed grade 정산 화면 진입과 claim 후 Preparation 복귀를 검증한다.
 * - RunPendingSettlementRestoreSmoke(): pendingSettlement 저장·캐시 소실·복구·claim을 검증한다.
 * - RunOfflineProgressSmoke(): Traveling 오프라인 미완료·완료·재호출·역행을 검증한다.
 * - RunForceWorldDebugSmoke(): ForceSeason/Disaster/RouteEvent 기본 재현을 검증한다.
 * - ForceSeason() / ForceDisaster() / ForceRouteEvent(): WorldSaveData 또는 Traveling inject hook을 검증한다.
 *
 * Important Notes
 * - 이 스크립트는 개발 검증용이며 runtime gameplay flow의 필수 구성 요소가 아니다.
 * - M2 출발 검증(BrokenWagon, MixedAnimalType, SlotExceeded)을 통과하는 샘플 caravan을 구성한다.
 * - Related Documentation: Docs/Personal_Documents/CSU/m3-offline-progress-pipeline.md
 */
using System;
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// Inspector ContextMenu로 무역 출발부터 정산 claim까지 수동 검증하는 debug harness이다.
    /// </summary>
    public sealed class TradeStartDebugHarness : MonoBehaviour
    {
        [SerializeField] private string tradeId = "debug_trade_001";
        [SerializeField] private string routeId = "dummyroute";
        [SerializeField] private float distanceKm = 100f;
        [SerializeField] private float starveGraceSeconds = 5f;
        [SerializeField] private CaravanData caravan = new CaravanData();

        [Tooltip("ForceSeason ContextMenu에 사용할 계절 ID입니다.")]
        [SerializeField] private string debugSeasonId = "winter";

        [Tooltip("ForceDisaster ContextMenu에 사용할 재난 ID입니다. 빈 문자열이면 재난 없음을 의미합니다.")]
        [SerializeField] private string debugDisasterId = "drought";

        [Tooltip("ForceRouteEvent ContextMenu에 사용할 route event ID입니다. Traveling trade가 필요합니다.")]
        [SerializeField] private string debugRouteEventId = "debug_route_event_001";

        /// <summary>
        /// Day 단위 raw 식량 소모율 샘플. InGameTimePolicyConfig.FoodConsumptionUnit=Day일 때 인게임 초당 0.1로 정규화된다.
        /// </summary>
        private const float SampleRawFoodConsumptionPerDay = 8640f;

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
                    maxAnimals = 5,
                    maxDurability = 100,
                    inventorySlotCount = 8
                },
                foodAmount = 30,
                starveGraceSeconds = starveGraceSeconds
            };

            caravan.animals.Add(new imsiAnimalData
            {
                animalName = "Debug Horse",
                foodPerKm = SampleRawFoodConsumptionPerDay,
                animalType = DraftAnimalType.Horse,
                increaseOverLoad = 5f
            });
            caravan.animals.Add(new imsiAnimalData
            {
                animalName = "Debug Horse",
                foodPerKm = SampleRawFoodConsumptionPerDay,
                animalType = DraftAnimalType.Horse,
                increaseOverLoad = 5f
            });

            var item = new imsiTradeItemData
            {
                id = "dummyitem",
                itemName = "Debug Wheat",
                weight = 5f,
                basePrice = 10,
                maxCount = 10
            };
            caravan.cargo.Add(new CargoEntry { item = item, quantity = 5 });
            caravan.currentDurability = caravan.wagon.maxDurability;

            ApplyConsumptionRateNormalization();

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
            ApplyConsumptionRateNormalization();
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

        /// <summary>
        /// SharedGameData와 Economy settle preview/claim apply를 포함해 3회 연속 E2E를 검증한다.
        /// </summary>
        /// <remarks>
        /// Boot flow로 InGame에 진입한 뒤 실행해야 SharedGameData가 로드되어 Economy preview가 skip되지 않는다.
        /// </remarks>
        [ContextMenu("Framework/Run Economy E2E Smoke")]
        public void RunEconomyE2ESmoke()
        {
            var coordinator = GetCoordinator();
            if (coordinator == null || FrameworkRoot.Instance == null || FrameworkRoot.Instance.TradeStart == null)
            {
                return;
            }

            var sharedGameData = FrameworkRoot.Instance.SharedGameData;
            if (sharedGameData == null || !sharedGameData.IsLoaded)
            {
                FrameworkLog.Warning("Economy E2E smoke skipped because shared game data is not loaded. Use Boot flow.");
                return;
            }

            var saveData = FrameworkRoot.Instance.CurrentSaveData;
            if (saveData == null || saveData.player == null)
            {
                FrameworkLog.Warning("Economy E2E smoke skipped because save data is not ready.");
                return;
            }

            for (var cycleIndex = 0; cycleIndex < 3; cycleIndex++)
            {
                FillSampleCaravan();

                var smokeTradeId = $"{tradeId}_economy_{cycleIndex + 1}";
                var currencyBeforeCycle = saveData.player.tradingCurrency;

                var startResult = FrameworkRoot.Instance.TradeStart.TryStartTrade(
                    caravan,
                    distanceKm,
                    smokeTradeId,
                    routeId);
                if (!startResult.canDepart || !FrameworkRoot.Instance.TradeStart.LastRecordSucceeded)
                {
                    FrameworkLog.Warning($"Economy E2E smoke failed to start cycle {cycleIndex + 1}.");
                    return;
                }

                coordinator.SetActiveCaravan(caravan);
                coordinator.ForceCompleteActiveTrade();

                var currencyAfterSettle = saveData.player.tradingCurrency;
                if (currencyAfterSettle != currencyBeforeCycle)
                {
                    FrameworkLog.Warning(
                        $"Economy E2E smoke failed in cycle {cycleIndex + 1}: tradingCurrency changed after settle. Before: {currencyBeforeCycle}, After settle: {currencyAfterSettle}");
                    return;
                }

                if (coordinator.LastSettlementResult == null)
                {
                    FrameworkLog.Warning($"Economy E2E smoke failed because settlement result was missing in cycle {cycleIndex + 1}.");
                    return;
                }

                if (!coordinator.ClaimSettlementAndReset())
                {
                    FrameworkLog.Warning($"Economy E2E smoke failed because claim failed in cycle {cycleIndex + 1}.");
                    return;
                }

                var currencyAfterClaim = saveData.player.tradingCurrency;
                if (currencyAfterClaim == currencyAfterSettle)
                {
                    FrameworkLog.Warning(
                        $"Economy E2E smoke failed in cycle {cycleIndex + 1}: tradingCurrency unchanged after claim. Value: {currencyAfterClaim}");
                    return;
                }

                if (currencyAfterClaim < 0)
                {
                    FrameworkLog.Warning(
                        $"Economy E2E smoke failed in cycle {cycleIndex + 1}: tradingCurrency became negative: {currencyAfterClaim}");
                    return;
                }

                FrameworkLog.Info(
                    $"Economy E2E smoke cycle {cycleIndex + 1}: tradingCurrency {currencyBeforeCycle} -> settle {currencyAfterSettle} -> claim {currencyAfterClaim}");
            }

            FrameworkLog.Info("Economy E2E smoke completed 3 consecutive trade cycles.");
        }

        /// <summary>
        /// 인게임 시간 배율이 식량 소모와 elapsed 동기화에 반영되는지 검증한다.
        /// </summary>
        /// <remarks>
        /// Boot flow로 InGame에 진입한 뒤 실행해야 GameTimeService와 coordinator가 준비된다.
        /// </remarks>
        [ContextMenu("Framework/Run InGame Food Consumption Smoke")]
        public void RunInGameFoodConsumptionSmoke()
        {
            var coordinator = GetCoordinator();
            if (coordinator == null || FrameworkRoot.Instance == null || FrameworkRoot.Instance.TradeStart == null
                || FrameworkRoot.Instance.GameTime == null)
            {
                return;
            }

            var gameTime = FrameworkRoot.Instance.GameTime;
            var saveData = FrameworkRoot.Instance.CurrentSaveData;
            if (saveData == null)
            {
                FrameworkLog.Warning("InGame food consumption smoke skipped because save data is not ready.");
                return;
            }

            if (!gameTime.TrySetInGameTimeMultiplier(60f))
            {
                FrameworkLog.Warning("InGame food consumption smoke skipped because multiplier could not be set.");
                return;
            }

            FillSampleCaravan();
            caravan.foodAmount = 10;

            var smokeTradeId = $"{tradeId}_food_smoke";
            var startResult = FrameworkRoot.Instance.TradeStart.TryStartTrade(
                caravan,
                distanceKm,
                smokeTradeId,
                routeId);
            if (!startResult.canDepart || !FrameworkRoot.Instance.TradeStart.LastRecordSucceeded)
            {
                FrameworkLog.Warning("InGame food consumption smoke failed to start trade.");
                return;
            }

            coordinator.SetActiveCaravan(caravan);
            BackdateActiveTradeStart(saveData, 10d);

            var foodBefore = CaravanCalculator.GetRemainingFood(caravan);
            coordinator.CheckProgressAndCompletion(saveProgress: false);

            if (Mathf.Abs(caravan.elapsedInGameSeconds - saveData.caravan.elapsedInGameSeconds) > 0.01f)
            {
                FrameworkLog.Warning(
                    $"InGame food consumption smoke failed: elapsed mismatch. Runtime: {caravan.elapsedInGameSeconds}, Save: {saveData.caravan.elapsedInGameSeconds}");
                return;
            }

            var foodAfter = CaravanCalculator.GetRemainingFood(caravan);
            if (foodAfter >= foodBefore)
            {
                FrameworkLog.Warning(
                    $"InGame food consumption smoke failed: food did not decrease. Before: {foodBefore}, After: {foodAfter}");
                return;
            }

            if (!caravan.runFoodDepleted && caravan.runFatalReason == JourneyFailureReason.None)
            {
                FrameworkLog.Warning(
                    $"InGame food consumption smoke failed: food should be depleted. Remaining: {foodAfter}");
                return;
            }

            FrameworkLog.Info(
                $"InGame food consumption smoke passed. Elapsed in-game: {caravan.elapsedInGameSeconds:0.#}s, Food {foodBefore:0.#} -> {foodAfter:0.#}");
        }

        /// <summary>
        /// Pause 중에는 현실 시간 backdate가 있어도 식량용 elapsed와 잔량이 증가·감소하지 않는지 검증한다.
        /// </summary>
        /// <remarks>
        /// Boot flow로 InGame에 진입한 뒤 실행해야 GameTimeService와 coordinator가 준비된다.
        /// 통과 시 ResumeGameTime()을 호출해 pause 상태를 정리한다.
        /// </remarks>
        [ContextMenu("Framework/Run Pause Food Freeze Smoke")]
        public void RunPauseFoodFreezeSmoke()
        {
            var coordinator = GetCoordinator();
            if (coordinator == null || FrameworkRoot.Instance == null || FrameworkRoot.Instance.TradeStart == null
                || FrameworkRoot.Instance.GameTime == null)
            {
                return;
            }

            var gameTime = FrameworkRoot.Instance.GameTime;
            var saveData = FrameworkRoot.Instance.CurrentSaveData;
            if (saveData == null)
            {
                FrameworkLog.Warning("Pause food freeze smoke skipped because save data is not ready.");
                return;
            }

            FillSampleCaravan();
            caravan.foodAmount = 30;

            var smokeTradeId = $"{tradeId}_pause_food_smoke";
            var startResult = FrameworkRoot.Instance.TradeStart.TryStartTrade(
                caravan,
                distanceKm,
                smokeTradeId,
                routeId);
            if (!startResult.canDepart || !FrameworkRoot.Instance.TradeStart.LastRecordSucceeded)
            {
                FrameworkLog.Warning("Pause food freeze smoke failed to start trade.");
                return;
            }

            coordinator.SetActiveCaravan(caravan);
            coordinator.CheckProgressAndCompletion(saveProgress: false);

            var elapsedBaseline = caravan.elapsedInGameSeconds;
            var foodBaseline = CaravanCalculator.GetRemainingFood(caravan);

            gameTime.PauseGameTime();
            BackdateActiveTradeStart(saveData, 30d);
            var pausedCheck = coordinator.CheckProgressAndCompletion(saveProgress: false);
            var elapsedPaused = caravan.elapsedInGameSeconds;
            var foodPaused = CaravanCalculator.GetRemainingFood(caravan);
            gameTime.ResumeGameTime();

            if (pausedCheck)
            {
                FrameworkLog.Warning("Pause food freeze smoke failed because progress check returned true while paused.");
                return;
            }

            if (Mathf.Abs(elapsedPaused - elapsedBaseline) > 0.01f)
            {
                FrameworkLog.Warning(
                    $"Pause food freeze smoke failed: elapsed changed while paused. Baseline: {elapsedBaseline}, Paused: {elapsedPaused}");
                return;
            }

            if (Mathf.Abs(foodPaused - foodBaseline) > 0.01f)
            {
                FrameworkLog.Warning(
                    $"Pause food freeze smoke failed: food changed while paused. Baseline: {foodBaseline}, Paused: {foodPaused}");
                return;
            }

            FrameworkLog.Info(
                $"Pause food freeze smoke passed. Elapsed held at {elapsedBaseline:0.#}s, Food held at {foodBaseline:0.#}.");
        }

        /// <summary>
        /// 식량 고갈 실패로 Failed 정산이 Settlement 화면에 진입하고 claim 후 Preparation으로 복귀하는지 검증한다.
        /// </summary>
        /// <remarks>
        /// foodAmount=0과 starveGraceSeconds=0으로 FoodDepleted fatal을 즉시 재현한다.
        /// ForceCompleteActiveTrade 성공 경로는 사용하지 않는다.
        /// </remarks>
        [ContextMenu("Framework/Run Failed Settlement Screen Smoke")]
        public void RunFailedSettlementScreenSmoke()
        {
            var coordinator = GetCoordinator();
            if (coordinator == null || FrameworkRoot.Instance == null || FrameworkRoot.Instance.TradeStart == null)
            {
                return;
            }

            var saveData = FrameworkRoot.Instance.CurrentSaveData;
            var screenRouter = FrameworkRoot.Instance.InGameScreenRouter;
            if (saveData == null)
            {
                FrameworkLog.Warning("Failed settlement screen smoke skipped because save data is not ready.");
                return;
            }

            FillSampleCaravan();
            caravan.foodAmount = 0;
            caravan.starveGraceSeconds = 0f;

            var smokeTradeId = $"{tradeId}_failed_settlement_smoke";
            var startResult = FrameworkRoot.Instance.TradeStart.TryStartTrade(
                caravan,
                distanceKm,
                smokeTradeId,
                routeId);
            if (!startResult.canDepart || !FrameworkRoot.Instance.TradeStart.LastRecordSucceeded)
            {
                FrameworkLog.Warning("Failed settlement screen smoke failed to start trade.");
                return;
            }

            coordinator.SetActiveCaravan(caravan);
            BackdateActiveTradeStart(saveData, 1d);
            var settlementReady = coordinator.CheckProgressAndCompletion(saveProgress: false);
            if (!settlementReady || coordinator.LastSettlementResult == null)
            {
                FrameworkLog.Warning("Failed settlement screen smoke failed because settlement was not created.");
                return;
            }

            var result = coordinator.LastSettlementResult;
            if (result.grade != JourneyResultGrade.Failed)
            {
                FrameworkLog.Warning($"Failed settlement screen smoke failed: grade was {result.grade}, expected Failed.");
                return;
            }

            if (result.failureReason != JourneyFailureReason.FoodDepleted)
            {
                FrameworkLog.Warning(
                    $"Failed settlement screen smoke failed: failureReason was {result.failureReason}, expected FoodDepleted.");
                return;
            }

            if (saveData.tradeProgress == null
                || saveData.tradeProgress.state != TradeProgressState.SettlementPending)
            {
                FrameworkLog.Warning(
                    $"Failed settlement screen smoke failed: trade state was {saveData.tradeProgress?.state}, expected SettlementPending.");
                return;
            }

            if (screenRouter != null && screenRouter.CurrentScreenState != InGameScreenState.Settlement)
            {
                FrameworkLog.Warning(
                    $"Failed settlement screen smoke failed: screen was {screenRouter.CurrentScreenState}, expected Settlement.");
                return;
            }

            var viewData = new SettlementViewData(
                smokeTradeId,
                result.grade,
                result.failureReason,
                result.revenue,
                result.cost,
                result.netProfit,
                result.cargoLost,
                result.durabilityLost,
                result.travelSeconds,
                result.foodConsumed,
                result.departureLoad,
                result.overloadRatio,
                true,
                "Trade failed.");
            if (!viewData.IsFailed)
            {
                FrameworkLog.Warning("Failed settlement screen smoke failed because SettlementViewData.IsFailed was false.");
                return;
            }

            var firstClaim = coordinator.ClaimSettlementAndReset();
            var duplicateClaim = coordinator.ClaimSettlementAndReset();
            if (!firstClaim || duplicateClaim)
            {
                FrameworkLog.Warning(
                    $"Failed settlement screen smoke failed claim validation. First: {firstClaim}, Duplicate: {duplicateClaim}");
                return;
            }

            if (saveData.tradeProgress.state != TradeProgressState.Failed)
            {
                FrameworkLog.Warning(
                    $"Failed settlement screen smoke failed: post-claim state was {saveData.tradeProgress.state}, expected Failed.");
                return;
            }

            var activeCaravan = coordinator.ActiveCaravan;
            if (activeCaravan == null || activeCaravan.state != JourneyState.Prepare)
            {
                FrameworkLog.Warning("Failed settlement screen smoke failed to return caravan to Prepare.");
                return;
            }

            if (screenRouter != null && screenRouter.CurrentScreenState != InGameScreenState.Preparation)
            {
                FrameworkLog.Warning(
                    $"Failed settlement screen smoke failed: post-claim screen was {screenRouter.CurrentScreenState}, expected Preparation.");
                return;
            }

            FrameworkLog.Info(
                "Failed settlement screen smoke passed. Failed grade -> Settlement -> claim -> Preparation/Failed state.");
        }

        /// <summary>
        /// pendingSettlement 저장 후 세션 캐시 소실을 시뮬레이션하고 복구·claim까지 검증한다.
        /// </summary>
        /// <remarks>
        /// CompleteLoadingAndEnterGame의 RestorePendingSettlement 경로를 Play Mode에서 재현한다.
        /// </remarks>
        [ContextMenu("Framework/Run Pending Settlement Restore Smoke")]
        public void RunPendingSettlementRestoreSmoke()
        {
            var coordinator = GetCoordinator();
            var root = FrameworkRoot.Instance;
            if (coordinator == null || root == null || root.TradeStart == null || root.SaveService == null)
            {
                return;
            }

            var saveData = root.CurrentSaveData;
            var bridge = root.SettlementUiBridge;
            if (saveData == null)
            {
                FrameworkLog.Warning("Pending settlement restore smoke skipped because save data is not ready.");
                return;
            }

            FillSampleCaravan();
            var smokeTradeId = $"{tradeId}_pending_restore_smoke";
            var startResult = root.TradeStart.TryStartTrade(caravan, distanceKm, smokeTradeId, routeId);
            if (!startResult.canDepart || !root.TradeStart.LastRecordSucceeded)
            {
                FrameworkLog.Warning("Pending settlement restore smoke failed to start trade.");
                return;
            }

            coordinator.SetActiveCaravan(caravan);
            coordinator.ForceCompleteActiveTrade();

            var pending = saveData.pendingSettlement;
            if (pending == null || !pending.hasResult || pending.tradeId != smokeTradeId)
            {
                FrameworkLog.Warning("Pending settlement restore smoke failed because pendingSettlement was not written.");
                return;
            }

            if (saveData.tradeProgress == null
                || saveData.tradeProgress.state != TradeProgressState.SettlementPending)
            {
                FrameworkLog.Warning(
                    $"Pending settlement restore smoke failed: trade state was {saveData.tradeProgress?.state}, expected SettlementPending.");
                return;
            }

            var savedGrade = pending.grade;
            root.SaveService.Save(saveData);

            coordinator.ClearSettlementCache();
            coordinator.SetActiveCaravan(null);
            bridge?.ClearPendingSettlement();

            if (!coordinator.RestorePendingSettlement(saveData))
            {
                FrameworkLog.Warning("Pending settlement restore smoke failed because RestorePendingSettlement returned false.");
                return;
            }

            if (coordinator.LastSettlementResult == null
                || coordinator.LastSettlementTradeId != smokeTradeId
                || coordinator.LastSettlementResult.grade != savedGrade)
            {
                FrameworkLog.Warning("Pending settlement restore smoke failed because coordinator cache did not match saved result.");
                return;
            }

            if (bridge != null)
            {
                if (!bridge.TryGetPendingSettlement(out var bridgeTradeId, out var bridgeResult)
                    || bridgeTradeId != smokeTradeId
                    || bridgeResult == null
                    || bridgeResult.grade != savedGrade)
                {
                    FrameworkLog.Warning("Pending settlement restore smoke failed because SettlementUiBridge cache was not restored.");
                    return;
                }
            }

            var firstClaim = coordinator.ClaimSettlementAndReset();
            var duplicateClaim = coordinator.ClaimSettlementAndReset();
            if (!firstClaim || duplicateClaim)
            {
                FrameworkLog.Warning(
                    $"Pending settlement restore smoke failed claim validation. First: {firstClaim}, Duplicate: {duplicateClaim}");
                return;
            }

            if (saveData.pendingSettlement != null && saveData.pendingSettlement.hasResult)
            {
                FrameworkLog.Warning("Pending settlement restore smoke failed because pendingSettlement was not cleared after claim.");
                return;
            }

            if (root.InGameScreenRouter != null
                && root.InGameScreenRouter.CurrentScreenState != InGameScreenState.Preparation)
            {
                FrameworkLog.Warning(
                    $"Pending settlement restore smoke failed: post-claim screen was {root.InGameScreenRouter.CurrentScreenState}, expected Preparation.");
                return;
            }

            FrameworkLog.Info("Pending settlement restore smoke passed.");
        }

        /// <summary>
        /// Traveling 오프라인 미완료·완료·재호출 no-op·시간 역행 스킵을 검증한다.
        /// </summary>
        /// <remarks>
        /// CompleteLoadingAndEnterGame의 ApplyOfflineProgressOnLoad 경로를 Play Mode에서 재현한다.
        /// 같은 CurrentSaveData를 쓰므로 Case B는 새 출발 없이 Case A Traveling을 재사용하고,
        /// Case D는 claim으로 SettlementPending을 비운 뒤 새 Traveling을 시작한다.
        /// </remarks>
        [ContextMenu("Framework/Run Offline Progress Smoke")]
        public void RunOfflineProgressSmoke()
        {
            var coordinator = GetCoordinator();
            var root = FrameworkRoot.Instance;
            if (coordinator == null || root == null || root.TradeStart == null || root.SaveService == null)
            {
                return;
            }

            var saveData = root.CurrentSaveData;
            if (saveData == null)
            {
                FrameworkLog.Warning("Offline progress smoke skipped because save data is not ready.");
                return;
            }

            var offlineCompletedCount = 0;
            var rollbackCount = 0;
            void OnOfflineCompleted(string completedTradeId) => offlineCompletedCount++;
            void OnRollback() => rollbackCount++;

            FrameworkEvents.TradeOfflineCompleted += OnOfflineCompleted;
            FrameworkEvents.TimeRollbackDetected += OnRollback;

            try
            {
                // Case A: 미완료 Traveling — elapsed 증가, state 유지, OfflineCompleted 미발생
                FillSampleCaravan();
                var travelingTradeId = $"{tradeId}_offline_traveling";
                var incompleteStart = root.TradeStart.TryStartTrade(caravan, distanceKm, travelingTradeId, routeId);
                if (!incompleteStart.canDepart || !root.TradeStart.LastRecordSucceeded)
                {
                    FrameworkLog.Warning("Offline progress smoke failed to start incomplete trade.");
                    return;
                }

                coordinator.SetActiveCaravan(caravan);
                var foodBefore = caravan.foodAmount;
                var elapsedBefore = caravan.elapsedInGameSeconds;
                BackdateActiveTradeStart(saveData, 10d);
                saveData.lastSavedUtcTicks = DateTime.UtcNow.AddSeconds(-30d).Ticks;

                var incompleteSettled = coordinator.ApplyOfflineProgressOnLoad(saveData);
                if (incompleteSettled
                    || saveData.tradeProgress.state != TradeProgressState.Traveling
                    || offlineCompletedCount != 0)
                {
                    FrameworkLog.Warning(
                        $"Offline incomplete case failed. Settled: {incompleteSettled}, State: {saveData.tradeProgress.state}, OfflineEvents: {offlineCompletedCount}");
                    return;
                }

                if (caravan.elapsedInGameSeconds <= elapsedBefore)
                {
                    FrameworkLog.Warning(
                        $"Offline incomplete case failed: elapsed did not increase. Before: {elapsedBefore}, After: {caravan.elapsedInGameSeconds}");
                    return;
                }

                // Case B: 동일 Traveling을 완료 구간으로 만들어 offline settle (새 TryStartTrade 금지)
                saveData.tradeProgress.expectedTradeEndUtcTick = DateTime.UtcNow.AddSeconds(-5d).Ticks;
                saveData.lastSavedUtcTicks = DateTime.UtcNow.AddSeconds(-60d).Ticks;
                offlineCompletedCount = 0;

                var completeSettled = coordinator.ApplyOfflineProgressOnLoad(saveData);
                if (!completeSettled
                    || saveData.tradeProgress.state != TradeProgressState.SettlementPending
                    || saveData.pendingSettlement == null
                    || !saveData.pendingSettlement.hasResult
                    || saveData.pendingSettlement.tradeId != travelingTradeId
                    || offlineCompletedCount != 1)
                {
                    FrameworkLog.Warning(
                        $"Offline complete case failed. Settled: {completeSettled}, State: {saveData.tradeProgress.state}, OfflineEvents: {offlineCompletedCount}");
                    return;
                }

                // Case C: SettlementPending에서 재호출 — no-op, 추가 OfflineCompleted 없음
                offlineCompletedCount = 0;
                var secondApply = coordinator.ApplyOfflineProgressOnLoad(saveData);
                if (secondApply || offlineCompletedCount != 0)
                {
                    FrameworkLog.Warning(
                        $"Offline re-apply case failed. Settled: {secondApply}, OfflineEvents: {offlineCompletedCount}");
                    return;
                }

                // Case D: claim으로 pending 정리 후 새 Traveling → 역행 스킵
                if (!coordinator.ClaimSettlementAndReset())
                {
                    FrameworkLog.Warning("Offline progress smoke failed to claim settlement before rollback case.");
                    return;
                }

                FillSampleCaravan();
                var rollbackTradeId = $"{tradeId}_offline_rollback";
                var rollbackStart = root.TradeStart.TryStartTrade(caravan, distanceKm, rollbackTradeId, routeId);
                if (!rollbackStart.canDepart || !root.TradeStart.LastRecordSucceeded)
                {
                    FrameworkLog.Warning("Offline progress smoke failed to start rollback trade.");
                    return;
                }

                coordinator.SetActiveCaravan(caravan);
                var elapsedRollbackBefore = caravan.elapsedInGameSeconds;
                var foodRollbackBefore = caravan.foodAmount;
                saveData.lastSavedUtcTicks = DateTime.UtcNow.AddHours(1d).Ticks;
                rollbackCount = 0;

                var rollbackSettled = coordinator.ApplyOfflineProgressOnLoad(saveData);
                if (rollbackSettled
                    || rollbackCount != 1
                    || saveData.tradeProgress.state != TradeProgressState.Traveling
                    || !Mathf.Approximately(caravan.elapsedInGameSeconds, elapsedRollbackBefore)
                    || caravan.foodAmount != foodRollbackBefore)
                {
                    FrameworkLog.Warning(
                        $"Offline rollback case failed. Settled: {rollbackSettled}, RollbackEvents: {rollbackCount}, State: {saveData.tradeProgress.state}");
                    return;
                }

                FrameworkLog.Info(
                    $"Offline progress smoke passed. Incomplete elapsed advanced (food was {foodBefore}), complete settled once, re-apply no-op, rollback skipped.");
            }
            finally
            {
                FrameworkEvents.TradeOfflineCompleted -= OnOfflineCompleted;
                FrameworkEvents.TimeRollbackDetected -= OnRollback;
            }
        }

        /// <summary>
        /// ForceSeason / ForceDisaster 저장 반영과 ForceRouteEvent Traveling 전후 결과를 짧게 검증한다.
        /// </summary>
        [ContextMenu("Framework/Run Force World Debug Smoke")]
        public void RunForceWorldDebugSmoke()
        {
            if (!TryGetDebugCommands(out var commands))
            {
                return;
            }

            var saveData = FrameworkRoot.Instance.CurrentSaveData;
            if (saveData == null)
            {
                FrameworkLog.Warning("Force world debug smoke skipped because save data is not ready.");
                return;
            }

            if (!commands.ForceSeason(debugSeasonId))
            {
                FrameworkLog.Warning("Force world debug smoke failed: ForceSeason returned false.");
                return;
            }

            if (saveData.world == null || saveData.world.currentSeasonId != debugSeasonId.Trim())
            {
                FrameworkLog.Warning(
                    $"Force world debug smoke failed: season was '{saveData.world?.currentSeasonId}', expected '{debugSeasonId}'.");
                return;
            }

            if (!commands.ForceDisaster(debugDisasterId))
            {
                FrameworkLog.Warning("Force world debug smoke failed: ForceDisaster returned false.");
                return;
            }

            var expectedDisaster = debugDisasterId?.Trim() ?? string.Empty;
            if (saveData.world.currentDisasterId != expectedDisaster)
            {
                FrameworkLog.Warning(
                    $"Force world debug smoke failed: disaster was '{saveData.world.currentDisasterId}', expected '{expectedDisaster}'.");
                return;
            }

            if (commands.ForceRouteEvent(debugRouteEventId))
            {
                FrameworkLog.Warning(
                    "Force world debug smoke failed: ForceRouteEvent succeeded before Traveling trade.");
                return;
            }

            if (FrameworkRoot.Instance.TradeStart == null)
            {
                FrameworkLog.Warning("Force world debug smoke skipped because TradeStart is not ready.");
                return;
            }

            FillSampleCaravan();
            var smokeTradeId = $"{tradeId}_force_route_smoke";
            var startResult = FrameworkRoot.Instance.TradeStart.TryStartTrade(
                caravan,
                distanceKm,
                smokeTradeId,
                routeId);
            if (!startResult.canDepart || !FrameworkRoot.Instance.TradeStart.LastRecordSucceeded)
            {
                FrameworkLog.Warning("Force world debug smoke failed to start Traveling trade.");
                return;
            }

            FrameworkRoot.Instance.TradeProgressCoordinator?.SetActiveCaravan(caravan);
            if (!commands.ForceRouteEvent(debugRouteEventId))
            {
                FrameworkLog.Warning("Force world debug smoke failed: ForceRouteEvent returned false while Traveling.");
                return;
            }

            if (!commands.TryConsumeForcedRouteEvent(smokeTradeId, out var consumedEventId)
                || consumedEventId != debugRouteEventId.Trim())
            {
                FrameworkLog.Warning(
                    $"Force world debug smoke failed: consumed event was '{consumedEventId}', expected '{debugRouteEventId}'.");
                return;
            }

            FrameworkLog.Info(
                $"Force world debug smoke passed. Season={saveData.world.currentSeasonId}, Disaster='{saveData.world.currentDisasterId}', RouteEvent consumed.");
        }

        /// <summary>
        /// WorldSaveData.currentSeasonId를 강제 변경하고 저장한다.
        /// </summary>
        [ContextMenu("Framework/Force Season")]
        public void ForceSeason()
        {
            if (!TryGetDebugCommands(out var commands))
            {
                return;
            }

            commands.ForceSeason(debugSeasonId);
        }

        /// <summary>
        /// WorldSaveData.currentDisasterId를 강제 변경하고 저장한다.
        /// </summary>
        [ContextMenu("Framework/Force Disaster")]
        public void ForceDisaster()
        {
            if (!TryGetDebugCommands(out var commands))
            {
                return;
            }

            commands.ForceDisaster(debugDisasterId);
        }

        /// <summary>
        /// Traveling trade에 route event 1회 주입 hook을 등록한다.
        /// </summary>
        /// <remarks>
        /// StartTradeAndRecordTime()으로 Traveling 상태를 만든 뒤 호출해야 한다.
        /// </remarks>
        [ContextMenu("Framework/Force Route Event")]
        public void ForceRouteEvent()
        {
            if (!TryGetDebugCommands(out var commands))
            {
                return;
            }

            commands.ForceRouteEvent(debugRouteEventId);
        }

        private void ApplyConsumptionRateNormalization()
        {
            var gameTime = FrameworkRoot.Instance != null ? FrameworkRoot.Instance.GameTime : null;
            if (gameTime != null)
            {
                CaravanConsumptionRateNormalizer.ApplyToCaravan(caravan, gameTime);
            }
        }

        private static void BackdateActiveTradeStart(SaveData saveData, double realSeconds)
        {
            if (saveData?.tradeProgress == null || saveData.tradeProgress.tradeStartUtcTick <= 0)
            {
                return;
            }

            var startUtc = new DateTime(saveData.tradeProgress.tradeStartUtcTick, DateTimeKind.Utc);
            saveData.tradeProgress.tradeStartUtcTick = startUtc.AddSeconds(-realSeconds).Ticks;
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

        private static bool TryGetDebugCommands(out FrameworkDebugCommands commands)
        {
            commands = null;
            if (FrameworkRoot.Instance == null || FrameworkRoot.Instance.DebugCommands == null)
            {
                FrameworkLog.Warning("Debug harness command skipped because FrameworkRoot.DebugCommands is not ready.");
                return false;
            }

            commands = FrameworkRoot.Instance.DebugCommands;
            return true;
        }
    }
}
