using System;
using System.Linq;
using ND.Framework;
using ND.Framework.CargoLoading;
using ND.UI.CargoSell;
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
        public const string ErrorIdentityMismatch = "ARRIVAL_SALE_IDENTITY_MISMATCH";

        [SerializeField] private MarketTradePanelController marketPanel;
        [SerializeField] private CargoSellPopupController cargoSellPopup;
        [SerializeField] private MarketData[] marketCatalog = Array.Empty<MarketData>();

        private string activeCaravanId = string.Empty;
        private string activeTradeId = string.Empty;

        public string LastErrorCode { get; private set; } = string.Empty;
        public string ActiveCaravanId => activeCaravanId;
        public string ActiveTradeId => activeTradeId;
        public bool IsOpen => marketPanel != null && marketPanel.IsOpen
            && marketPanel.Model != null
            && marketPanel.Model.TradeMode == MarketTradeMode.SellOnly;

        public event Action<string> ErrorChanged;
        public event Action<string, string> SaleOpened;
        public event Action<string, string> SettlementRequested;

        private void OnDisable()
        {
            if (cargoSellPopup == null)
                return;
            cargoSellPopup.ConfirmRequested -= HandleCargoSellConfirmed;
            cargoSellPopup.CloseRequested -= HandleCargoSellClosed;
        }

        public bool IsSalePending(string caravanId, string tradeId)
        {
            FrameworkRoot root = FrameworkRoot.Instance;
            if (root?.CurrentSaveData == null || string.IsNullOrWhiteSpace(caravanId) || string.IsNullOrWhiteSpace(tradeId)
                || !SaveDataLookup.TryGetCaravan(root.CurrentSaveData, caravanId, out ND.Framework.CaravanSaveData caravan) || caravan == null
                || !SaveDataLookup.TryGetTradeProgress(root.CurrentSaveData, caravanId, out ND.Framework.TradeProgressSaveData progress) || progress == null
                || !string.Equals(progress.activeTradeId, tradeId, StringComparison.Ordinal)
                || !SaveDataLookup.TryGetPendingSettlement(root.CurrentSaveData, caravanId, tradeId, out PendingSettlementSaveData pending)
                || pending == null || !pending.hasResult || pending.claimed || pending.grade == JourneyResultGrade.Failed)
                return false;

            return caravan.state == JourneyState.Selling && progress.state == ND.Framework.TradeProgressState.Selling
                || caravan.state == JourneyState.Settling && progress.state == ND.Framework.TradeProgressState.SettlementPending;
        }

        public bool TryResolveSinglePendingIdentity(out string caravanId, out string tradeId)
        {
            caravanId = string.Empty;
            tradeId = string.Empty;
            FrameworkRoot root = FrameworkRoot.Instance;
            if (root?.CurrentSaveData?.caravans == null)
                return false;

            string foundCaravanId = string.Empty;
            string foundTradeId = string.Empty;
            foreach (ND.Framework.CaravanSaveData caravan in root.CurrentSaveData.caravans)
            {
                if (caravan == null
                    || !SaveDataLookup.TryGetTradeProgress(
                        root.CurrentSaveData,
                        caravan.caravanId,
                        out ND.Framework.TradeProgressSaveData progress)
                    || !IsSalePending(caravan.caravanId, progress.activeTradeId))
                    continue;
                if (!string.IsNullOrEmpty(foundCaravanId))
                    return false;
                foundCaravanId = caravan.caravanId;
                foundTradeId = progress.activeTradeId;
            }
            caravanId = foundCaravanId;
            tradeId = foundTradeId;
            return !string.IsNullOrEmpty(caravanId) && !string.IsNullOrEmpty(tradeId);
        }

        /// <summary>
        /// Opens the sell-only destination market only when the requested Caravan and trade
        /// still identify the same eligible Pending settlement.
        /// </summary>
        public bool OpenForCaravan(string caravanId, string tradeId)
        {
            FrameworkRoot root = FrameworkRoot.Instance;
            if (marketPanel == null)
                return Fail(ErrorPanelMissing);
            if (root == null || root.CurrentSaveData == null || root.SharedGameData == null)
                return Fail(MarketInventoryMutationSession.ErrorInvalidFramework);
            if (SaveDataLookup.TryGetCaravan(root.CurrentSaveData, caravanId, out ND.Framework.CaravanSaveData settlementCaravan)
                && settlementCaravan?.state == JourneyState.Settling
                && SaveDataLookup.TryGetTradeProgress(root.CurrentSaveData, caravanId, out ND.Framework.TradeProgressSaveData settlementProgress)
                && settlementProgress.state == ND.Framework.TradeProgressState.SettlementPending
                && string.Equals(settlementProgress.activeTradeId, tradeId, StringComparison.Ordinal)
                && SaveDataLookup.TryGetPendingSettlement(root.CurrentSaveData, caravanId, tradeId, out PendingSettlementSaveData settlementPending)
                && settlementPending != null && settlementPending.hasResult && !settlementPending.claimed
                && settlementPending.grade != JourneyResultGrade.Failed)
            {
                if (root.SettlementUiBridge == null || !root.SettlementUiBridge.PresentSettlement(caravanId, tradeId))
                    return Fail(ErrorSettlementPresentation);

                SetError(string.Empty);
                SettlementRequested?.Invoke(caravanId, tradeId);
                return true;
            }

            if (string.IsNullOrWhiteSpace(caravanId) || string.IsNullOrWhiteSpace(tradeId)
                || !SaveDataLookup.TryGetCaravan(root.CurrentSaveData, caravanId, out ND.Framework.CaravanSaveData savedCaravan)
                || savedCaravan == null || savedCaravan.state != JourneyState.Selling
                || !SaveDataLookup.TryGetTradeProgress(root.CurrentSaveData, caravanId, out ND.Framework.TradeProgressSaveData progress)
                || progress.state != ND.Framework.TradeProgressState.Selling
                || !SaveDataLookup.TryGetPendingSettlement(root.CurrentSaveData, caravanId, tradeId, out PendingSettlementSaveData pending)
                || pending == null || !pending.hasResult || pending.grade == JourneyResultGrade.Failed)
            {
                return Fail(ErrorPendingMissing);
            }
            if (!string.Equals(progress.activeTradeId, tradeId, StringComparison.Ordinal))
                return Fail(ErrorIdentityMismatch);

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
            marketPanel.SetExternalArrivalSalePresentation(cargoSellPopup != null);
            if (!marketPanel.OpenForArrivalSale(caravanId, tradeId, destinationMarket))
            {
                marketPanel.SetExternalArrivalSalePresentation(false);
                return Fail(marketPanel.LastErrorCode);
            }

            if (cargoSellPopup != null)
            {
                cargoSellPopup.ConfirmRequested -= HandleCargoSellConfirmed;
                cargoSellPopup.ConfirmRequested += HandleCargoSellConfirmed;
                cargoSellPopup.CloseRequested -= HandleCargoSellClosed;
                cargoSellPopup.CloseRequested += HandleCargoSellClosed;
                var popupData = CargoSellViewDataBuilder.Build(
                    caravanId,
                    tradeId,
                    ResolveCaravanDisplayName(savedCaravan),
                    destinationTown.DisplayName,
                    savedCaravan.cargo,
                    marketPanel.Model.Items,
                    Array.Empty<CargoSellPendingSaleRowViewData>(),
                    marketPanel.Model.MaximumCargoWeight);
                if (!cargoSellPopup.Open(popupData))
                {
                    marketPanel.Close();
                    return Fail(ErrorPanelMissing);
                }
            }

            activeCaravanId = caravanId ?? string.Empty;
            activeTradeId = tradeId ?? string.Empty;
            SetError(string.Empty);
            SaleOpened?.Invoke(activeCaravanId, activeTradeId);
            return true;
        }

        /// <summary>
        /// Compatibility entry point for older callers. It fails when zero or multiple eligible
        /// Pending settlements exist, or when the sole Pending belongs to another Caravan.
        /// </summary>
        public bool OpenForCaravan(string caravanId)
        {
            if (!TryResolveSinglePendingIdentity(out string resolvedCaravanId, out string resolvedTradeId)
                || !string.Equals(caravanId, resolvedCaravanId, StringComparison.Ordinal))
            {
                return Fail(ErrorPendingMissing);
            }
            return OpenForCaravan(resolvedCaravanId, resolvedTradeId);
        }

        /// <summary>
        /// Commits a non-empty sell draft, or skips selling when the draft is empty, then opens
        /// the already-saved settlement. Unsold cargo remains on the same Caravan.
        /// </summary>
        public bool ConfirmSaleAndOpenSettlement()
        {
            return ConfirmSaleAndOpenSettlement(null);
        }

        private bool ConfirmSaleAndOpenSettlement(
            System.Collections.Generic.IReadOnlyList<MarketTransactionLine> explicitLines)
        {
            if (!IsOpen || string.IsNullOrWhiteSpace(activeCaravanId) || string.IsNullOrWhiteSpace(activeTradeId))
                return Fail(ErrorPendingMissing);
            if (!string.Equals(activeCaravanId, marketPanel.ActiveCaravanId, StringComparison.Ordinal)
                || !string.Equals(activeTradeId, marketPanel.ActiveTradeId, StringComparison.Ordinal))
            {
                return Fail(ErrorIdentityMismatch);
            }

            FrameworkRoot root = FrameworkRoot.Instance;
            TradeProgressCoordinator coordinator =
                root?.TradeProgressCoordinator;

            if (coordinator == null)
            {
                return Fail(
                    MarketInventoryMutationSession.ErrorInvalidFramework);
            }

            TradeProgressCoordinator.ArrivalSaleTransitionSnapshot
                transitionSnapshot = null;
            bool completionAlreadyPublished = false;

            bool hasDraft = explicitLines != null
                ? explicitLines.Count > 0
                : marketPanel.Model.HasDraft;
            if (hasDraft)
            {
                if (!SaveDataLookup.TryGetPendingSettlement(
                        root.CurrentSaveData,
                        activeCaravanId,
                        activeTradeId,
                        out PendingSettlementSaveData pending)
                    || pending == null)
                {
                    return Fail(ErrorPendingMissing);
                }

                long revenueBefore =
                    pending.arrivalSaleRevenue;
                var soldItemsBefore =
                    pending.soldItems;

                MarketTransactionResult transaction = explicitLines == null
                    ? marketPanel.Commit(
                        result =>
                            TryStageArrivalSale(pending, result)
                            && coordinator.TryStageArrivalSaleCompletion(
                                activeCaravanId,
                                activeTradeId,
                                out transitionSnapshot),
                        () =>
                        {
                            pending.arrivalSaleRevenue =
                                revenueBefore;
                            pending.soldItems =
                                soldItemsBefore;

                            coordinator.RollbackArrivalSaleCompletion(
                                transitionSnapshot);
                        })
                    : marketPanel.CommitExplicit(
                        explicitLines,
                        result =>
                            TryStageArrivalSale(pending, result)
                            && coordinator.TryStageArrivalSaleCompletion(
                                activeCaravanId,
                                activeTradeId,
                                out transitionSnapshot),
                        () =>
                        {
                            pending.arrivalSaleRevenue = revenueBefore;
                            pending.soldItems = soldItemsBefore;
                            coordinator.RollbackArrivalSaleCompletion(transitionSnapshot);
                        });

                if (transaction == null || !transaction.Success)
                {
                    return Fail(
                        transaction?.ErrorCode
                        ?? MarketInventoryMutationSession
                            .ErrorInvalidTransaction);
                }
            }
            else
            {
                if (!coordinator.TryCommitEmptyArrivalSaleCompletion(
                        activeCaravanId,
                        activeTradeId,
                        out bool saveFailed))
                {
                    return Fail(
                        saveFailed
                            ? MarketInventoryMutationSession.ErrorSaveFailed
                            : MarketInventoryMutationSession.ErrorInvalidTransaction);
                }

                completionAlreadyPublished = true;
            }

            if (!completionAlreadyPublished)
            {
                coordinator.PublishArrivalSaleCompletion(
                    transitionSnapshot);
            }

            if (!string.Equals(activeCaravanId, marketPanel.ActiveCaravanId, StringComparison.Ordinal)
                || !string.Equals(activeTradeId, marketPanel.ActiveTradeId, StringComparison.Ordinal))
            {
                return Fail(ErrorIdentityMismatch);
            }
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
            if (cargoSellPopup != null)
                cargoSellPopup.gameObject.SetActive(false);
            SetError(string.Empty);
            SettlementRequested?.Invoke(caravanId, tradeId);
            return true;
        }

        private static bool TryStageArrivalSale(
            PendingSettlementSaveData pending,
            MarketTransactionResult transaction)
        {
            if (pending == null || transaction == null)
                return false;

            pending.arrivalSaleRevenue = Math.Max(0L, transaction.SaleRevenue);
            pending.soldItems = new System.Collections.Generic.List<SettlementItemSaveData>();
            if (transaction.Items != null)
            {
                foreach (MarketTransactionItemSummary item in transaction.Items)
                {
                    if (item == null || item.SellQuantity <= 0)
                        continue;
                    long total = Math.Max(0L, item.SaleRevenue);
                    pending.soldItems.Add(new SettlementItemSaveData
                    {
                        itemId = item.ItemId ?? string.Empty,
                        quantity = item.SellQuantity,
                        unitPrice = total / item.SellQuantity,
                        totalAmount = total
                    });
                }
            }

            return true;
        }

        public void ConfirmSaleFromUi()
        {
            ConfirmSaleAndOpenSettlement();
        }

        private void HandleCargoSellConfirmed(
            System.Collections.Generic.IReadOnlyList<CargoSellPendingSaleRowViewData> pending)
        {
            bool succeeded = ConfirmSaleAndOpenSettlement(CargoSellMarketTransactionBuilder.Build(pending));
            if (!succeeded && cargoSellPopup != null)
            {
                // The sale may already be durably completed even when only settlement
                // presentation fails. Do not leave a Settling Caravan inside a retryable sell UI.
                bool saleCompleted = string.Equals(
                    LastErrorCode,
                    ErrorSettlementPresentation,
                    StringComparison.Ordinal);
                cargoSellPopup.SetSubmissionResult(saleCompleted, LastErrorCode);
            }
        }

        private void HandleCargoSellClosed()
        {
            marketPanel?.Close();
            activeCaravanId = string.Empty;
            activeTradeId = string.Empty;
            if (cargoSellPopup != null)
                cargoSellPopup.gameObject.SetActive(false);
        }

        private bool Fail(string error)
        {
            SetError(string.IsNullOrWhiteSpace(error)
                ? MarketInventoryMutationSession.ErrorInvalidFramework
                : error);
            return false;
        }

        private static string ResolveCaravanDisplayName(ND.Framework.CaravanSaveData caravan)
        {
            string displayName = caravan?.displayName?.Trim() ?? string.Empty;
            return string.IsNullOrEmpty(displayName)
                ? $"Caravan {(caravan != null ? caravan.slotIndex + 1 : 1)}"
                : displayName;
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
