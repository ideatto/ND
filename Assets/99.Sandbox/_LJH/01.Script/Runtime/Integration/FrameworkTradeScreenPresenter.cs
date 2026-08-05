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
    private string presentedCaravanId = string.Empty;
    private string presentedTradeId = string.Empty;

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

    /// <summary>
    /// Opens the preparation UI at Caravan selection without deriving the view from the
    /// globally selected Caravan. Each Caravan option remains responsible for its own
    /// availability check.
    /// </summary>
    public void OpenPreparationSelection()
    {
        ClearPresentedTrade();
        isTradeScreenOpen = true;
        currentScreenState = InGameScreenState.Preparation;
        view?.ShowPreparation();
    }

    /// <summary>
    /// Opens the settlement presentation that Framework has already validated and requested.
    /// This path intentionally avoids remapping the screen from selectedCaravanId because the
    /// settled Caravan can differ from the currently selected Caravan.
    /// </summary>
    public void OpenSettlementScreen()
    {
        isTradeScreenOpen = true;
        currentScreenState = InGameScreenState.Settlement;
        view?.ShowSettlement();
    }

    /// <summary>
    /// Opens S7 for one explicit trade. Framework owns progress; this presenter only keeps
    /// the identity required to avoid showing another Caravan's mirrored legacy state.
    /// </summary>
    public void OpenTravelingScreen(string caravanId, string tradeId)
    {
        if (string.IsNullOrWhiteSpace(caravanId) || string.IsNullOrWhiteSpace(tradeId))
            return;

        presentedCaravanId = caravanId.Trim();
        presentedTradeId = tradeId.Trim();
        isTradeScreenOpen = true;
        currentScreenState = InGameScreenState.Traveling;
        nextTravelingRefreshTime = 0f;
        RefreshTravelingView();
    }


    /// <summary>Closes the trade UI without changing Framework trade state.</summary>
    public void CloseTradeScreen()
    {
        isTradeScreenOpen = false;
        ClearPresentedTrade();
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
            SettlementUiBridge settlementUiBridge = root?.SettlementUiBridge;
            bool hasFailedSettlement = settlementUiBridge != null
                && settlementUiBridge.TryGetPendingSettlement(
                    out _,
                    out _,
                    out JourneyResultData pendingResult)
                && pendingResult != null
                && pendingResult.grade == JourneyResultGrade.Failed;
            if (state == InGameScreenState.Settlement
                && settlementUiBridge != null
                && (settlementUiBridge.IsSettlementPresentationRequested || hasFailedSettlement))
            {
                // Arrival closes the traveling presentation while it waits for the player to
                // sell cargo. A successful sale explicitly requests settlement afterwards.
                // Failed travel has no sale step, so its pending result must reopen settlement
                // even when the traveling presentation was already closed.
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
        if (result == null)
            return;

        if (result.grade == JourneyResultGrade.Failed)
        {
            // Failure has no destination sale step and belongs to the event Caravan, not the
            // globally focused Caravan. Open S8 directly even when another Caravan is selected.
            OpenSettlementScreen();
            return;
        }

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
        TradeProgressViewData progressViewData =
            !string.IsNullOrEmpty(presentedCaravanId) && !string.IsNullOrEmpty(presentedTradeId)
                ? TradeProgressViewDataBuilder.Build(
                    root != null ? root.CurrentSaveData : null,
                    root,
                    presentedCaravanId,
                    presentedTradeId)
                : TradeProgressViewDataBuilder.Build(
                    root != null ? root.CurrentSaveData : null,
                    root);
        view?.ShowTraveling(progressViewData);
    }

    private void ClearPresentedTrade()
    {
        presentedCaravanId = string.Empty;
        presentedTradeId = string.Empty;
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
