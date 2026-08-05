/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Multi-active runtime registry, online tick, offline restore, explicit Economy ID, and claim regression paths를
 *   현재 production 서비스 조립으로 Editor에서 검증한다.
 *
 * Usage for Team Members
 * - Unity Editor: ND/Framework/Run Multi-active Progress E2E Checks
 * - CI/batchmode: ND.Framework.Editor.FrameworkM1LoopE2EEditorTests.RunAllFromBatchMode
 *
 * Important Notes
 * - Editor 전용이며 Player build에 포함되지 않는다.
 * - 성공 로그는 정적·Editor 경로 검증 결과이며 Play Mode runtime PASS를 의미하지 않는다.
 * - Claim regression은 Depart → ForceComplete → empty arrival sale(또는 failed settle) →
 *   SettlementPending → ClaimSettlement 경로로 Lucky 소비/유지와 town 정합을 검증한다.
 * - 출발 fixture는 BaseToRiver의 FromTown(BaseCamp)으로 caravan/player currentTownId를 맞춘다.
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using ND.Economy;
using UnityEditor;
using UnityEngine;

namespace ND.Framework.Editor
{
    public static class FrameworkM1LoopE2EEditorTests
    {
        private const string RouteId = "BaseToRiver";
        private const string DepartureTownId = "BaseCamp";
        private const string ItemId = "Apple";
        private const float DistanceKm = 100f;

        [MenuItem("ND/Framework/Run Multi-active Progress E2E Checks")]
        public static void RunAll()
        {
            RunOnlineTickLifecycleGateChecks();
            RunRuntimeRegistryChecks();
            RunMultiActiveOnlineTickChecks();
            RunMultiActiveOfflineRestoreChecks();
            RunExplicitEconomyTradeIdChecks();
            RunTradePreparationCommitChecks();
            RunSettlementPresentationIdentityChecks();
            RunClaimRegressionChecks();
            RunExactForcedTradeCompletionChecks();
            Debug.Log("[Framework Multi-active E2E] All checks passed.");
        }

        public static void RunAllFromBatchMode()
        {
            RunAll();
        }

        [MenuItem("ND/Framework/Run Work F Currency Command Focused Checks")]
        public static void RunWorkFCurrencyCommandFocusedChecks()
        {
            RunTradingCurrencySuccessCheck();
            RunDevelopmentCurrencySuccessCheck();
            RunCurrencyValidationChecks();
            RunCurrencyOverflowChecks();
            RunCurrencySaveRollbackChecks();
            RunCurrencyMissingDependencyCheck();
            Debug.Log("Work F currency command focused checks passed");
        }

        private static void RunTradingCurrencySuccessCheck()
        {
            var saveData = new SaveData();
            saveData.player.tradingCurrency = 1000;
            saveData.player.developmentCurrency = 200;
            var save = new ConfigurableSaveService();
            var commands = new FrameworkDebugCommands(null, () => saveData, save);
            var eventCount = 0;
            var eventValue = 0L;
            Action<long> onChanged = value =>
            {
                eventCount++;
                eventValue = value;
            };
            FrameworkEvents.TradingCurrencyChanged += onChanged;
            SaveResult result;
            try
            {
                result = commands.TryAddTradingCurrency(100);
            }
            finally
            {
                FrameworkEvents.TradingCurrencyChanged -= onChanged;
            }

            if (!result.Succeeded
                || saveData.player.tradingCurrency != 1100
                || saveData.player.developmentCurrency != 200
                || save.SaveCalls != 1
                || eventCount != 1
                || eventValue != 1100)
            {
                throw new InvalidOperationException("Trading currency success transaction check failed.");
            }
        }

        private static void RunDevelopmentCurrencySuccessCheck()
        {
            var saveData = new SaveData();
            saveData.player.tradingCurrency = 1000;
            saveData.player.developmentCurrency = 200;
            var save = new ConfigurableSaveService();
            var commands = new FrameworkDebugCommands(null, () => saveData, save);
            var tradingEventCount = 0;
            Action<long> onChanged = _ => tradingEventCount++;
            FrameworkEvents.TradingCurrencyChanged += onChanged;
            SaveResult result;
            try
            {
                result = commands.TryAddDevelopmentCurrency(100);
            }
            finally
            {
                FrameworkEvents.TradingCurrencyChanged -= onChanged;
            }

            if (!result.Succeeded
                || saveData.player.developmentCurrency != 300
                || saveData.player.tradingCurrency != 1000
                || save.SaveCalls != 1
                || tradingEventCount != 0)
            {
                throw new InvalidOperationException("Development currency success transaction check failed.");
            }
        }

        private static void RunCurrencyValidationChecks()
        {
            VerifyRejectedCurrencyAmount(0);
            VerifyRejectedCurrencyAmount(-1);
        }

        private static void VerifyRejectedCurrencyAmount(long amount)
        {
            var saveData = new SaveData();
            var save = new ConfigurableSaveService();
            var commands = new FrameworkDebugCommands(null, () => saveData, save);
            var tradingBefore = saveData.player.tradingCurrency;
            var developmentBefore = saveData.player.developmentCurrency;
            var eventCount = 0;
            Action<long> onChanged = _ => eventCount++;
            FrameworkEvents.TradingCurrencyChanged += onChanged;
            try
            {
                var tradingResult = commands.TryAddTradingCurrency(amount);
                var developmentResult = commands.TryAddDevelopmentCurrency(amount);
                if (tradingResult.Succeeded
                    || developmentResult.Succeeded
                    || tradingResult.FailureReason != SaveFailureReason.InvalidData
                    || developmentResult.FailureReason != SaveFailureReason.InvalidData
                    || saveData.player.tradingCurrency != tradingBefore
                    || saveData.player.developmentCurrency != developmentBefore
                    || save.SaveCalls != 0
                    || eventCount != 0)
                {
                    throw new InvalidOperationException(
                        $"Currency amount {amount} was not rejected without side effects.");
                }
            }
            finally
            {
                FrameworkEvents.TradingCurrencyChanged -= onChanged;
            }
        }

        private static void RunCurrencyOverflowChecks()
        {
            var saveData = new SaveData();
            saveData.player.tradingCurrency = long.MaxValue;
            saveData.player.developmentCurrency = long.MaxValue;
            var save = new ConfigurableSaveService();
            var commands = new FrameworkDebugCommands(null, () => saveData, save);
            var eventCount = 0;
            Action<long> onChanged = _ => eventCount++;
            FrameworkEvents.TradingCurrencyChanged += onChanged;
            try
            {
                var tradingResult = commands.TryAddTradingCurrency(1);
                var developmentResult = commands.TryAddDevelopmentCurrency(1);
                if (tradingResult.Succeeded
                    || developmentResult.Succeeded
                    || saveData.player.tradingCurrency != long.MaxValue
                    || saveData.player.developmentCurrency != long.MaxValue
                    || save.SaveCalls != 0
                    || eventCount != 0)
                {
                    throw new InvalidOperationException("Currency overflow was not rejected before mutation.");
                }
            }
            finally
            {
                FrameworkEvents.TradingCurrencyChanged -= onChanged;
            }
        }

        private static void RunCurrencySaveRollbackChecks()
        {
            VerifyCurrencySaveRollback(true);
            VerifyCurrencySaveRollback(false);
        }

