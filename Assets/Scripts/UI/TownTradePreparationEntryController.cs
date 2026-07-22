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

    public void Configure(FrameworkTradeScreenPresenter presenter)
    {
        tradeScreenPresenter = presenter;
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

        FrameworkRoot root = FrameworkRoot.Instance;
        if (root == null || root.InGameScreenRouter == null ||
            !CanBeginFromScreen(root.InGameScreenRouter.CurrentScreenState) ||
            !root.TryBeginTradePreparationFromTown())
        {
            Debug.LogWarning(
                "[Town Trade] Trade preparation could not be started from the current state.",
                this);
            return false;
        }

        tradeScreenPresenter.OpenTradeScreen();
        return true;
    }

    public static bool CanBeginFromScreen(InGameScreenState screenState)
    {
        return screenState == InGameScreenState.Town;
    }
}
