using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders one fixed Caravan Overview slot without reading SaveData or evaluating unlock rules.
/// The Provider owns slot state; this View only applies the supplied presentation snapshot.
/// </summary>
[DisallowMultipleComponent]
public sealed class CaravanSlotView : MonoBehaviour
{
    [Header("Slot Identity")]
    [SerializeField, Min(0)] private int slotIndex;

    [Header("Main Content")]
    [SerializeField] private TMP_Text displayNameText;
    [SerializeField] private Button settingButton;
    [SerializeField] private Button cargoButton;
    [SerializeField] private Button journeyButton;
    [SerializeField] private TMP_Text journeyStateText;

    [Header("Empty State")]
    [Tooltip("A dedicated action shown only for an unlocked Empty slot. It requests creation without generating an ID in UI code.")]
    [SerializeField] private Button createButton;
    [SerializeField] private TMP_Text createButtonLabel;
    [SerializeField] private string createIdleLabel = "Create Caravan";
    [SerializeField] private string createPendingLabel = "Creating...";

    [Header("Locked State")]
    [Tooltip("Place this overlay last so it darkens the slot and blocks input below it.")]
    [SerializeField] private GameObject lockOverlay;
    [Tooltip("The overlay remains clickable only for a valid Locked slot so it can show the unlock hint.")]
    [SerializeField] private Button lockOverlayButton;

    [Header("Fallback Labels")]
    [SerializeField] private string emptySlotLabel = "Empty Caravan Slot";
    [SerializeField] private string unknownSlotLabel = "Caravan data unavailable";
    [SerializeField] private string defaultUnlockHintText = "Complete the unlock requirement first.";

    private CaravanSlotState currentState = CaravanSlotState.Unknown;
    private string currentCaravanId = string.Empty;
    private string currentUnlockHintText = string.Empty;
    private bool isCreatePending;

    public int SlotIndex => slotIndex;
    public bool IsCreatePending => isCreatePending;

    // These events expose UI intent without coupling the View to Framework commands.
    public event Action<string> SettingRequested;
    public event Action<string> CargoRequested;
    public event Action<int> CreateRequested;
    public event Action<string> UnlockHintRequested;

    private void OnEnable()
    {
        // Re-register after prefab/scene activation so newly assigned button references are never skipped.
        RegisterButtonListeners();
    }

    private void OnDisable()
    {
        UnregisterButtonListeners();
    }

    /// <summary>Applies one Provider-owned slot snapshot.</summary>
    public void Bind(CaravanBlockViewData data)
    {
        if (data == null)
        {
            ShowUnknown();
            return;
        }

        currentState = data.slotState;
        currentCaravanId = data.slotState == CaravanSlotState.Occupied
            ? data.caravanId ?? string.Empty
            : string.Empty;

        switch (data.slotState)
        {
            case CaravanSlotState.Occupied:
                ShowOccupied(data);
                break;
            case CaravanSlotState.Empty:
                ShowEmpty();
                break;
            case CaravanSlotState.Locked:
                ShowLocked(data.unlockHintText);
                break;
            case CaravanSlotState.Unknown:
            default:
                ShowUnknown();
                break;
        }
    }

    /// <summary>
    /// Uses a fail-closed visual before a Provider supplies valid data.
    /// This prevents placeholder slots from appearing unlocked or accepting input.
    /// </summary>
    public void ShowUnknown()
    {
        currentState = CaravanSlotState.Unknown;
        currentCaravanId = string.Empty;
        currentUnlockHintText = string.Empty;
        isCreatePending = false;

        SetText(displayNameText, unknownSlotLabel);
        SetText(journeyStateText, "-");
        SetActionButtonsVisible(false);
        SetButtonsInteractable(false, false);
        SetJourneyButtonInteractable(false);
        SetCreateButtonVisible(false);
        SetLockOverlayVisible(true, false);
    }

    private void ShowOccupied(CaravanBlockViewData data)
    {
        string displayName = string.IsNullOrWhiteSpace(data.displayName)
            ? $"Caravan {slotIndex + 1}"
            : data.displayName;

        SetText(displayNameText, displayName);
        SetText(journeyStateText, data.state.ToString());
        currentUnlockHintText = string.Empty;
        isCreatePending = false;

        // Setting and Cargo panels decide whether their inner controls are editable.
        // Overview buttons remain available so a traveling Caravan can still be inspected.
        bool hasValidIdentity = !string.IsNullOrWhiteSpace(currentCaravanId);
        SetActionButtonsVisible(true);
        SetButtonsInteractable(hasValidIdentity, hasValidIdentity);
        SetJourneyButtonInteractable(hasValidIdentity);
        SetCreateButtonVisible(false);
        SetLockOverlayVisible(false, false);
    }

    private void ShowEmpty()
    {
        SetText(displayNameText, emptySlotLabel);
        SetText(journeyStateText, "-");
        currentUnlockHintText = string.Empty;
        isCreatePending = false;

        // UI raises slotIndex only. Framework owns Caravan creation, ID generation, and persistence.
        // Hiding the occupied-only actions lets CaravanInfo and CreateButton fill the entire row.
        SetActionButtonsVisible(false);
        SetButtonsInteractable(false, false);
        SetJourneyButtonInteractable(false);
        SetCreateButtonVisible(true);
        SetLockOverlayVisible(false, false);
    }

