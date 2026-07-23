/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - develop 무역 3사이클 loop integrity와 Economy settle/claim E2E를 Editor에서 자동 검증한다.
 * - Play mode ContextMenu smoke와 동일한 coordinator 경로를 서비스 조립으로 재현한다.
 *
 * Main Features
 * - Verifies asset ID generation, normalization stability, duplicate repair, and mapper round trips.
 * - caravan ID 기반 플레이어 출발 command의 성공, 중복, 병렬 caravan, pending, 저장 실패와 재진입을 검증한다.
 * - 구조 대출 발급 → 제한 모드 → 무역 출발 → 제한 해제 → 수동 상환 통합 검증.
 * - 원자 claim: 저장 실패 원복, 정상 claim, currentTownId/Town 이벤트, 중복 claim 거부, 재실행 Town 복원 검증.
 * - SharedGameData 로드, 3회 loop integrity, settle 후 currency 불변·claim 후 currency 변화 검증.
 * - 인게임 식량 소모 elapsed 동기화 및 배율 효과 검증.
 * - Pause 중 식량 elapsed 정지, Failed 정산 화면 진입·claim 후 Town 복귀 검증.
 * - PendingSettlementSaveData 저장·캐시 소실 후 복구·claim·손상 케이스 검증.
 * - Traveling 오프라인 미완료·완료·역행 복구 검증.
 * - Unity batchmode -executeMethod 진입점 제공.
 *
 * Usage for Team Members
 * - Unity Editor: ND/Framework/Run M1 Loop + Economy E2E Checks
 * - CI/batchmode: ND.Framework.Editor.FrameworkM1LoopE2EEditorTests.RunAllFromBatchMode
 *
 * Important Notes
 * - Editor 전용이며 Player build에 포함되지 않는다.
 * - JsonSaveService는 persistentDataPath에 저장할 수 있으나 테스트는 in-memory SaveData 참조를 우선 사용한다.
 * - Related Documentation: Docs/Personal_Documents/CSU/0712_m3-offline-progress-pipeline.md
 */
