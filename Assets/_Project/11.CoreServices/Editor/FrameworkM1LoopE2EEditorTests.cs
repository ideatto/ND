/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - develop 무역 3사이클 loop integrity와 Economy settle/claim E2E를 Editor에서 자동 검증한다.
 * - Play mode ContextMenu smoke와 동일한 coordinator 경로를 서비스 조립으로 재현한다.
 *
 * Main Features
 * - SharedGameData 로드, 3회 loop integrity, settle 후 currency 불변·claim 후 currency 변화 검증.
 * - 인게임 식량 소모 elapsed 동기화 및 배율 효과 검증.
 * - Pause 중 식량 elapsed 정지, Failed 정산 화면 진입·claim 복귀 검증.
 * - Unity batchmode -executeMethod 진입점 제공.
 *
 * Usage for Team Members
 * - Unity Editor: ND/Framework/Run M1 Loop + Economy E2E Checks
 * - CI/batchmode: ND.Framework.Editor.FrameworkM1LoopE2EEditorTests.RunAllFromBatchMode
 *
 * Important Notes
 * - Editor 전용이며 Player build에 포함되지 않는다.
 * - JsonSaveService는 persistentDataPath에 저장할 수 있으나 테스트는 in-memory SaveData 참조를 우선 사용한다.
 * - Related Documentation: Docs/Personal_Documents/CSU/m2-pause-failed-force-smoke.md
 */
