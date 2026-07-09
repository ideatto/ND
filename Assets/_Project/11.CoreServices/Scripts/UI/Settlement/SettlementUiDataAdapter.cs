using UnityEngine;

namespace ND.Framework
{
    public sealed class SettlementUiDataAdapter : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour settlementViewBehaviour;
        [SerializeField] private bool refreshOnEnable = true;

        private ISettlementView settlementView;
        private SettlementUiBridge subscribedBridge;
        private bool isClaimProcessing;

        public void OnClickClaimSettlement()
        {
            if (isClaimProcessing)
            {
                return;
            }

            var bridge = GetBridge();
            if (bridge == null)
            {
                ShowNoSettlement("No settlement bridge.");
                return;
            }

            isClaimProcessing = true;
            SetClaimInteractable(false);

            var claimed = bridge.ClaimSettlementAndReset();
            if (!claimed)
            {
                isClaimProcessing = false;
                RefreshSettlementView();
                return;
            }

            ClearClaimProcessing();
        }

        private void OnEnable()
        {
            ResolveView();
            FrameworkEvents.InGameScreenChanged += HandleScreenChanged;
            SubscribeBridge();

            if (refreshOnEnable)
            {
                RefreshSettlementView();
            }
        }

        private void OnDisable()
        {
            FrameworkEvents.InGameScreenChanged -= HandleScreenChanged;
            UnsubscribeBridge();
            ClearClaimProcessing();
        }

        private void HandleScreenChanged(InGameScreenState screenState)
        {
            if (screenState == InGameScreenState.Settlement)
            {
                RefreshSettlementView();
                return;
            }

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
            if (bridge == null)
            {
                ShowNoSettlement("No settlement bridge.");
                return;
            }

            string tradeId;
            JourneyResultData result;
            if (!bridge.TryGetPendingSettlement(out tradeId, out result))
            {
                ShowNoSettlement("No settlement result.");
                return;
            }

            ShowSettlement(tradeId, result);
        }

        private void ShowSettlement(string tradeId, JourneyResultData result)
        {
            ResolveView();
            if (settlementView == null)
            {
                return;
            }

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
                canClaim,
                CreateStatusMessage(result));
        }

        private static string CreateStatusMessage(JourneyResultData result)
        {
            if (result == null)
            {
                return "No settlement result.";
            }

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
            if (settlementView != null)
            {
                return;
            }

            settlementView = settlementViewBehaviour as ISettlementView;
            if (settlementView == null && settlementViewBehaviour != null)
            {
                FrameworkLog.Warning("Settlement view behaviour does not implement ISettlementView.");
            }
        }

        private void SubscribeBridge()
        {
            var bridge = GetBridge();
            if (bridge == null || subscribedBridge == bridge)
            {
                return;
            }

            UnsubscribeBridge();
            subscribedBridge = bridge;
            subscribedBridge.SettlementReady += HandleSettlementReady;
        }

        private void UnsubscribeBridge()
        {
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

        private void ClearClaimProcessing()
        {
            isClaimProcessing = false;
        }
    }
}