        private static void VerifyCurrencySaveRollback(bool trading)
        {
            var saveData = new SaveData();
            saveData.player.tradingCurrency = 1000;
            saveData.player.developmentCurrency = 200;
            var player = saveData.player;
            var save = new ConfigurableSaveService { ShouldSucceed = false };
            var commands = new FrameworkDebugCommands(null, () => saveData, save);
            var eventCount = 0;
            Action<long> onChanged = _ => eventCount++;
            FrameworkEvents.TradingCurrencyChanged += onChanged;
            try
            {
                var result = trading
                    ? commands.TryAddTradingCurrency(100)
                    : commands.TryAddDevelopmentCurrency(100);
                if (result.Succeeded
                    || result.FailureReason != SaveFailureReason.WriteFailed
                    || !ReferenceEquals(saveData.player, player)
                    || saveData.player.tradingCurrency != 1000
                    || saveData.player.developmentCurrency != 200
                    || save.SaveCalls != 1
                    || eventCount != 0)
                {
                    throw new InvalidOperationException(
                        $"{(trading ? "Trading" : "Development")} currency save rollback check failed.");
                }
            }
            finally
            {
                FrameworkEvents.TradingCurrencyChanged -= onChanged;
            }
        }

        private static void RunCurrencyMissingDependencyCheck()
        {
            var commands = new FrameworkDebugCommands(null, () => null, null);
            var result = commands.TryAddTradingCurrency(100);
            if (result.Succeeded
                || result.FailureReason != SaveFailureReason.InvalidData
                || result.FailedDataCategory != "tradingCurrency")
            {
                throw new InvalidOperationException("Missing currency command dependency was not rejected.");
            }
        }

        private static void RunTradePreparationCommitChecks()
        {
            var saveData = new SaveData();
            string caravanA = saveData.selectedCaravanId;
            // NewCaravanId() is internal to the runtime assembly; Editor tests use the public GUID helper.
            string caravanB = SaveDataLookup.NewInstanceId();
            saveData.caravans.Add(new CaravanSaveData { caravanId = caravanB });
            string caravanC = SaveDataLookup.NewInstanceId();
            saveData.caravans.Add(new CaravanSaveData { caravanId = caravanC });
            var store = new FrameworkTradePrepareCommitStore(() => saveData);
            var commitA = CreateCommit(caravanA, "trade-a", "town-a");
            var commitB = CreateCommit(caravanB, "trade-b", "town-b");

            if (!store.TryStage(commitA)
                || !store.TryStage(commitB)
                || saveData.tradePreparationCommits.Count != 2
                || !store.TryGet(caravanA, "trade-a", out var storedA)
                || storedA.selectedDestinationTownId != "town-a"
                || !store.TryGet(caravanB, "trade-b", out var storedB)
                || storedB.selectedDestinationTownId != "town-b")
            {
                throw new InvalidOperationException("Exact multi-commit coexistence check failed.");
            }

            if (!store.TryStage(CreateCommit(caravanA, "trade-a", "changed"))
                || saveData.tradePreparationCommits.Count != 2
                || !store.TryGet(caravanA, "trade-a", out storedA)
                || storedA.selectedDestinationTownId != "town-a")
            {
                throw new InvalidOperationException("Exact duplicate commit stage was not idempotent.");
            }

            if (store.TryStage(CreateCommit(caravanA, "trade-a2", "town-a2"))
                || !store.TryGet(caravanA, "trade-a", out _))
            {
                throw new InvalidOperationException("Same-Caravan conflicting commit was not rejected.");
            }

            if (store.TryStage(CreateCommit(caravanB, "trade-a", "town-b"))
                || saveData.tradePreparationCommits.Count != 2)
            {
                throw new InvalidOperationException("Cross-Caravan duplicate trade identity was not rejected.");
            }

            if (!store.TryStage(CreateCommit(caravanC, "trade-c", "town-c")))
                throw new InvalidOperationException("Rollback isolation setup failed.");
            store.Rollback(caravanC, "trade-c");
            if (store.TryGet(caravanC, "trade-c", out _)
                || !store.TryGet(caravanA, "trade-a", out _)
                || !store.TryGet(caravanB, "trade-b", out _))
            {
                throw new InvalidOperationException("Exact rollback changed an unrelated Caravan commit.");
            }

            if (!store.TryComplete(caravanA, "trade-a", out _)
                || store.TryGet(caravanA, "trade-a", out _)
                || !store.TryGet(caravanB, "trade-b", out storedB)
                || storedB.selectedDestinationTownId != "town-b")
            {
                throw new InvalidOperationException("Completing one commit changed another Caravan commit.");
            }

            var legacyData = new SaveData();
            string legacyCaravanId = legacyData.selectedCaravanId;
            legacyData.tradeProgressEntries.Add(new TradeProgressSaveData
            {
                caravanId = legacyCaravanId,
                activeTradeId = "legacy-trade"
            });
            legacyData.tradePreparationCommits = null;
            legacyData.tradePreparationCommit = new TradePreparationCommitSaveData
            {
                hasCommit = true,
                tradeId = "legacy-trade",
                destinationTownId = "legacy-town"
            };

            if (!FrameworkTradePrepareCommitStore.Normalize(legacyData)
                || legacyData.tradePreparationCommits.Count != 1
                || legacyData.tradePreparationCommits[0].caravanId != legacyCaravanId
                || legacyData.tradePreparationCommit.hasCommit
                || FrameworkTradePrepareCommitStore.Normalize(legacyData))
            {
                throw new InvalidOperationException("Legacy commit migration was not exact and idempotent.");
            }

            var receipt = PendingSettlementSaveDataMapper.ToSave(
                new JourneyResultData(), "receipt-trade", RouteId);
            receipt.caravanId = caravanA;
            var wrongOwner = new TradePreparationCommitSaveData
            {
                hasCommit = true,
                caravanId = caravanB,
                tradeId = receipt.tradeId,
                purchaseCost = 999L
            };
            PendingSettlementSaveDataMapper.ApplyPreparation(receipt, wrongOwner);
            if (receipt.purchaseCost != 0L)
                throw new InvalidOperationException("A preparation receipt crossed Caravan ownership.");

            var exactOwner = new TradePreparationCommitSaveData
            {
                hasCommit = true,
                caravanId = caravanA,
                tradeId = receipt.tradeId,
                purchaseCost = 120L,
                mercenaryCost = 30L,
                purchasedItems = new List<TradePreparationItemSaveData>
                {
                    new TradePreparationItemSaveData
                    {
                        itemId = ItemId,
                        quantity = 2,
                        purchaseUnitPrice = 60L
                    }
                }
            };
            PendingSettlementSaveDataMapper.ApplyPreparation(receipt, exactOwner);
            var receiptCopy = PendingSettlementSaveDataMapper.Copy(receipt);
            receipt.purchasedItems[0].quantity = 9;
            if (receiptCopy.purchaseCost != 120L
                || receiptCopy.mercenaryCost != 30L
                || receiptCopy.purchasedItems.Count != 1
                || receiptCopy.purchasedItems[0].quantity != 2
                || receiptCopy.purchasedItems[0].totalAmount != 120L)
            {
                throw new InvalidOperationException(
                    "Pending settlement receipt was not copied completely and independently.");
            }
        }

        private static global::TradePrepareCommitData CreateCommit(
            string caravanId,
            string tradeId,
            string destinationTownId)
        {
            return new global::TradePrepareCommitData
            {
                caravanId = caravanId,
                tradeId = tradeId,
                currentTownId = "BaseCamp",
                selectedDestinationTownId = destinationTownId,
                routeId = RouteId
            };
        }

