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
    }

    private void OnDisable()
    {
        FrameworkEvents.InGameScreenChanged -= HandleScreenChanged;
        view = null;
    }

    private void Update()
    {
        if (view == null || currentScreenState != InGameScreenState.Traveling)
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
        currentScreenState = state;
        if (view == null || !isTradeScreenOpen)
        {
            return;
        }

        switch (state)
        {
            case InGameScreenState.Traveling:
                nextTravelingRefreshTime = 0f;
                RefreshTravelingView();
                break;
            case InGameScreenState.Settlement:
                view.HideTradeScreens();
                break;
            case InGameScreenState.Preparation:
            default:
                view.ShowPreparation();
                break;
        }
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
