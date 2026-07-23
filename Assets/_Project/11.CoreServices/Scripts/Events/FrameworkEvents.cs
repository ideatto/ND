/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - CoreServices 전역에서 사용하는 framework event bus를 제공한다.
 * - 서비스, scene controller, UI adapter 사이의 직접 참조를 줄이고 상태 변경 알림을 중계한다.
 *
 * Main Features
 * - 공용 데이터 준비 완료, 저장 데이터 로드 완료, scene 변경, 무역 완료/정산, 인게임 화면 상태 변경 이벤트를 제공한다.
 * - 구조 대출 발급·상환·종료와 출발 전 제한 모드 진입·해제의 저장 확정 이벤트를 제공한다.
 * - debug ForceRouteEvent 주입 hook 알림을 제공한다.
 * - 이벤트 발행 시 framework log를 남겨 디버그 흐름을 추적할 수 있게 한다.
 *
 * Usage for Team Members
 * - 구독자는 OnEnable/OnDisable 또는 명확한 수명 주기에서 이벤트를 구독/해제해야 한다.
 * - 이벤트를 직접 Invoke하지 말고 Raise... 메서드를 통해 발행해야 한다.
 *
 * Main Public APIs
 * - RaiseSharedGameDataLoaded(...): 공용 기준 데이터 준비 완료를 알린다.
 * - RaiseLoadCompleted(...): 저장 데이터 준비 완료를 알린다.
 * - RaiseSceneChanged(...): Unity scene 전환 완료를 알린다.
 * - RaiseTradeSettlementReady(...): 무역 정산 결과가 UI에 표시될 준비가 되었음을 알린다.
 * - RaiseInGameScreenChanged(...): 인게임 화면 상태 변경을 알린다.
 * - RaiseRouteEventForced(...): debug route event 1회 주입 hook을 알린다.
 *
 * Important Notes
 * - static event bus이므로 구독 해제를 누락하면 비활성 객체가 이벤트를 계속 받을 수 있다.
 * - 이벤트 인자의 null 가능성은 발행하는 서비스의 상태 검증에 따른다.
 */
using System;
using ND.Economy;

namespace ND.Framework
{
    /// <summary>
    /// CoreServices 내부 상태 변경을 느슨하게 전달하는 정적 이벤트 허브이다.
    /// </summary>
    /// <remarks>
    /// Unity 생명주기를 가진 구독자는 비활성화 시 반드시 구독을 해제해야 한다.
    /// </remarks>
    public static class FrameworkEvents
    {
        /// <summary>
        /// 공용 기준 데이터가 검증되어 후속 시스템이 ID 기반 조회를 사용할 수 있을 때 발생한다.
        /// </summary>
        public static event Action<ISharedGameDataProvider> SharedGameDataLoaded;

        /// <summary>
        /// 저장 데이터가 준비되어 game scene 진입 전 후속 시스템이 초기화될 수 있을 때 발생한다.
        /// </summary>
        public static event Action<SaveData> LoadCompleted;

        /// <summary>
        /// 새 Caravan이 SaveData에 추가되고 영속 저장까지 성공한 뒤 한 번 발생한다.
        /// </summary>
        /// <remarks>
        /// 인자 순서는 caravanId, slotIndex이다. 저장 실패 시 발생하지 않으며 구독자는 비활성화 시 구독을 해제해야 한다.
        /// </remarks>
        public static event Action<string, int> CaravanCreated;

        /// <summary>
        /// SceneFlowService가 scene load 완료 콜백을 받은 뒤 발생한다.
        /// </summary>
        public static event Action<string> SceneChanged;

        /// <summary>
        /// 오프라인 무역 완료를 알리기 위한 이벤트이다.
        /// </summary>
        public static event Action<string> TradeOfflineCompleted;

        /// <summary>
        /// 저장된 시간보다 현재 시간이 뒤로 이동한 상황을 알리기 위한 이벤트이다.
        /// </summary>
        public static event Action TimeRollbackDetected;

        /// <summary>
        /// 디버그 또는 도구 코드가 현재 무역을 즉시 완료하도록 요청할 때 발생한다.
        /// </summary>
        public static event Action CompleteTradeRequested;

        /// <summary>
        /// 무역 정산 결과가 생성되어 settlement UI가 표시할 수 있을 때 발생한다.
        /// </summary>
        public static event Action<string, string, JourneyResultData> TradeSettlementReady;

        /// <summary>
        /// 인게임 화면 상태가 preparation, traveling, settlement 중 하나로 변경될 때 발생한다.
        /// </summary>
        public static event Action<InGameScreenState> InGameScreenChanged;