        private static void RunOnlineTickLifecycleGateChecks()
        {
            var save = new ConfigurableSaveService();
            var context = TestContext.Create(save);
            InitializeSelectedCaravan(context);
            var command = CreateDepartureCommand(context, save);
            var departure = command.Depart(new TradeDepartureRequest
            {
                CaravanId = context.SaveData.selectedCaravanId,
                RouteId = RouteId
            });
            if (!departure.DepartureSucceeded)
                throw new InvalidOperationException("Online tick gate setup departure failed.");

            SaveDataLookup.TryGetTradeProgress(
                context.SaveData, context.SaveData.selectedCaravanId, out var progress);
            var now = context.GameTime.CurrentUtc;
            progress.tradeStartUtcTick = now.AddMinutes(-2).Ticks;
            progress.expectedTradeEndUtcTick = now.AddMinutes(-1).Ticks;
            context.SaveData.lastSavedUtcTicks = now.AddMinutes(-3).Ticks;
            var savesBeforeGate = save.SaveCalls;
            var readyCount = 0;
            var offlineCount = 0;
            Action<string, string, JourneyResultData> onReady = (_, __, ___) => readyCount++;
            Action<string> onOffline = _ => offlineCount++;
            FrameworkEvents.TradeSettlementReady += onReady;
            FrameworkEvents.TradeOfflineCompleted += onOffline;
            try
            {
                if (CanRunOnlineProgressTick(
                        isEnabled: false,
                        context.SaveData,
                        context.SharedGameData))
                {
                    context.Coordinator.CheckProgressAndCompletion(saveProgress: false);
                }

                if (progress.state != TradeProgressState.Traveling
                    || context.SaveData.pendingSettlements.Count != 0
                    || save.SaveCalls != savesBeforeGate
                    || readyCount != 0
                    || offlineCount != 0)
                {
                    throw new InvalidOperationException(
                        "Disabled online tick consumed a Traveling entry before offline restore.");
                }

                // 성공 도착은 Selling으로 들어가며, Claim 전 empty/arrival sale 완료 후에만
                // SettlementPending이 된다.
                if (!context.Coordinator.ApplyOfflineProgressOnLoad(context.SaveData)
                    || progress.state != TradeProgressState.Selling
                    || offlineCount != 1
                    || readyCount != 1)
                {
                    throw new InvalidOperationException(
                        "Offline restore did not run before online tick activation.");
                }

                var pendingCount = context.SaveData.pendingSettlements.Count;
                if (!CanRunOnlineProgressTick(
                        isEnabled: true,
                        context.SaveData,
                        context.SharedGameData))
                {
                    throw new InvalidOperationException(
                        "Online tick gate did not enable after load prerequisites completed.");
                }
                context.Coordinator.CheckProgressAndCompletion(saveProgress: false);
                if (context.SaveData.pendingSettlements.Count != pendingCount
                    || readyCount != 1
                    || offlineCount != 1)
                {
                    throw new InvalidOperationException(
                        "Online tick duplicated settlement after offline restore.");
                }

                if (CanRunOnlineProgressTick(
                        isEnabled: false,
                        context.SaveData,
                        context.SharedGameData))
                {
                    throw new InvalidOperationException(
                        "Title-session disable state still allowed online tick.");
                }
            }
            finally
            {
                FrameworkEvents.TradeSettlementReady -= onReady;
                FrameworkEvents.TradeOfflineCompleted -= onOffline;
            }
        }

        private static bool CanRunOnlineProgressTick(
            bool isEnabled,
            SaveData saveData,
            ISharedGameDataProvider sharedGameData)
        {
            var method = typeof(FrameworkRoot).GetMethod(
                "CanRunOnlineProgressTick",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null)
                throw new MissingMethodException("FrameworkRoot online tick gate was not found.");
            return (bool)method.Invoke(null, new object[] { isEnabled, saveData, sharedGameData });
        }

        private static void RunRuntimeRegistryChecks()
        {
            var context = TestContext.Create();
            var selectedId = context.SaveData.selectedCaravanId;
            var secondSave = AddCaravan(context, "registry-second");

            context.Coordinator.RebuildRuntimeCaravans();
            if (!context.Coordinator.TryGetRuntimeCaravan(selectedId, out var selectedRuntime)
                || !context.Coordinator.TryGetRuntimeCaravan(secondSave.caravanId, out var secondRuntime)
                || ReferenceEquals(selectedRuntime, secondRuntime)
                || !ReferenceEquals(context.Coordinator.ActiveCaravan, selectedRuntime))
            {
                throw new InvalidOperationException("Runtime registry did not retain every Caravan by ID.");
            }

            context.SaveData.selectedCaravanId = secondSave.caravanId;
            if (!ReferenceEquals(context.Coordinator.ActiveCaravan, secondRuntime)
                || !context.Coordinator.RegisterRuntimeCaravan(secondSave.caravanId, secondRuntime))
            {
                throw new InvalidOperationException("Runtime registry lookup changed or rejected the same registered instance.");
            }

            var duplicate = CaravanSaveDataMapper.ToRuntime(secondSave);
            if (context.Coordinator.RegisterRuntimeCaravan(secondSave.caravanId, duplicate))
            {
                throw new InvalidOperationException("Runtime registry replaced an existing ID with another instance.");
            }
        }

        private static void RunMultiActiveOnlineTickChecks()
        {
            var save = new ConfigurableSaveService();
            var context = TestContext.Create(save);
            var firstId = context.SaveData.selectedCaravanId;
            InitializeSelectedCaravan(context);
            var secondSave = AddCaravan(context, "online-second");
            var command = CreateDepartureCommand(context, save);
            var firstDeparture = command.Depart(new TradeDepartureRequest
            {
                CaravanId = firstId,
                RouteId = RouteId
            });
            var secondDeparture = command.Depart(new TradeDepartureRequest
            {
                CaravanId = secondSave.caravanId,
                RouteId = RouteId
            });
            if (!firstDeparture.DepartureSucceeded || !secondDeparture.DepartureSucceeded)
                throw new InvalidOperationException("Multi-active online setup departure failed.");

            SaveDataLookup.TryGetTradeProgress(context.SaveData, firstId, out var firstProgress);
            SaveDataLookup.TryGetTradeProgress(context.SaveData, secondSave.caravanId, out var secondProgress);
            var now = context.GameTime.CurrentUtc;
            firstProgress.tradeStartUtcTick = now.AddMinutes(-2).Ticks;
            firstProgress.expectedTradeEndUtcTick = now.AddMinutes(-1).Ticks;
            secondProgress.tradeStartUtcTick = now.AddMinutes(-1).Ticks;
            secondProgress.expectedTradeEndUtcTick = now.AddMinutes(1).Ticks;
            context.SaveData.tradeProgressEntries.Insert(0, null);
            context.SaveData.tradeProgressEntries.Insert(1, new TradeProgressSaveData
            {
                caravanId = "missing-online-caravan",
                activeTradeId = "missing-online-trade",
                state = TradeProgressState.Traveling,
                tradeStartUtcTick = now.AddMinutes(-1).Ticks,
                expectedTradeEndUtcTick = now.AddMinutes(1).Ticks
            });
            context.SaveData.selectedCaravanId = secondSave.caravanId;

            var selectedBefore = context.SaveData.selectedCaravanId;
            var savesBefore = save.SaveCalls;
            var ready = new List<string>();
            Action<string, string, JourneyResultData> onReady =
                (caravanId, tradeId, _) => ready.Add(caravanId + ":" + tradeId);
            FrameworkEvents.TradeSettlementReady += onReady;
            try
            {
                if (!context.Coordinator.CheckProgressAndCompletion())
                    throw new InvalidOperationException("Multi-active online tick did not report completion.");
            }
            finally
            {
                FrameworkEvents.TradeSettlementReady -= onReady;
            }

            if (firstProgress.state != TradeProgressState.Selling
                || secondProgress.state != TradeProgressState.Traveling
                || context.SaveData.selectedCaravanId != selectedBefore
                || !SaveDataLookup.TryGetPendingSettlement(
                    context.SaveData, firstId, firstDeparture.TradeId, out _)
                || ready.Count != 1
                || ready[0] != firstId + ":" + firstDeparture.TradeId
                || save.SaveCalls != savesBefore + 1)
            {
                throw new InvalidOperationException(
                    "Online tick did not isolate IDs, selection, invalid entries, events, or the batch save.");
            }

            RunSimultaneousOnlineCompletionCheck();
            RunSaveFailureEventSuppressionCheck();
        }

