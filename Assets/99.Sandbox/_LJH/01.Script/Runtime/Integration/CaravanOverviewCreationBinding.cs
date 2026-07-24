using System;
using ND.Framework;
using UnityEngine;

/// <summary>
/// Routes an empty Caravan slot's create intent to the Framework composition root.
/// </summary>
/// <remarks>
/// This UI-owned binding never generates a caravanId or mutates SaveData. Framework's
/// CaravanManagementService remains the single owner of identity, persistence, and rollback.
/// </remarks>
[DisallowMultipleComponent]
public sealed class CaravanOverviewCreationBinding : MonoBehaviour
{
    [Tooltip("Presenter that forwards an empty slot's CreateRequested intent.")]
    [SerializeField] private CaravanOverviewPresenter overviewPresenter;

    [Tooltip("Displays Framework command failures without changing saved state.")]
    [SerializeField] private NoticeUI noticeUI;

    private void OnEnable()
    {
        ResolveSceneReferences();
        if (overviewPresenter == null)
        {
            Debug.LogError(
                $"{nameof(CaravanOverviewCreationBinding)} requires a "
                    + $"{nameof(CaravanOverviewPresenter)} reference.",
                this);
            return;
        }

        overviewPresenter.CreateRequested += HandleCreateRequested;
    }

    private void OnDisable()
    {
        if (overviewPresenter != null)
            overviewPresenter.CreateRequested -= HandleCreateRequested;
    }

    private void HandleCreateRequested(int slotIndex)
    {
        try
        {
            FrameworkRoot frameworkRoot = FrameworkRoot.Instance;
            if (frameworkRoot == null || frameworkRoot.CaravanManagement == null)
            {
                ShowFailure("Caravan data is not ready yet.");
                return;
            }

            // Do not construct IDs or edit SaveData here. The Framework command performs the
            // complete transactional create/save/runtime-registration operation.
            CaravanCreationResult result =
                frameworkRoot.CaravanManagement.CreateCaravan(slotIndex);
            if (!result.Succeeded)
            {
                ShowFailure(GetFailureMessage(result.FailureReason));
                return;
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"Caravan creation request failed: {exception}", this);
            ShowFailure("The Caravan could not be created.");
        }
        finally
        {
            // A Provider refresh projects the newly persisted slot. Clearing pending in finally
            // also restores the create button after every validation, save, or unexpected failure.
            overviewPresenter.SetCreatePending(slotIndex, false);
            overviewPresenter.Refresh();
        }
    }

    private void ShowFailure(string message)
    {
        Debug.LogWarning($"[CaravanCreation] {message}", this);
        noticeUI?.Show(message);
    }

    private static string GetFailureMessage(CaravanCreationFailureReason failureReason)
    {
        switch (failureReason)
        {
            case CaravanCreationFailureReason.SaveDataUnavailable:
                return "Caravan data is not ready yet.";
            case CaravanCreationFailureReason.InvalidSlotIndex:
                return "This Caravan slot is invalid.";
            case CaravanCreationFailureReason.SlotAlreadyOccupied:
                return "This Caravan slot is already occupied.";
            case CaravanCreationFailureReason.SlotLocked:
                return "This Caravan slot is locked.";
            case CaravanCreationFailureReason.SaveFailed:
                return "The Caravan could not be saved.";
            default:
                return "The Caravan could not be created.";
        }
    }

    private void ResolveSceneReferences()
    {
        // This connector lives outside nested UI Prefabs and resolves their scene instances.
        if (overviewPresenter == null)
        {
            overviewPresenter =
                FindFirstObjectByType<CaravanOverviewPresenter>(FindObjectsInactive.Include);
        }

        if (noticeUI == null)
            noticeUI = FindFirstObjectByType<NoticeUI>(FindObjectsInactive.Include);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveSceneReferences();
    }

    private void OnValidate()
    {
        ResolveSceneReferences();
    }
#endif
}
