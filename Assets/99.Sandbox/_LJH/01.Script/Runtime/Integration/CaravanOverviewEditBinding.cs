using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Identifies which detached Caravan edit panel was requested from Overview.</summary>
public enum CaravanOverviewEditTarget
{
    None,
    Setting,
    Cargo
}

/// <summary>Exposes a Caravan ID through the Unity Inspector without losing its string payload.</summary>
[Serializable]
public sealed class CaravanIdUnityEvent : UnityEvent<string>
{
}

/// <summary>
/// Bridges Caravan Overview button intent to detached S3/S4 entry points and S3 services.
/// </summary>
/// <remarks>
/// Provider and Command dependencies remain behind UI-owned interfaces so a temporary implementation
/// can be replaced by Framework without changing the Overview Presenter or S3 panel.
/// </remarks>
[DisallowMultipleComponent]
public sealed class CaravanOverviewEditBinding : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Presenter that forwards SettingRequested and CargoRequested with the selected caravanId.")]
    [SerializeField] private CaravanOverviewPresenter overviewPresenter;

    [Header("Detached Trade Panels")]
    [Tooltip("Receives public S3/S4 open requests and publishes detached edit events.")]
    [SerializeField] private TradePrepareUIManager tradePrepareUi;

    [Header("S3 Services")]
    [Tooltip("Assign a MonoBehaviour implementing ICaravanSettingViewDataProvider.")]
    [SerializeField] private MonoBehaviour settingProviderBehaviour;

    [Tooltip("Assign a MonoBehaviour implementing ICaravanSettingCommand.")]
    [SerializeField] private MonoBehaviour settingCommandBehaviour;

    [Tooltip("Displays Provider and Command failures while keeping invalid edits uncommitted.")]
    [SerializeField] private NoticeUI noticeUI;

    [Header("Scene Entry Points")]
    [Tooltip("Assign the future public S3 entry point. The selected caravanId is passed as its argument.")]
    [SerializeField] private CaravanIdUnityEvent settingOpenRequested = new CaravanIdUnityEvent();

    [Tooltip("Assign the future public S4 entry point. The selected caravanId is passed as its argument.")]
    [SerializeField] private CaravanIdUnityEvent cargoOpenRequested = new CaravanIdUnityEvent();

    public string CurrentEditCaravanId { get; private set; } = string.Empty;
    public CaravanOverviewEditTarget CurrentEditTarget { get; private set; }

    private ICaravanSettingViewDataProvider settingProvider;
    private ICaravanSettingCommand settingCommand;
    private bool hasRuntimeServiceOverride;

    private void OnEnable()
    {
        ResolveSceneReferences();

        if (!hasRuntimeServiceOverride)
        {
            ResolveSerializedServices();
        }

        if (overviewPresenter == null)
        {
            Debug.LogError(
                $"{nameof(CaravanOverviewEditBinding)} requires a {nameof(CaravanOverviewPresenter)} reference.",
                this);
        }
        else
        {
            // Lifecycle-scoped subscriptions prevent duplicate requests after this object is reopened.
            overviewPresenter.SettingRequested += HandleSettingRequested;
            overviewPresenter.CargoRequested += HandleCargoRequested;
        }

        if (tradePrepareUi == null)
        {
            Debug.LogError(
                $"{nameof(CaravanOverviewEditBinding)} requires a {nameof(TradePrepareUIManager)} reference.",
                this);
        }
        else
        {
            tradePrepareUi.OnCaravanSettingDataRequested += HandleSettingDataRequested;
            tradePrepareUi.OnCaravanSettingConfirmRequested += HandleSettingConfirmRequested;
            tradePrepareUi.OnCaravanEditClosed += HandleEditClosed;
        }
    }

    private void OnDisable()
    {
        if (overviewPresenter != null)
        {
            overviewPresenter.SettingRequested -= HandleSettingRequested;
            overviewPresenter.CargoRequested -= HandleCargoRequested;
        }

        if (tradePrepareUi != null)
        {
            tradePrepareUi.OnCaravanSettingDataRequested -= HandleSettingDataRequested;
            tradePrepareUi.OnCaravanSettingConfirmRequested -= HandleSettingConfirmRequested;
            tradePrepareUi.OnCaravanEditClosed -= HandleEditClosed;
        }

        ClearCurrentEdit();
    }

    /// <summary>Clears only UI routing state after a detached edit panel closes.</summary>
    public void ClearCurrentEdit()
    {
        CurrentEditCaravanId = string.Empty;
        CurrentEditTarget = CaravanOverviewEditTarget.None;
    }

    /// <summary>Supports composition-root injection when production services are not scene components.</summary>
    public void SetSettingServices(
        ICaravanSettingViewDataProvider runtimeProvider,
        ICaravanSettingCommand runtimeCommand)
    {
        // Explicit null values intentionally keep detached S3 fail-closed during incomplete composition.
        hasRuntimeServiceOverride = true;
        settingProvider = runtimeProvider;
        settingCommand = runtimeCommand;
    }

    /// <summary>Returns S3 service resolution to the Inspector-assigned MonoBehaviours.</summary>
    public void UseSerializedSettingServices()
    {
        hasRuntimeServiceOverride = false;
        ResolveSerializedServices();
    }

    private void HandleSettingRequested(string caravanId)
    {
        if (!TryBeginEdit(caravanId, CaravanOverviewEditTarget.Setting, out string normalizedCaravanId))
        {
            return;
        }

        if (tradePrepareUi != null)
        {
            // Direct code routing avoids losing caravanId in a parameterless Inspector button callback.
            tradePrepareUi.OpenCaravanSetting(normalizedCaravanId);
            return;
        }

        settingOpenRequested?.Invoke(normalizedCaravanId);
    }

    private void HandleCargoRequested(string caravanId)
    {
        if (!TryBeginEdit(caravanId, CaravanOverviewEditTarget.Cargo, out string normalizedCaravanId))
        {
            return;
        }

        if (tradePrepareUi != null)
        {
            // S4 keeps the same identity-safe entry path even though its Provider is implemented later.
            tradePrepareUi.OpenCaravanCargo(normalizedCaravanId);
            return;
        }

        cargoOpenRequested?.Invoke(normalizedCaravanId);
    }

    private bool TryBeginEdit(
        string caravanId,
        CaravanOverviewEditTarget target,
        out string normalizedCaravanId)
    {
        normalizedCaravanId = string.IsNullOrWhiteSpace(caravanId)
            ? string.Empty
            : caravanId.Trim();
        if (string.IsNullOrEmpty(normalizedCaravanId))
        {
            // An invalid identity must fail closed before any S3/S4 panel becomes visible.
            Debug.LogError(
                $"Cannot open Caravan {target} because caravanId is empty.",
                this);
            return false;
        }

        CurrentEditCaravanId = normalizedCaravanId;
        CurrentEditTarget = target;
        return true;
    }

    private void HandleSettingDataRequested(string caravanId)
    {
        if (settingProvider == null)
        {
            FailSettingRequest(
                CaravanSettingFailureCodes.ServiceUnavailable,
                "Caravan setting data is not connected.");
            return;
        }

        CaravanSettingViewData viewData;
        try
        {
            viewData = settingProvider.GetSetting(caravanId);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Caravan setting Provider failed: {exception}", this);
            FailSettingRequest(
                CaravanSettingFailureCodes.ServiceUnavailable,
                "Caravan setting data could not be loaded.");
            return;
        }

        if (viewData == null)
        {
            FailSettingRequest(
                CaravanSettingFailureCodes.CaravanNotFound,
                "The selected Caravan could not be found.");
            return;
        }

        if (tradePrepareUi == null || !tradePrepareUi.ShowCaravanSetting(viewData))
        {
            FailSettingRequest(
                CaravanSettingFailureCodes.InvalidDraft,
                "The selected Caravan setting data is invalid.");
        }
    }

    private void HandleSettingConfirmRequested(CaravanSettingDraft draft)
    {
        if (settingCommand == null)
        {
            ShowFailure(
                CaravanSettingFailureCodes.ServiceUnavailable,
                "Caravan setting changes cannot be saved yet.");
            return;
        }

        CaravanSettingCommandResult result;
        try
        {
            // A snapshot prevents asynchronous consumers from observing later UI-side Draft mutations.
            result = settingCommand.Execute(draft != null ? draft.CreateSnapshot() : null);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Caravan setting Command failed: {exception}", this);
            ShowFailure(
                CaravanSettingFailureCodes.SaveFailed,
                "Caravan setting changes could not be saved.");
            return;
        }

        if (result == null || !result.succeeded)
        {
            ShowFailure(
                result != null ? result.errorCode : CaravanSettingFailureCodes.SaveFailed,
                result != null ? result.userMessage : "Caravan setting changes could not be saved.");
            return;
        }

        // Only a successful Command may close S3 and refresh the Overview from authoritative data.
        tradePrepareUi?.CloseCaravanEdit();
        overviewPresenter?.Refresh();
    }

    private void HandleEditClosed()
    {
        ClearCurrentEdit();
    }

    private void FailSettingRequest(string errorCode, string userMessage)
    {
        ShowFailure(errorCode, userMessage);

        // A failed query owns no editable snapshot, so clear the pending manager identity immediately.
        tradePrepareUi?.CloseCaravanEdit();
        ClearCurrentEdit();
    }

    private void ShowFailure(string errorCode, string userMessage)
    {
        string safeCode = string.IsNullOrWhiteSpace(errorCode) ? "UNKNOWN" : errorCode.Trim();
        string safeMessage = string.IsNullOrWhiteSpace(userMessage)
            ? "The Caravan setting request failed."
            : userMessage.Trim();

        Debug.LogWarning($"[CaravanSetting] {safeCode} - {safeMessage}", this);
        noticeUI?.Show(safeMessage);
    }

    private void ResolveSerializedServices()
    {
        if (settingProviderBehaviour == null || settingCommandBehaviour == null)
        {
            // A scene connector may host one component that implements both temporary service contracts.
            MonoBehaviour[] localBehaviours = GetComponents<MonoBehaviour>();
            for (int index = 0; index < localBehaviours.Length; index++)
            {
                MonoBehaviour candidate = localBehaviours[index];
                if (settingProviderBehaviour == null && candidate is ICaravanSettingViewDataProvider)
                {
                    settingProviderBehaviour = candidate;
                }

                if (settingCommandBehaviour == null && candidate is ICaravanSettingCommand)
                {
                    settingCommandBehaviour = candidate;
                }
            }
        }

        settingProvider = settingProviderBehaviour as ICaravanSettingViewDataProvider;
        settingCommand = settingCommandBehaviour as ICaravanSettingCommand;

        if (settingProviderBehaviour != null && settingProvider == null)
        {
            Debug.LogError(
                $"{settingProviderBehaviour.GetType().Name} must implement {nameof(ICaravanSettingViewDataProvider)}.",
                this);
        }

        if (settingCommandBehaviour != null && settingCommand == null)
        {
            Debug.LogError(
                $"{settingCommandBehaviour.GetType().Name} must implement {nameof(ICaravanSettingCommand)}.",
                this);
        }
    }

    private void ResolveSceneReferences()
    {
        // The connector lives outside nested UI Prefabs, so it resolves scene instances after Prefab expansion.
        if (overviewPresenter == null)
        {
            overviewPresenter = FindFirstObjectByType<CaravanOverviewPresenter>(FindObjectsInactive.Include);
        }

        if (tradePrepareUi == null)
        {
            tradePrepareUi = FindFirstObjectByType<TradePrepareUIManager>(FindObjectsInactive.Include);
        }

        if (noticeUI == null)
        {
            noticeUI = FindFirstObjectByType<NoticeUI>(FindObjectsInactive.Include);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        // Most scenes keep the Presenter and its edit Binding on the same Overview object.
        overviewPresenter = GetComponent<CaravanOverviewPresenter>();
        ResolveSceneReferences();
    }

    private void OnValidate()
    {
        if (overviewPresenter == null)
        {
            overviewPresenter = GetComponent<CaravanOverviewPresenter>();
        }

        ResolveSceneReferences();

        if (settingProviderBehaviour != null
            && !(settingProviderBehaviour is ICaravanSettingViewDataProvider))
        {
            Debug.LogError(
                $"{settingProviderBehaviour.GetType().Name} must implement {nameof(ICaravanSettingViewDataProvider)}.",
                this);
        }

        if (settingCommandBehaviour != null && !(settingCommandBehaviour is ICaravanSettingCommand))
        {
            Debug.LogError(
                $"{settingCommandBehaviour.GetType().Name} must implement {nameof(ICaravanSettingCommand)}.",
                this);
        }
    }
#endif
}
