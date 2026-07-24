using ND.Framework;
using UnityEngine;

/// <summary>
/// Town-screen entry point for starting a new trade-preparation flow.
/// Market UI closes independently; this component owns only the explicit
/// Town -> Preparation transition and opens the flow at TownRoutePanel.
/// </summary>
public sealed class TownTradePreparationEntryController : MonoBehaviour
{
    [SerializeField] private FrameworkTradeScreenPresenter tradeScreenPresenter;
    [SerializeField] private TradePrepareRuntimeContextProvider runtimeContext;

    public void Configure(FrameworkTradeScreenPresenter presenter)
    {
        tradeScreenPresenter = presenter;
    }

    public void Configure(
        FrameworkTradeScreenPresenter presenter,
        TradePrepareRuntimeContextProvider context)
    {
        tradeScreenPresenter = presenter;
        runtimeContext = context;
    }

    /// <summary>Unity Button entry point.</summary>
    public void OnClickBeginTradePreparation()
    {
        TryBeginTradePreparation();
    }

    public bool TryBeginTradePreparation()
    {
        return TryOpenTradePreparationWithoutSelection();
    }

    public bool TryBeginTradePreparation(string caravanId)
    {
        return TryBeginTradePreparationInternal(caravanId, false);
    }

    private bool TryOpenTradePreparationWithoutSelection()
    {
        if (tradeScreenPresenter == null)
        {
            Debug.LogError("[Town Trade] Trade screen presenter is not connected.", this);
            return false;
        }

        FrameworkRoot root = FrameworkRoot.Instance;
        if (root == null || root.InGameScreenRouter == null)
            return false;

        InGameScreenState currentState = root.InGameScreenRouter.CurrentScreenState;
        if (currentState != InGameScreenState.Preparation
            && (currentState != InGameScreenState.Town
                || !root.TryBeginTradePreparationFromTown()))
        {
            return false;
        }

        tradeScreenPresenter.OpenTradeScreen();
        return true;
    }

    private bool TryBeginTradePreparationInternal(string caravanId, bool selectOnlyAvailable)
    {
        if (tradeScreenPresenter == null)
        {
            Debug.LogError(
                "[Town Trade] Trade screen presenter is not connected.",
                this);
            return false;
        }

        FrameworkRoot root = FrameworkRoot.Instance;
        if (root == null || root.InGameScreenRouter == null)
        {
            Debug.LogWarning(
                "[Town Trade] Trade preparation could not be started from the current state.",
                this);
            return false;
        }

        ResolveRuntimeContext();
        if (runtimeContext == null)
            return false;

        string selectedCaravanId = caravanId;
        bool canSelect = selectOnlyAvailable
            ? runtimeContext.TryGetOnlyAvailableDepartureCaravanId(out selectedCaravanId)
            : runtimeContext.CanSelectDepartureCaravan(selectedCaravanId);
        if (!canSelect)
        {
            Debug.LogWarning(
                selectOnlyAvailable
                    ? "[Town Trade] Exactly one selectable departure Caravan is required."
                    : $"[Town Trade] Caravan '{caravanId}' is not a selectable departure option.",
                this);
            return false;
        }

        InGameScreenState currentState = root.InGameScreenRouter.CurrentScreenState;
        if (currentState != InGameScreenState.Preparation
            && (currentState != InGameScreenState.Town
                || !root.TryBeginTradePreparationFromTown()))
        {
            Debug.LogWarning(
                "[Town Trade] Trade preparation could not be started from the current state.",
                this);
            return false;
        }

        // Entering Preparation initializes a fresh Runtime Draft. Select the Caravan only after
        // that transition, otherwise HandleScreenChanged would immediately erase the selected ID.
        bool selected = runtimeContext.SelectDepartureCaravan(selectedCaravanId);
        if (!selected)
        {
            Debug.LogWarning(
                selectOnlyAvailable
                    ? "[Town Trade] Exactly one selectable departure Caravan is required."
                    : $"[Town Trade] Caravan '{caravanId}' is not a selectable departure option.",
                this);
            return false;
        }

        tradeScreenPresenter.OpenTradeScreen();
        return true;
    }

    private void ResolveRuntimeContext()
    {
        if (runtimeContext == null)
        {
            runtimeContext = Object.FindAnyObjectByType<TradePrepareRuntimeContextProvider>(
                FindObjectsInactive.Include);
        }
    }

    public static bool CanBeginFromScreen(InGameScreenState screenState)
    {
        return screenState == InGameScreenState.Town ||
               screenState == InGameScreenState.Preparation;
    }
}
