using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ND.Framework
{
    public sealed class InGameSettlementTestView : MonoBehaviour, ISettlementView
    {
        [Header("Panels")]
        [SerializeField] private GameObject preparationPanel;
        [SerializeField] private GameObject travelingPanel;
        [SerializeField] private GameObject settlementPanel;
        [SerializeField] private bool showAllPanelsForDebug;

        [Header("Settlement Text")]
        [SerializeField] private TMP_Text tradeIdText;
        [SerializeField] private TMP_Text gradeText;
        [SerializeField] private TMP_Text failureReasonText;
        [SerializeField] private TMP_Text netProfitText;

        [Header("Actions")]
        [SerializeField] private Button claimSettlementButton;

        public void ShowSettlement(SettlementViewData viewData)
        {
            if (viewData == null)
            {
                ShowNoSettlement("No settlement view data.");
                return;
            }

            SetText(tradeIdText, viewData.TradeId);
            SetText(gradeText, viewData.Grade.ToString());
            SetText(failureReasonText, viewData.FailureReason.ToString());
            SetText(netProfitText, viewData.NetProfit.ToString());
            SetClaimInteractable(viewData.CanClaim);
        }

        public void ShowNoSettlement(string reason)
        {
            SetText(tradeIdText, reason ?? "No settlement.");
            SetText(gradeText, string.Empty);
            SetText(failureReasonText, string.Empty);
            SetText(netProfitText, string.Empty);
            SetClaimInteractable(false);
        }

        public void SetClaimInteractable(bool interactable)
        {
            if (claimSettlementButton != null)
            {
                claimSettlementButton.interactable = interactable;
            }
        }

        private void OnEnable()
        {
            FrameworkEvents.InGameScreenChanged += HandleScreenChanged;
            RefreshCurrentScreen();
        }

        private void OnDisable()
        {
            FrameworkEvents.InGameScreenChanged -= HandleScreenChanged;
        }

        private void HandleScreenChanged(InGameScreenState screenState)
        {
            ApplyScreenState(screenState);
        }

        private void RefreshCurrentScreen()
        {
            var root = FrameworkRoot.Instance;
            if (root == null || root.InGameScreenRouter == null)
            {
                ApplyScreenState(InGameScreenState.Preparation);
                return;
            }

            ApplyScreenState(root.InGameScreenRouter.CurrentScreenState);
        }

        private void ApplyScreenState(InGameScreenState screenState)
        {
            if (showAllPanelsForDebug)
            {
                SetActive(preparationPanel, true);
                SetActive(travelingPanel, true);
                SetActive(settlementPanel, true);
                return;
            }

            SetActive(preparationPanel, screenState == InGameScreenState.Preparation);
            SetActive(travelingPanel, screenState == InGameScreenState.Traveling);
            SetActive(settlementPanel, screenState == InGameScreenState.Settlement);
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }

        private static void SetActive(GameObject target, bool isActive)
        {
            if (target != null && target.activeSelf != isActive)
            {
                target.SetActive(isActive);
            }
        }
    }
}
