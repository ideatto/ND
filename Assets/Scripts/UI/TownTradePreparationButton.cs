using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Button adapter for reopening the existing trade-preparation flow from Town.
/// Attach this component to a Unity UI Button; it delegates state validation and
/// screen opening to TownTradePreparationEntryController.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class TownTradePreparationButton : MonoBehaviour
{
    [SerializeField] private TownTradePreparationEntryController entryController;
    [SerializeField] private FrameworkTradeScreenPresenter tradeScreenPresenter;

    private Button button;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (button != null)
            button.onClick.AddListener(HandleClick);
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    private void ResolveReferences()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (entryController == null)
            entryController = GetComponent<TownTradePreparationEntryController>();
        if (entryController == null)
            entryController = gameObject.AddComponent<TownTradePreparationEntryController>();

        if (tradeScreenPresenter == null)
        {
            tradeScreenPresenter = Object.FindAnyObjectByType<FrameworkTradeScreenPresenter>(
                FindObjectsInactive.Include);
        }

        if (entryController != null && tradeScreenPresenter != null)
            entryController.Configure(tradeScreenPresenter);
    }

    private void HandleClick()
    {
        ResolveReferences();
        if (entryController == null || !entryController.TryBeginTradePreparation())
        {
            Debug.LogWarning(
                "[Town Trade] Begin-trade button could not open the preparation screen.",
                this);
        }
    }
}
