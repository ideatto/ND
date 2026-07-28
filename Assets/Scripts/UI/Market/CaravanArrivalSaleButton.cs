using ND.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.Market
{
    /// <summary>Caravan status UI button that opens the sell-only arrival market.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class CaravanArrivalSaleButton : MonoBehaviour
    {
        [SerializeField] private CaravanArrivalSaleController saleController;
        [SerializeField] private string caravanId = string.Empty;
        [SerializeField] private string tradeId = string.Empty;

        private Button button;
        private bool explicitlyBound;

        /// <summary>
        /// Binds this action to one exact eligible Arrival Sale Pending. Both IDs remain
        /// authoritative until the next bind and are never replaced by current selection.
        /// </summary>
        public void Bind(string caravanIdValue, string tradeIdValue)
        {
            caravanId = caravanIdValue ?? string.Empty;
            tradeId = tradeIdValue ?? string.Empty;
            explicitlyBound = !string.IsNullOrWhiteSpace(caravanId)
                && !string.IsNullOrWhiteSpace(tradeId);
            RefreshInteractable();
        }

        /// <summary>
        /// Preserves the legacy single-ID call surface. It does not create an explicit binding;
        /// opening succeeds only when the controller can resolve one eligible Pending globally.
        /// </summary>
        public void Bind(string value)
        {
            caravanId = value ?? string.Empty;
            tradeId = string.Empty;
            explicitlyBound = false;
            RefreshInteractable();
        }

        private void Awake()
        {
            button = GetComponent<Button>();
            explicitlyBound = !string.IsNullOrWhiteSpace(caravanId)
                && !string.IsNullOrWhiteSpace(tradeId);
        }

        private void OnEnable()
        {
            button ??= GetComponent<Button>();
            button.onClick.AddListener(OpenSale);
            FrameworkEvents.TradeSettlementReady += HandleSettlementReady;
            FrameworkEvents.InGameScreenChanged += HandleScreenChanged;
            RefreshInteractable();
        }

        private void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(OpenSale);
            FrameworkEvents.TradeSettlementReady -= HandleSettlementReady;
            FrameworkEvents.InGameScreenChanged -= HandleScreenChanged;
        }

        public void OpenSale()
        {
            if (!explicitlyBound)
                saleController?.TryResolveSinglePendingIdentity(out caravanId, out tradeId);
            if (saleController != null && saleController.OpenForCaravan(caravanId, tradeId))
                // A completed sale reuses this action as "reopen pending settlement". Keep it
                // available until Payment claims the exact pending identity.
                RefreshInteractable();
            else
                Debug.LogError(
                    $"[Arrival Sale UI] Open failed. CaravanId={caravanId}, TradeId={tradeId}, Error={saleController?.LastErrorCode ?? "CONTROLLER_MISSING"}",
                    this);
        }

        public void RefreshInteractable()
        {
            if (button == null)
                button = GetComponent<Button>();
            if (!explicitlyBound
                && (string.IsNullOrWhiteSpace(caravanId)
                    || string.IsNullOrWhiteSpace(tradeId)
                    || saleController == null
                    || !saleController.IsSalePending(caravanId, tradeId)))
            {
                saleController?.TryResolveSinglePendingIdentity(out caravanId, out tradeId);
            }
            button.interactable = saleController != null
                && saleController.IsSalePending(caravanId, tradeId);
        }

        private void HandleSettlementReady(
            string arrivedCaravanId,
            string tradeId,
            JourneyResultData result)
        {
            if (!explicitlyBound
                && saleController != null
                && saleController.TryResolveSinglePendingIdentity(
                    out string resolvedCaravanId,
                    out string resolvedTradeId))
            {
                caravanId = resolvedCaravanId;
                tradeId = resolvedTradeId;
            }

            if (string.Equals(arrivedCaravanId, caravanId, System.StringComparison.Ordinal)
                && string.Equals(tradeId, this.tradeId, System.StringComparison.Ordinal))
                RefreshInteractable();
        }

        private void HandleScreenChanged(InGameScreenState state)
        {
            RefreshInteractable();
        }
    }
}