        private static void RunSimultaneousOnlineCompletionCheck()
        {
            var save = new ConfigurableSaveService();
            var context = TestContext.Create(save);
            var firstId = context.SaveData.selectedCaravanId;
            InitializeSelectedCaravan(context);
            var secondSave = AddCaravan(context, "online-simultaneous");
            var command = CreateDepartureCommand(context, save);
            var first = command.Depart(new TradeDepartureRequest { CaravanId = firstId, RouteId = RouteId });
            var second = command.Depart(new TradeDepartureRequest { CaravanId = secondSave.caravanId, RouteId = RouteId });
            SaveDataLookup.TryGetTradeProgress(context.SaveData, firstId, out var firstProgress);
            SaveDataLookup.TryGetTradeProgress(context.SaveData, secondSave.caravanId, out var secondProgress);
            var now = context.GameTime.CurrentUtc;
            firstProgress.expectedTradeEndUtcTick = now.AddSeconds(-1).Ticks;
            secondProgress.expectedTradeEndUtcTick = now.AddSeconds(-1).Ticks;
            var savesBefore = save.SaveCalls;
            var readyCount = 0;
            Action<string, string, JourneyResultData> onReady = (_, __, ___) => readyCount++;
            FrameworkEvents.TradeSettlementReady += onReady;
            try
            {
                context.Coordinator.CheckProgressAndCompletion(saveProgress: false);
                context.Coordinator.CheckProgressAndCompletion(saveProgress: false);
            }
            finally
            {
                FrameworkEvents.TradeSettlementReady -= onReady;
            }

            if (firstProgress.state != TradeProgressState.Selling
                || secondProgress.state != TradeProgressState.Selling
                || !SaveDataLookup.TryGetPendingSettlement(context.SaveData, firstId, first.TradeId, out _)
                || !SaveDataLookup.TryGetPendingSettlement(
                    context.SaveData, secondSave.caravanId, second.TradeId, out _)
                || readyCount != 2
                || save.SaveCalls != savesBefore + 1)
            {
                throw new InvalidOperationException("Simultaneous online completions were not independently persisted once.");
            }
        }

        private static void RunSaveFailureEventSuppressionCheck()
        {
            var save = new ConfigurableSaveService { ShouldSucceed = false };
            var context = TestContext.Create(save);
            InitializeSelectedCaravan(context);
            var command = CreateDepartureCommand(context, save);
            save.ShouldSucceed = true;
            var departure = command.Depart(new TradeDepartureRequest
            {
                CaravanId = context.SaveData.selectedCaravanId,
                RouteId = RouteId
            });
            SaveDataLookup.TryGetTradeProgress(
                context.SaveData, context.SaveData.selectedCaravanId, out var progress);
            progress.expectedTradeEndUtcTick = context.GameTime.CurrentUtc.AddSeconds(-1).Ticks;
            save.ShouldSucceed = false;
            var readyCount = 0;
            Action<string, string, JourneyResultData> onReady = (_, __, ___) => readyCount++;
            FrameworkEvents.TradeSettlementReady += onReady;
            try
            {
                if (!context.Coordinator.CheckProgressAndCompletion()
                    || progress.activeTradeId != departure.TradeId)
                {
                    throw new InvalidOperationException("Save-failure setup did not reach explicit settlement.");
                }
            }
            finally
            {
                FrameworkEvents.TradeSettlementReady -= onReady;
            }
            if (readyCount != 0)
                throw new InvalidOperationException("Settlement success event was raised before persistence succeeded.");
        }

        private static void RunMultiActiveOfflineRestoreChecks()
        {
            var save = new ConfigurableSaveService();
            var context = TestContext.Create(save);
            var firstId = context.SaveData.selectedCaravanId;
            InitializeSelectedCaravan(context);
            var secondSave = AddCaravan(context, "offline-second");
            var preparingSave = AddCaravan(context, "offline-preparing");
            var command = CreateDepartureCommand(context, save);
            command.Depart(new TradeDepartureRequest { CaravanId = firstId, RouteId = RouteId });
            command.Depart(new TradeDepartureRequest { CaravanId = secondSave.caravanId, RouteId = RouteId });
            SaveDataLookup.TryGetTradeProgress(context.SaveData, firstId, out var firstProgress);
            SaveDataLookup.TryGetTradeProgress(context.SaveData, secondSave.caravanId, out var secondProgress);
            var now = context.GameTime.CurrentUtc;
            firstProgress.activeTradeId = "offline-complete";
            firstProgress.tradeStartUtcTick = now.AddMinutes(-3).Ticks;
            firstProgress.expectedTradeEndUtcTick = now.AddMinutes(-2).Ticks;
            secondProgress.activeTradeId = "offline-traveling";
            secondProgress.tradeStartUtcTick = now.AddMinutes(-1).Ticks;
            secondProgress.expectedTradeEndUtcTick = now.AddMinutes(1).Ticks;
            context.SaveData.tradeProgressEntries.Add(new TradeProgressSaveData
            {
                caravanId = preparingSave.caravanId,
                state = TradeProgressState.Preparing
            });
            context.SaveData.tradeProgressEntries.Insert(0, new TradeProgressSaveData
            {
                caravanId = "missing-offline-caravan",
                activeTradeId = "missing-offline-trade",
                state = TradeProgressState.Traveling,
                tradeStartUtcTick = now.AddMinutes(-1).Ticks,
                expectedTradeEndUtcTick = now.AddMinutes(1).Ticks
            });
            context.SaveData.selectedCaravanId = preparingSave.caravanId;
            context.SaveData.lastSavedUtcTicks = now.AddMinutes(-4).Ticks;

            var selectedBefore = context.SaveData.selectedCaravanId;
            var savesBefore = save.SaveCalls;
            var ready = new List<string>();
            var offline = new List<string>();
            Action<string, string, JourneyResultData> onReady =
                (caravanId, tradeId, _) => ready.Add(caravanId + ":" + tradeId);
            Action<string> onOffline = tradeId => offline.Add(tradeId);
            FrameworkEvents.TradeSettlementReady += onReady;
            FrameworkEvents.TradeOfflineCompleted += onOffline;
            try
            {
                if (!context.Coordinator.ApplyOfflineProgressOnLoad(context.SaveData))
                    throw new InvalidOperationException("Multi-active offline restore did not report completion.");
            }
            finally
            {
                FrameworkEvents.TradeSettlementReady -= onReady;
                FrameworkEvents.TradeOfflineCompleted -= onOffline;
            }

            if (firstProgress.state != TradeProgressState.Selling
                || secondProgress.state != TradeProgressState.Traveling
                || context.SaveData.selectedCaravanId != selectedBefore
                || !SaveDataLookup.TryGetPendingSettlement(
                    context.SaveData, firstId, "offline-complete", out _)
                || ready.Count != 1 || offline.Count != 1
                || ready[0] != firstId + ":offline-complete"
                || offline[0] != "offline-complete"
                || save.SaveCalls != savesBefore + 1)
            {
                throw new InvalidOperationException(
                    "Offline restore did not isolate mixed states, IDs, selection, errors, or the batch save.");
            }
        }

