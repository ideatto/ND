using ND.Framework;
using UnityEngine;

/// <summary>Connects MainUI rename intent to the ID-scoped persistence service.</summary>
[DisallowMultipleComponent]
public sealed class CaravanOverviewRenameBinding : MonoBehaviour
{
    [SerializeField] private CaravanOverviewPresenter presenter;
    [SerializeField] private CaravanRenamePopupController popup;

    private void OnEnable()
    {
        if (presenter == null || popup == null)
        {
            Debug.LogError("Caravan rename binding references are not configured.", this);
            return;
        }
        presenter.RenameRequested -= Open;
        presenter.RenameRequested += Open;
    }

    private void OnDisable()
    {
        if (presenter != null) presenter.RenameRequested -= Open;
    }

    private void Open(string caravanId)
    {
        FrameworkRoot root = FrameworkRoot.Instance;
        if (root?.CurrentSaveData == null
            || !SaveDataLookup.TryGetCaravan(
                root.CurrentSaveData,
                caravanId,
                out ND.Framework.CaravanSaveData caravan)
            || caravan == null)
        {
            Debug.LogError($"Caravan rename failed to open. CaravanId={caravanId}", this);
            return;
        }

        popup.Open(caravan.caravanId, caravan.displayName, Submit);
    }

    private void Submit(string caravanId, string displayName)
    {
        FrameworkRoot root = FrameworkRoot.Instance;
        var service = new CaravanRenameService(
            () => root != null ? root.CurrentSaveData : null,
            root != null ? root.SaveService : null);
        CaravanRenameResult result = service.Execute(caravanId, displayName);
        if (!result.Succeeded)
        {
            popup?.ShowError(result.Error);
            return;
        }

        popup?.gameObject.SetActive(false);
        presenter?.Refresh();
    }
}
