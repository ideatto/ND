using System;
using System.Linq;
using ND.Framework;
using ND.Framework.CargoLoading;
using UnityEngine;

namespace ND.UI.Market
{
    /// <summary>
    /// Connects one arrived Caravan status action to its destination sell-only market.
    /// It resolves saved IDs and forwards UI intent without directly mutating SaveData.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaravanArrivalSaleController : MonoBehaviour
    {
        public const string ErrorPanelMissing = "ARRIVAL_SALE_PANEL_MISSING";
        public const string ErrorPendingMissing = "ARRIVAL_SALE_PENDING_MISSING";
        public const string ErrorDestinationMissing = "ARRIVAL_SALE_DESTINATION_MISSING";
        public const string ErrorMarketMissing = "ARRIVAL_SALE_MARKET_MISSING";
        public const string ErrorSettlementPresentation = "ARRIVAL_SALE_SETTLEMENT_PRESENTATION_FAILED";

        [SerializeField] private MarketTradePanelController marketPanel;
        [SerializeField] private MarketData[] marketCatalog = Array.Empty<MarketData>();

        private string activeCaravanId = string.Empty;
        private string activeTradeId = string.Empty;

        public string LastErrorCode { get; private set; } = string.Empty;
        public string ActiveCaravanId => activeCaravanId;
        public bool IsOpen => marketPanel != null && marketPanel.IsOpen
            && marketPanel.Model != null
            && marketPanel.Model.TradeMode == MarketTradeMode.SellOnly;

        public event Action<string> ErrorChanged;
        public event Action<string, string> SaleOpened;
        public event Action<string, string> SettlementRequested;

        public bool IsSalePending(string caravanId)
        {
            FrameworkRoot root = FrameworkRoot.Instance;
            return root?.CurrentSaveData != null
                && SaveDataLookup.TryGetTradeProgress(
                    root.CurrentSaveData,
                    caravanId,
                    out ND.Framework.TradeProgressSaveData progress)
                && progress.state == ND.Framework.TradeProgressState.SettlementPending
                && SaveDataLookup.TryGetPendingSettlement(
                    root.CurrentSaveData,
                    caravanId,
                    progress.activeTradeId,
                    out PendingSettlementSaveData pending)
                && pending != null
                && pending.hasResult
                && pending.grade != JourneyResultGrade.Failed;
        }

        public bool TryResolveSinglePendingCaravanId(out string caravanId)
        {
            caravanId = string.Empty;
            FrameworkRoot root = FrameworkRoot.Instance;
            if (root?.CurrentSaveData?.caravans == null)
                return false;

            string found = string.Empty;
            foreach (ND.Framework.CaravanSaveData caravan in root.CurrentSaveData.caravans)
            {
                if (caravan == null || !IsSalePending(caravan.caravanId))
                    continue;
                if (!string.IsNullOrEmpty(found))
                    return false;
                found = caravan.caravanId;
            }
            caravanId = found;
            return !string.IsNullOrEmpty(caravanId);
        }

        /// <summary>Called by the Caravan status UI when its arrival-sale button is pressed.</summary>
        public bool OpenForCaravan(string caravanId)
        {
            FrameworkRoot root = FrameworkRoot.Instance;
            if (marketPanel == null)
                return Fail(ErrorPanelMissing);
            if (root == null || root.CurrentSaveData == null || root.SharedGameData == null)
                return Fail(MarketInventoryMutationSession.ErrorInvalidFramework);
            if (!SaveDataLookup.TryGetTradeProgress(
                    root.CurrentSaveData, caravanId, out ND.Framework.TradeProgressSaveData progress)
                || progress.state != ND.Framework.TradeProgressState.SettlementPending
                || !SaveDataLookup.TryGetPendingSettlement(
                    root.CurrentSaveData, caravanId, progress.activeTradeId, out PendingSettlementSaveData pending)
                || pending == null
                || !pending.hasResult
                || pending.grade == JourneyResultGrade.Failed)
            {
                return Fail(ErrorPendingMissing);
            }

            if (!root.SharedGameData.TryGetRoute(progress.activeRouteId, out SharedRouteDefinition route)
                || route == null
                || !root.SharedGameData.TryGetTown(route.ToTownId, out SharedTownDefinition destinationTown))
            {
                return Fail(ErrorDestinationMissing);
            }

            MarketData destinationMarket = marketCatalog?.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(candidate.MarketId, destinationTown.MarketId, StringComparison.Ordinal));
            if (destinationMarket == null)
                return Fail(ErrorMarketMissing);

            marketPanel.ConfigureCatalog(marketCatalog);
            if (!marketPanel.OpenForArrivalSale(caravanId, destinationMarket))
                return Fail(marketPanel.LastErrorCode);

            activeCaravanId = caravanId ?? string.Empty;
            activeTradeId = progress.activeTradeId ?? string.Empty;
            SetError(string.Empty);
            SaleOpened?.Invoke(activeCaravanId, activeTradeId);
            return true;
        }

        /// <summary>
        /// Commits a non-empty sell draft, or skips selling when the draft is empty, then opens
        /// the already-saved settlement. Unsold cargo remains on the same Caravan.
        /// </summary>
        public bool ConfirmSaleAndOpenSettlement()
        {
            if (!IsOpen || string.IsNullOrWhiteSpace(activeCaravanId) || string.IsNullOrWhiteSpace(activeTradeId))
                return Fail(ErrorPendingMissing);

            if (marketPanel.Model.HasDraft)
            {
                MarketTransactionResult transaction = marketPanel.Commit();
                if (transaction == null || !transaction.Success)
                    return Fail(transaction?.ErrorCode ?? MarketInventoryMutationSession.ErrorInvalidTransaction);
            }

            FrameworkRoot root = FrameworkRoot.Instance;
            if (root?.SettlementUiBridge == null
                || !root.SettlementUiBridge.PresentSettlement(activeCaravanId, activeTradeId))
            {
                return Fail(ErrorSettlementPresentation);
            }

            string caravanId = activeCaravanId;
            string tradeId = activeTradeId;
            activeCaravanId = string.Empty;
            activeTradeId = string.Empty;
            marketPanel.Close();
            SetError(string.Empty);
            SettlementRequested?.Invoke(caravanId, tradeId);
            return true;
        }

        public void ConfirmSaleFromUi()
        {
            ConfirmSaleAndOpenSettlement();
        }

        private bool Fail(string error)
        {
            SetError(string.IsNullOrWhiteSpace(error)
                ? MarketInventoryMutationSession.ErrorInvalidFramework
                : error);
            return false;
        }

        private void SetError(string error)
        {
            string normalized = error ?? string.Empty;
            if (string.Equals(LastErrorCode, normalized, StringComparison.Ordinal))
                return;
            LastErrorCode = normalized;
            ErrorChanged?.Invoke(LastErrorCode);
        }
    }
}