#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using ND.Economy;
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
        // SharedGameData catalog에 존재하는 실제 route ID이다. 원자 claim은 route.ToTownId와 commit destination 일치가 필요하다.
        private const string RouteId = "BaseToRiver";
        private const string ItemId = "Apple";
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
            RunAssetInstanceIdPersistenceChecks();
            RunMultiCaravanSaveDataChecks();
            RunCaravanIdNormalizationPersistenceChecks();
            RunMultiCaravanDepartureCommandChecks();
            RunRescueLoanIntegrationChecks();
            RunTradePreparationCommitStoreE2E();
            RunAtomicSettlementClaimE2E();
            RunArrivalSalePendingE2E();
            RunAutomaticArrivalClaimE2E();
            var context = TestContext.Create();
            RunLoopIntegritySmoke(context);
            RunEconomyE2E(context);
            RunInGameFoodConsumptionE2E(context);
            RunPauseFoodFreezeE2E(TestContext.Create());
            RunFailedSettlementScreenE2E(TestContext.Create());
            RunPendingSettlementRestoreE2E(TestContext.Create());
            RunOfflineProgressE2E(TestContext.Create());
            Debug.Log("[Framework M1 E2E] All checks passed.");
        }

        private static void RunAssetInstanceIdPersistenceChecks()
        {
            var firstGeneratedId = SaveDataLookup.NewInstanceId();
            var secondGeneratedId = SaveDataLookup.NewInstanceId();
            Guid parsedId;
            if (firstGeneratedId.Length != 32 || !Guid.TryParse(firstGeneratedId, out parsedId)
                || firstGeneratedId == secondGeneratedId)
            {
                throw new InvalidOperationException("Asset instance ID generation did not produce unique N-format GUIDs.");
            }

            var data = new SaveData();
            JsonSaveService.NormalizeData(data);
            var caravan = data.caravans[0];
            caravan.wagon.wagonName = "Persistence Wagon";
            caravan.wagon.instanceId = firstGeneratedId;
            caravan.animals.Add(new AnimalSaveData
            {
                instanceId = firstGeneratedId,
                animalName = "Persistence Horse"
            });
            caravan.mercenaries.Add(new MercenarySaveData
            {
                mercName = "Persistence Guard"
            });

            if (!JsonSaveService.NormalizeData(data)
                || caravan.wagon.instanceId != firstGeneratedId
                || string.IsNullOrWhiteSpace(caravan.animals[0].instanceId)
                || caravan.animals[0].instanceId == firstGeneratedId
                || string.IsNullOrWhiteSpace(caravan.mercenaries[0].instanceId))
            {
                throw new InvalidOperationException("Asset instance ID backfill, preservation, or duplicate repair failed.");
            }

            var animalId = caravan.animals[0].instanceId;
            var mercenaryId = caravan.mercenaries[0].instanceId;
            if (JsonSaveService.NormalizeData(data)
                || caravan.wagon.instanceId != firstGeneratedId
                || caravan.animals[0].instanceId != animalId
                || caravan.mercenaries[0].instanceId != mercenaryId)
            {
                throw new InvalidOperationException("Asset instance ID normalization was not stable on a second pass.");
            }

            var runtime = CaravanSaveDataMapper.ToRuntime(caravan);
            var roundTrip = new CaravanSaveData();
            CaravanSaveDataMapper.CopyToSave(runtime, roundTrip);
            if (roundTrip.wagon.instanceId != firstGeneratedId
                || roundTrip.animals.Count != 1 || roundTrip.animals[0].instanceId != animalId
                || roundTrip.mercenaries.Count != 1 || roundTrip.mercenaries[0].instanceId != mercenaryId)
            {
                throw new InvalidOperationException("Asset instance IDs were not preserved by the save/runtime mapper round trip.");
            }

            Debug.Log("[Framework M1 E2E] Asset instance ID generation, normalization, and mapper round trip passed.");
        }

        private static void RunMultiCaravanSaveDataChecks()
        {
            var data = new SaveData();
            JsonSaveService.NormalizeData(data);
            if (data.caravans.Count != 1 || string.IsNullOrEmpty(data.caravans[0].caravanId)
                || data.selectedCaravanId != data.caravans[0].caravanId
                || data.tradeProgressEntries.Count != 0 || data.pendingSettlements.Count != 0)
            {
                throw new InvalidOperationException("Multi-caravan new game defaults are invalid.");
            }

            var firstId = data.selectedCaravanId;
            var second = new CaravanSaveData { caravanId = Guid.NewGuid().ToString("N"), foodAmount = 17 };
            data.caravans.Add(second);
            data.tradeProgressEntries.Add(new TradeProgressSaveData
            {
                caravanId = firstId,
                activeTradeId = "trade-a",
                state = TradeProgressState.Traveling
            });
            data.pendingSettlements.Add(new PendingSettlementSaveData
            {
                caravanId = firstId,
                tradeId = "trade-a",
                hasResult = true
            });

            TradeProgressSaveData otherProgress;
            PendingSettlementSaveData otherPending;
            if (SaveDataLookup.TryGetTradeProgress(data, second.caravanId, out otherProgress)
                || SaveDataLookup.TryGetPendingSettlement(data, second.caravanId, "trade-a", out otherPending))
            {
                throw new InvalidOperationException("Multi-caravan child lookup leaked data across caravan IDs.");
            }

            data.selectedCaravanId = second.caravanId;
            var json = JsonUtility.ToJson(data);
            var restored = JsonUtility.FromJson<SaveData>(json);
            JsonSaveService.NormalizeData(restored);
            if (restored.caravans.Count != 2 || restored.selectedCaravanId != second.caravanId
                || restored.caravan == null || restored.caravan.foodAmount != 17)
            {
                throw new InvalidOperationException("Multi-caravan JSON round-trip did not preserve IDs or selected data.");
            }

            restored.caravans[0].caravanId = string.Empty;
            restored.selectedCaravanId = "missing";
            JsonSaveService.NormalizeData(restored);
            if (string.IsNullOrEmpty(restored.caravans[0].caravanId)
                || restored.selectedCaravanId != restored.caravans[0].caravanId)
            {
                throw new InvalidOperationException("Multi-caravan ID normalization failed.");
            }
        }

        private static void RunCaravanIdNormalizationPersistenceChecks()
        {
            AssertCaravanIdRepair(string.Empty, "empty");
            AssertCaravanIdRepair("   ", "whitespace");

            var duplicateData = CreateNormalizedSaveData("duplicate");
            duplicateData.caravans.Add(new CaravanSaveData { caravanId = "duplicate" });
            if (!JsonSaveService.NormalizeData(duplicateData)
                || duplicateData.caravans[0].caravanId != "duplicate"
                || duplicateData.caravans[1].caravanId == "duplicate"
                || duplicateData.caravans[1].caravanId.Length != 32)
            {
                throw new InvalidOperationException("Duplicate caravan ID normalization failed.");
            }

            var repairedDuplicateId = duplicateData.caravans[1].caravanId;
            if (JsonSaveService.NormalizeData(duplicateData)
                || duplicateData.caravans[1].caravanId != repairedDuplicateId)
            {
                throw new InvalidOperationException("Caravan ID normalization was not idempotent.");
            }

            AssertSelectedCaravanRepair(string.Empty);
            AssertSelectedCaravanRepair("missing");
            RunCaravanIdLoadPersistenceCheck();

            Debug.Log("[Framework M1 E2E] Caravan ID normalization and load persistence passed.");
        }

        private static SaveData CreateNormalizedSaveData(string caravanId)
        {
            var data = new SaveData();
            JsonSaveService.NormalizeData(data);
            data.caravans[0].caravanId = caravanId;
            data.selectedCaravanId = caravanId;
            return data;
        }

        private static void AssertCaravanIdRepair(string invalidId, string caseLabel)
        {
            var data = CreateNormalizedSaveData(invalidId);
            if (!JsonSaveService.NormalizeData(data)
                || data.caravans[0].caravanId.Length != 32
                || data.selectedCaravanId != data.caravans[0].caravanId)
            {
                throw new InvalidOperationException($"Caravan {caseLabel} ID normalization failed.");
            }
        }

        private static void AssertSelectedCaravanRepair(string invalidSelectedId)
        {
            var data = CreateNormalizedSaveData("caravan_a");
            data.selectedCaravanId = invalidSelectedId;
            if (!JsonSaveService.NormalizeData(data) || data.selectedCaravanId != "caravan_a")
            {
                throw new InvalidOperationException("Selected caravan ID normalization failed.");
            }
        }

        private static void RunCaravanIdLoadPersistenceCheck()
        {
            var testDirectory = Path.Combine(Path.GetTempPath(), "nd-caravan-id-normalization-" + Guid.NewGuid().ToString("N"));
            var testPath = Path.Combine(testDirectory, "save_data.json");
            Directory.CreateDirectory(testDirectory);

            try
            {
                var service = new JsonSaveService();
                var savePathField = typeof(JsonSaveService).GetField("savePath", BindingFlags.Instance | BindingFlags.NonPublic);
                if (savePathField == null)
                {
                    throw new InvalidOperationException("JsonSaveService save path seam was not found.");
                }

                savePathField.SetValue(service, testPath);
                var data = CreateNormalizedSaveData(string.Empty);
                File.WriteAllText(testPath, JsonUtility.ToJson(data, true));

                var firstLoad = service.Load();
                var firstId = firstLoad.caravans[0].caravanId;
                var firstJson = File.ReadAllText(testPath);
                var secondLoad = service.Load();
                var secondJson = File.ReadAllText(testPath);
                if (firstId.Length != 32 || secondLoad.caravans[0].caravanId != firstId || secondJson != firstJson)
                {
                    throw new InvalidOperationException("Caravan ID was not persisted by exactly the first load normalization.");
                }
            }
            finally
            {
                if (File.Exists(testPath)) File.Delete(testPath);
                if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory);
            }
        }

        private static void RunMultiCaravanDepartureCommandChecks()
        {
            var save = new ConfigurableSaveService();
            var context = TestContext.Create(save);
            var command = CreatePlayerDepartureCommand(context, save);
            var firstId = context.SaveData.selectedCaravanId;
            CaravanSaveData firstSave;
            SaveDataLookup.TryGetCaravan(context.SaveData, firstId, out firstSave);
            CaravanSaveDataMapper.CopyToSave(CreateSampleCaravan(context.GameTime), firstSave);

            var secondRuntime = CreateSampleCaravan(context.GameTime);
            var secondSave = new CaravanSaveData { caravanId = Guid.NewGuid().ToString("N") };
            CaravanSaveDataMapper.CopyToSave(secondRuntime, secondSave);
            context.SaveData.caravans.Add(secondSave);

            var first = command.Depart(new TradeDepartureRequest
            {
                CaravanId = firstId,
                RouteId = RouteId
            });
            if (!first.DepartureSucceeded || !first.SaveSucceeded || string.IsNullOrEmpty(first.TradeId)
                || firstSave.state != JourneyState.Traveling || save.SaveCalls != 1)
            {
                throw new InvalidOperationException("Multi-caravan departure command happy path failed.");
            }

            var duplicate = command.Depart(new TradeDepartureRequest
            {
                CaravanId = firstId,
                RouteId = RouteId
            });
            if (duplicate.DepartureSucceeded || duplicate.FailureReason != TradeDepartureFailureReason.AlreadyTraveling
                || save.SaveCalls != 1)
            {
                throw new InvalidOperationException("Same-caravan duplicate departure was not blocked.");
            }

            var second = command.Depart(new TradeDepartureRequest
            {
                CaravanId = secondSave.caravanId,
                RouteId = RouteId
            });
            TradeProgressSaveData firstProgress;
            TradeProgressSaveData secondProgress;
            if (!second.DepartureSucceeded || !second.SaveSucceeded || first.TradeId == second.TradeId
                || !SaveDataLookup.TryGetTradeProgress(context.SaveData, firstId, out firstProgress)
                || !SaveDataLookup.TryGetTradeProgress(context.SaveData, secondSave.caravanId, out secondProgress)
                || firstProgress.activeTradeId != first.TradeId || secondProgress.activeTradeId != second.TradeId)
            {
                throw new InvalidOperationException("Independent caravan departure failed or overwrote another progress entry.");
            }

            var pendingSave = new CaravanSaveData { caravanId = Guid.NewGuid().ToString("N") };
            CaravanSaveDataMapper.CopyToSave(CreateSampleCaravan(context.GameTime), pendingSave);
            context.SaveData.caravans.Add(pendingSave);
            context.SaveData.pendingSettlements.Add(new PendingSettlementSaveData
            {
                caravanId = pendingSave.caravanId,
                tradeId = "pending-trade",
                hasResult = true
            });
            var pending = command.Depart(new TradeDepartureRequest
            {
                CaravanId = pendingSave.caravanId,
                RouteId = RouteId
            });
            var missing = command.Depart(new TradeDepartureRequest
            {
                CaravanId = "missing-caravan",
                RouteId = RouteId
            });
            if (pending.FailureReason != TradeDepartureFailureReason.SettlementPending
                || missing.FailureReason != TradeDepartureFailureReason.CaravanNotFound)
            {
                throw new InvalidOperationException("Pending or missing caravan validation failed.");
            }

            var invalidCoreSave = new CaravanSaveData { caravanId = Guid.NewGuid().ToString("N") };
            context.SaveData.caravans.Add(invalidCoreSave);
            var saveCallsBeforeCoreFailure = save.SaveCalls;
            var coreRejected = command.Depart(new TradeDepartureRequest
            {
                CaravanId = invalidCoreSave.caravanId,
                RouteId = RouteId
            });
            var invalidRoute = command.Depart(new TradeDepartureRequest
            {
                CaravanId = invalidCoreSave.caravanId,
                RouteId = "missing-route"
            });
            if (coreRejected.FailureReason != TradeDepartureFailureReason.CoreRejected
                || coreRejected.CoreResult == null || coreRejected.CoreResult.canDepart
                || invalidRoute.FailureReason != TradeDepartureFailureReason.RouteNotFound
                || save.SaveCalls != saveCallsBeforeCoreFailure)
            {
                throw new InvalidOperationException("Core rejection or route validation mutated persistent state.");
            }

            var failingSave = new ConfigurableSaveService { ShouldSucceed = false };
            var failingContext = TestContext.Create(failingSave);
            var failingCommand = CreatePlayerDepartureCommand(failingContext, failingSave);
            CaravanSaveData failingCaravan;
            SaveDataLookup.TryGetCaravan(failingContext.SaveData, failingContext.SaveData.selectedCaravanId, out failingCaravan);
            CaravanSaveDataMapper.CopyToSave(CreateSampleCaravan(failingContext.GameTime), failingCaravan);
            var failedSaveResult = failingCommand.Depart(new TradeDepartureRequest
            {
                CaravanId = failingCaravan.caravanId,
                RouteId = RouteId
            });
            TradeProgressSaveData rolledBackProgress;
            if (!failedSaveResult.DepartureSucceeded || failedSaveResult.SaveSucceeded
                || failedSaveResult.FailureReason != TradeDepartureFailureReason.SaveFailed
                || failingCaravan.state == JourneyState.Traveling
                || SaveDataLookup.TryGetTradeProgress(failingContext.SaveData, failingCaravan.caravanId, out rolledBackProgress))
            {
                throw new InvalidOperationException("Departure save failure result or rollback policy failed.");
            }

            TradeDepartureResult reentered = null;
            var reentrySave = new ConfigurableSaveService();
            var reentryContext = TestContext.Create(reentrySave);
            var reentryCommand = CreatePlayerDepartureCommand(reentryContext, reentrySave);
            CaravanSaveData reentryCaravan;
            SaveDataLookup.TryGetCaravan(reentryContext.SaveData, reentryContext.SaveData.selectedCaravanId, out reentryCaravan);
            CaravanSaveDataMapper.CopyToSave(CreateSampleCaravan(reentryContext.GameTime), reentryCaravan);
            var reentryRequest = new TradeDepartureRequest { CaravanId = reentryCaravan.caravanId, RouteId = RouteId };
            reentrySave.OnSave = () => reentered = reentryCommand.Depart(reentryRequest);
            var reentryFirst = reentryCommand.Depart(reentryRequest);
            if (!reentryFirst.DepartureSucceeded || reentered == null
                || reentered.FailureReason != TradeDepartureFailureReason.RequestInProgress
                || reentrySave.SaveCalls != 1)
            {
                throw new InvalidOperationException("Same-caravan departure reentry was not blocked.");
            }
        }

        private static TradeStartService CreatePlayerDepartureCommand(TestContext context, ISaveService saveService)
        {
            return new TradeStartService(
                () => context.SaveData,
                saveService,
                new TradeProgressRecorder(context.GameTime, context.GameTime),
                context.ScreenRouter,
                getSharedGameData: () => context.SharedGameData);
        }

        private static void RunRescueLoanIntegrationChecks()
        {
            RunRescueLoanNormalizationChecks();

            var data = new SaveData();
            data.player.tradingCurrency = 400L;
            var save = new ConfigurableSaveService();
            var definition = new RescueLoanDefinition { LoanId = "rescue", MinimumTradeCost = 1000L };
            var service = new RescueLoanCommandService(save, () => data, definition, () => 1234L);
            var issuedEvents = 0;
            Action<IssueRescueLoanResult> onIssued = _ => issuedEvents++;
            FrameworkEvents.RescueLoanIssued += onIssued;
            try
            {
                var issue = service.IssueRescueLoan();
                if (!issue.Succeeded || data.player.tradingCurrency != 1400L
                    || data.rescueLoan.originalPrincipal != 1000L
                    || data.rescueLoan.remainingPrincipal != 1000L
                    || !data.rescueLoan.isActive || !data.rescueLoan.isRestrictedPreparation
                    || issuedEvents != 1 || save.SaveCalls != 1)
                {
                    throw new InvalidOperationException("Rescue loan issue integration failed.");
                }

                if (service.IssueRescueLoan().Succeeded || save.SaveCalls != 1)
                {
                    throw new InvalidOperationException("Active rescue loan duplicate issue was not rejected before save.");
                }
            }
            finally
            {
                FrameworkEvents.RescueLoanIssued -= onIssued;
            }

            data.rescueLoan.isRestrictedPreparation = false;
            var partial = service.RepayRescueLoan(200L);
            if (!partial.Succeeded || data.player.tradingCurrency != 1200L
                || data.rescueLoan.remainingPrincipal != 800L || !data.rescueLoan.isActive)
            {
                throw new InvalidOperationException("Rescue loan partial repayment integration failed.");
            }

            data.player.tradingCurrency = 2000L;
            var full = service.RepayRescueLoan(800L);
            if (!full.Succeeded || data.rescueLoan.remainingPrincipal != 0L
                || data.rescueLoan.isActive || data.rescueLoan.isRestrictedPreparation)
            {
                throw new InvalidOperationException("Rescue loan full repayment integration failed.");
            }

            var rollbackData = new SaveData();
            rollbackData.player.tradingCurrency = 100L;
            var failingSave = new ConfigurableSaveService { ShouldSucceed = false };
            var rollbackService = new RescueLoanCommandService(
                failingSave, () => rollbackData, definition, () => 5678L);
            if (rollbackService.IssueRescueLoan().Succeeded
                || rollbackData.player.tradingCurrency != 100L
                || rollbackData.rescueLoan.isActive
                || rollbackData.rescueLoan.remainingPrincipal != 0L)
            {
                throw new InvalidOperationException("Rescue loan issue save-failure rollback failed.");
            }

            rollbackData.player.tradingCurrency = 2000L;
            rollbackData.rescueLoan = new RescueLoanSaveData
            {
                loanId = "rescue",
                originalPrincipal = 1000L,
                remainingPrincipal = 500L,
                isActive = true
            };
            if (rollbackService.RepayRescueLoan(100L).Succeeded
                || rollbackData.player.tradingCurrency != 2000L
                || rollbackData.rescueLoan.remainingPrincipal != 500L
                || !rollbackData.rescueLoan.isActive)
            {
                throw new InvalidOperationException("Rescue loan repayment save-failure rollback failed.");
            }

            var rebankrupt = rollbackService.EvaluateStatus();
            rollbackData.player.tradingCurrency = 100L;
            rebankrupt = rollbackService.EvaluateStatus();
            if (!rebankrupt.IsValid || !rebankrupt.NeedsRecovery || !rebankrupt.IsRebankrupt
                || rebankrupt.CanOfferLoan || rebankrupt.Shortfall != 900L)
            {
                throw new InvalidOperationException("Rescue loan rebankruptcy status integration failed.");
            }

            rollbackData.rescueLoan.isActive = false;
            var offer = rollbackService.EvaluateStatus();
            if (!offer.CanOfferLoan || offer.IsRebankrupt)
            {
                throw new InvalidOperationException("Rescue loan offer status integration failed.");
            }

            RunRestrictedDepartureHappyPathCheck();
            RunRestrictedDepartureRollbackCheck(rollbackData, definition);
            Debug.Log("[Framework Rescue Loan] All integration checks passed.");
        }

        /// <summary>
        /// 대출 발급 → 제한 모드 → 무역 출발 → 제한 해제 → 수동 상환 순서를 검증한다.
        /// </summary>
        private static void RunRestrictedDepartureHappyPathCheck()
        {
            var data = new SaveData();
            data.player.tradingCurrency = 400L;
            var save = new ConfigurableSaveService();
            var definition = new RescueLoanDefinition { LoanId = "rescue", MinimumTradeCost = 1000L };
            var loanService = new RescueLoanCommandService(save, () => data, definition, () => 9012L);

            var restrictedEntered = 0;
            var restrictedExited = 0;
            var repaidEvents = 0;
            Action onRestrictedEntered = () => restrictedEntered++;
            Action onRestrictedExited = () => restrictedExited++;
            Action<RepayRescueLoanResult> onRepaid = _ => repaidEvents++;
            FrameworkEvents.RescueRestrictedModeEntered += onRestrictedEntered;
            FrameworkEvents.RescueRestrictedModeExited += onRestrictedExited;
            FrameworkEvents.RescueLoanRepaid += onRepaid;
            try
            {
                var issue = loanService.IssueRescueLoan();
                if (!issue.Succeeded
                    || data.player.tradingCurrency != 1400L
                    || !data.rescueLoan.isActive
                    || !data.rescueLoan.isRestrictedPreparation
                    || !loanService.IsRestrictedPreparation
                    || restrictedEntered != 1)
                {
                    throw new InvalidOperationException(
                        "Rescue loan happy path failed at issue/restricted-mode entry.");
                }

                var policy = ScriptableObject.CreateInstance<InGameTimePolicyConfig>();
                var time = new GameTimeService(policy);
                var recorder = new TradeProgressRecorder(time, time);
                var tradeStart = new TradeStartService(() => data, save, recorder);
                var caravan = CreateSampleCaravan(time);
                var saveCallsBeforeDepart = save.SaveCalls;

                var depart = tradeStart.TryStartTrade(
                    caravan,
                    DistanceKm,
                    "loan_happy_depart",
                    RouteId);
                if (!depart.canDepart
                    || !tradeStart.LastRecordSucceeded
                    || data.rescueLoan.isRestrictedPreparation
                    || loanService.IsRestrictedPreparation
                    || restrictedExited != 1
                    || data.tradeProgress.state != TradeProgressState.Traveling
                    || caravan.state != JourneyState.Traveling
                    || save.SaveCalls != saveCallsBeforeDepart + 1)
                {
                    throw new InvalidOperationException(
                        "Rescue loan happy path failed at restricted departure / mode exit.");
                }

                // 전액 상환은 재화가 MinimumTradeCost 아래로 떨어져 거절되므로,
                // 제한 해제 이후 허용되는 수동 부분 상환으로 통합 경로를 검증한다.
                var repayAmount = 400L;
                var currencyBeforeRepay = data.player.tradingCurrency;
                var remainingBeforeRepay = data.rescueLoan.remainingPrincipal;
                var repay = loanService.RepayRescueLoan(repayAmount);
                if (!repay.Succeeded
                    || data.player.tradingCurrency != currencyBeforeRepay - repayAmount
                    || data.rescueLoan.remainingPrincipal != remainingBeforeRepay - repayAmount
                    || !data.rescueLoan.isActive
                    || data.rescueLoan.isRestrictedPreparation
                    || repaidEvents != 1)
                {
                    throw new InvalidOperationException(
                        "Rescue loan happy path failed at manual repayment after restriction exit. "
                        + $"Succeeded={repay.Succeeded}, Message={repay.Message}, "
                        + $"Currency={data.player.tradingCurrency}, Remaining={data.rescueLoan.remainingPrincipal}, "
                        + $"Active={data.rescueLoan.isActive}, Restricted={data.rescueLoan.isRestrictedPreparation}, "
                        + $"RepaidEvents={repaidEvents}");
                }

                // 정산 등으로 재화가 충분해진 뒤의 잔액 전액 상환도 같은 경로에서 확인한다.
                data.player.tradingCurrency = 2500L;
                var close = loanService.RepayRescueLoan(data.rescueLoan.remainingPrincipal);
                if (!close.Succeeded
                    || data.rescueLoan.remainingPrincipal != 0L
                    || data.rescueLoan.isActive
                    || data.rescueLoan.isRestrictedPreparation
                    || repaidEvents != 2)
                {
                    throw new InvalidOperationException(
                        "Rescue loan happy path failed at final repayment after currency recovery. "
                        + $"Succeeded={close.Succeeded}, Message={close.Message}, "
                        + $"Remaining={data.rescueLoan.remainingPrincipal}, Active={data.rescueLoan.isActive}");
                }
            }
            finally
            {
                FrameworkEvents.RescueRestrictedModeEntered -= onRestrictedEntered;
                FrameworkEvents.RescueRestrictedModeExited -= onRestrictedExited;
                FrameworkEvents.RescueLoanRepaid -= onRepaid;
            }

            Debug.Log(
                "[Framework Rescue Loan] Happy path passed: issue -> restricted -> depart -> exit -> repay.");
        }

        private static void RunRescueLoanNormalizationChecks()
        {
            var normalize = typeof(JsonSaveService).GetMethod(
                "NormalizeRescueLoan",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (normalize == null)
            {
                throw new InvalidOperationException("Rescue loan normalizer was not found.");
            }

            var data = new SaveData { rescueLoan = null };
            normalize.Invoke(null, new object[] { data });
            if (data.rescueLoan == null)
            {
                throw new InvalidOperationException("Missing rescue loan save data was not normalized.");
            }

            data.rescueLoan = new RescueLoanSaveData
            {
                loanId = string.Empty,
                originalPrincipal = -1L,
                remainingPrincipal = 20L,
                issuedUtcTicks = -3L,
                isActive = true,
                isRestrictedPreparation = true
            };
            normalize.Invoke(null, new object[] { data });
            if (data.rescueLoan.originalPrincipal != 0L
                || data.rescueLoan.remainingPrincipal != 0L
                || data.rescueLoan.issuedUtcTicks != 0L
                || data.rescueLoan.isActive || data.rescueLoan.isRestrictedPreparation)
            {
                throw new InvalidOperationException("Corrupted rescue loan save data was not normalized safely.");
            }

            data.rescueLoan = new RescueLoanSaveData
            {
                loanId = "rescue",
                originalPrincipal = 100L,
                remainingPrincipal = 200L,
                isActive = true
            };
            normalize.Invoke(null, new object[] { data });
            if (data.rescueLoan.remainingPrincipal != 100L || !data.rescueLoan.isActive)
            {
                throw new InvalidOperationException("Rescue loan remaining principal clamp failed.");
            }
        }

        private static void RunRestrictedDepartureRollbackCheck(
            SaveData data,
            RescueLoanDefinition definition)
        {
            data.rescueLoan = new RescueLoanSaveData
            {
                loanId = definition.LoanId,
                originalPrincipal = definition.MinimumTradeCost,
                remainingPrincipal = definition.MinimumTradeCost,
                isActive = true,
                isRestrictedPreparation = true
            };
            var policy = ScriptableObject.CreateInstance<InGameTimePolicyConfig>();
            var time = new GameTimeService(policy);
            var recorder = new TradeProgressRecorder(time, time);
            var failingSave = new ConfigurableSaveService { ShouldSucceed = false };
            var start = new TradeStartService(() => data, failingSave, recorder);
            var caravan = CreateSampleCaravan(time);
            var result = start.TryStartTrade(caravan, DistanceKm, "loan_departure", RouteId);
            if (result.canDepart || !data.rescueLoan.isRestrictedPreparation
                || (data.tradeProgress != null
                    && data.tradeProgress.state == TradeProgressState.Traveling)
                || caravan.state == JourneyState.Traveling)
            {
                throw new InvalidOperationException("Restricted departure save-failure rollback failed.");
            }
        }

        private sealed class ConfigurableSaveService : ISaveService
        {
            public bool ShouldSucceed = true;
            public int SaveCalls;
            public Action OnSave;

            public bool HasSaveData() => false;
            public SaveData CreateNewGameData() => new SaveData();
            public SaveData Load() => new SaveData();
            public SaveResult Save(SaveData data)
            {
                SaveCalls++;
                OnSave?.Invoke();
                return ShouldSucceed
                    ? SaveResult.Success()
                    : SaveResult.Failure(SaveFailureReason.WriteFailed, "test failure");
            }
            public void ResetSaveData() { }
        }

        private static void RunTradePreparationCommitStoreE2E()
        {
            var saveData = new SaveData();
            var store = new FrameworkTradePrepareCommitStore(() => saveData);
            var commit = new global::TradePrepareCommitData
            {
                tradeId = "editor_prepare_commit",
                currentTownId = "BaseTown",
                selectedDestinationTownId = "DestinationTown",
                routeId = RouteId,
                selectedWagonId = "WagonA",
                purchaseCost = 120L,
                foodCost = 30L,
                mercenaryCost = 10L,
                estimatedSellRevenue = 300L,
                selectedAnimals = new[]
                {
                    new global::DraftAnimalSelectionData { draftAnimalId = "Horse", quantity = 2 }
                },
                purchasedItems = new[]
                {
                    new global::TradeItemBundle
                    {
                        itemId = ItemId,
                        quantity = 3,
                        purchaseUnitPrice = 40L,
                        sellUnitPrice = 100L
                    }
                },
                selectedMercenaryIds = new[] { "Guard" }
            };

            if (!store.TryStage(commit))
                throw new InvalidOperationException("Trade preparation commit stage failed.");

            string json = JsonUtility.ToJson(saveData);
            saveData = JsonUtility.FromJson<SaveData>(json);
            FrameworkTradePrepareCommitStore.Normalize(saveData);
            store = new FrameworkTradePrepareCommitStore(() => saveData);

            if (!store.TryGet(commit.tradeId, out global::TradePrepareCommitData restored) ||
                restored.TotalCost != 160L ||
                restored.purchasedItems.Length != 1 ||
                restored.purchasedItems[0].quantity != 3 ||
                restored.selectedAnimals.Length != 1 ||
                restored.selectedAnimals[0].quantity != 2 ||
                restored.selectedMercenaryIds.Length != 1)
            {
                throw new InvalidOperationException("Trade preparation commit persistence restore failed.");
            }

            var conflicting = commit.CreateSnapshot();
            conflicting.tradeId = "other_trade";
            if (store.TryStage(conflicting))
                throw new InvalidOperationException("Trade preparation commit accepted a conflicting trade ID.");

            if (!store.TryComplete(commit.tradeId, out global::TradePrepareCommitData completed) ||
                completed.tradeId != commit.tradeId ||
                saveData.tradePreparationCommit.hasCommit)
            {
                throw new InvalidOperationException("Trade preparation commit completion failed.");
            }
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

                // TradeStartService must replace the claimed caravan from the previous cycle.
                // This assertion prevents the second trade from remaining stuck in Traveling.
                if (!ReferenceEquals(context.Coordinator.ActiveCaravan, caravan))
                {
                    throw new InvalidOperationException(
                        $"Loop integrity smoke failed to register the active caravan in cycle {cycleIndex + 1}.");
                }

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

                var firstClaim = ClaimCurrentSettlement(context);
                var duplicateClaim = ClaimCurrentSettlement(context);
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

                if (!ClaimCurrentSettlement(context))
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

            var firstClaim = ClaimCurrentSettlement(context);
            var duplicateClaim = ClaimCurrentSettlement(context);
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

            if (context.ScreenRouter.CurrentScreenState != InGameScreenState.Town)
            {
                throw new InvalidOperationException(
                    $"Failed settlement screen E2E failed: post-claim screen was {context.ScreenRouter.CurrentScreenState}, expected Town.");
            }

            Debug.Log(
                "[Framework M1 E2E] Failed settlement screen passed. Failed grade -> Settlement -> claim -> Town/Failed state.");
        }

        private static void RunPendingSettlementRestoreE2E(TestContext context)
        {
            RunPendingSettlementSuccessRestoreAndClaim(context);
            RunPendingSettlementFailedRestoreAndClaim(TestContext.Create());
            RunPendingSettlementCorruptCases(TestContext.Create());
            RunPendingSettlementProgressRecheckKeepsCache(TestContext.Create());
            Debug.Log("[Framework M1 E2E] Pending settlement restore checks passed.");
        }

        private static void RunPendingSettlementSuccessRestoreAndClaim(TestContext context)
        {
            var caravan = CreateSampleCaravan(context.GameTime);
            var tradeId = "editor_pending_restore_success";
            var startResult = context.TradeStart.TryStartTrade(caravan, DistanceKm, tradeId, RouteId);
            if (!startResult.canDepart || !context.TradeStart.LastRecordSucceeded)
            {
                throw new InvalidOperationException("Pending restore E2E failed to start success trade.");
            }

            context.Coordinator.SetActiveCaravan(caravan);
            context.Coordinator.ForceCompleteActiveTrade();

            var pending = context.SaveData.pendingSettlement;
            if (pending == null || !pending.hasResult || pending.tradeId != tradeId || pending.claimed)
            {
                throw new InvalidOperationException("Pending restore E2E failed because pendingSettlement was not written on settle.");
            }

            var savedGrade = pending.grade;
            var savedRevenue = pending.revenue;
            var currencyBeforeClaim = context.SaveData.player.tradingCurrency;

            SimulateSessionCacheLoss(context);

            if (!context.Coordinator.RestorePendingSettlement(context.SaveData))
            {
                throw new InvalidOperationException("Pending restore E2E failed to restore success settlement.");
            }

            if (context.Coordinator.LastSettlementResult == null
                || context.Coordinator.LastSettlementTradeId != tradeId
                || context.Coordinator.LastSettlementResult.grade != savedGrade
                || context.Coordinator.LastSettlementResult.revenue != savedRevenue)
            {
                throw new InvalidOperationException("Pending restore E2E failed because restored cache did not match saved pending result.");
            }

            if (context.ScreenRouter.CurrentScreenState != InGameScreenState.Traveling)
            {
                throw new InvalidOperationException(
                    $"Pending restore E2E failed: successful arrival opened Settlement before the sale step. Screen: {context.ScreenRouter.CurrentScreenState}.");
            }

            var firstClaim = ClaimCurrentSettlement(context);
            var duplicateClaim = ClaimCurrentSettlement(context);
            if (!firstClaim || duplicateClaim)
            {
                throw new InvalidOperationException(
                    $"Pending restore E2E claim validation failed. First: {firstClaim}, Duplicate: {duplicateClaim}");
            }

            if (context.SaveData.tradeProgress.state != TradeProgressState.Completed)
            {
                throw new InvalidOperationException(
                    $"Pending restore E2E failed: post-claim state was {context.SaveData.tradeProgress.state}, expected Completed.");
            }

            if (context.SaveData.pendingSettlement != null && context.SaveData.pendingSettlement.hasResult)
            {
                throw new InvalidOperationException("Pending restore E2E failed because pendingSettlement was not cleared after claim.");
            }

            if (context.SaveData.player.tradingCurrency == currencyBeforeClaim)
            {
                throw new InvalidOperationException("Pending restore E2E failed because trading currency did not change after restored claim.");
            }

            Debug.Log("[Framework M1 E2E] Pending settlement success restore + claim passed.");
        }

        private static void RunPendingSettlementFailedRestoreAndClaim(TestContext context)
        {
            var caravan = CreateSampleCaravan(context.GameTime);
            caravan.foodAmount = 0;
            caravan.starveGraceSeconds = 0f;

            var tradeId = "editor_pending_restore_failed";
            var startResult = context.TradeStart.TryStartTrade(caravan, DistanceKm, tradeId, RouteId);
            if (!startResult.canDepart || !context.TradeStart.LastRecordSucceeded)
            {
                throw new InvalidOperationException("Pending restore failed-path E2E failed to start trade.");
            }

            context.Coordinator.SetActiveCaravan(caravan);
            BackdateActiveTradeStart(context.SaveData, 1d);
            if (!context.Coordinator.CheckProgressAndCompletion(saveProgress: false)
                || context.Coordinator.LastSettlementResult == null
                || context.Coordinator.LastSettlementResult.grade != JourneyResultGrade.Failed)
            {
                throw new InvalidOperationException("Pending restore failed-path E2E failed to create Failed settlement.");
            }

            SimulateSessionCacheLoss(context);
            if (!context.Coordinator.RestorePendingSettlement(context.SaveData))
            {
                throw new InvalidOperationException("Pending restore failed-path E2E failed to restore Failed settlement.");
            }

            if (context.Coordinator.LastSettlementResult.grade != JourneyResultGrade.Failed
                || context.Coordinator.LastSettlementResult.failureReason != JourneyFailureReason.FoodDepleted)
            {
                throw new InvalidOperationException("Pending restore failed-path E2E failed because Failed grade was not preserved.");
            }

            if (!ClaimCurrentSettlement(context))
            {
                throw new InvalidOperationException("Pending restore failed-path E2E failed to claim restored Failed settlement.");
            }

            if (context.SaveData.tradeProgress.state != TradeProgressState.Failed)
            {
                throw new InvalidOperationException(
                    $"Pending restore failed-path E2E failed: post-claim state was {context.SaveData.tradeProgress.state}, expected Failed.");
            }

            Debug.Log("[Framework M1 E2E] Pending settlement Failed restore + claim passed.");
        }

        private static void RunPendingSettlementCorruptCases(TestContext context)
        {
            AssertCorruptRestoreBlocked(
                context,
                "hasResult_false",
                pending =>
                {
                    pending.hasResult = false;
                });

            AssertCorruptRestoreBlocked(
                TestContext.Create(),
                "tradeId_mismatch",
                pending =>
                {
                    pending.tradeId = "other_trade_id";
                });

            AssertCorruptRestoreBlocked(
                TestContext.Create(),
                "claimed_true",
                pending =>
                {
                    pending.claimed = true;
                });

            AssertCorruptRestoreBlocked(
                TestContext.Create(),
                "unsupported_resultVersion",
                pending =>
                {
                    pending.resultVersion = PendingSettlementSaveData.CurrentResultVersion + 100;
                });

            Debug.Log("[Framework M1 E2E] Pending settlement corrupt restore cases passed.");
        }

        private static void AssertCorruptRestoreBlocked(
            TestContext context,
            string caseName,
            Action<PendingSettlementSaveData> mutatePending)
        {
            var caravan = CreateSampleCaravan(context.GameTime);
            var tradeId = $"editor_pending_corrupt_{caseName}";
            var startResult = context.TradeStart.TryStartTrade(caravan, DistanceKm, tradeId, RouteId);
            if (!startResult.canDepart || !context.TradeStart.LastRecordSucceeded)
            {
                throw new InvalidOperationException($"Pending corrupt E2E ({caseName}) failed to start trade.");
            }

            context.Coordinator.SetActiveCaravan(caravan);
            context.Coordinator.ForceCompleteActiveTrade();
            if (context.SaveData.pendingSettlement == null || !context.SaveData.pendingSettlement.hasResult)
            {
                throw new InvalidOperationException($"Pending corrupt E2E ({caseName}) failed because pendingSettlement was missing.");
            }

            mutatePending(context.SaveData.pendingSettlement);
            SimulateSessionCacheLoss(context);

            if (context.Coordinator.RestorePendingSettlement(context.SaveData))
            {
                throw new InvalidOperationException($"Pending corrupt E2E ({caseName}) unexpectedly restored.");
            }

            if (ClaimCurrentSettlement(context))
            {
                throw new InvalidOperationException($"Pending corrupt E2E ({caseName}) unexpectedly allowed claim.");
            }
        }

        private static void RunPendingSettlementProgressRecheckKeepsCache(TestContext context)
        {
            var caravan = CreateSampleCaravan(context.GameTime);
            var tradeId = "editor_pending_restore_recheck";
            var startResult = context.TradeStart.TryStartTrade(caravan, DistanceKm, tradeId, RouteId);
            if (!startResult.canDepart || !context.TradeStart.LastRecordSucceeded)
            {
                throw new InvalidOperationException("Pending restore recheck E2E failed to start trade.");
            }

            context.Coordinator.SetActiveCaravan(caravan);
            context.Coordinator.ForceCompleteActiveTrade();
            SimulateSessionCacheLoss(context);
            if (!context.Coordinator.RestorePendingSettlement(context.SaveData))
            {
                throw new InvalidOperationException("Pending restore recheck E2E failed to restore settlement.");
            }

            var gradeBefore = context.Coordinator.LastSettlementResult.grade;
            var recheckCreatedSettlement = context.Coordinator.CheckProgressAndCompletion(saveProgress: false);
            if (recheckCreatedSettlement)
            {
                throw new InvalidOperationException("Pending restore recheck E2E failed because progress check created a new settlement.");
            }

            if (context.Coordinator.LastSettlementResult == null
                || context.Coordinator.LastSettlementResult.grade != gradeBefore)
            {
                throw new InvalidOperationException("Pending restore recheck E2E failed because progress check cleared restored cache.");
            }

            Debug.Log("[Framework M1 E2E] Pending settlement progress recheck keeps restored cache passed.");
        }

        private static void RunOfflineProgressE2E(TestContext context)
        {
            RunOfflineIncompleteProgress(context);
            RunOfflineCompleteAndPending(TestContext.Create());
            RunOfflineTimeRollback(TestContext.Create());
            Debug.Log("[Framework M1 E2E] Offline progress checks passed.");
        }

        private static void RunOfflineIncompleteProgress(TestContext context)
        {
            var caravan = CreateSampleCaravan(context.GameTime);
            var tradeId = "editor_offline_incomplete";
            var startResult = context.TradeStart.TryStartTrade(caravan, DistanceKm, tradeId, RouteId);
            if (!startResult.canDepart || !context.TradeStart.LastRecordSucceeded)
            {
                throw new InvalidOperationException("Offline incomplete E2E failed to start trade.");
            }

            context.Coordinator.SetActiveCaravan(caravan);
            var elapsedBefore = caravan.elapsedInGameSeconds;
            BackdateActiveTradeStart(context.SaveData, FoodConsumptionBackdateRealSeconds);
            context.SaveData.lastSavedUtcTicks = DateTime.UtcNow.AddSeconds(-30d).Ticks;

            var offlineCompletedCount = 0;
            void OnOfflineCompleted(string completedTradeId) => offlineCompletedCount++;
            FrameworkEvents.TradeOfflineCompleted += OnOfflineCompleted;
            try
            {
                var settled = context.Coordinator.ApplyOfflineProgressOnLoad(context.SaveData);
                if (settled
                    || context.SaveData.tradeProgress.state != TradeProgressState.Traveling
                    || offlineCompletedCount != 0)
                {
                    throw new InvalidOperationException(
                        $"Offline incomplete E2E failed. Settled: {settled}, State: {context.SaveData.tradeProgress.state}, Events: {offlineCompletedCount}");
                }

                if (caravan.elapsedInGameSeconds <= elapsedBefore)
                {
                    throw new InvalidOperationException(
                        $"Offline incomplete E2E failed: elapsed did not increase. Before: {elapsedBefore}, After: {caravan.elapsedInGameSeconds}");
                }
            }
            finally
            {
                FrameworkEvents.TradeOfflineCompleted -= OnOfflineCompleted;
            }

            Debug.Log("[Framework M1 E2E] Offline incomplete progress passed.");
        }

        private static void RunOfflineCompleteAndPending(TestContext context)
        {
            var caravan = CreateSampleCaravan(context.GameTime);
            var tradeId = "editor_offline_complete";
            var startResult = context.TradeStart.TryStartTrade(caravan, DistanceKm, tradeId, RouteId);
            if (!startResult.canDepart || !context.TradeStart.LastRecordSucceeded)
            {
                throw new InvalidOperationException("Offline complete E2E failed to start trade.");
            }

            context.Coordinator.SetActiveCaravan(caravan);
            context.SaveData.tradeProgress.expectedTradeEndUtcTick = DateTime.UtcNow.AddSeconds(-5d).Ticks;
            context.SaveData.lastSavedUtcTicks = DateTime.UtcNow.AddSeconds(-60d).Ticks;

            var offlineCompletedCount = 0;
            void OnOfflineCompleted(string completedTradeId) => offlineCompletedCount++;
            FrameworkEvents.TradeOfflineCompleted += OnOfflineCompleted;
            try
            {
                var settled = context.Coordinator.ApplyOfflineProgressOnLoad(context.SaveData);
                if (!settled
                    || context.SaveData.tradeProgress.state != TradeProgressState.SettlementPending
                    || context.SaveData.pendingSettlement == null
                    || !context.SaveData.pendingSettlement.hasResult
                    || context.SaveData.pendingSettlement.tradeId != tradeId
                    || offlineCompletedCount != 1)
                {
                    throw new InvalidOperationException(
                        $"Offline complete E2E failed. Settled: {settled}, State: {context.SaveData.tradeProgress.state}, Events: {offlineCompletedCount}");
                }

                offlineCompletedCount = 0;
                var secondApply = context.Coordinator.ApplyOfflineProgressOnLoad(context.SaveData);
                if (secondApply || offlineCompletedCount != 0)
                {
                    throw new InvalidOperationException(
                        $"Offline complete re-apply E2E failed. Settled: {secondApply}, Events: {offlineCompletedCount}");
                }

                if (!context.Coordinator.RestorePendingSettlement(context.SaveData)
                    || context.Coordinator.LastSettlementTradeId != tradeId)
                {
                    throw new InvalidOperationException("Offline complete E2E failed to restore pending settlement after offline settle.");
                }
            }
            finally
            {
                FrameworkEvents.TradeOfflineCompleted -= OnOfflineCompleted;
            }

            Debug.Log("[Framework M1 E2E] Offline complete + pending restore passed.");
        }

        private static void RunOfflineTimeRollback(TestContext context)
        {
            var caravan = CreateSampleCaravan(context.GameTime);
            var tradeId = "editor_offline_rollback";
            var startResult = context.TradeStart.TryStartTrade(caravan, DistanceKm, tradeId, RouteId);
            if (!startResult.canDepart || !context.TradeStart.LastRecordSucceeded)
            {
                throw new InvalidOperationException("Offline rollback E2E failed to start trade.");
            }

            context.Coordinator.SetActiveCaravan(caravan);
            var elapsedBefore = caravan.elapsedInGameSeconds;
            var foodBefore = caravan.foodAmount;
            context.SaveData.lastSavedUtcTicks = DateTime.UtcNow.AddHours(1d).Ticks;

            var rollbackCount = 0;
            void OnRollback() => rollbackCount++;
            FrameworkEvents.TimeRollbackDetected += OnRollback;
            try
            {
                var settled = context.Coordinator.ApplyOfflineProgressOnLoad(context.SaveData);
                if (settled
                    || rollbackCount != 1
                    || context.SaveData.tradeProgress.state != TradeProgressState.Traveling
                    || !Mathf.Approximately(caravan.elapsedInGameSeconds, elapsedBefore)
                    || caravan.foodAmount != foodBefore)
                {
                    throw new InvalidOperationException(
                        $"Offline rollback E2E failed. Settled: {settled}, RollbackEvents: {rollbackCount}, State: {context.SaveData.tradeProgress.state}");
                }
            }
            finally
            {
                FrameworkEvents.TimeRollbackDetected -= OnRollback;
            }

            Debug.Log("[Framework M1 E2E] Offline time rollback passed.");
        }

        private static void SimulateSessionCacheLoss(TestContext context)
        {
            context.Coordinator.ClearSettlementCache();
            context.Coordinator.SetActiveCaravan(null);
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
                starveGraceSeconds = StarveGraceSeconds
            };

            caravan.animals.Add(new imsiAnimalData
            {
                instanceId = SaveDataLookup.NewInstanceId(),
                animalName = "Editor Horse",
                foodPerKm = SampleRawFoodConsumptionPerDay,
                animalType = DraftAnimalType.Horse,
                increaseOverLoad = 5f
            });
            caravan.animals.Add(new imsiAnimalData
            {
                instanceId = SaveDataLookup.NewInstanceId(),
                animalName = "Editor Horse",
                foodPerKm = SampleRawFoodConsumptionPerDay,
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

            public ISaveService SaveService { get; private set; }

            public ISharedGameDataProvider SharedGameData { get; private set; }

            public TradeProgressCoordinator Coordinator { get; private set; }

            public TradeStartService TradeStart { get; private set; }

            public InGameScreenStateRouter ScreenRouter { get; private set; }

            public static TestContext Create(ISaveService saveServiceOverride = null)
            {
                var policyConfig = Resources.Load<InGameTimePolicyConfig>(InGameTimePolicyConfig.ResourceName);
                if (policyConfig == null)
                {
                    policyConfig = ScriptableObject.CreateInstance<InGameTimePolicyConfig>();
                }

                var gameTime = new GameTimeService(policyConfig);
                var saveService = saveServiceOverride ?? new JsonSaveService();
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
                var commitStore = new FrameworkTradePrepareCommitStore(() => saveData);
                var coordinator = new TradeProgressCoordinator(
                    () => saveData,
                    saveService,
                    gameTime,
                    recorder,
                    router,
                    gameTime,
                    () => sharedGameData,
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
                    // Production wiring registers the same departure reference in FrameworkRoot.
                    caravan =>
                    {
                        StageTestCommit(saveData, sharedGameData, commitStore);
                        coordinator.SetActiveCaravan(caravan);
                    },
                    () => sharedGameData);

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

            private static void StageTestCommit(
                SaveData saveData,
                ISharedGameDataProvider sharedGameData,
                FrameworkTradePrepareCommitStore commitStore)
            {
                var progress = saveData.tradeProgress;
                if (progress == null || !sharedGameData.TryGetRoute(progress.activeRouteId, out var route) || route == null)
                {
                    throw new InvalidOperationException("Test commit could not resolve the active route.");
                }

                if (!commitStore.TryStage(new global::TradePrepareCommitData
                {
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

        /// <summary>
        /// 원자 claim: 정상 claim → currentTownId/Town 이벤트 → 저장 실패 원복 → 중복 claim → 재실행 화면을 검증한다.
        /// </summary>
        private static void RunAtomicSettlementClaimE2E()
        {
            var saveService = new ConfigurableSaveService();
            var context = TestContext.Create(saveService);
            var caravan = CreateSampleCaravan(context.GameTime);
            if (!context.TradeStart.TryStartTrade(
                    caravan, DistanceKm, "editor_atomic_claim", RouteId).canDepart)
            {
                throw new InvalidOperationException("Atomic claim E2E failed to start trade.");
            }

            context.Coordinator.ForceCompleteActiveTrade();
            if (context.ScreenRouter.CurrentScreenState != InGameScreenState.Traveling
                || context.SaveData.tradeProgress.state != TradeProgressState.SettlementPending)
            {
                throw new InvalidOperationException(
                    "Atomic claim E2E expected a saved pending result without automatic Settlement presentation. "
                    + $"Screen={context.ScreenRouter.CurrentScreenState}, State={context.SaveData.tradeProgress.state}.");
            }

            var currencyBefore = context.SaveData.player.tradingCurrency;
            var townBefore = context.SaveData.player.currentTownId;
            var caravanBefore = JsonUtility.ToJson(context.SaveData.caravan);
            var expectedDestination = context.SaveData.tradePreparationCommit.destinationTownId;

            // 저장 실패 강제 → 상태 원복 확인
            saveService.ShouldSucceed = false;
            var failedSaveClaim = context.Coordinator.ClaimSettlement(
                context.SaveData.selectedCaravanId,
                context.SaveData.tradeProgress.activeTradeId);
            if (failedSaveClaim.Succeeded
                || failedSaveClaim.FailureReason != ClaimSettlementFailureReason.SaveFailed
                || failedSaveClaim.SaveResult == null
                || context.SaveData.player.tradingCurrency != currencyBefore
                || context.SaveData.player.currentTownId != townBefore
                || context.SaveData.tradeProgress.state != TradeProgressState.SettlementPending
                || context.SaveData.pendingSettlement == null || !context.SaveData.pendingSettlement.hasResult
                || context.SaveData.tradePreparationCommit == null || !context.SaveData.tradePreparationCommit.hasCommit
                || JsonUtility.ToJson(context.SaveData.caravan) != caravanBefore
                || context.ScreenRouter.CurrentScreenState == InGameScreenState.Town)
            {
                throw new InvalidOperationException("Atomic claim E2E save-failure rollback did not restore all staged values.");
            }

            Debug.Log("[Framework M1 E2E] Atomic claim: save-failure rollback restored staged values.");

            // 정상 Claim → currentTownId → Town 이벤트
            saveService.ShouldSucceed = true;
            InGameScreenState? townEventState = null;
            Action<InGameScreenState> onScreenChanged = state => townEventState = state;
            FrameworkEvents.InGameScreenChanged += onScreenChanged;
            try
            {
                var successfulClaim = context.Coordinator.ClaimSettlement(
                    context.SaveData.selectedCaravanId,
                    context.SaveData.tradeProgress.activeTradeId);
                if (!successfulClaim.Succeeded || successfulClaim.SaveResult == null
                    || !successfulClaim.SaveResult.Succeeded)
                {
                    throw new InvalidOperationException("Atomic claim E2E normal claim failed.");
                }
            }
            finally
            {
                FrameworkEvents.InGameScreenChanged -= onScreenChanged;
            }

            if (context.ScreenRouter.CurrentScreenState != InGameScreenState.Town
                || context.SaveData.player.currentTownId != expectedDestination
                || string.IsNullOrWhiteSpace(context.SaveData.player.currentTownId)
                || context.SaveData.player.currentTownId == townBefore
                || (context.SaveData.pendingSettlement != null
                    && context.SaveData.pendingSettlement.hasResult)
                || context.SaveData.tradePreparationCommit.hasCommit
                || townEventState != InGameScreenState.Town)
            {
                throw new InvalidOperationException(
                    $"Atomic claim E2E normal claim town routing failed. TownId: {context.SaveData.player.currentTownId}, Expected: {expectedDestination}, Screen: {context.ScreenRouter.CurrentScreenState}, Event: {townEventState}");
            }

            Debug.Log(
                $"[Framework M1 E2E] Atomic claim: normal claim succeeded. currentTownId={context.SaveData.player.currentTownId}, Town event raised.");

            // 중복 Claim 확인
            if (ClaimCurrentSettlement(context))
            {
                throw new InvalidOperationException("Atomic claim E2E duplicate claim unexpectedly succeeded.");
            }

            Debug.Log("[Framework M1 E2E] Atomic claim: duplicate claim correctly rejected.");

            // 종료 후 재실행 화면 확인 (새 router가 저장 데이터를 Town으로 복원)
            var relaunchRouter = new InGameScreenStateRouter();
            InGameScreenState? relaunchEventState = null;
            Action<InGameScreenState> onRelaunchScreenChanged = state => relaunchEventState = state;
            FrameworkEvents.InGameScreenChanged += onRelaunchScreenChanged;
            try
            {
                relaunchRouter.RefreshFromSaveData(context.SaveData, forceNotify: true);
            }
            finally
            {
                FrameworkEvents.InGameScreenChanged -= onRelaunchScreenChanged;
            }

            if (InGameScreenStateRouter.MapFromSaveData(context.SaveData) != InGameScreenState.Town
                || relaunchRouter.CurrentScreenState != InGameScreenState.Town
                || relaunchEventState != InGameScreenState.Town)
            {
                throw new InvalidOperationException(
                    $"Atomic claim E2E relaunch screen restore failed. Map: {InGameScreenStateRouter.MapFromSaveData(context.SaveData)}, Router: {relaunchRouter.CurrentScreenState}, Event: {relaunchEventState}");
            }

            Debug.Log("[Framework M1 E2E] Atomic claim: relaunch restored Town screen from save data.");

            var mismatchContext = TestContext.Create(new ConfigurableSaveService());
            var mismatchCaravan = CreateSampleCaravan(mismatchContext.GameTime);
            if (!mismatchContext.TradeStart.TryStartTrade(
                    mismatchCaravan, DistanceKm, "editor_destination_mismatch", RouteId).canDepart)
            {
                throw new InvalidOperationException("Destination mismatch E2E failed to start trade.");
            }

            mismatchContext.Coordinator.ForceCompleteActiveTrade();
            var mismatchCurrency = mismatchContext.SaveData.player.tradingCurrency;
            mismatchContext.SaveData.tradePreparationCommit.destinationTownId = "OtherTown";
            if (ClaimCurrentSettlement(mismatchContext)
                || mismatchContext.SaveData.player.tradingCurrency != mismatchCurrency
                || mismatchContext.SaveData.tradeProgress.state != TradeProgressState.SettlementPending
                || mismatchContext.ScreenRouter.CurrentScreenState != InGameScreenState.Traveling)
            {
                throw new InvalidOperationException("Destination mismatch E2E mutated claim state.");
            }

            Debug.Log("[Framework M1 E2E] Atomic claim rollback, normal claim, Town event, duplicate reject, relaunch, and destination validation passed.");
        }

        private static void RunAutomaticArrivalClaimE2E()
        {
            var context = TestContext.Create(new ConfigurableSaveService());
            var bridgeObject = new GameObject("SettlementAutoArrivalE2E");
            var bridge = bridgeObject.AddComponent<SettlementUiBridge>();
            bridge.Initialize(
                () => context.SaveData,
                context.Coordinator,
                context.ScreenRouter,
                autoClaimOnArrival: true);

            int settlementScreenEvents = 0;
            int townScreenEvents = 0;
            Action<InGameScreenState> onScreenChanged = state =>
            {
                if (state == InGameScreenState.Settlement) settlementScreenEvents++;
                if (state == InGameScreenState.Town) townScreenEvents++;
            };
            FrameworkEvents.InGameScreenChanged += onScreenChanged;

            try
            {
                CaravanData caravan = CreateSampleCaravan(context.GameTime);
                if (!context.TradeStart.TryStartTrade(
                        caravan,
                        DistanceKm,
                        "editor_auto_arrival_claim",
                        RouteId).canDepart)
                {
                    throw new InvalidOperationException("Automatic arrival E2E failed to start trade.");
                }

                context.Coordinator.ForceCompleteActiveTrade();

                bool pendingCleared = context.SaveData.pendingSettlement == null
                    || !context.SaveData.pendingSettlement.hasResult;
                if (!pendingCleared
                    || context.SaveData.tradeProgress.state == TradeProgressState.SettlementPending
                    || context.ScreenRouter.CurrentScreenState != InGameScreenState.Town
                    || settlementScreenEvents != 0
                    || townScreenEvents != 1
                    || bridge.HasPendingSettlement)
                {
                    throw new InvalidOperationException(
                        "Automatic arrival E2E failed. "
                        + $"State={context.SaveData.tradeProgress.state}, "
                        + $"Screen={context.ScreenRouter.CurrentScreenState}, "
                        + $"SettlementEvents={settlementScreenEvents}, TownEvents={townScreenEvents}, "
                        + $"PendingCleared={pendingCleared}, BridgePending={bridge.HasPendingSettlement}.");
                }

                Debug.Log("[Framework M1 E2E] Automatic arrival claim passed. Settlement UI skipped -> Town.");
            }
            finally
            {
                FrameworkEvents.InGameScreenChanged -= onScreenChanged;
                UnityEngine.Object.DestroyImmediate(bridgeObject);
            }
        }

        private static void RunArrivalSalePendingE2E()
        {
            var context = TestContext.Create(new ConfigurableSaveService());
            var bridgeObject = new GameObject("SettlementArrivalSalePendingE2E");
            var bridge = bridgeObject.AddComponent<SettlementUiBridge>();
            bridge.Initialize(
                () => context.SaveData,
                context.Coordinator,
                context.ScreenRouter,
                autoClaimOnArrival: false);

            int settlementReadyEvents = 0;
            bridge.SettlementReady += (_, __) => settlementReadyEvents++;

            try
            {
                CaravanData caravan = CreateSampleCaravan(context.GameTime);
                if (!context.TradeStart.TryStartTrade(
                        caravan,
                        DistanceKm,
                        "editor_arrival_sale_pending",
                        RouteId).canDepart)
                {
                    throw new InvalidOperationException("Arrival sale pending E2E failed to start trade.");
                }

                context.Coordinator.ForceCompleteActiveTrade();
                string caravanId = context.SaveData.selectedCaravanId;
                string tradeId = context.SaveData.tradeProgress.activeTradeId;
                if (context.SaveData.tradeProgress.state != TradeProgressState.SettlementPending
                    || context.SaveData.pendingSettlement == null
                    || !context.SaveData.pendingSettlement.hasResult
                    || !bridge.HasPendingSettlement
                    || settlementReadyEvents != 0
                    || context.ScreenRouter.CurrentScreenState == InGameScreenState.Settlement)
                {
                    throw new InvalidOperationException(
                        "Arrival sale pending E2E did not preserve the pre-sale settlement boundary.");
                }

                if (!bridge.PresentSettlement(caravanId, tradeId)
                    || settlementReadyEvents != 1
                    || context.ScreenRouter.CurrentScreenState != InGameScreenState.Settlement
                    || context.SaveData.tradeProgress.state != TradeProgressState.SettlementPending)
                {
                    throw new InvalidOperationException(
                        "Arrival sale pending E2E failed to present the saved settlement explicitly.");
                }

                Debug.Log("[Framework M1 E2E] Arrival remains pending until sale completion presents settlement.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bridgeObject);
            }
        }

        private static bool ClaimCurrentSettlement(TestContext context)
        {
            return context.Coordinator.ClaimSettlement(
                context.SaveData.selectedCaravanId,
                context.SaveData.tradeProgress.activeTradeId).Succeeded;
        }
    }
}
#endif