        /// <summary>
        /// SaveData에 확정 반영된 플레이어 무역 화폐가 변경되었을 때 발생한다.
        /// </summary>
        public static event Action<long> TradingCurrencyChanged;

        /// <summary>구조 대출 발급 저장이 성공한 뒤 한 번 발생한다.</summary>
        public static event Action<IssueRescueLoanResult> RescueLoanIssued;

        /// <summary>구조 대출 상환 저장이 성공한 뒤 한 번 발생한다.</summary>
        public static event Action<RepayRescueLoanResult> RescueLoanRepaid;

        /// <summary>전액 상환 저장으로 구조 대출이 비활성화된 뒤 발생한다.</summary>
        public static event Action RescueLoanClosed;

        /// <summary>구조 대출 발급 저장으로 출발 전 제한 모드에 진입한 뒤 발생한다.</summary>
        public static event Action RescueRestrictedModeEntered;

        /// <summary>무역 출발과 제한 해제 상태가 함께 저장된 뒤 발생한다.</summary>
        public static event Action RescueRestrictedModeExited;

        /// <summary>
        /// debug ForceRouteEvent가 Traveling trade에 route event 1회 주입 hook을 등록했을 때 발생한다.
        /// </summary>
        /// <remarks>
        /// 인자 순서는 tradeId, eventId이다.
        /// Core 로드/약탈 적용 API가 연결되기 전까지는 Framework stub hook이며, 구독자는 중복 처리를 방지해야 한다.
        /// FrameworkDebugCommands.TryConsumeForcedRouteEvent로 pending hook을 1회 소모할 수 있다.
        /// </remarks>
        public static event Action<string, string> RouteEventForced;

        /// <summary>
        /// 공용 기준 데이터 준비 완료 이벤트를 발행한다.
        /// </summary>
        /// <param name="provider">검증을 통과한 공용 데이터 provider.</param>
        /// <remarks>
        /// SaveData 로드 완료와 별개로, 도시·상품·마차·견인 동물·무역로 기준 데이터가 먼저 준비되었음을 알린다.
        /// </remarks>
        public static void RaiseSharedGameDataLoaded(ISharedGameDataProvider provider)
        {
            // 공용 데이터는 SaveData의 ID를 해석하는 기준이므로 LoadCompleted보다 먼저 발행한다.
            FrameworkLog.Info($"SharedGameDataLoaded event raised. Summary: {provider?.Summary ?? "None"}");
            SharedGameDataLoaded?.Invoke(provider);
        }

        /// <summary>
        /// 저장 데이터 로드 완료 이벤트를 발행한다.
        /// </summary>
        /// <param name="data">로드 또는 생성이 완료된 현재 저장 데이터.</param>
        /// <remarks>
        /// FrameworkRoot가 game scene 진입 전에 호출하며, 구독자는 전달된 참조를 수정할 수 있으므로 저장 데이터 변경에 주의해야 한다.
        /// </remarks>
        public static void RaiseLoadCompleted(SaveData data)
        {
            // 로드 완료 흐름은 여러 시스템 초기화의 기준점이므로 발행 시점을 로그로 남긴다.
            FrameworkLog.Info("LoadCompleted event raised.");
            LoadCompleted?.Invoke(data);
        }

        /// <summary>저장이 완료된 새 Caravan의 ID와 영속 슬롯을 구독자에게 전달한다.</summary>
        public static void RaiseCaravanCreated(string caravanId, int slotIndex)
        {
            FrameworkLog.Info($"CaravanCreated event raised. CaravanId: {caravanId}, SlotIndex: {slotIndex}");
            CaravanCreated?.Invoke(caravanId, slotIndex);
        }

        /// <summary>
        /// scene 변경 완료 이벤트를 발행한다.
        /// </summary>
        /// <param name="sceneName">로드가 완료된 Unity scene 이름.</param>
        public static void RaiseSceneChanged(string sceneName)
        {
            // 비동기 로드 완료 콜백 이후의 scene 이름을 추적할 수 있도록 기록한다.
            FrameworkLog.Info($"Scene changed: {sceneName}");
            SceneChanged?.Invoke(sceneName);
        }

        /// <summary>
        /// 오프라인 무역 완료 이벤트를 발행한다.
        /// </summary>
        /// <param name="tradeId">완료된 무역의 식별자.</param>
        public static void RaiseTradeOfflineCompleted(string tradeId)
        {
            // 완료된 무역 식별자를 함께 남겨 후속 UI와 로그를 연결할 수 있게 한다.
            FrameworkLog.Info($"TradeOfflineCompleted event raised. TradeId: {tradeId}");
            TradeOfflineCompleted?.Invoke(tradeId);
        }

