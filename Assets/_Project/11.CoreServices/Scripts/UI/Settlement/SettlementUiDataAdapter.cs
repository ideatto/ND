/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - SettlementUiBridge의 pending settlement를 ISettlementView 구현체가 표시할 SettlementViewData로 변환한다.
 * - SettlementViewData의 long 금액과 M2 계산값을 UI view model로 변환한다.
 *
 * Main Features
 * - InGameScreenChanged와 SettlementReady 이벤트를 구독해 settlement view를 갱신한다.
 * - claim 중복 클릭을 방지하고 처리 중 버튼 상호작용을 비활성화한다.
 * - view component가 ISettlementView를 구현하는지 확인한다.
 *
 * Usage for Team Members
 * - settlementViewBehaviour에는 ISettlementView를 구현한 MonoBehaviour를 연결한다.
 * - claim 버튼은 OnClickClaimSettlement()에 연결한다.
 * - refreshOnEnable이 true이면 활성화 시 bridge의 pending settlement를 즉시 표시한다.
 *
 * Main Public APIs
 * - OnClickClaimSettlement(): UI claim 버튼에서 호출하는 entry point.
 *
 * Important Notes
 * - 이 adapter는 저장 데이터나 Core 정산을 직접 수정하지 않고 SettlementUiBridge에 위임한다.
 * - OnDisable에서 이벤트 구독과 claim processing 상태를 정리한다.
 */
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// Settlement bridge 데이터를 UI view contract로 변환하고 claim 요청을 전달하는 MonoBehaviour adapter이다.
    /// </summary>
    public sealed class SettlementUiDataAdapter : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour settlementViewBehaviour;
        [SerializeField] private bool refreshOnEnable = true;

        private ISettlementView settlementView;
        private SettlementUiBridge subscribedBridge;
        private bool isClaimProcessing;

        /// <summary>
        /// settlement claim 버튼 클릭을 처리한다.
        /// </summary>
        /// <remarks>
        /// 중복 클릭을 막기 위해 처리 중에는 추가 요청을 무시하고 claim 버튼을 비활성화한다.
        /// </remarks>
        public void OnClickClaimSettlement()
        {
            // 같은 settlement를 여러 번 claim하지 않도록 adapter 단에서 먼저 중복 입력을 막는다.
            if (isClaimProcessing)
            {
                return;
            }

            // A stale or duplicated completion callback can arrive after the first claim has
            // already routed to Preparation. Ignore it instead of reopening S8 with
            // "No settlement result."
            if (!IsSettlementScreenActive())
            {
                return;
            }

            var bridge = GetBridge();
            // bridge가 없으면 claim을 처리할 수 없으므로 사용자에게 결과 없음 상태를 표시한다.
            if (bridge == null)
            {
                ShowNoSettlement("No settlement bridge.");
                return;
            }

            isClaimProcessing = true;
            SetClaimInteractable(false);

            // 실제 claim과 저장 데이터 갱신은 bridge/coordinator가 수행한다.
            var claimed = bridge.ClaimSettlementAndReset();
            if (!claimed)
            {
                // claim 실패 시 처리 상태를 되돌리고 현재 pending settlement를 다시 표시한다.
                isClaimProcessing = false;
                RefreshSettlementView();
                return;
            }

            ClearClaimProcessing();
        }

        private void OnEnable()
        {
            // 활성화 시 view 참조를 확보하고 화면 상태/정산 준비 이벤트를 구독한다.
            ResolveView();
            FrameworkEvents.InGameScreenChanged += HandleScreenChanged;
            SubscribeBridge();

            // scene 진입 직후 이미 pending settlement가 있을 수 있으므로 설정에 따라 즉시 refresh한다.
            // This adapter can live under the shared trade UI root. Only refresh while the
            // Framework is actually in Settlement, otherwise enabling S1 would show an empty S8.
            if (refreshOnEnable && IsSettlementScreenActive())
            {
                RefreshSettlementView();
            }
        }

        private void OnDisable()
        {
            // 비활성 adapter가 이벤트를 받아 UI를 갱신하지 않도록 모든 구독과 처리 상태를 정리한다.
            FrameworkEvents.InGameScreenChanged -= HandleScreenChanged;
            UnsubscribeBridge();
            ClearClaimProcessing();
        }

        private void HandleScreenChanged(InGameScreenState screenState)
        {
            // settlement 화면으로 전환될 때 bridge cache를 읽어 표시 데이터를 갱신한다.
            if (screenState == InGameScreenState.Settlement)
            {
                RefreshSettlementView();
                return;
            }

            // preparation으로 돌아오면 이전 claim 처리 상태를 초기화해 다음 무역을 받을 수 있게 한다.
            if (screenState == InGameScreenState.Preparation)
            {
                ClearClaimProcessing();
            }
        }

        private void HandleSettlementReady(string tradeId, JourneyResultData result)
        {
            ShowSettlement(tradeId, result);
        }

        private void RefreshSettlementView()
        {
            ResolveView();

            var bridge = GetBridge();
            // bridge가 없으면 settlement 결과가 준비되지 않은 상태로 표시한다.
            if (bridge == null)
            {
                ShowNoSettlement("No settlement bridge.");
                return;
            }

            string caravanId;
            string tradeId;
            JourneyResultData result;
            // pending settlement가 없으면 claim 버튼이 남아 있지 않도록 결과 없음 상태로 갱신한다.
            if (!bridge.TryGetPendingSettlement(out caravanId, out tradeId, out result))
            {
                ShowNoSettlement("No settlement result.");
                return;
            }

            ShowSettlement(tradeId, result);
        }

        private void ShowSettlement(string tradeId, JourneyResultData result)
        {
            ResolveView();
            // view가 연결되지 않은 경우 adapter는 저장 상태를 바꾸지 않고 표시만 생략한다.
            if (settlementView == null)
            {
                return;
            }

            // result가 없으면 view data를 만들 수 없으므로 결과 없음 상태로 fallback한다.
            if (result == null)
            {
                ShowNoSettlement("Settlement result is null.");
                return;
            }

            var viewData = CreateViewData(tradeId, result, !isClaimProcessing);
            settlementView.ShowSettlement(viewData);
            settlementView.SetClaimInteractable(viewData.CanClaim);
        }

        private SettlementViewData CreateViewData(string tradeId, JourneyResultData result, bool canClaim)
        {
            return new SettlementViewData(
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
                canClaim,
                CreateStatusMessage(result));
        }

        private static string CreateStatusMessage(JourneyResultData result)
        {
            // result가 없을 때도 UI가 표시할 수 있는 안정적인 메시지를 반환한다.
            if (result == null)
            {
                return "No settlement result.";
            }

            // UI 표시 문구는 결과 등급만 기준으로 단순하게 유지한다.
            switch (result.grade)
            {
                case JourneyResultGrade.Failed:
                    return "Trade failed.";
                case JourneyResultGrade.PartialSuccess:
                    return "Trade completed with losses.";
                case JourneyResultGrade.Success:
                default:
                    return "Trade completed.";
            }
        }

        private void ShowNoSettlement(string reason)
        {
            ResolveView();
            // view가 없으면 화면 갱신 대신 로그를 남겨 scene wiring 문제를 추적할 수 있게 한다.
            if (settlementView == null)
            {
                FrameworkLog.Warning(reason);
                return;
            }

            settlementView.ShowNoSettlement(reason);
            settlementView.SetClaimInteractable(false);
        }

        private void SetClaimInteractable(bool interactable)
        {
            ResolveView();
            settlementView?.SetClaimInteractable(interactable);
        }

        private void ResolveView()
        {
            // 이미 확인된 view는 반복 cast하지 않는다.
            if (settlementView != null)
            {
                return;
            }

            // serialized MonoBehaviour가 ISettlementView를 구현해야 adapter가 표시를 위임할 수 있다.
            settlementView = settlementViewBehaviour as ISettlementView;
            if (settlementView == null && settlementViewBehaviour != null)
            {
                FrameworkLog.Warning("Settlement view behaviour does not implement ISettlementView.");
            }
        }

        private void SubscribeBridge()
        {
            var bridge = GetBridge();
            // bridge가 없거나 이미 같은 bridge를 구독 중이면 중복 구독하지 않는다.
            if (bridge == null || subscribedBridge == bridge)
            {
                return;
            }

            // root 교체 같은 상황에 대비해 이전 bridge 구독을 먼저 정리한다.
            UnsubscribeBridge();
            subscribedBridge = bridge;
            subscribedBridge.SettlementReady += HandleSettlementReady;
        }

        private void UnsubscribeBridge()
        {
            // 구독 중인 bridge가 없으면 해제할 이벤트도 없다.
            if (subscribedBridge == null)
            {
                return;
            }

            subscribedBridge.SettlementReady -= HandleSettlementReady;
            subscribedBridge = null;
        }

        private static SettlementUiBridge GetBridge()
        {
            return FrameworkRoot.Instance != null ? FrameworkRoot.Instance.SettlementUiBridge : null;
        }

        private static bool IsSettlementScreenActive()
        {
            // Use the same SaveData router as the screen presenter so adapter visibility cannot
            // disagree with the Framework state after loading a pending settlement.
            var root = FrameworkRoot.Instance;
            return InGameScreenStateRouter.MapFromSaveData(root != null ? root.CurrentSaveData : null)
                == InGameScreenState.Settlement;
        }

        private void ClearClaimProcessing()
        {
            isClaimProcessing = false;
        }
    }
}
