using ND.Framework;
using UnityEngine;

/// <summary>
/// Routes Framework screen state to a UI implementation and refreshes traveling display data.
/// It does not advance progress, settle a trade, or mutate SaveData.
/// </summary>
public sealed class FrameworkTradeScreenPresenter : MonoBehaviour
{
    [Tooltip("Assign a MonoBehaviour implementing ITradeScreenView.")]
    [SerializeField] private MonoBehaviour viewBehaviour;

    [SerializeField, Min(0.05f)] private float travelingRefreshInterval = 0.2f;

    private ITradeScreenView view;
    private InGameScreenState currentScreenState;
    private float nextTravelingRefreshTime;
    private bool isTradeScreenOpen;

    private void OnEnable()
    {
        view = viewBehaviour as ITradeScreenView;
        FrameworkEvents.InGameScreenChanged += HandleScreenChanged;
        FrameworkEvents.TradeSettlementReady += HandleTradeSettlementReady;
    }

    private void OnDisable()
    {
        FrameworkEvents.InGameScreenChanged -= HandleScreenChanged;
        FrameworkEvents.TradeSettlementReady -= HandleTradeSettlementReady;
        view = null;
    }

    private void Update()
    {
        // Background progress polling must not reactivate S7 after the trade screen was closed.
        if (view == null || !isTradeScreenOpen || currentScreenState != InGameScreenState.Traveling)
        {
            return;
        }

        if (Time.unscaledTime < nextTravelingRefreshTime)
        {
            return;
        }

        nextTravelingRefreshTime = Time.unscaledTime + travelingRefreshInterval;
        RefreshTravelingView();
    }

    public void RefreshFromCurrentSaveData()
    {
        FrameworkRoot root = FrameworkRoot.Instance;
        InGameScreenState state = InGameScreenStateRouter.MapFromSaveData(
            root != null ? root.CurrentSaveData : null);

        HandleScreenChanged(state);
    }

    /// <summary>Opens the trade UI at the screen matching the current Framework save state.</summary>
    public void OpenTradeScreen()
    {
        // Trade UI navigation begins only from an explicit user action such as Start Trade Button.
        isTradeScreenOpen = true;
        RefreshFromCurrentSaveData();
    }

    /// <summary>Closes the trade UI without changing Framework trade state.</summary>
    public void CloseTradeScreen()
    {
        isTradeScreenOpen = false;
        view?.HideTradeScreens();
    }

    private void HandleScreenChanged(InGameScreenState state)
    {
        InGameScreenState previousState = currentScreenState;
        currentScreenState = state;
        if (view == null)
        {
            return;
        }

        if (!isTradeScreenOpen)
        {
            FrameworkRoot root = FrameworkRoot.Instance;
            if (state == InGameScreenState.Settlement
                && root?.SettlementUiBridge != null
                && root.SettlementUiBridge.IsSettlementPresentationRequested)
            {
                // Arrival closes the traveling presentation while it waits for the player to
                // sell cargo. A successful sale explicitly requests settlement afterwards, so
                // reopen only the settlement presentation without reopening preparation.
                isTradeScreenOpen = true;
                view.ShowSettlement();
                return;
            }

            // Keep the visual root closed when Framework state changes through another entry point.
            view.HideTradeScreens();
            return;
        }

        // A successful settlement claim routes Framework to Town. Older saves can still map
        // back to Preparation, so both transitions close the completed trade flow.
        if (previousState == InGameScreenState.Settlement &&
            (state == InGameScreenState.Preparation || state == InGameScreenState.Town))
        {
            CloseTradeScreen();
            return;
        }

        switch (state)
        {
            case InGameScreenState.Traveling:
                nextTravelingRefreshTime = 0f;
                RefreshTravelingView();
                break;
            case InGameScreenState.Settlement:
            {
                FrameworkRoot root = FrameworkRoot.Instance;
                if (root?.SettlementUiBridge != null
                    && root.SettlementUiBridge.TryGetPendingSettlement(
                        out _,
                        out _,
                        out JourneyResultData pendingResult)
                    && pendingResult != null
                    && pendingResult.grade != JourneyResultGrade.Failed
                    && !root.SettlementUiBridge.IsSettlementPresentationRequested)
                {
                    // A successful arrival remains sale-pending. Only the sale completion flow
                    // may explicitly request settlement presentation.
                    CloseTradeScreen();
                    break;
                }

                // Keep the trade UI root alive and let its settlement adapter display S8.
                // The presenter only routes state; Framework still owns settlement and claim.
                view.ShowSettlement();
                break;
            }
            case InGameScreenState.Town:
            case InGameScreenState.Market:
                // Town owns its own market entry point. Never fall through to Preparation,
                // otherwise arrival would immediately reopen the cargo/departure flow.
                CloseTradeScreen();
                break;
            case InGameScreenState.Preparation:
            default:
                view.ShowPreparation();
                break;
        }
    }

    private void HandleTradeSettlementReady(
        string caravanId,
        string tradeId,
        JourneyResultData result)
    {
        // Failed journeys still use the existing settlement screen route.
        if (result == null || result.grade == JourneyResultGrade.Failed)
            return;

        FrameworkRoot root = FrameworkRoot.Instance;
        if (root?.CurrentSaveData == null
            || !string.Equals(
                root.CurrentSaveData.selectedCaravanId,
                caravanId,
                System.StringComparison.Ordinal))
        {
            return;
        }

        // SettlementPending is already stored by Framework. Closing this presentation leaves
        // the caravan status UI responsible only for rendering its sale-waiting action.
        CloseTradeScreen();
    }

    private void RefreshTravelingView()
    {
        FrameworkRoot root = FrameworkRoot.Instance;
        TradeProgressViewData progressViewData = TradeProgressViewDataBuilder.Build(
            root != null ? root.CurrentSaveData : null,
            root);
        view?.ShowTraveling(progressViewData);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (viewBehaviour != null && !(viewBehaviour is ITradeScreenView))
        {
            Debug.LogError(
                $"{viewBehaviour.GetType().Name} must implement {nameof(ITradeScreenView)}.",
                this);
        }
    }
#endif
}