        /// <summary>
        /// 시간 역행 감지 이벤트를 발행한다.
        /// </summary>
        public static void RaiseTimeRollbackDetected()
        {
            // 시간 역행은 저장 데이터 신뢰도에 영향을 줄 수 있으므로 warning으로 기록한다.
            FrameworkLog.Warning("TimeRollbackDetected event raised.");
            TimeRollbackDetected?.Invoke();
        }

        /// <summary>
        /// 현재 진행 중인 무역의 즉시 완료 요청 이벤트를 발행한다.
        /// </summary>
        /// <remarks>
        /// 주로 debug command에서 사용하며 실제 완료 처리는 TradeProgressCoordinator 구독자가 수행한다.
        /// </remarks>
        public static void RaiseCompleteTradeRequested()
        {
            // 요청 이벤트만 발행하고 실제 상태 변경은 구독 중인 coordinator가 담당한다.
            FrameworkLog.Info("CompleteTradeRequested event raised.");
            CompleteTradeRequested?.Invoke();
        }

        /// <summary>
        /// 무역 정산 결과 준비 이벤트를 발행한다.
        /// </summary>
        /// <param name="tradeId">정산 결과가 연결된 active trade ID.</param>
        /// <param name="result">Core 무역 계산에서 생성된 정산 결과.</param>
        /// <remarks>
        /// SettlementUiBridge가 이 이벤트를 받아 pending settlement를 캐시하고 settlement 화면으로 이동시킨다.
        /// </remarks>
        public static void RaiseTradeSettlementReady(string caravanId, string tradeId, JourneyResultData result)
        {
            // UI bridge가 active trade와 result를 검증할 수 있도록 두 값을 그대로 전달한다.
            FrameworkLog.Info($"TradeSettlementReady event raised. TradeId: {tradeId}");
            TradeSettlementReady?.Invoke(caravanId, tradeId, result);
        }

        /// <summary>
        /// 인게임 화면 상태 변경 이벤트를 발행한다.
        /// </summary>
        /// <param name="screenState">새로 적용할 인게임 화면 상태.</param>
        public static void RaiseInGameScreenChanged(InGameScreenState screenState)
        {
            // 화면 router의 상태 변화는 UI 패널 전환의 기준이므로 상태값을 로그에 남긴다.
            FrameworkLog.Info($"InGameScreenChanged event raised. ScreenState: {screenState}");
            InGameScreenChanged?.Invoke(screenState);
        }

        /// <summary>확정된 플레이어 무역 화폐 스냅샷을 구독자에게 전달한다.</summary>
        public static void RaiseTradingCurrencyChanged(long tradingCurrency)
        {
            FrameworkLog.Info($"TradingCurrencyChanged event raised. Currency: {tradingCurrency}");
            TradingCurrencyChanged?.Invoke(tradingCurrency);
        }

        public static void RaiseRescueLoanIssued(IssueRescueLoanResult result)
        {
            FrameworkLog.Info($"RescueLoanIssued event raised. LoanId: {result?.LoanId ?? string.Empty}");
            RescueLoanIssued?.Invoke(result);
        }

        public static void RaiseRescueLoanRepaid(RepayRescueLoanResult result)
        {
            FrameworkLog.Info($"RescueLoanRepaid event raised. Amount: {result?.RepaidAmount ?? 0L}");
            RescueLoanRepaid?.Invoke(result);
        }

        public static void RaiseRescueLoanClosed()
        {
            FrameworkLog.Info("RescueLoanClosed event raised.");
            RescueLoanClosed?.Invoke();
        }

        public static void RaiseRescueRestrictedModeEntered()
        {
            FrameworkLog.Info("RescueRestrictedModeEntered event raised.");
            RescueRestrictedModeEntered?.Invoke();
        }

        public static void RaiseRescueRestrictedModeExited()
        {
            FrameworkLog.Info("RescueRestrictedModeExited event raised.");
            RescueRestrictedModeExited?.Invoke();
        }

        /// <summary>
        /// debug route event 1회 주입 hook 등록 이벤트를 발행한다.
        /// </summary>
        /// <param name="tradeId">주입 대상 active trade ID.</param>
        /// <param name="eventId">주입할 route event ID.</param>
        /// <remarks>
        /// FrameworkDebugCommands.ForceRouteEvent가 Traveling 검증 후 호출한다.
        /// Core 적용은 구독자 또는 TryConsumeForcedRouteEvent 경로에서 처리한다.
        /// </remarks>
        public static void RaiseRouteEventForced(string tradeId, string eventId)
        {
            FrameworkLog.Info($"RouteEventForced event raised. TradeId: {tradeId}, EventId: {eventId}");
            RouteEventForced?.Invoke(tradeId, eventId);
        }
    }
}
