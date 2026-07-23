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
    [Tooltip("Displays JourneyState without acting as a button. Travel progress interaction belongs to the World Map UI.")]
    [SerializeField] private GameObject journeyStateDisplay;
    [SerializeField] private TMP_Text journeyStateText;

    [Header("Journey State Icons")]
    [Tooltip("Optional icon target. When the matching Sprite is missing, journeyStateText remains visible as a fallback.")]
    [SerializeField] private Image journeyStateIconImage;
    [SerializeField] private Sprite prepareStateIcon;
    [SerializeField] private Sprite travelingStateIcon;
    [SerializeField] private Sprite settlingStateIcon;
    [SerializeField] private Sprite completedStateIcon;
    [Tooltip("Optional Animator on the state icon. Its controller should expose the configured bool parameter.")]
    [SerializeField] private Animator journeyStateIconAnimator;
    [SerializeField] private string travelingAnimatorParameter = "IsTraveling";

    [Header("Action Icons")]
    [Tooltip("Optional child Image placed inside the Set button.")]
    [SerializeField] private Image settingButtonIconImage;
    [SerializeField] private Sprite settingButtonIcon;
    [Tooltip("Optional child Image placed inside the Cargo button.")]
    [SerializeField] private Image cargoButtonIconImage;
    [SerializeField] private Sprite cargoLoadButtonIcon;
    [Tooltip("Reserved for the future arrival-sale action. It is not selected until Framework exposes a distinct sale-pending state.")]
    [SerializeField] private Sprite cargoSellButtonIcon;
    [Tooltip("Optional label hidden only when a valid Set icon is available.")]
    [SerializeField] private TMP_Text settingButtonText;
    [Tooltip("Optional label hidden only when a valid Cargo icon is available.")]
    [SerializeField] private TMP_Text cargoButtonText;

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
    private bool canRequestSetting;
    private bool canRequestCargo;

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
        SetTravelingAnimation(false);
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

        SetDisplayNameVisible(true);
        SetText(displayNameText, unknownSlotLabel);
        SetText(journeyStateText, "-");
        ClearJourneyStateIcon();
        ApplyActionIcons();
        SetOccupiedControlsVisible(false);
        SetButtonsInteractable(false, false);
        SetCreateButtonVisible(false);
        SetLockOverlayVisible(true, false);
    }

    private void ShowOccupied(CaravanBlockViewData data)
    {
        string displayName = string.IsNullOrWhiteSpace(data.displayName)
            ? $"Caravan {slotIndex + 1}"
            : data.displayName;

        SetDisplayNameVisible(true);
        SetText(displayNameText, displayName);
        ApplyJourneyStatePresentation(data.state);
        ApplyActionIcons();
        currentUnlockHintText = string.Empty;
        isCreatePending = false;

        // Until Framework exposes a distinct arrival-sale state, only Prepare permits editing.
        // Traveling, Settling, and Completed remain display-only to avoid mutating an active run.
        bool hasValidIdentity = !string.IsNullOrWhiteSpace(currentCaravanId);
        bool canEdit = hasValidIdentity && data.state == JourneyState.Prepare;
        SetOccupiedControlsVisible(true);
        SetButtonsInteractable(canEdit, canEdit);
        SetCreateButtonVisible(false);
        SetLockOverlayVisible(false, false);
    }

    private void ShowEmpty()
    {
        SetDisplayNameVisible(true);
        SetText(displayNameText, emptySlotLabel);
        SetText(journeyStateText, "-");
        ClearJourneyStateIcon();
        ApplyActionIcons();
        currentUnlockHintText = string.Empty;
        isCreatePending = false;

        // UI raises slotIndex only. Framework owns Caravan creation, ID generation, and persistence.
        // Hiding the occupied-only actions lets CaravanInfo and CreateButton fill the entire row.
        SetOccupiedControlsVisible(false);
        SetButtonsInteractable(false, false);
        SetCreateButtonVisible(true);
        SetLockOverlayVisible(false, false);
    }

    private void ShowLocked(string unlockHintText)
    {
        // The lock icon communicates this state by itself. Hide Empty-slot labels and
        // actions underneath the overlay so they do not compete with the icon.
        SetDisplayNameVisible(false);
        SetText(journeyStateText, "-");
        ClearJourneyStateIcon();
        ApplyActionIcons();
        currentUnlockHintText = string.IsNullOrWhiteSpace(unlockHintText)
            ? defaultUnlockHintText
            : unlockHintText;
        isCreatePending = false;
        SetOccupiedControlsVisible(false);
        SetButtonsInteractable(false, false);
        SetCreateButtonVisible(false);

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

        // Empty and pending slots do not expose occupied-only actions or JourneyState presentation.
        SetOccupiedControlsVisible(false);
        SetButtonsInteractable(false, false);
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
        canRequestSetting = canOpenSetting;
        canRequestCargo = canOpenCargo;

        if (settingButton != null)
        {
            settingButton.interactable = canOpenSetting;
        }

        if (cargoButton != null)
        {
            cargoButton.interactable = canOpenCargo;
        }
    }

    private void ApplyJourneyStatePresentation(JourneyState state)
    {
        SetText(journeyStateText, state.ToString());

        Sprite stateIcon = ResolveJourneyStateIcon(state);
        bool canShowIcon = journeyStateIconImage != null && stateIcon != null;

        if (journeyStateIconImage != null)
        {
            journeyStateIconImage.sprite = stateIcon;
            journeyStateIconImage.enabled = canShowIcon;
            journeyStateIconImage.preserveAspect = true;
        }

        if (journeyStateText != null)
        {
            journeyStateText.gameObject.SetActive(!canShowIcon);
        }

        SetTravelingAnimation(canShowIcon && state == JourneyState.Traveling);
    }

    private Sprite ResolveJourneyStateIcon(JourneyState state)
    {
        switch (state)
        {
            case JourneyState.Prepare:
                return prepareStateIcon;
            case JourneyState.Traveling:
                return travelingStateIcon;
            case JourneyState.Settling:
                return settlingStateIcon;
            case JourneyState.Completed:
                return completedStateIcon;
            default:
                return null;
        }
    }

    private void ClearJourneyStateIcon()
    {
        SetTravelingAnimation(false);
        if (journeyStateIconImage != null)
        {
            journeyStateIconImage.sprite = null;
            journeyStateIconImage.enabled = false;
        }

        if (journeyStateText != null)
        {
            journeyStateText.gameObject.SetActive(true);
        }

    }

    private void SetTravelingAnimation(bool isTraveling)
    {
        if (journeyStateIconAnimator == null
            || string.IsNullOrWhiteSpace(travelingAnimatorParameter))
        {
            return;
        }

        int parameterHash = Animator.StringToHash(travelingAnimatorParameter);
        foreach (AnimatorControllerParameter parameter in journeyStateIconAnimator.parameters)
        {
            if (parameter.nameHash == parameterHash
                && parameter.type == AnimatorControllerParameterType.Bool)
            {
                journeyStateIconAnimator.SetBool(parameterHash, isTraveling);
                return;
            }
        }
    }

    private void ApplyActionIcons()
    {
        ApplyOptionalButtonIcon(settingButtonIconImage, settingButtonText, settingButtonIcon);
        ApplyOptionalButtonIcon(cargoButtonIconImage, cargoButtonText, cargoLoadButtonIcon);
    }

    private static void ApplyOptionalButtonIcon(
        Image iconImage,
        TMP_Text fallbackText,
        Sprite icon)
    {
        bool canShowIcon = iconImage != null && icon != null;
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = canShowIcon;
            iconImage.preserveAspect = true;
        }

        if (fallbackText != null)
        {
            fallbackText.gameObject.SetActive(!canShowIcon);
        }
    }

    private void SetOccupiedControlsVisible(bool visible)
    {
        // Setting/Cargo remain actions, while JourneyState is display-only and never receives a click listener.
        // All three describe an existing Caravan and must not consume layout width for Empty slots.
        if (settingButton != null)
        {
            settingButton.gameObject.SetActive(visible);
        }

        if (cargoButton != null)
        {
            cargoButton.gameObject.SetActive(visible);
        }

        if (journeyStateDisplay != null)
        {
            journeyStateDisplay.SetActive(visible);
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

    private void SetDisplayNameVisible(bool visible)
    {
        if (displayNameText != null)
        {
            displayNameText.gameObject.SetActive(visible);
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
        if (canRequestSetting
            && currentState == CaravanSlotState.Occupied
            && !string.IsNullOrWhiteSpace(currentCaravanId))
        {
            SettingRequested?.Invoke(currentCaravanId);
        }
    }

    private void HandleCargoClicked()
    {
        if (canRequestCargo
            && currentState == CaravanSlotState.Occupied
            && !string.IsNullOrWhiteSpace(currentCaravanId))
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