        private static void RunExplicitEconomyTradeIdChecks()
        {
            var context = TestContext.Create();
            var selectedProgress = new TradeProgressSaveData
            {
                caravanId = context.SaveData.selectedCaravanId,
                activeTradeId = "selected-trade",
                activeRouteId = RouteId,
                state = TradeProgressState.Traveling
            };
            context.SaveData.tradeProgressEntries.Add(selectedProgress);
            var explicitProgress = new TradeProgressSaveData
            {
                caravanId = "economy-explicit",
                activeTradeId = "explicit-trade",
                activeRouteId = RouteId,
                state = TradeProgressState.Traveling
            };
            var runtime = CreateSampleCaravan(context.GameTime);
            runtime.caravanId = explicitProgress.caravanId;
            var otherProgress = new TradeProgressSaveData
            {
                caravanId = "economy-other",
                activeTradeId = "other-trade",
                activeRouteId = RouteId,
                state = TradeProgressState.Traveling
            };
            var otherRuntime = CreateSampleCaravan(context.GameTime);
            otherRuntime.caravanId = otherProgress.caravanId;
            context.SaveData.tradePreparationCommits.Add(new TradePreparationCommitSaveData
            {
                hasCommit = true,
                caravanId = explicitProgress.caravanId,
                tradeId = explicitProgress.activeTradeId,
                routeId = RouteId,
                mercenaryCost = 17L
            });
            context.SaveData.tradePreparationCommits.Add(new TradePreparationCommitSaveData
            {
                hasCommit = true,
                caravanId = otherProgress.caravanId,
                tradeId = otherProgress.activeTradeId,
                routeId = RouteId,
                mercenaryCost = 43L
            });
            var durabilityBeforeSettlementInput = runtime.currentDurability;
            var result = new JourneyResultData { durabilityLost = 12f };
            var otherResult = new JourneyResultData();
            var input = FrameworkEconomyM1InputBuilder.TryBuild(
                context.SaveData, explicitProgress, runtime, result, context.SharedGameData);
            var otherInput = FrameworkEconomyM1InputBuilder.TryBuild(
                context.SaveData, otherProgress, otherRuntime, otherResult, context.SharedGameData);
            var bridge = new EconomyM1SettlementBridge();
            if (input == null || input.TradeId != explicitProgress.activeTradeId
                || input.MercenaryCost != 17L
                || input.CartRepairCost != 0L
                || runtime.currentDurability != durabilityBeforeSettlementInput
                || otherInput == null || otherInput.TradeId != otherProgress.activeTradeId
                || otherInput.MercenaryCost != 43L
                || !bridge.TryCalculateAndFill(
                    context.SaveData, explicitProgress, runtime, result, context.SharedGameData)
                || !bridge.TryCalculateAndFill(
                    context.SaveData, otherProgress, otherRuntime, otherResult, context.SharedGameData)
                || !bridge.TryGetPendingResult(
                    explicitProgress.caravanId, explicitProgress.activeTradeId, out _)
                || !bridge.TryGetPendingResult(
                    otherProgress.caravanId, otherProgress.activeTradeId, out _)
                || bridge.TryApplyPendingEconomy(
                    context.SaveData, runtime, selectedProgress.activeTradeId)
                || !bridge.TryApplyPendingEconomy(
                    context.SaveData,
                    runtime,
                    explicitProgress.caravanId,
                    explicitProgress.activeTradeId))
            {
                throw new InvalidOperationException(
                    "Economy settlement did not isolate exact preparation or result identities.");
            }

            bridge.ClearPending(explicitProgress.caravanId, explicitProgress.activeTradeId);
            if (bridge.TryGetPendingResult(
                    explicitProgress.caravanId, explicitProgress.activeTradeId, out _)
                || !bridge.TryGetPendingResult(
                    otherProgress.caravanId, otherProgress.activeTradeId, out _))
            {
                throw new InvalidOperationException(
                    "Clearing one Economy result changed another Caravan result.");
            }
        }

        private static void RunClaimRegressionChecks()
        {
            RunZeroSaleClaimLuckyRegressionChecks();
            RunFailedTradeClaimLuckyRegressionChecks();
        }

        /// <summary>
        /// 성공 도착 후 판매 수량 0 완료 → Claim 경로에서 Lucky 소비/유지 계약을 검증한다.
        /// </summary>
        private static void RunZeroSaleClaimLuckyRegressionChecks()
        {
            var save = new ConfigurableSaveService();
            var context = TestContext.Create(save);
            InitializeSelectedCaravan(context);

            var caravanId = context.SaveData.selectedCaravanId;
            var departure = context.TradeStart.Depart(new TradeDepartureRequest
            {
                CaravanId = caravanId,
                RouteId = RouteId
            });
            if (!departure.DepartureSucceeded)
            {
                throw new InvalidOperationException(
                    "Claim zero-sale regression departure failed: " + departure.FailureReason);
            }

            if (!SaveDataLookup.TryGetTradeProgress(context.SaveData, caravanId, out var progress)
                || string.IsNullOrWhiteSpace(progress.activeTradeId))
            {
                throw new InvalidOperationException(
                    "Claim zero-sale regression did not create an active trade progress entry.");
            }

            var tradeId = progress.activeTradeId;
            context.Coordinator.ForceCompleteActiveTrade();
            if (!SaveDataLookup.TryGetTradeProgress(context.SaveData, caravanId, out progress)
                || progress.state != TradeProgressState.Selling)
            {
                throw new InvalidOperationException(
                    "Claim zero-sale regression did not enter Selling after forced arrival.");
            }

            if (!context.Coordinator.TryCommitEmptyArrivalSaleCompletion(
                    caravanId, tradeId, out var arrivalSaveFailed)
                || arrivalSaveFailed
                || !SaveDataLookup.TryGetTradeProgress(context.SaveData, caravanId, out progress)
                || progress.state != TradeProgressState.SettlementPending)
            {
                throw new InvalidOperationException(
                    "Claim zero-sale regression failed to reach SettlementPending.");
            }

            AssertClaimLuckyConsumptionContract(context, save, caravanId, tradeId, expectDestinationTown: true);
        }

