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
        if (tradeScreenPresenter == null)
        {
            Debug.LogError(
                "[Town Trade] Trade screen presenter is not connected.",
                this);
            return false;
        }

        ResolveRuntimeContext();
        if (runtimeContext == null)
        {
            Debug.LogWarning(
                "[Town Trade] Caravan selection data is not connected.",
                this);
            return false;
        }

        // The global screen state can represent another selected Caravan that is Traveling.
        // Refresh all options and let the selection panel enforce each Caravan's canSelect flag.
        runtimeContext.RefreshFromFramework();
        tradeScreenPresenter.OpenPreparationSelection();
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
