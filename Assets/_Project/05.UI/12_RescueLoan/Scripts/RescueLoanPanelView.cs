using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.RescueLoan
{
    public sealed class RescueLoanPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject launcherObject;
        [SerializeField] private TMP_Text launcherLabel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text tradeMoneyText;
        [SerializeField] private TMP_Text minimumTradeCostText;
        [SerializeField] private TMP_Text principalText;
        [SerializeField] private TMP_Text remainingPrincipalText;
        [SerializeField] private TMP_Text restrictionText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private GameObject repaymentRow;
        [SerializeField] private TMP_InputField repaymentInput;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionLabel;

        private bool issueMode;
        public event Action IssueRequested;
        public event Action<long> RepaymentRequested;
        public string StatusDisplay => TextOf(statusText);
        public string TradeMoneyDisplay => TextOf(tradeMoneyText);
        public string RemainingPrincipalDisplay => TextOf(remainingPrincipalText);
        public string MessageDisplay => TextOf(messageText);
        public string LauncherDisplay => TextOf(launcherLabel);
        public bool IsLauncherVisible => launcherObject != null && launcherObject.activeSelf;
        public bool IsActionInteractable => actionButton != null && actionButton.interactable;

        private void Awake() => actionButton?.onClick.AddListener(HandleActionClicked);
        private void OnDestroy() => actionButton?.onClick.RemoveListener(HandleActionClicked);

        public void Refresh(RescueLoanPanelSnapshot snapshot)
        {
            bool available = snapshot != null && snapshot.IsAvailable;
            bool hasAction = available && (snapshot.CanOfferLoan || snapshot.IsActive);
            issueMode = available && snapshot.CanOfferLoan && !snapshot.IsActive;
            if (launcherObject != null) launcherObject.SetActive(hasAction);
            Set(launcherLabel, issueMode ? "구조 대출" : "대출 상환");
            Set(titleText, issueMode ? "구조 대출 안내" : "대출 상환");
            Set(statusText, available ? StatusLabel(snapshot) : "구조 대출 서비스를 사용할 수 없습니다.");
            Set(tradeMoneyText, $"현재 무역 자금  {Amount(snapshot?.TradeMoney ?? 0L)}");
            Set(minimumTradeCostText, $"최소 무역 비용  {Amount(snapshot?.MinimumTradeCost ?? 0L)}");
            Set(principalText, issueMode
                ? $"고정 대출 원금  {Amount(snapshot?.MinimumTradeCost ?? 0L)}"
                : $"최초 원금  {Amount(snapshot?.OriginalPrincipal ?? 0L)}");
            Set(remainingPrincipalText, $"남은 원금  {Amount(snapshot?.RemainingPrincipal ?? 0L)}");
            Set(restrictionText, snapshot != null && snapshot.IsRestrictedPreparation
                ? "제한 상태: 전액 상환 또는 첫 출발까지 적용"
                : "제한 상태: 없음");
            if (repaymentRow != null) repaymentRow.SetActive(!issueMode && snapshot?.IsActive == true);
            if (repaymentInput != null) repaymentInput.interactable = !issueMode && snapshot?.IsActive == true;
            if (actionButton != null) actionButton.interactable = hasAction;
            Set(actionLabel, issueMode ? "고정 원금 대출 받기" : "상환하기");
        }

        public void ShowMessage(string message) => Set(messageText, message ?? string.Empty);
        public void SetRepaymentInput(string value) { if (repaymentInput != null) repaymentInput.text = value ?? string.Empty; }
        public static bool TryParseRepayment(string value, out long amount) =>
            long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out amount) && amount > 0L;

        private void HandleActionClicked()
        {
            if (issueMode) { IssueRequested?.Invoke(); return; }
            if (!TryParseRepayment(repaymentInput != null ? repaymentInput.text : string.Empty, out long amount))
            {
                ShowMessage("상환 금액은 1 이상의 정수여야 합니다.");
                return;
            }
            RepaymentRequested?.Invoke(amount);
        }

        private static string StatusLabel(RescueLoanPanelSnapshot snapshot)
        {
            if (snapshot.IsRebankrupt) return $"재파산: 추가 대출 불가 (부족액 {Amount(snapshot.Shortfall)})";
            if (snapshot.CanOfferLoan) return $"대출 가능 · 부족액 {Amount(snapshot.Shortfall)}";
            if (snapshot.IsActive) return "활성 대출 상환 가능";
            if (snapshot.NeedsRecovery) return "복구 필요";
            return "정상: 구조 대출 불필요";
        }

        private static string Amount(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
        private static string TextOf(TMP_Text target) => target != null ? target.text : string.Empty;
        private static void Set(TMP_Text target, string value) { if (target != null) target.text = value; }
    }
}
