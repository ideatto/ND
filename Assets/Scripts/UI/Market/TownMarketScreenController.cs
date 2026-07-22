using System;
using ND.Framework;
using UnityEngine;

namespace ND.UI.Market
{
    /// <summary>
    /// Owns only the transient Town <-> Market screen transition.
    /// Market transactions remain owned by MarketTradePanelController.
    /// </summary>
    public static class TownMarketScreenTransition
    {
        public static bool TryOpen(
            InGameScreenState currentState,
            Func<bool> openMarketPanel,
            InGameScreenStateRouter router)
        {
            if (currentState != InGameScreenState.Town || openMarketPanel == null || router == null)
                return false;
            if (!openMarketPanel())
                return false;

            router.RequestScreen(InGameScreenState.Market);
            return true;
        }

        public static bool TryClose(
            InGameScreenState currentState,
            Action closeMarketPanel,
            InGameScreenStateRouter router)
        {
            if (currentState != InGameScreenState.Market || closeMarketPanel == null || router == null)
                return false;

            closeMarketPanel();
            router.RequestScreen(InGameScreenState.Town);
            return true;
        }
    }

    /// <summary>Scene-facing button adapter. No prefab or scene mutation is performed here.</summary>
    public sealed class TownMarketScreenController : MonoBehaviour
    {
        [SerializeField] private MarketTradePanelController marketTradePanel;

        public void Configure(MarketTradePanelController panel)
        {
            marketTradePanel = panel;
        }

        public void OnClickOpenMarket()
        {
            if (!TryOpenMarket())
                Debug.LogWarning("[Town Market] Market could not be opened from the current state.", this);
        }

        public void OnClickCloseMarket()
        {
            if (!TryCloseMarket())
                Debug.LogWarning("[Town Market] Market could not be closed from the current state.", this);
        }

        public bool TryOpenMarket()
        {
            FrameworkRoot root = FrameworkRoot.Instance;
            return root != null && marketTradePanel != null &&
                TownMarketScreenTransition.TryOpen(
                    root.InGameScreenRouter.CurrentScreenState,
                    marketTradePanel.Open,
                    root.InGameScreenRouter);
        }

        public bool TryCloseMarket()
        {
            FrameworkRoot root = FrameworkRoot.Instance;
            return root != null && marketTradePanel != null &&
                TownMarketScreenTransition.TryClose(
                    root.InGameScreenRouter.CurrentScreenState,
                    marketTradePanel.Close,
                    root.InGameScreenRouter);
        }
    }
}