        /// <summary>
        /// 실패 무역이 Selling을 건너뛰고 SettlementPending으로 들어간 뒤 Claim 시 Lucky를 소비하는지 검증한다.
        /// </summary>
        private static void RunFailedTradeClaimLuckyRegressionChecks()
        {
            var save = new ConfigurableSaveService();
            var context = TestContext.Create(save);
            InitializeSelectedCaravan(context);

            var caravanId = context.SaveData.selectedCaravanId;
            var departure = context.TradeStart.Depart(new TradeDepartureRequest
            {
                CaravanId = caravanId,
                RouteId = RouteId
            });
            if (!departure.DepartureSucceeded)
            {
                throw new InvalidOperationException(
                    "Claim failed-trade regression departure failed: " + departure.FailureReason);
            }

            if (!SaveDataLookup.TryGetTradeProgress(context.SaveData, caravanId, out var progress)
                || string.IsNullOrWhiteSpace(progress.activeTradeId)
                || !context.Coordinator.TryGetRuntimeCaravan(caravanId, out var runtime)
                || runtime == null)
            {
                throw new InvalidOperationException(
                    "Claim failed-trade regression setup could not resolve trade runtime state.");
            }

            var tradeId = progress.activeTradeId;
            runtime.runFatalReason = JourneyFailureReason.FoodDepleted;
            context.Coordinator.ForceCompleteActiveTrade();
            if (!SaveDataLookup.TryGetTradeProgress(context.SaveData, caravanId, out progress)
                || progress.state != TradeProgressState.SettlementPending)
            {
                throw new InvalidOperationException(
                    "Claim failed-trade regression did not enter SettlementPending.");
            }

            AssertClaimLuckyConsumptionContract(context, save, caravanId, tradeId, expectDestinationTown: false);
        }

        /// <summary>
        /// Claim Save 실패 시 Lucky 유지, 성공 시 전량 소비, 다른 tradeId 격리, 중복 Claim 거부를 검증한다.
        /// </summary>
        private static void AssertClaimLuckyConsumptionContract(
            TestContext context,
            ConfigurableSaveService save,
            string caravanId,
            string tradeId,
            bool expectDestinationTown)
        {
            var otherTradeId = "claim-regression-other-" + Guid.NewGuid().ToString("N");
            WeatherLuckyStore.Add(tradeId);
            WeatherLuckyStore.Add(tradeId);
            WeatherLuckyStore.Add(otherTradeId);
            try
            {
                var snapshot = JsonUtility.ToJson(context.SaveData);
                save.ShouldSucceed = false;
                var failed = context.Coordinator.ClaimSettlement(caravanId, tradeId);
                if (failed.Succeeded
                    || failed.FailureReason != ClaimSettlementFailureReason.SaveFailed
                    || JsonUtility.ToJson(context.SaveData) != snapshot
                    || !context.Coordinator.TryGetPendingEconomyResult(caravanId, tradeId, out _)
                    || WeatherLuckyStore.GetCount(tradeId) != 2)
                {
                    throw new InvalidOperationException(
                        "Claim save failure did not roll back state or retain Lucky and Economy results.");
                }

                save.ShouldSucceed = true;
                var succeeded = context.Coordinator.ClaimSettlement(caravanId, tradeId);
                if (!succeeded.Succeeded
                    || context.Coordinator.TryGetPendingEconomyResult(caravanId, tradeId, out _)
                    || context.Coordinator.ClaimSettlement(caravanId, tradeId).Succeeded
                    || WeatherLuckyStore.GetCount(tradeId) != 0
                    || WeatherLuckyStore.GetCount(otherTradeId) != 1)
                {
                    throw new InvalidOperationException(
                        "Explicit claim, Lucky consumption, isolation, or duplicate prevention regressed.");
                }

                SaveDataLookup.TryGetCaravan(context.SaveData, caravanId, out var caravanSave);
                if (expectDestinationTown && string.IsNullOrWhiteSpace(caravanSave.currentTownId))
                    throw new InvalidOperationException("Claim did not retain the destination currentTownId.");
            }
            finally
            {
                WeatherLuckyStore.Consume(tradeId);
                WeatherLuckyStore.Consume(otherTradeId);
            }
        }

        private static void RunExactForcedTradeCompletionChecks()
        {
            RunExactForcedTradeSuccessAndIsolationCheck();
            RunExactForcedTradeValidationChecks();
            RunExactForcedTradeSaveRollbackCheck();
        }

        private static void RunExactForcedTradeSuccessAndIsolationCheck()
        {
            var save = new ConfigurableSaveService();
            var context = TestContext.Create(save);
            var caravanA = context.SaveData.selectedCaravanId;
            var caravanB = "force-arrival-b";
            var runtimeA = AddForcedTradeFixture(context, caravanA, "force-trade-a");
            var runtimeB = AddForcedTradeFixture(context, caravanB, "force-trade-b");
            context.SaveData.selectedCaravanId = caravanB;
            var selectedBefore = context.SaveData.selectedCaravanId;
            var saveB = JsonUtility.ToJson(runtimeB);
            var savesBefore = save.SaveCalls;
            var ready = new List<string>();
            Action<string, string, JourneyResultData> onReady =
                (caravanId, tradeId, _) => ready.Add(caravanId + ":" + tradeId);
            FrameworkEvents.TradeSettlementReady += onReady;
            ForcedTradeCompletionResult result;
            try
            {
                result = context.Coordinator.TryForceCompleteTrade(caravanA, "force-trade-a");
            }
            finally
            {
                FrameworkEvents.TradeSettlementReady -= onReady;
            }

            SaveDataLookup.TryGetTradeProgress(context.SaveData, caravanA, out var progressA);
            SaveDataLookup.TryGetTradeProgress(context.SaveData, caravanB, out var progressB);
            if (!result.Succeeded
                || result.FailureReason != ForcedTradeCompletionFailureReason.None
                || progressA.state != TradeProgressState.Selling
                || progressB.state != TradeProgressState.Traveling
                || runtimeA.state != JourneyState.Selling
                || JsonUtility.ToJson(runtimeB) != saveB
                || context.SaveData.selectedCaravanId != selectedBefore
                || !SaveDataLookup.TryGetPendingSettlement(
                    context.SaveData, caravanA, "force-trade-a", out _)
                || SaveDataLookup.TryGetPendingSettlement(
                    context.SaveData, caravanB, "force-trade-b", out _)
                || save.SaveCalls != savesBefore + 1
                || ready.Count != 1
                || ready[0] != caravanA + ":force-trade-a")
            {
                throw new InvalidOperationException(
                    "Exact force arrival did not preserve identity, lifecycle, selection, isolation, save, or event ordering.");
            }

            var duplicate = context.Coordinator.TryForceCompleteTrade(caravanA, "force-trade-a");
            if (duplicate.Succeeded
                || duplicate.FailureReason != ForcedTradeCompletionFailureReason.NotTraveling
                || save.SaveCalls != savesBefore + 1
                || ready.Count != 1)
            {
                throw new InvalidOperationException("Duplicate exact force arrival was not rejected before save.");
            }
        }