#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace ND.Framework.Editor
{
    /// <summary>
    /// Framework M1 loop integrity와 Economy E2E를 Editor에서 실행하는 검증 스크립트이다.
    /// </summary>
    public static class FrameworkM1LoopE2EEditorTests
    {
        private const int CycleCount = 3;
        private const string RouteId = "dummyroute";
        private const string ItemId = "dummyitem";
        private const float DistanceKm = 100f;
        private const float StarveGraceSeconds = 5f;
        private const float SampleRawFoodConsumptionPerDay = 8640f;
        private const float FoodConsumptionTestMultiplier = 60f;
        private const double FoodConsumptionBackdateRealSeconds = 10d;

        /// <summary>
        /// loop integrity smoke와 Economy E2E 검증을 순서대로 실행한다.
        /// </summary>
        [MenuItem("ND/Framework/Run M1 Loop + Economy E2E Checks")]
        public static void RunAll()
        {
            var context = TestContext.Create();
            RunLoopIntegritySmoke(context);
            RunEconomyE2E(context);
            RunInGameFoodConsumptionE2E(context);
            RunPauseFoodFreezeE2E(TestContext.Create());
            RunFailedSettlementScreenE2E(TestContext.Create());
            Debug.Log("[Framework M1 E2E] All checks passed.");
        }

        /// <summary>
        /// Unity batchmode에서 호출하는 진입점이다.
        /// </summary>
        public static void RunAllFromBatchMode()
        {
            try
            {
                RunAll();
            }
            catch (Exception exception)
            {
                Debug.LogError("[Framework M1 E2E] Failed: " + exception.Message);
                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.Exit(0);
        }

        private static void RunLoopIntegritySmoke(TestContext context)
        {
            for (var cycleIndex = 0; cycleIndex < CycleCount; cycleIndex++)
            {
                var caravan = CreateSampleCaravan(context.GameTime);
                var smokeTradeId = $"editor_smoke_{cycleIndex + 1}";

                var startResult = context.TradeStart.TryStartTrade(
                    caravan,
                    DistanceKm,
                    smokeTradeId,
                    RouteId);
                if (!startResult.canDepart || !context.TradeStart.LastRecordSucceeded)
                {
                    throw new InvalidOperationException($"Loop integrity smoke failed to start cycle {cycleIndex + 1}.");
                }

                context.Coordinator.SetActiveCaravan(caravan);
                context.Coordinator.ForceCompleteActiveTrade();

                if (context.Coordinator.LastSettlementResult == null)
                {
                    throw new InvalidOperationException(
                        $"Loop integrity smoke failed because settlement result was missing in cycle {cycleIndex + 1}.");
                }

                if (context.Coordinator.CheckProgressAndCompletion())
                {
                    throw new InvalidOperationException(
                        $"Loop integrity smoke failed because settlement was recreated in cycle {cycleIndex + 1}.");
                }

                var firstClaim = context.Coordinator.ClaimSettlementAndReset();
                var duplicateClaim = context.Coordinator.ClaimSettlementAndReset();
                if (!firstClaim || duplicateClaim)
                {
                    throw new InvalidOperationException(
                        $"Loop integrity smoke failed claim validation in cycle {cycleIndex + 1}. First: {firstClaim}, Duplicate: {duplicateClaim}");
                }

                var activeCaravan = context.Coordinator.ActiveCaravan;
                if (activeCaravan == null || activeCaravan.state != JourneyState.Prepare)
                {
                    throw new InvalidOperationException(
                        $"Loop integrity smoke failed to return to preparation in cycle {cycleIndex + 1}.");
                }

                if (context.Coordinator.LastSettlementResult != null)
                {
                    throw new InvalidOperationException(
                        $"Loop integrity smoke failed because settlement cache remained after cycle {cycleIndex + 1}.");
                }
            }

            Debug.Log("[Framework M1 E2E] Loop integrity smoke completed 3 consecutive trade cycles.");
        }

        private static void RunEconomyE2E(TestContext context)
        {
            if (context.SharedGameData == null || !context.SharedGameData.IsLoaded)
            {
                throw new InvalidOperationException("Economy E2E failed because shared game data is not loaded.");
            }

            for (var cycleIndex = 0; cycleIndex < CycleCount; cycleIndex++)
            {
                var caravan = CreateSampleCaravan(context.GameTime);
                var tradeId = $"editor_economy_{cycleIndex + 1}";
                var currencyBeforeCycle = context.SaveData.player.tradingCurrency;

                var startResult = context.TradeStart.TryStartTrade(caravan, DistanceKm, tradeId, RouteId);
                if (!startResult.canDepart || !context.TradeStart.LastRecordSucceeded)
                {
                    throw new InvalidOperationException($"Economy E2E failed to start cycle {cycleIndex + 1}.");
                }

                context.Coordinator.SetActiveCaravan(caravan);
                context.Coordinator.ForceCompleteActiveTrade();

                var currencyAfterSettle = context.SaveData.player.tradingCurrency;
                if (currencyAfterSettle != currencyBeforeCycle)
                {
                    throw new InvalidOperationException(
                        $"Economy E2E cycle {cycleIndex + 1}: tradingCurrency changed after settle preview. Before: {currencyBeforeCycle}, After settle: {currencyAfterSettle}");
                }

                if (context.Coordinator.LastSettlementResult == null)
                {
                    throw new InvalidOperationException($"Economy E2E cycle {cycleIndex + 1}: settlement result is missing.");
                }

                if (!context.Coordinator.ClaimSettlementAndReset())
                {
                    throw new InvalidOperationException($"Economy E2E cycle {cycleIndex + 1}: claim failed.");
                }

                var currencyAfterClaim = context.SaveData.player.tradingCurrency;
                if (currencyAfterClaim == currencyAfterSettle)
                {
                    throw new InvalidOperationException(
                        $"Economy E2E cycle {cycleIndex + 1}: tradingCurrency did not change after claim. Settle: {currencyAfterSettle}, After claim: {currencyAfterClaim}");
                }

                if (currencyAfterClaim < 0)
                {
                    throw new InvalidOperationException(
                        $"Economy E2E cycle {cycleIndex + 1}: tradingCurrency became negative: {currencyAfterClaim}");
                }

                Debug.Log(
                    $"[Framework M1 E2E] Economy cycle {cycleIndex + 1}: tradingCurrency {currencyBeforeCycle} -> settle {currencyAfterSettle} -> claim {currencyAfterClaim}");
            }

            Debug.Log("[Framework M1 E2E] Economy E2E completed 3 consecutive trade cycles.");
        }

        private static void RunInGameFoodConsumptionE2E(TestContext context)
        {
            RunInGameFoodConsumptionHighMultiplierCase(context);

            var baselineContext = TestContext.Create();
            baselineContext.GameTime.TrySetInGameTimeMultiplier(1f);
            RunInGameFoodConsumptionBaselineMultiplierCase(baselineContext);

            Debug.Log("[Framework M1 E2E] InGame food consumption E2E passed.");
        }

        private static void RunInGameFoodConsumptionHighMultiplierCase(TestContext context)
        {
            if (!context.GameTime.TrySetInGameTimeMultiplier(FoodConsumptionTestMultiplier))
            {
                throw new InvalidOperationException("InGame food consumption E2E failed because multiplier could not be set.");
            }

            var caravan = CreateSampleCaravan(context.GameTime);
            caravan.foodAmount = 10;

            var tradeId = "editor_food_high_multiplier";
            var startResult = context.TradeStart.TryStartTrade(caravan, DistanceKm, tradeId, RouteId);
            if (!startResult.canDepart || !context.TradeStart.LastRecordSucceeded)
            {
                throw new InvalidOperationException("InGame food consumption E2E failed to start high-multiplier trade.");
            }

            context.Coordinator.SetActiveCaravan(caravan);
            BackdateActiveTradeStart(context.SaveData, FoodConsumptionBackdateRealSeconds);

            var foodBefore = CaravanCalculator.GetRemainingFood(caravan);
            context.Coordinator.CheckProgressAndCompletion(saveProgress: false);

            if (Mathf.Abs(caravan.elapsedInGameSeconds - context.SaveData.caravan.elapsedInGameSeconds) > 0.01f)
            {
                throw new InvalidOperationException(
                    $"InGame food consumption E2E failed: elapsed mismatch. Runtime: {caravan.elapsedInGameSeconds}, Save: {context.SaveData.caravan.elapsedInGameSeconds}");
            }

            var foodAfter = CaravanCalculator.GetRemainingFood(caravan);
            if (foodAfter >= foodBefore)
            {
                throw new InvalidOperationException(
                    $"InGame food consumption E2E failed: food did not decrease. Before: {foodBefore}, After: {foodAfter}");
            }

            if (!caravan.runFoodDepleted && caravan.runFatalReason == JourneyFailureReason.None)
            {
                throw new InvalidOperationException(
                    $"InGame food consumption E2E failed: food should be depleted with multiplier {FoodConsumptionTestMultiplier}. Remaining: {foodAfter}");
            }
        }

        private static void RunInGameFoodConsumptionBaselineMultiplierCase(TestContext context)
        {
            var caravan = CreateSampleCaravan(context.GameTime);
            caravan.foodAmount = 10;

            var tradeId = "editor_food_baseline_multiplier";
            var startResult = context.TradeStart.TryStartTrade(caravan, DistanceKm, tradeId, RouteId);
            if (!startResult.canDepart || !context.TradeStart.LastRecordSucceeded)
            {
                throw new InvalidOperationException("InGame food consumption E2E failed to start baseline-multiplier trade.");
            }

            context.Coordinator.SetActiveCaravan(caravan);
            BackdateActiveTradeStart(context.SaveData, FoodConsumptionBackdateRealSeconds);
            context.Coordinator.CheckProgressAndCompletion(saveProgress: false);

            var foodAfter = CaravanCalculator.GetRemainingFood(caravan);
            if (foodAfter <= 0f || caravan.runFoodDepleted)
            {
                throw new InvalidOperationException(
                    $"InGame food consumption E2E failed: baseline multiplier should not deplete food this quickly. Remaining: {foodAfter}");
            }
        }

        private static void RunPauseFoodFreezeE2E(TestContext context)
        {
            var caravan = CreateSampleCaravan(context.GameTime);
            caravan.foodAmount = 30;

            var tradeId = "editor_pause_food_freeze";
            var startResult = context.TradeStart.TryStartTrade(caravan, DistanceKm, tradeId, RouteId);
            if (!startResult.canDepart || !context.TradeStart.LastRecordSucceeded)
            {
                throw new InvalidOperationException("Pause food freeze E2E failed to start trade.");
            }

            context.Coordinator.SetActiveCaravan(caravan);
            context.Coordinator.CheckProgressAndCompletion(saveProgress: false);

            var elapsedBaseline = caravan.elapsedInGameSeconds;
            var foodBaseline = CaravanCalculator.GetRemainingFood(caravan);

            context.GameTime.PauseGameTime();
            BackdateActiveTradeStart(context.SaveData, 30d);
            var pausedCheck = context.Coordinator.CheckProgressAndCompletion(saveProgress: false);
            var elapsedPaused = caravan.elapsedInGameSeconds;
            var foodPaused = CaravanCalculator.GetRemainingFood(caravan);
            context.GameTime.ResumeGameTime();

            if (pausedCheck)
            {
                throw new InvalidOperationException(
                    "Pause food freeze E2E failed because progress check returned true while paused.");
            }

            if (Mathf.Abs(elapsedPaused - elapsedBaseline) > 0.01f)
            {
                throw new InvalidOperationException(
                    $"Pause food freeze E2E failed: elapsed changed while paused. Baseline: {elapsedBaseline}, Paused: {elapsedPaused}");
            }

            if (Mathf.Abs(foodPaused - foodBaseline) > 0.01f)
            {
                throw new InvalidOperationException(
                    $"Pause food freeze E2E failed: food changed while paused. Baseline: {foodBaseline}, Paused: {foodPaused}");
            }

            Debug.Log(
                $"[Framework M1 E2E] Pause food freeze passed. Elapsed held at {elapsedBaseline:0.#}s, Food held at {foodBaseline:0.#}.");
        }

        private static void RunFailedSettlementScreenE2E(TestContext context)
        {
            var caravan = CreateSampleCaravan(context.GameTime);
            caravan.foodAmount = 0;
            caravan.starveGraceSeconds = 0f;

            var tradeId = "editor_failed_settlement";
            var startResult = context.TradeStart.TryStartTrade(caravan, DistanceKm, tradeId, RouteId);
            if (!startResult.canDepart || !context.TradeStart.LastRecordSucceeded)
            {
                throw new InvalidOperationException("Failed settlement screen E2E failed to start trade.");
            }

            context.Coordinator.SetActiveCaravan(caravan);
            BackdateActiveTradeStart(context.SaveData, 1d);
            var settlementReady = context.Coordinator.CheckProgressAndCompletion(saveProgress: false);
            if (!settlementReady || context.Coordinator.LastSettlementResult == null)
            {
                throw new InvalidOperationException("Failed settlement screen E2E failed because settlement was not created.");
            }

            var result = context.Coordinator.LastSettlementResult;
            if (result.grade != JourneyResultGrade.Failed)
            {
                throw new InvalidOperationException(
                    $"Failed settlement screen E2E failed: grade was {result.grade}, expected Failed.");
            }

            if (result.failureReason != JourneyFailureReason.FoodDepleted)
            {
                throw new InvalidOperationException(
                    $"Failed settlement screen E2E failed: failureReason was {result.failureReason}, expected FoodDepleted.");
            }

            if (context.SaveData.tradeProgress.state != TradeProgressState.SettlementPending)
            {
                throw new InvalidOperationException(
                    $"Failed settlement screen E2E failed: trade state was {context.SaveData.tradeProgress.state}, expected SettlementPending.");
            }

            if (context.ScreenRouter.CurrentScreenState != InGameScreenState.Settlement)
            {
                throw new InvalidOperationException(
                    $"Failed settlement screen E2E failed: screen was {context.ScreenRouter.CurrentScreenState}, expected Settlement.");
            }

            var viewData = new SettlementViewData(
                tradeId,
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
                throw new InvalidOperationException(
                    "Failed settlement screen E2E failed because SettlementViewData.IsFailed was false.");
            }

            var firstClaim = context.Coordinator.ClaimSettlementAndReset();
            var duplicateClaim = context.Coordinator.ClaimSettlementAndReset();
            if (!firstClaim || duplicateClaim)
            {
                throw new InvalidOperationException(
                    $"Failed settlement screen E2E failed claim validation. First: {firstClaim}, Duplicate: {duplicateClaim}");
            }

            if (context.SaveData.tradeProgress.state != TradeProgressState.Failed)
            {
                throw new InvalidOperationException(
                    $"Failed settlement screen E2E failed: post-claim state was {context.SaveData.tradeProgress.state}, expected Failed.");
            }

            var activeCaravan = context.Coordinator.ActiveCaravan;
            if (activeCaravan == null || activeCaravan.state != JourneyState.Prepare)
            {
                throw new InvalidOperationException(
                    "Failed settlement screen E2E failed to return caravan to Prepare.");
            }

            if (context.ScreenRouter.CurrentScreenState != InGameScreenState.Preparation)
            {
                throw new InvalidOperationException(
                    $"Failed settlement screen E2E failed: post-claim screen was {context.ScreenRouter.CurrentScreenState}, expected Preparation.");
            }

            Debug.Log(
                "[Framework M1 E2E] Failed settlement screen passed. Failed grade -> Settlement -> claim -> Preparation/Failed state.");
        }

        private static void BackdateActiveTradeStart(SaveData saveData, double realSeconds)
        {
            if (saveData?.tradeProgress == null || saveData.tradeProgress.tradeStartUtcTick <= 0)
            {
                throw new InvalidOperationException("InGame food consumption E2E failed because trade start tick is missing.");
            }

            var startUtc = new DateTime(saveData.tradeProgress.tradeStartUtcTick, DateTimeKind.Utc);
            saveData.tradeProgress.tradeStartUtcTick = startUtc.AddSeconds(-realSeconds).Ticks;
        }

        private static CaravanData CreateSampleCaravan(IInGameTimeProvider inGameTimeProvider)
        {
            var caravan = new CaravanData
            {
                wagon = new imsiWagonData
                {
                    wagonName = "Editor Test Wagon",
                    overLoad = 30f,
                    maxLoad = 60f,
                    minAnimals = 1,
                    maxAnimals = 5,
                    maxDurability = 100,
                    inventorySlotCount = 8
                },
                foodAmount = 30,
                starveGraceSeconds = StarveGraceSeconds
            };

            caravan.animals.Add(new imsiAnimalData
            {
                animalName = "Editor Horse",
                foodPerKm = SampleRawFoodConsumptionPerDay,
                animalType = DraftAnimalType.Horse,
                increaseOverLoad = 5f
            });
            caravan.animals.Add(new imsiAnimalData
            {
                animalName = "Editor Horse",
                foodPerKm = SampleRawFoodConsumptionPerDay,
                animalType = DraftAnimalType.Horse,
                increaseOverLoad = 5f
            });

            var item = new imsiTradeItemData
            {
                id = ItemId,
                itemName = "Editor Wheat",
                weight = 5f,
                basePrice = 10,
                maxCount = 10
            };
            caravan.cargo.Add(new CargoEntry { item = item, quantity = 5 });
            caravan.currentDurability = caravan.wagon.maxDurability;

            CaravanConsumptionRateNormalizer.ApplyToCaravan(caravan, inGameTimeProvider);

            return caravan;
        }

        private sealed class TestContext
        {
            public SaveData SaveData { get; private set; }

            public GameTimeService GameTime { get; private set; }

            public JsonSaveService SaveService { get; private set; }

            public ISharedGameDataProvider SharedGameData { get; private set; }

            public TradeProgressCoordinator Coordinator { get; private set; }

            public TradeStartService TradeStart { get; private set; }

            public InGameScreenStateRouter ScreenRouter { get; private set; }

            public static TestContext Create()
            {
                var policyConfig = Resources.Load<InGameTimePolicyConfig>(InGameTimePolicyConfig.ResourceName);
                if (policyConfig == null)
                {
                    policyConfig = ScriptableObject.CreateInstance<InGameTimePolicyConfig>();
                }

                var gameTime = new GameTimeService(policyConfig);
                var saveService = new JsonSaveService();
                var sharedGameDataService = new SharedGameDataService();
                if (!sharedGameDataService.LoadInitialData())
                {
                    throw new InvalidOperationException(
                        "Shared game data load failed: " + sharedGameDataService.LastErrorSummary);
                }

                var sharedGameData = sharedGameDataService.CurrentData;
                if (sharedGameData == null || !sharedGameData.IsLoaded)
                {
                    throw new InvalidOperationException("Shared game data provider is missing after load.");
                }

                var saveData = saveService.CreateNewGameData();
                var recorder = new TradeProgressRecorder(gameTime, gameTime);
                var router = new InGameScreenStateRouter();
                var coordinator = new TradeProgressCoordinator(
                    () => saveData,
                    saveService,
                    gameTime,
                    recorder,
                    router,
                    gameTime,
                    () => sharedGameData);
                var tradeStart = new TradeStartService(
                    () => saveData,
                    saveService,
                    recorder,
                    router,
                    coordinator.ClearSettlementCache);

                return new TestContext
                {
                    SaveData = saveData,
                    GameTime = gameTime,
                    SaveService = saveService,
                    SharedGameData = sharedGameData,
                    Coordinator = coordinator,
                    TradeStart = tradeStart,
                    ScreenRouter = router
                };
            }
        }
    }
}
#endif
