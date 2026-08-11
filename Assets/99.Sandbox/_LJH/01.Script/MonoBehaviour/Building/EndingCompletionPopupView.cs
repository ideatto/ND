using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 프리팹에 배치된 엔딩 팝업의 입력과 표시 상태만 담당한다.
/// Backdrop도 닫기 입력으로 취급하며 런타임에 UI 오브젝트를 생성하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EndingCompletionPopupView : MonoBehaviour
{
    [SerializeField] private Button backdropButton;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button endGameButton;
    [SerializeField] private Button continueButton;

    public event Action EndGameRequested;
    public event Action ContinueRequested;

    public void Configure(
        Button backdrop,
        TMP_Text message,
        Button endButton,
        Button keepPlayingButton)
    {
        backdropButton = backdrop;
        messageText = message;
        endGameButton = endButton;
        continueButton = keepPlayingButton;
    }

    private void Awake()
    {
        backdropButton?.onClick.AddListener(Close);
        endGameButton?.onClick.AddListener(HandleEndGameClicked);
        continueButton?.onClick.AddListener(HandleContinueClicked);
    }

    private void OnDestroy()
    {
        backdropButton?.onClick.RemoveListener(Close);
        endGameButton?.onClick.RemoveListener(HandleEndGameClicked);
        continueButton?.onClick.RemoveListener(HandleContinueClicked);
    }

    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);

    public void Bind(string endingBuildingDisplayName)
    {
        if (messageText == null) return;
        string displayName = string.IsNullOrWhiteSpace(endingBuildingDisplayName)
            ? "엔딩 건물"
            : endingBuildingDisplayName.Trim();
        messageText.text =
            $"<size=38><b>{displayName}를 완성하여 목적을 모두 이뤘습니다.</b></size>\n\n"
            + "게임을 끝내거나, 계속 플레이할 수 있습니다.";
    }

    private void HandleEndGameClicked() => EndGameRequested?.Invoke();
    private void HandleContinueClicked() => ContinueRequested?.Invoke();
}