        private static void RunExactForcedTradeValidationChecks()
        {
            var save = new ConfigurableSaveService();
            var context = TestContext.Create(save);
            var caravanA = context.SaveData.selectedCaravanId;
            AddForcedTradeFixture(context, caravanA, "force-validation-a");
            var snapshot = JsonUtility.ToJson(context.SaveData);
            var mismatch = context.Coordinator.TryForceCompleteTrade(
                caravanA, "force-validation-other");
            if (mismatch.Succeeded
                || mismatch.FailureReason != ForcedTradeCompletionFailureReason.TradeIdentityMismatch
                || JsonUtility.ToJson(context.SaveData) != snapshot
                || save.SaveCalls != 0)
            {
                throw new InvalidOperationException("Exact force arrival identity mismatch mutated or saved state.");
            }

            SaveDataLookup.TryGetTradeProgress(context.SaveData, caravanA, out var progress);
            progress.state = TradeProgressState.SettlementPending;
            snapshot = JsonUtility.ToJson(context.SaveData);
            var invalidState = context.Coordinator.TryForceCompleteTrade(
                caravanA, "force-validation-a");
            if (invalidState.Succeeded
                || invalidState.FailureReason != ForcedTradeCompletionFailureReason.NotTraveling
                || JsonUtility.ToJson(context.SaveData) != snapshot
                || save.SaveCalls != 0)
            {
                throw new InvalidOperationException("Exact force arrival invalid state mutated or saved state.");
            }
        }

        private static void RunExactForcedTradeSaveRollbackCheck()
        {
            var save = new ConfigurableSaveService { ShouldSucceed = false };
            var context = TestContext.Create(save);
            var caravanA = context.SaveData.selectedCaravanId;
            var caravanB = "force-rollback-b";
            var runtimeA = AddForcedTradeFixture(context, caravanA, "force-rollback-a");
            var runtimeB = AddForcedTradeFixture(context, caravanB, "force-rollback-b");
            context.SaveData.selectedCaravanId = caravanB;
            var saveSnapshot = JsonUtility.ToJson(context.SaveData);
            var runtimeASnapshot = JsonUtility.ToJson(runtimeA);
            var runtimeBSnapshot = JsonUtility.ToJson(runtimeB);
            var readyCount = 0;
            Action<string, string, JourneyResultData> onReady = (_, __, ___) => readyCount++;
            FrameworkEvents.TradeSettlementReady += onReady;
            ForcedTradeCompletionResult result;
            try
            {
                result = context.Coordinator.TryForceCompleteTrade(caravanA, "force-rollback-a");
            }
            finally
            {
                FrameworkEvents.TradeSettlementReady -= onReady;
            }

            if (result.Succeeded
                || result.FailureReason != ForcedTradeCompletionFailureReason.SaveFailed
                || result.SaveResult == null
                || JsonUtility.ToJson(context.SaveData) != saveSnapshot
                || JsonUtility.ToJson(runtimeA) != runtimeASnapshot
                || JsonUtility.ToJson(runtimeB) != runtimeBSnapshot
                || context.Coordinator.LastSettlementResult != null
                || !string.IsNullOrEmpty(context.Coordinator.LastSettlementTradeId)
                || save.SaveCalls != 1
                || readyCount != 0)
            {
                throw new InvalidOperationException(
                    "Exact force arrival save failure did not fully roll back or suppress publication.");
            }
        }

        private static CaravanData AddForcedTradeFixture(
            TestContext context,
            string caravanId,
            string tradeId)
        {
            if (!SaveDataLookup.TryGetCaravan(context.SaveData, caravanId, out var caravanSave))
            {
                caravanSave = new CaravanSaveData { caravanId = caravanId };
                context.SaveData.caravans.Add(caravanSave);
            }

            var runtime = CreateSampleCaravan(context.GameTime);
            runtime.caravanId = caravanId;
            runtime.state = JourneyState.Traveling;
            runtime.progress01 = 0.25f;
            CaravanSaveDataMapper.CopyToSave(runtime, caravanSave);
            context.SaveData.tradeProgressEntries.Add(new TradeProgressSaveData
            {
                caravanId = caravanId,
                activeTradeId = tradeId,
                activeRouteId = "deterministic-force-arrival-route",
                state = TradeProgressState.Traveling,
                tradeStartUtcTick = context.GameTime.CurrentUtc.AddMinutes(-1).Ticks,
                expectedTradeEndUtcTick = context.GameTime.CurrentUtc.AddMinutes(1).Ticks
            });
            context.Coordinator.SetActiveCaravan(runtime);
            return runtime;
        }

