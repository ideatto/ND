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
 * - Unity batchmode -executeMethod 진입점 제공.
 *
 * Usage for Team Members
 * - Unity Editor: ND/Framework/Run M1 Loop + Economy E2E Checks
 * - CI/batchmode: ND.Framework.Editor.FrameworkM1LoopE2EEditorTests.RunAllFromBatchMode
 *
 * Important Notes
 * - Editor 전용이며 Player build에 포함되지 않는다.
 * - JsonSaveService는 persistentDataPath에 저장할 수 있으나 테스트는 in-memory SaveData 참조를 우선 사용한다.
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

        /// <summary>
        /// loop integrity smoke와 Economy E2E 검증을 순서대로 실행한다.
        /// </summary>
        [MenuItem("ND/Framework/Run M1 Loop + Economy E2E Checks")]
        public static void RunAll()
        {
            var context = TestContext.Create();
            RunLoopIntegritySmoke(context);
            RunEconomyE2E(context);
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
                var caravan = CreateSampleCaravan();
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
                var caravan = CreateSampleCaravan();
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

        private static CaravanData CreateSampleCaravan()
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
                foodPerKm = 0.1f,
                animalType = DraftAnimalType.Horse,
                increaseOverLoad = 5f
            });
            caravan.animals.Add(new imsiAnimalData
            {
                animalName = "Editor Horse",
                foodPerKm = 0.1f,
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
                    TradeStart = tradeStart
                };
            }
        }
    }
}
#endif