    private void ShowLocked(string unlockHintText)
    {
        // Locked is an unavailable Empty slot: keep the Empty base visible beneath the filter.
        SetText(displayNameText, emptySlotLabel);
        SetText(journeyStateText, "-");
        currentUnlockHintText = string.IsNullOrWhiteSpace(unlockHintText)
            ? defaultUnlockHintText
            : unlockHintText;
        isCreatePending = false;
        SetActionButtonsVisible(false);
        SetButtonsInteractable(false, false);
        SetJourneyButtonInteractable(false);
        SetCreateButtonVisible(true);
        if (createButton != null)
        {
            createButton.interactable = false;
        }

        SetLockOverlayVisible(true, true);
    }

    /// <summary>
    /// Applies the local in-flight state while Framework creates an Empty-slot Caravan.
    /// This state never changes Provider data or assumes that creation succeeded.
    /// </summary>
    public void SetCreatePending(bool pending)
    {
        if (currentState != CaravanSlotState.Empty)
        {
            isCreatePending = false;
            return;
        }

        isCreatePending = pending;

        // Empty and pending slots cannot open configuration or journey actions.
        SetActionButtonsVisible(false);
        SetButtonsInteractable(false, false);
        SetJourneyButtonInteractable(false);
        SetCreateButtonVisible(true);

        if (createButton != null)
        {
            createButton.interactable = !pending;
        }

        SetText(createButtonLabel, pending ? createPendingLabel : createIdleLabel);
    }

    private void SetCreateButtonVisible(bool visible)
    {
        if (createButton == null)
        {
            return;
        }

        createButton.gameObject.SetActive(visible);
        createButton.interactable = visible;

        if (visible && !isCreatePending)
        {
            SetText(createButtonLabel, createIdleLabel);
        }

        if (visible)
        {
            // Keep the dedicated action above the display-only route/name text inside CaravanInfo.
            createButton.transform.SetAsLastSibling();
        }
    }

    private void SetButtonsInteractable(
        bool canOpenSetting,
        bool canOpenCargo)
    {
        if (settingButton != null)
        {
            settingButton.interactable = canOpenSetting;
        }

        if (cargoButton != null)
        {
            cargoButton.interactable = canOpenCargo;
        }
    }

    private void SetActionButtonsVisible(bool visible)
    {
        // These controls describe an existing Caravan and must not consume layout width for Empty slots.
        if (settingButton != null)
        {
            settingButton.gameObject.SetActive(visible);
        }

        if (cargoButton != null)
        {
            cargoButton.gameObject.SetActive(visible);
        }

        if (journeyButton != null)
        {
            journeyButton.gameObject.SetActive(visible);
        }
    }

    private void SetJourneyButtonInteractable(bool interactable)
    {
        if (journeyButton != null)
        {
            journeyButton.interactable = interactable;
        }
    }

    private void SetLockOverlayVisible(bool visible, bool canShowUnlockHint)
    {
        if (lockOverlay == null)
        {
            return;
        }

        lockOverlay.SetActive(visible);
        if (lockOverlayButton != null)
        {
            lockOverlayButton.interactable = visible && canShowUnlockHint;
        }

        if (visible)
        {
            // The overlay must remain above every interactive child after scene or prefab reordering.
            lockOverlay.transform.SetAsLastSibling();
        }
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value ?? string.Empty;
        }
    }

    private void RegisterButtonListeners()
    {
        settingButton?.onClick.AddListener(HandleSettingClicked);
        cargoButton?.onClick.AddListener(HandleCargoClicked);
        createButton?.onClick.AddListener(HandleCreateClicked);
        lockOverlayButton?.onClick.AddListener(HandleLockOverlayClicked);
    }

    private void UnregisterButtonListeners()
    {
        settingButton?.onClick.RemoveListener(HandleSettingClicked);
        cargoButton?.onClick.RemoveListener(HandleCargoClicked);
        createButton?.onClick.RemoveListener(HandleCreateClicked);
        lockOverlayButton?.onClick.RemoveListener(HandleLockOverlayClicked);
    }

    private void HandleCreateClicked()
    {
        if (currentState == CaravanSlotState.Empty && !isCreatePending)
        {
            // Lock the Empty slot immediately so repeated clicks cannot enqueue duplicate creation requests.
            SetCreatePending(true);

            // The command consumer must return a Framework-created Caravan and then refresh the Provider.
            CreateRequested?.Invoke(slotIndex);
        }
    }

    private void HandleLockOverlayClicked()
    {
        if (currentState == CaravanSlotState.Locked
            && !string.IsNullOrWhiteSpace(currentUnlockHintText))
        {
            // Locked-slot clicks provide guidance only and never attempt to mutate unlock state.
            UnlockHintRequested?.Invoke(currentUnlockHintText);
        }
    }

    private void HandleSettingClicked()
    {
        if (currentState == CaravanSlotState.Occupied && !string.IsNullOrWhiteSpace(currentCaravanId))
        {
            SettingRequested?.Invoke(currentCaravanId);
        }
    }

    private void HandleCargoClicked()
    {
        if (currentState == CaravanSlotState.Occupied && !string.IsNullOrWhiteSpace(currentCaravanId))
        {
            CargoRequested?.Invoke(currentCaravanId);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (lockOverlay != null && lockOverlay.transform.parent != transform)
        {
            Debug.LogWarning(
                $"{nameof(CaravanSlotView)} lockOverlay should be a direct child so it can cover the entire slot.",
                this);
        }
    }
#endif
}
