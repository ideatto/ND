using System.Collections;
using ND.Framework;
using UnityEngine;

public sealed class BakeryProductionPopupPresenter : MonoBehaviour
{
    [SerializeField] private BakeryProductionPopupView view;
    [SerializeField] private NoticeUI noticeUI;
    private Coroutine refreshRoutine;
    private int selectedQuantity = 1;

    public void Configure(BakeryProductionPopupView popupView, NoticeUI notice)
    { view = popupView; noticeUI = notice; }

    private void OnEnable()
    {
        if (view != null)
        {
            view.PartialReceiveRequested += OpenQuantityModal;
            view.ReceiveAllRequested += ReceiveAll;
            view.QuantityMinRequested += SelectMinimum;
            view.QuantityMinusRequested += DecreaseQuantity;
            view.QuantityPlusRequested += IncreaseQuantity;
            view.QuantityMaxRequested += SelectMaximum;
            view.QuantitySliderChanged += SetSelectedQuantity;
            view.QuantityConfirmRequested += ConfirmPartialReceive;
        }
        FrameworkEvents.BakeryProductionChanged += Refresh;
        FrameworkEvents.HomeInventoryChanged += Refresh;
        refreshRoutine = StartCoroutine(RefreshRemainingTime());
        Refresh();
    }

    private void OnDisable()
    {
        if (view != null)
        {
            view.PartialReceiveRequested -= OpenQuantityModal;
            view.ReceiveAllRequested -= ReceiveAll;
            view.QuantityMinRequested -= SelectMinimum;
            view.QuantityMinusRequested -= DecreaseQuantity;
            view.QuantityPlusRequested -= IncreaseQuantity;
            view.QuantityMaxRequested -= SelectMaximum;
            view.QuantitySliderChanged -= SetSelectedQuantity;
            view.QuantityConfirmRequested -= ConfirmPartialReceive;
        }
        FrameworkEvents.BakeryProductionChanged -= Refresh;
        FrameworkEvents.HomeInventoryChanged -= Refresh;
        if (refreshRoutine != null) StopCoroutine(refreshRoutine);
        refreshRoutine = null;
    }

    public bool TryOpen()
    {
        if (view == null || !BakeryProductionViewDataBuilder.TryBuild(FrameworkRoot.Instance, out _))
            return false;
        view.Open();
        Refresh();
        return true;
    }

    private IEnumerator RefreshRemainingTime()
    {
        var wait = new WaitForSecondsRealtime(1f);
        while (true) { yield return wait; Refresh(); }
    }

    private void Refresh()
    {
        if (view != null && BakeryProductionViewDataBuilder.TryBuild(
                FrameworkRoot.Instance, out BakeryProductionViewData data))
            view.Bind(data);
    }

    private void OpenQuantityModal()
    {
        int maximum = GetMaximumReceivable();
        if (maximum <= 0)
        {
            ShowCollectionFailure(BakeryCollectionFailureReason.WarehouseFull);
            return;
        }
        selectedQuantity = Mathf.Clamp(selectedQuantity, 0, maximum);
        view?.ShowQuantityModal(selectedQuantity, maximum);
    }

    private void SelectMinimum() => SetSelectedQuantity(0);
    private void DecreaseQuantity() => SetSelectedQuantity(selectedQuantity - 1);
    private void IncreaseQuantity() => SetSelectedQuantity(selectedQuantity + 1);
    private void SelectMaximum() => SetSelectedQuantity(GetMaximumReceivable());

    private void SetSelectedQuantity(int value)
    {
        selectedQuantity = Mathf.Clamp(value, 0, Mathf.Max(0, GetMaximumReceivable()));
        view?.SetQuantity(selectedQuantity);
    }

    private int GetMaximumReceivable() =>
        FrameworkRoot.Instance?.BakeryProduction?.GetReceivableCount() ?? 0;

    private void ConfirmPartialReceive()
    {
        if (selectedQuantity <= 0) return;
        Receive(selectedQuantity, true);
    }
    private void ReceiveAll() => Receive(int.MaxValue, false);

    private void Receive(int requestedQuantity, bool closeQuantityModal)
    {
        BakeryCollectionResult result =
            FrameworkRoot.Instance?.BakeryProduction?.Collect(requestedQuantity);
        if (result == null)
        {
            noticeUI?.Show("빵 생산 정보를 불러오지 못했습니다.");
            return;
        }
        if (result.Succeeded)
        {
            if (closeQuantityModal) view?.HideQuantityModal();
            Refresh();
            return;
        }
        ShowCollectionFailure(result.FailureReason);
        Refresh();
    }

    private void ShowCollectionFailure(BakeryCollectionFailureReason failureReason)
    {
        switch (failureReason)
        {
            case BakeryCollectionFailureReason.WarehouseUnavailable:
                noticeUI?.Show("빵을 받으려면 창고를 먼저 건설해야 합니다.");
                break;
            case BakeryCollectionFailureReason.NothingStored:
                noticeUI?.Show("현재 받을 수 있는 빵이 없습니다.");
                break;
            case BakeryCollectionFailureReason.WarehouseFull:
                noticeUI?.Show("창고에 빵을 보관할 공간이 없습니다.");
                break;
            default:
                noticeUI?.Show("빵을 받지 못했습니다. 잠시 후 다시 시도해 주세요.");
                break;
        }
    }
}
