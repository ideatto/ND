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

        private Button button;
        private bool explicitlyBound;

        public void Bind(string value)
        {
            caravanId = value ?? string.Empty;
            explicitlyBound = !string.IsNullOrWhiteSpace(caravanId);
            RefreshInteractable();
        }

        private void Awake()
        {
            button = GetComponent<Button>();
            explicitlyBound = !string.IsNullOrWhiteSpace(caravanId);
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
            if (string.IsNullOrWhiteSpace(caravanId))
                saleController?.TryResolveSinglePendingCaravanId(out caravanId);
            if (saleController != null && saleController.OpenForCaravan(caravanId))
                button.interactable = false;
            else
                Debug.LogError(
                    $"[Arrival Sale UI] Open failed. CaravanId={caravanId}, Error={saleController?.LastErrorCode ?? "CONTROLLER_MISSING"}",
                    this);
        }

        public void RefreshInteractable()
        {
            if (button == null)
                button = GetComponent<Button>();
            if (!explicitlyBound
                && (string.IsNullOrWhiteSpace(caravanId)
                    || saleController == null
                    || !saleController.IsSalePending(caravanId)))
            {
                saleController?.TryResolveSinglePendingCaravanId(out caravanId);
            }
            button.interactable = saleController != null
                && saleController.IsSalePending(caravanId);
        }

        private void HandleSettlementReady(
            string arrivedCaravanId,
            string tradeId,
            JourneyResultData result)
        {
            if (!explicitlyBound
                && saleController != null
                && saleController.IsSalePending(arrivedCaravanId))
            {
                caravanId = arrivedCaravanId ?? string.Empty;
            }

            if (string.Equals(arrivedCaravanId, caravanId, System.StringComparison.Ordinal))
                RefreshInteractable();
        }

        private void HandleScreenChanged(InGameScreenState state)
        {
            RefreshInteractable();
        }
    }
}
