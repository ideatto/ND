/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Core caravan 출발 검증과 framework 저장 데이터 기록을 연결한다.
 * - 무역 출발 시 active trade 상태, caravan 저장 데이터, 인게임 화면 상태를 갱신한다.
 *
 * Main Features
 * - CaravanValidator와 JourneyRunner를 이용해 출발 가능 여부를 검증한다.
 * - TradeProgressRecorder를 통해 시작 시간과 예상 종료 시간을 저장한다.
 * - 출발 성공 시 caravan 상태를 SaveData에 복사하고 traveling 화면으로 전환한다.
 *
 * Usage for Team Members
 * - UI 또는 debug harness는 TryStartTrade(...)를 호출해 출발을 요청한다.
 * - 반환된 DepartureValidationResult.canDepart와 LastRecordSucceeded를 함께 확인하면 Core 출발과 framework 기록 성공 여부를 구분할 수 있다.
 *
 * Main Public APIs
 * - LastRecordSucceeded: 마지막 출발 요청의 framework 기록 성공 여부.
 * - TryStartTrade(...): caravan 출발과 저장 데이터 기록을 시도한다.
 *
 * Important Notes
 * - tradeId가 기록되지 않으면 출발을 framework blocked 결과로 거부한다.
 * - 즉시 저장 출발은 tradeProgress, caravan, 구조 대출 제한 상태를 한 저장 경계에서 확정한다.
 */
