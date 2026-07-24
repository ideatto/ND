using System;
using System.Linq;
using ND.Framework;
using ND.Framework.CargoLoading;
using UnityEngine;

namespace ND.UI.Market
{
    /// <summary>
    /// Opens a buy-only market for one caravan after its arrival sale and settlement complete.
    /// The caravan's saved town is authoritative; player.currentTownId is not used for resolution.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaravanTownPurchaseController : MonoBehaviour
    {
        public const string ErrorPanelMissing = "TOWN_PURCHASE_PANEL_MISSING";
        public const string ErrorCaravanMissing = "TOWN_PURCHASE_CARAVAN_MISSING";
        public const string ErrorTownMissing = "TOWN_PURCHASE_TOWN_MISSING";
        public const string ErrorMarketMissing = "TOWN_PURCHASE_MARKET_MISSING";

        [SerializeField] private MarketTradePanelController marketPanel;
        [SerializeField] private MarketData[] marketCatalog = Array.Empty<MarketData>();

        public event Action<string> ErrorChanged;
        public event Action<string, string> PurchaseOpened;

        public bool OpenForCaravan(string caravanId)
        {
            FrameworkRoot root = FrameworkRoot.Instance;
            if (marketPanel == null)
                return Fail(ErrorPanelMissing);
            if (root == null || root.CurrentSaveData == null || root.SharedGameData == null)
                return Fail(MarketInventoryMutationSession.ErrorInvalidFramework);
            if (!SaveDataLookup.TryGetCaravan(
                    root.CurrentSaveData,
                    caravanId,
                    out ND.Framework.CaravanSaveData caravan))
            {
                return Fail(ErrorCaravanMissing);
            }

            if (string.IsNullOrWhiteSpace(caravan.currentTownId)
                || !root.SharedGameData.TryGetTown(
                    caravan.currentTownId,
                    out SharedTownDefinition town))
            {
                return Fail(ErrorTownMissing);
            }

            MarketData market = marketCatalog?.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(candidate.MarketId, town.MarketId, StringComparison.Ordinal));
            if (market == null)
                return Fail(ErrorMarketMissing);

            if (!marketPanel.OpenForTownPurchase(caravanId, market))
                return Fail(marketPanel.LastErrorCode);

            SetError(string.Empty);
            PurchaseOpened?.Invoke(caravanId, market.MarketId);
            return true;
        }

        private bool Fail(string error)
        {
            SetError(error);
            Debug.LogError($"[Town Purchase] Open failed: {error}", this);
            return false;
        }

        private void SetError(string error)
        {
            ErrorChanged?.Invoke(error ?? string.Empty);
        }
    }
}
