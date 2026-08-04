using ND.Economy;
using ND.Framework;
using UnityEngine;
using FrameworkSaveData = ND.Framework.SaveData;

namespace ND.UI.RescueLoan
{
    /// <summary>Framework 구조 대출 command와 패널 View를 연결한다.</summary>
    public sealed class RescueLoanPanelPresenter : MonoBehaviour
    {
        [SerializeField] private RescueLoanPanelView view;
        [SerializeField] private string loanId = "rescue_loan";
        [SerializeField, Min(1)] private long minimumTradeCost = 1000L;
        private bool subscribed;

        private void OnEnable()
        {
            ConfigureTestDefinition();
            if (view != null)
            {
                view.IssueRequested += HandleIssueRequested;
                view.RepaymentRequested += HandleRepaymentRequested;
            }

            SubscribeFrameworkEvents();
            Refresh();
        }

        private void Start()
        {
            ConfigureTestDefinition();
            Refresh();
        }

        private void OnDisable()
        {
            if (view != null)
            {
                view.IssueRequested -= HandleIssueRequested;
                view.RepaymentRequested -= HandleRepaymentRequested;
            }

            UnsubscribeFrameworkEvents();
        }

        public RescueLoanPanelSnapshot BuildCurrentSnapshot()
        {
            FrameworkRoot root = FrameworkRoot.Instance;
            RescueLoanCommandService service = root != null ? root.RescueLoan : null;
            FrameworkSaveData data = root != null ? root.CurrentSaveData : null;
            RescueStatusResult status = service != null ? service.EvaluateStatus() : null;
            RescueLoanSaveData loan = data != null ? data.rescueLoan : null;

            return new RescueLoanPanelSnapshot
            {
                IsAvailable = service != null && data?.player != null && loan != null && status?.IsValid == true,
                CanOfferLoan = status?.CanOfferLoan == true,
                NeedsRecovery = status?.NeedsRecovery == true,
                IsRebankrupt = status?.IsRebankrupt == true,
                IsActive = loan?.isActive == true,
                IsRestrictedPreparation = loan?.isRestrictedPreparation == true,
                LoanId = loanId ?? string.Empty,
                TradeMoney = data?.player?.tradingCurrency ?? 0L,
                MinimumTradeCost = minimumTradeCost,
                Shortfall = status?.Shortfall ?? 0L,
                OriginalPrincipal = loan?.originalPrincipal ?? 0L,
                RemainingPrincipal = loan?.remainingPrincipal ?? 0L
            };
        }

        public void Refresh()
        {
            ConfigureTestDefinition();
            view?.Refresh(BuildCurrentSnapshot());
        }

        private void HandleIssueRequested()
        {
            RescueLoanCommandService service = FrameworkRoot.Instance?.RescueLoan;
            SaveResult result = service?.IssueRescueLoan();
            ShowCommandResult(result, "구조 대출이 발급되었습니다.");
        }

        private void HandleRepaymentRequested(long amount)
        {
            RescueLoanCommandService service = FrameworkRoot.Instance?.RescueLoan;
            SaveResult result = service?.RepayRescueLoan(amount);
            ShowCommandResult(result, $"{amount:N0}을 상환했습니다.", RepaymentFailureMessage(result));
        }

        private void ShowCommandResult(SaveResult result, string successMessage, string failureMessage = null)
        {
            Refresh();
            view?.ShowMessage(result != null && result.Succeeded
                ? successMessage
                : failureMessage ?? $"요청 실패: {result?.FailureReason.ToString() ?? "서비스 없음"}");
        }

        public static string RepaymentFailureMessage(SaveResult result)
        {
            if (result?.Message?.IndexOf(
                    nameof(RescueLoanFailureReason.RepaymentWouldTriggerRecovery),
                    System.StringComparison.Ordinal) >= 0)
            {
                return "상환 후 보유 금액이 대출금보다 작을 수 없습니다.";
            }

            return null;
        }

        private void SubscribeFrameworkEvents()
        {
            if (subscribed) return;
            FrameworkEvents.RescueLoanIssued += HandleLoanChanged;
            FrameworkEvents.RescueLoanRepaid += HandleLoanChanged;
            FrameworkEvents.RescueLoanClosed += HandleLoanClosed;
            FrameworkEvents.LoadCompleted += HandleLoadCompleted;
            FrameworkEvents.TradingCurrencyChanged += HandleTradingCurrencyChanged;
            subscribed = true;
        }

        private void UnsubscribeFrameworkEvents()
        {
            if (!subscribed) return;
            FrameworkEvents.RescueLoanIssued -= HandleLoanChanged;
            FrameworkEvents.RescueLoanRepaid -= HandleLoanChanged;
            FrameworkEvents.RescueLoanClosed -= HandleLoanClosed;
            FrameworkEvents.LoadCompleted -= HandleLoadCompleted;
            FrameworkEvents.TradingCurrencyChanged -= HandleTradingCurrencyChanged;
            subscribed = false;
        }

        private void HandleLoanChanged(IssueRescueLoanResult _) => Refresh();
        private void HandleLoanChanged(RepayRescueLoanResult _) => Refresh();
        private void HandleLoanClosed() => Refresh();
        private void HandleLoadCompleted(FrameworkSaveData _)
        {
            ConfigureTestDefinition();
            Refresh();
        }
        private void HandleTradingCurrencyChanged(long _) => Refresh();

        private void ConfigureTestDefinition()
        {
            FrameworkRoot root = FrameworkRoot.Instance;
            if (root == null || minimumTradeCost <= 0L || string.IsNullOrWhiteSpace(loanId)) return;
            root.ConfigureRescueLoanDefinition(new RescueLoanDefinition
            {
                LoanId = loanId,
                MinimumTradeCost = minimumTradeCost
            });
        }
    }
}
