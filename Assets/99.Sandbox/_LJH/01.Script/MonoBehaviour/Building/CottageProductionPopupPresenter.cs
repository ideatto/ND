using System.Collections;
using ND.Framework;
using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// Popup 입력을 CottageProductionService command로 전달하고 이벤트 발생 후 ViewData를 다시 읽는다.
/// 1초 Coroutine은 열린 화면의 카운트다운 표현만 갱신하며 생산 상태를 진행시키지 않는다.
/// </summary>
public sealed class CottageProductionPopupPresenter : MonoBehaviour
{
    [SerializeField] private CottageProductionPopupView view;
    [SerializeField] private NoticeUI noticeUI;
    private Coroutine remainingTimeRoutine;

    public void Configure(CottageProductionPopupView popupView, NoticeUI notice)
    {
        view = popupView;
        noticeUI = notice;
    }

    private void OnEnable()
    {
        if (view != null)
        {
            view.WagonReceiveRequested += ReceiveWagon;
            view.DraftAnimalReceiveRequested += ReceiveDraftAnimal;
            view.ReceiveAllRequested += ReceiveAll;
        }
        FrameworkEvents.CottageProductionChanged += Refresh;
        FrameworkEvents.SharedGameDataLoaded += HandleFrameworkReady;
        FrameworkEvents.LoadCompleted += HandleFrameworkReady;
        remainingTimeRoutine = StartCoroutine(RefreshRemainingTime());
        Refresh();
    }

    private void OnDisable()
    {
        if (view != null)
        {
            view.WagonReceiveRequested -= ReceiveWagon;
            view.DraftAnimalReceiveRequested -= ReceiveDraftAnimal;
            view.ReceiveAllRequested -= ReceiveAll;
        }
        FrameworkEvents.CottageProductionChanged -= Refresh;
        FrameworkEvents.SharedGameDataLoaded -= HandleFrameworkReady;
        FrameworkEvents.LoadCompleted -= HandleFrameworkReady;
        if (remainingTimeRoutine != null)
        {
            StopCoroutine(remainingTimeRoutine);
            remainingTimeRoutine = null;
        }
    }

    public bool TryOpen()
    {
        FrameworkRoot root = FrameworkRoot.Instance;
        if (view == null || !CottageProductionViewDataBuilder.TryBuild(root, out _))
            return false;
        view.Open();
        Refresh();
        return true;
    }

    private IEnumerator RefreshRemainingTime()
    {
        var wait = new WaitForSecondsRealtime(1f);
        while (true)
        {
            yield return wait;
            Refresh();
        }
    }

    private void Refresh()
    {
        if (view != null
            && CottageProductionViewDataBuilder.TryBuild(
                FrameworkRoot.Instance, out CottageProductionViewData data))
            view.Bind(data);
    }

    private void ReceiveWagon() => Collect(CottageCollectionTarget.Wagon);
    private void ReceiveDraftAnimal() => Collect(CottageCollectionTarget.DraftAnimal);
    private void ReceiveAll() => Collect(CottageCollectionTarget.All);

    private void Collect(CottageCollectionTarget target)
    {
        CottageCollectionResult result =
            FrameworkRoot.Instance?.CottageProduction?.Collect(
                target, PlayerMainManager.Instance);
        if (result == null)
        {
            noticeUI?.Show("생산 정보를 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.");
            return;
        }
        if (result.Succeeded)
        {
            Refresh();
            return;
        }
        switch (result.FailureReason)
        {
            case CottageCollectionFailureReason.FarmUnavailable:
                noticeUI?.Show("물품을 받으려면 목장을 먼저 건설해야 합니다.");
                break;
            case CottageCollectionFailureReason.NothingStored:
                noticeUI?.Show("현재 받을 수 있는 생산물이 없습니다.");
                break;
            case CottageCollectionFailureReason.InventoryFull:
                noticeUI?.Show("운송수단 보관 공간이 부족합니다.");
                break;
            case CottageCollectionFailureReason.InvalidConfiguration:
                noticeUI?.Show("오두막 생산물 설정을 찾을 수 없습니다.");
                break;
            default:
                noticeUI?.Show("생산물을 받지 못했습니다. 잠시 후 다시 시도해 주세요.");
                break;
        }
        Refresh();
    }

    private void HandleFrameworkReady(ISharedGameDataProvider _) => Refresh();
    private void HandleFrameworkReady(ND.Framework.SaveData _) => Refresh();
}
