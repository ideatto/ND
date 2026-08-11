using ND.Framework;
using UnityEngine;

/// <summary>
/// 엔딩 Popup의 선택을 기존 게임 흐름에 연결한다.
/// 끝내기는 저장을 거쳐 타이틀로 돌아가는 FrameworkRoot 경로를 재사용한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EndingCompletionPopupPresenter : MonoBehaviour
{
    [SerializeField] private EndingCompletionPopupView view;

    public void Configure(EndingCompletionPopupView popupView) => view = popupView;

    private void Awake()
    {
        if (view == null)
            view = GetComponent<EndingCompletionPopupView>();
    }

    private void OnEnable()
    {
        if (view == null) return;
        view.EndGameRequested += HandleEndGameRequested;
        view.ContinueRequested += HandleContinueRequested;
    }

    private void OnDisable()
    {
        if (view == null) return;
        view.EndGameRequested -= HandleEndGameRequested;
        view.ContinueRequested -= HandleContinueRequested;
    }

    public void Open(EndingCompletionViewData data)
    {
        if (data == null || !data.IsCompleted || view == null)
            return;

        view.Bind(data.DisplayName);
        view.Open();
        view.transform.SetAsLastSibling();
    }

    private void HandleContinueRequested() => view?.Close();

    private void HandleEndGameRequested()
    {
        view?.Close();
        FrameworkRoot.Instance?.ReturnToTitle();
    }
}