        private static void RunSettlementPresentationIdentityChecks()
        {
            var saveData = new SaveData();
            var caravanA = saveData.selectedCaravanId;
            var caravanB = SaveDataLookup.NewInstanceId();
            saveData.caravans.Add(new CaravanSaveData { caravanId = caravanB });
            saveData.selectedCaravanId = caravanB;

            const string tradeA = "presentation-a";
            const string tradeB = "presentation-b";
            saveData.tradeProgressEntries.Add(new TradeProgressSaveData
            {
                caravanId = caravanA,
                activeTradeId = tradeA,
                state = TradeProgressState.SettlementPending
            });
            saveData.tradeProgressEntries.Add(new TradeProgressSaveData
            {
                caravanId = caravanB,
                activeTradeId = tradeB,
                state = TradeProgressState.SettlementPending
            });

            var resultA = new JourneyResultData { grade = JourneyResultGrade.Success };
            var resultB = new JourneyResultData { grade = JourneyResultGrade.Failed };
            var pendingA = PendingSettlementSaveDataMapper.ToSave(resultA, tradeA, RouteId);
            pendingA.caravanId = caravanA;
            var pendingB = PendingSettlementSaveDataMapper.ToSave(resultB, tradeB, RouteId);
            pendingB.caravanId = caravanB;
            saveData.pendingSettlements.Add(pendingA);
            saveData.pendingSettlements.Add(pendingB);

            var bridgeObject = new GameObject("SettlementPresentationIdentityEditorTest");
            try
            {
                var bridge = bridgeObject.AddComponent<SettlementUiBridge>();
                bridge.Initialize(() => saveData, null, new InGameScreenStateRouter());

                // A successful arrival must remain durable without taking the immediate-failure
                // presentation cursor. A later failed Caravan may then present immediately.
                FrameworkEvents.RaiseTradeSettlementReady(caravanA, tradeA, resultA);
                if (bridge.TryGetPendingSettlement(out _, out _, out _))
                {
                    throw new InvalidOperationException(
                        "Successful arrival occupied the immediate-failure settlement cursor.");
                }

                FrameworkEvents.RaiseTradeSettlementReady(caravanB, tradeB, resultB);
                if (!bridge.TryGetPendingSettlement(
                        out var failedCaravanId,
                        out var failedTradeId,
                        out var failedResult)
                    || failedCaravanId != caravanB
                    || failedTradeId != tradeB
                    || failedResult == null
                    || failedResult.grade != JourneyResultGrade.Failed)
                {
                    throw new InvalidOperationException(
                        "Failed arrival did not acquire the empty settlement cursor.");
                }

                bridge.ClearPendingSettlement();
                if (!bridge.PresentSettlement(caravanA, tradeA)
                    || !bridge.TryGetPendingSettlement(
                        out var presentedCaravanId,
                        out var presentedTradeId,
                        out var presentedResult)
                    || presentedCaravanId != caravanA
                    || presentedTradeId != tradeA
                    || presentedResult == null
                    || !SaveDataLookup.TryGetPendingSettlement(saveData, caravanB, tradeB, out _))
                {
                    throw new InvalidOperationException(
                        "Exact settlement presentation followed selected Caravan state.");
                }

                FrameworkEvents.RaiseTradeSettlementReady(caravanB, tradeB, resultB);
                if (!bridge.TryGetPendingSettlement(
                        out presentedCaravanId,
                        out presentedTradeId,
                        out presentedResult)
                    || presentedCaravanId != caravanA
                    || presentedTradeId != tradeA
                    || presentedResult == null
                    || !SaveDataLookup.TryGetPendingSettlement(saveData, caravanB, tradeB, out _))
                {
                    throw new InvalidOperationException(
                        "A later settlement notification overwrote the presented identity.");
                }

                bridge.ClearPendingSettlement();
                if (bridge.ClaimSettlementAndReset()
                    || !SaveDataLookup.TryGetPendingSettlement(saveData, caravanA, tradeA, out _)
                    || !SaveDataLookup.TryGetPendingSettlement(saveData, caravanB, tradeB, out _))
                {
                    throw new InvalidOperationException(
                        "Missing presentation cursor mutated a durable pending settlement.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bridgeObject);
            }
        }

        private static CaravanSaveData AddCaravan(TestContext context, string caravanId)
        {
            var runtime = CreateSampleCaravan(context.GameTime);
            runtime.caravanId = caravanId;
            runtime.currentTownId = DepartureTownId;
            var save = new CaravanSaveData();
            CaravanSaveDataMapper.CopyToSave(runtime, save);
            save.currentTownId = DepartureTownId;
            context.SaveData.caravans.Add(save);
            return save;
        }

        /// <summary>
        /// 선택 Caravan에 출발 가능한 sample loadout을 넣고 BaseToRiver FromTown에 town ID를 맞춘다.
        /// </summary>
        private static void InitializeSelectedCaravan(TestContext context)
        {
            if (!SaveDataLookup.TryGetCaravan(
                    context.SaveData, context.SaveData.selectedCaravanId, out var selectedSave))
            {
                throw new InvalidOperationException("Selected Caravan save data is missing.");
            }

            var runtime = CreateSampleCaravan(context.GameTime);
            runtime.caravanId = selectedSave.caravanId;
            runtime.currentTownId = DepartureTownId;
            CaravanSaveDataMapper.CopyToSave(runtime, selectedSave);
            selectedSave.currentTownId = DepartureTownId;
            context.SaveData.player.currentTownId = DepartureTownId;
        }

        private static TradeStartService CreateDepartureCommand(
            TestContext context,
            ISaveService saveService)
        {
            return new TradeStartService(
                () => context.SaveData,
                saveService,
                new TradeProgressRecorder(context.GameTime, context.GameTime),
                context.ScreenRouter,
                getSharedGameData: () => context.SharedGameData);
        }

        private static CaravanData CreateSampleCaravan(IInGameTimeProvider timeProvider)
        {
            var caravan = new CaravanData
            {
                wagon = new imsiWagonData
                {
                    instanceId = SaveDataLookup.NewInstanceId(),
                    wagonName = "Editor Test Wagon",
                    overLoad = 30f,
                    maxLoad = 60f,
                    minAnimals = 1,
                    maxAnimals = 5,
                    maxDurability = 100,
                    inventorySlotCount = 8
                },
                foodAmount = 30,
                starveGraceSeconds = 5f
            };
            caravan.animals.Add(new imsiAnimalData
            {
                instanceId = SaveDataLookup.NewInstanceId(),
                animalName = "Editor Horse",
                foodPerKm = 8640f,
                animalType = DraftAnimalType.Horse,
                increaseOverLoad = 5f
            });
            caravan.mercenaries.Add(new imsiMercenaryData
            {
                instanceId = SaveDataLookup.NewInstanceId(),
                mercName = "Editor Guard",
                combatPower = 10,
                contractCount = 1
            });
            caravan.cargo.Add(new CargoEntry
            {
                item = new imsiTradeItemData
                {
                    id = ItemId,
                    itemName = "Editor Apple",
                    weight = 5f,
                    basePrice = 10,
                    maxCount = 10
                },
                quantity = 5
            });
            caravan.currentDurability = caravan.wagon.maxDurability;
            CaravanConsumptionRateNormalizer.ApplyToCaravan(caravan, timeProvider);
            return caravan;
        }

        private sealed class ConfigurableSaveService : ISaveService
        {
            public bool ShouldSucceed = true;
            public int SaveCalls;

            public bool HasSaveData() => false;
            public SaveData CreateNewGameData() => new SaveData();
            public SaveData Load() => new SaveData();
            public SaveResult Save(SaveData data)
            {
                SaveCalls++;
                return ShouldSucceed
                    ? SaveResult.Success()
                    : SaveResult.Failure(SaveFailureReason.WriteFailed, "test failure");
            }
            public void ResetSaveData() { }
        }

        private sealed class TestContext
        {
            public SaveData SaveData { get; private set; }
            public GameTimeService GameTime { get; private set; }
            public ISharedGameDataProvider SharedGameData { get; private set; }
            public TradeProgressCoordinator Coordinator { get; private set; }
            public TradeStartService TradeStart { get; private set; }
            public InGameScreenStateRouter ScreenRouter { get; private set; }

            public static TestContext Create(ISaveService saveServiceOverride = null)
            {
                var policy = Resources.Load<InGameTimePolicyConfig>(InGameTimePolicyConfig.ResourceName);
                if (policy == null) policy = ScriptableObject.CreateInstance<InGameTimePolicyConfig>();
                var gameTime = new GameTimeService(policy);
                var saveService = saveServiceOverride ?? new ConfigurableSaveService();
                var sharedService = new SharedGameDataService();
                if (!sharedService.LoadInitialData())
                    throw new InvalidOperationException(
                        "Shared game data load failed: " + sharedService.LastErrorSummary);
                var sharedData = sharedService.CurrentData;
                var saveData = saveService.CreateNewGameData();
                var recorder = new TradeProgressRecorder(gameTime, gameTime);
                var router = new InGameScreenStateRouter();
                var commitStore = new FrameworkTradePrepareCommitStore(() => saveData);
                var coordinator = new TradeProgressCoordinator(
                    () => saveData,
                    saveService,
                    gameTime,
                    recorder,
                    router,
                    gameTime,
                    () => sharedData,
                    commitStore,
                    commitStore);
                var tradeStart = new TradeStartService(
                    () => saveData,
                    saveService,
                    recorder,
                    router,
                    () =>
                    {
                        coordinator.ClearSettlementCache();
                        coordinator.ClearPendingSettlementSave(saveData);
                    },
                    caravan =>
                    {
                        StageCommit(saveData, sharedData, commitStore, caravan.caravanId);
                        coordinator.SetActiveCaravan(caravan);
                    },
                    () => sharedData,
                    coordinator.GetOrCreateRuntimeCaravan);

                return new TestContext
                {
                    SaveData = saveData,
                    GameTime = gameTime,
                    SharedGameData = sharedData,
                    Coordinator = coordinator,
                    TradeStart = tradeStart,
                    ScreenRouter = router
                };
            }

            private static void StageCommit(
                SaveData saveData,
                ISharedGameDataProvider sharedData,
                FrameworkTradePrepareCommitStore commitStore,
                string caravanId)
            {
                if (!SaveDataLookup.TryGetTradeProgress(saveData, caravanId, out var progress)
                    || !sharedData.TryGetRoute(progress.activeRouteId, out var route)
                    || route == null
                    || !commitStore.TryStage(new global::TradePrepareCommitData
                    {
                        caravanId = caravanId,
                        tradeId = progress.activeTradeId,
                        currentTownId = saveData.player.currentTownId,
                        selectedDestinationTownId = route.ToTownId,
                        routeId = progress.activeRouteId
                    }))
                {
                    throw new InvalidOperationException("Test trade preparation commit could not be staged.");
                }
            }
        }
    }
}
#endif