using System;
using ND.Economy;
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// 무역 출발 요청을 Core 검증, 저장 데이터 기록, 화면 전환으로 연결하는 서비스이다.
    /// </summary>
    public sealed class TradeStartService
    {
        private readonly Func<SaveData> getCurrentSaveData;
        private readonly ISaveService saveService;
        private readonly TradeProgressRecorder tradeProgressRecorder;
        private readonly InGameScreenStateRouter inGameScreenRouter;
        private readonly Action clearSettlementCache;
        private readonly Action<CaravanData> setActiveCaravan;

        /// <summary>
        /// 마지막 TryStartTrade 호출에서 TradeProgressRecorder 기록이 성공했는지 나타낸다.
        /// </summary>
        public bool LastRecordSucceeded { get; private set; }

        /// <summary>
        /// 무역 출발 서비스에 필요한 저장 데이터 접근자와 framework 서비스를 주입한다.
        /// </summary>
        /// <param name="getCurrentSaveData">현재 SaveData를 반환하는 접근자.</param>
        /// <param name="saveService">출발 성공 후 저장을 수행할 서비스.</param>
        /// <param name="tradeProgressRecorder">active trade 시간과 상태를 기록하는 recorder.</param>
        /// <param name="inGameScreenRouter">출발 성공 후 traveling 화면으로 전환할 router.</param>
        /// <param name="clearSettlementCache">새 출발 전 이전 정산 cache를 비우는 callback.</param>
        /// <param name="setActiveCaravan">Registers the newly departed runtime caravan with the progress coordinator.</param>
        public TradeStartService(
            Func<SaveData> getCurrentSaveData,
            ISaveService saveService,
            TradeProgressRecorder tradeProgressRecorder,
            InGameScreenStateRouter inGameScreenRouter = null,
            Action clearSettlementCache = null,
            Action<CaravanData> setActiveCaravan = null)
        {
            this.getCurrentSaveData = getCurrentSaveData;
            this.saveService = saveService;
            this.tradeProgressRecorder = tradeProgressRecorder;
            this.inGameScreenRouter = inGameScreenRouter;
            this.clearSettlementCache = clearSettlementCache;
            this.setActiveCaravan = setActiveCaravan;
        }

        /// <summary>
        /// caravan 무역 출발을 검증하고 저장 데이터와 화면 상태를 갱신한다.
        /// </summary>
        /// <param name="caravan">출발할 runtime caravan 데이터.</param>
        /// <param name="distanceKm">이동할 거리(km).</param>
        /// <param name="tradeId">active trade로 기록할 고유 ID.</param>
        /// <param name="routeId">active route로 기록할 ID.</param>
        /// <param name="saveImmediately">출발 성공 후 즉시 저장할지 여부.</param>
        /// <returns>Core 출발 검증 결과. framework 기록 실패 시 canDepart가 false인 결과를 반환한다.</returns>
        /// <remarks>
        /// 성공 시 saveData.tradeProgress와 saveData.caravan이 변경되고 InGameScreenState.Traveling으로 전환된다.
        /// </remarks>
        public DepartureValidationResult TryStartTrade(
            CaravanData caravan,
            float distanceKm,
            string tradeId,
            string routeId,
            bool saveImmediately = true)
        {
            LastRecordSucceeded = false;

            // Core 출발 조건을 먼저 확인해 유효하지 않은 caravan이 저장 데이터에 기록되지 않게 한다.
            var result = CaravanValidator.Validate(caravan);
            if (!result.canDepart)
            {
                return result;
            }

            // recorder가 없으면 출발 시간을 복구할 수 없으므로 framework 단계에서 출발을 막는다.
            if (tradeProgressRecorder == null)
            {
                FrameworkLog.Warning("Trade start time was not recorded because trade progress recorder is null.");
                return CreateFrameworkBlockedResult();
            }

            var saveData = getCurrentSaveData != null ? getCurrentSaveData() : null;
            if (saveData == null || saveData.tradeProgress == null || saveData.caravan == null)
            {
                FrameworkLog.Warning("Trade start was blocked because required save data is missing.");
                return CreateFrameworkBlockedResult();
            }

            var tradeProgressSnapshot = JsonUtility.ToJson(saveData.tradeProgress);
            var caravanSaveSnapshot = JsonUtility.ToJson(saveData.caravan);
            var runtimeCaravanSnapshot = JsonUtility.ToJson(caravan);
            var restrictedPreparationBefore = saveData.rescueLoan != null
                && saveData.rescueLoan.isRestrictedPreparation;
            var expectedSeconds = CaravanCalculator.GetTravelSeconds(caravan, distanceKm);
            var expectedDuration = TimeSpan.FromSeconds(Math.Max(0f, expectedSeconds));

            // active trade ID와 예상 종료 시각을 저장 데이터에 먼저 기록해 이후 진행률 계산 기준을 만든다.
            LastRecordSucceeded = tradeProgressRecorder.RecordStartedTrade(
                saveData,
                tradeId,
                routeId,
                expectedDuration);

            // 저장 기록이 실패하면 Core 출발을 진행해도 framework가 상태를 추적할 수 없으므로 거부한다.
            if (!LastRecordSucceeded)
            {
                RestoreDepartureSnapshot(
                    saveData,
                    caravan,
                    tradeProgressSnapshot,
                    caravanSaveSnapshot,
                    runtimeCaravanSnapshot,
                    restrictedPreparationBefore);
                return CreateFrameworkBlockedResult();
            }

            // 기록 후 Core 최종 출발을 수행해 caravan runtime 상태를 traveling으로 전환한다.
            result = JourneyRunner.TryDepart(caravan, distanceKm);
            if (!result.canDepart)
            {
                FrameworkLog.Warning("Trade start was recorded but Core departure failed during final validation.");
                RestoreDepartureSnapshot(
                    saveData,
                    caravan,
                    tradeProgressSnapshot,
                    caravanSaveSnapshot,
                    runtimeCaravanSnapshot,
                    restrictedPreparationBefore);
                LastRecordSucceeded = false;
                return result;
            }

            CaravanSaveDataMapper.CopyToSave(caravan, saveData.caravan);

            if (saveImmediately)
            {
                if (saveService == null)
                {
                    FrameworkLog.Warning("Trade start time was recorded but save was skipped because save service is null.");
                    RestoreDepartureSnapshot(
                        saveData,
                        caravan,
                        tradeProgressSnapshot,
                        caravanSaveSnapshot,
                        runtimeCaravanSnapshot,
                        restrictedPreparationBefore);
                    LastRecordSucceeded = false;
                    return CreateFrameworkBlockedResult();
                }

                if (saveData.rescueLoan != null)
                {
                    saveData.rescueLoan.isRestrictedPreparation = false;
                }

                var saveResult = saveService.Save(saveData);
                if (!saveResult.Succeeded)
                {
                    RestoreDepartureSnapshot(
                        saveData,
                        caravan,
                        tradeProgressSnapshot,
                        caravanSaveSnapshot,
                        runtimeCaravanSnapshot,
                        restrictedPreparationBefore);
                    LastRecordSucceeded = false;
                    return CreateFrameworkBlockedResult();
                }

                if (restrictedPreparationBefore)
                {
                    FrameworkEvents.RaiseRescueRestrictedModeExited();
                }
            }

            clearSettlementCache?.Invoke();
            setActiveCaravan?.Invoke(caravan);
            inGameScreenRouter?.RequestScreen(InGameScreenState.Traveling);

            return result;
        }

        private static void RestoreDepartureSnapshot(
            SaveData saveData,
            CaravanData runtimeCaravan,
            string tradeProgressSnapshot,
            string caravanSaveSnapshot,
            string runtimeCaravanSnapshot,
            bool restrictedPreparationBefore)
        {
            JsonUtility.FromJsonOverwrite(tradeProgressSnapshot, saveData.tradeProgress);
            JsonUtility.FromJsonOverwrite(caravanSaveSnapshot, saveData.caravan);
            JsonUtility.FromJsonOverwrite(runtimeCaravanSnapshot, runtimeCaravan);
            if (saveData.rescueLoan != null)
            {
                saveData.rescueLoan.isRestrictedPreparation = restrictedPreparationBefore;
            }
        }

        private static DepartureValidationResult CreateFrameworkBlockedResult()
        {
            return new DepartureValidationResult { canDepart = false };
        }
    }

    /// <summary>
    /// 구조 대출 계산 결과를 현재 저장 상태에 원자적으로 반영하는 command service이다.
    /// </summary>
    public sealed class RescueLoanCommandService
    {
        private readonly ISaveService saveService;
        private readonly Func<SaveData> getSaveData;
        private readonly RescueLoanDefinition definition;
        private readonly Func<long> utcTicksProvider;

        public RescueLoanCommandService(
            ISaveService saveService,
            Func<SaveData> getSaveData,
            RescueLoanDefinition definition,
            Func<long> utcTicksProvider)
        {
            this.saveService = saveService;
            this.getSaveData = getSaveData;
            this.definition = definition;
            this.utcTicksProvider = utcTicksProvider;
        }

        /// <summary>현재 저장 상태가 출발 전 제한 모드이면 true이다.</summary>
        public bool IsRestrictedPreparation
        {
            get
            {
                var data = getSaveData != null ? getSaveData() : null;
                return data != null && data.rescueLoan != null
                    && data.rescueLoan.isRestrictedPreparation;
            }
        }

        /// <summary>현재 재화와 대출 활성 상태로 복구 필요·대출 가능·재파산 상태를 계산한다.</summary>
        public RescueStatusResult EvaluateStatus()
        {
            var data = getSaveData != null ? getSaveData() : null;
            return RescueLoanCalculator.EvaluateStatus(new RescueStatusInput
            {
                UsableTradeMoney = data != null && data.player != null
                    ? data.player.tradingCurrency
                    : -1L,
                MinimumTradeCost = definition != null ? definition.MinimumTradeCost : 0L,
                HasActiveLoan = data != null && data.rescueLoan != null && data.rescueLoan.isActive
            });
        }

        /// <summary>
        /// 고정 원금 구조 대출을 발급하고 재화와 대출 상태를 한 번 저장한다.
        /// </summary>
        /// <returns>계산 또는 입력 실패는 InvalidData, 저장 실패는 저장 서비스 결과를 반환한다.</returns>
        public SaveResult IssueRescueLoan()
        {
            var data = GetValidData();
            if (data == null || definition == null || utcTicksProvider == null)
            {
                return InvalidCommand("Rescue loan issue dependencies or save data are invalid.");
            }

            var calculation = RescueLoanCalculator.Issue(new IssueRescueLoanInput
            {
                LoanId = definition.LoanId,
                TradeMoneyBefore = data.player.tradingCurrency,
                MinimumTradeCost = definition.MinimumTradeCost,
                HasActiveLoan = data.rescueLoan.isActive,
                IssuedUtcTicks = utcTicksProvider()
            });
            if (!calculation.Success)
            {
                return InvalidCommand($"Rescue loan issue was rejected: {calculation.FailureReason}.");
            }

            var currencyBefore = data.player.tradingCurrency;
            var loanBefore = CloneLoan(data.rescueLoan);
            data.player.tradingCurrency = calculation.TradeMoneyAfter;
            data.rescueLoan.loanId = calculation.LoanId;
            data.rescueLoan.originalPrincipal = calculation.Principal;
            data.rescueLoan.remainingPrincipal = calculation.RemainingPrincipal;
            data.rescueLoan.isActive = true;
            data.rescueLoan.issuedUtcTicks = calculation.IssuedUtcTicks;
            data.rescueLoan.isRestrictedPreparation = calculation.EnterRestrictedMode;

            var result = saveService.Save(data);
            if (!result.Succeeded)
            {
                RestoreLoan(data, currencyBefore, loanBefore);
                return result;
            }

            FrameworkEvents.RaiseRescueLoanIssued(calculation);
            if (calculation.EnterRestrictedMode)
            {
                FrameworkEvents.RaiseRescueRestrictedModeEntered();
            }

            return result;
        }

        /// <summary>
        /// 요청 금액을 명시적으로 상환하고 재화와 대출 상태를 한 번 저장한다.
        /// </summary>
        /// <returns>계산 또는 입력 실패는 InvalidData, 저장 실패는 저장 서비스 결과를 반환한다.</returns>
        public SaveResult RepayRescueLoan(long amount)
        {
            var data = GetValidData();
            if (data == null || definition == null)
            {
                return InvalidCommand("Rescue loan repayment dependencies or save data are invalid.");
            }

            var calculation = RescueLoanCalculator.Repay(new RepayRescueLoanInput
            {
                TradeMoneyBefore = data.player.tradingCurrency,
                MinimumTradeCost = definition.MinimumTradeCost,
                OriginalPrincipal = data.rescueLoan.originalPrincipal,
                RemainingPrincipalBefore = data.rescueLoan.remainingPrincipal,
                IsActive = data.rescueLoan.isActive,
                IsRestrictedPreparation = data.rescueLoan.isRestrictedPreparation,
                RequestedAmount = amount
            });
            if (!calculation.Success)
            {
                return InvalidCommand($"Rescue loan repayment was rejected: {calculation.FailureReason}.");
            }

            var currencyBefore = data.player.tradingCurrency;
            var loanBefore = CloneLoan(data.rescueLoan);
            data.player.tradingCurrency = calculation.TradeMoneyAfter;
            data.rescueLoan.remainingPrincipal = calculation.RemainingPrincipalAfter;
            data.rescueLoan.isActive = calculation.IsActiveAfter;
            data.rescueLoan.isRestrictedPreparation = calculation.IsRestrictedPreparationAfter;

            var result = saveService.Save(data);
            if (!result.Succeeded)
            {
                RestoreLoan(data, currencyBefore, loanBefore);
                return result;
            }

            FrameworkEvents.RaiseRescueLoanRepaid(calculation);
            if (!calculation.IsActiveAfter)
            {
                FrameworkEvents.RaiseRescueLoanClosed();
            }

            return result;
        }

        private SaveData GetValidData()
        {
            var data = getSaveData != null ? getSaveData() : null;
            return saveService != null && data != null && data.player != null && data.rescueLoan != null
                ? data
                : null;
        }

        private static SaveResult InvalidCommand(string message)
        {
            return SaveResult.Failure(SaveFailureReason.InvalidData, message, "rescueLoan");
        }

        private static RescueLoanSaveData CloneLoan(RescueLoanSaveData source)
        {
            return new RescueLoanSaveData
            {
                loanId = source.loanId,
                originalPrincipal = source.originalPrincipal,
                remainingPrincipal = source.remainingPrincipal,
                isActive = source.isActive,
                issuedUtcTicks = source.issuedUtcTicks,
                isRestrictedPreparation = source.isRestrictedPreparation
            };
        }

        private static void RestoreLoan(SaveData data, long currencyBefore, RescueLoanSaveData loanBefore)
        {
            data.player.tradingCurrency = currencyBefore;
            data.rescueLoan.loanId = loanBefore.loanId;
            data.rescueLoan.originalPrincipal = loanBefore.originalPrincipal;
            data.rescueLoan.remainingPrincipal = loanBefore.remainingPrincipal;
            data.rescueLoan.isActive = loanBefore.isActive;
            data.rescueLoan.issuedUtcTicks = loanBefore.issuedUtcTicks;
            data.rescueLoan.isRestrictedPreparation = loanBefore.isRestrictedPreparation;
        }
    }
}
