using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 건설 또는 증축을 최종 확인하는 Popup의 화면 표시와
/// 사용자 입력 전달을 담당한다.
/// 실제 건설 처리와 재화 차감은 수행하지 않는다.
/// </summary>
public class BuildingConfirmPopupPresenter : MonoBehaviour
{
    [Header("Popup")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button returnBackdropButton;

    [Header("Content")]
    [SerializeField] private TMP_Text messageText;

    [Header("Action")]
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button confirmButton;

    private BuildingConfirmViewData currentViewData;

    /// <summary>확인 버튼을 누르면 최종 확인한 건물의 buildId를 전달한다.</summary>
    public event Action<string> ConfirmRequested;

    /// <summary>취소 버튼 또는 배경을 누르면 Popup 닫기 요청을 전달한다.</summary>
    public event Action CancelRequested;

    private void Awake()
    {
        returnBackdropButton?.onClick.AddListener(HandleCancelClicked);
        cancelButton?.onClick.AddListener(HandleCancelClicked);
        confirmButton?.onClick.AddListener(HandleConfirmClicked);

        SetVisible(false);
    }

    private void OnDestroy()
    {
        returnBackdropButton?.onClick.RemoveListener(HandleCancelClicked);
        cancelButton?.onClick.RemoveListener(HandleCancelClicked);
        confirmButton?.onClick.RemoveListener(HandleConfirmClicked);
    }

    /// <summary>전달받은 확인 데이터를 렌더링하고 Popup을 표시한다.</summary>
    public void Show(BuildingConfirmViewData viewData)
    {
        if(viewData == null)
        {
            Hide();
            return;
        }

        currentViewData = viewData;

        RenderMessage(viewData);
        RenderConfirmButton(viewData);

        SetVisible(true);
    }

    /// <summary>현재 확인 데이터를 비우고 Popup을 숨긴다.</summary>
    public void Hide()
    {
        currentViewData = null;
        SetVisible(false);
    }

    // 건물 이름의 받침 여부에 맞춰 을/를 조사를 선택한다.
    private void RenderMessage(BuildingConfirmViewData viewData)
    {
        if(messageText == null)
        {
            return;
        }

        string actionText = viewData.isConstruction ? "건설" : "증축";
        string objectParticle = HasFinalConsonant(viewData.displayName) ? "을" : "를";

        messageText.text = $"{viewData.displayName}{objectParticle} {actionText}하시겠습니까?";
    }

    // ViewData가 최종 진행 가능할 때만 확인 버튼 입력을 허용한다.
    private void RenderConfirmButton(BuildingConfirmViewData viewData)
    {
        if(confirmButton != null)
        {
            confirmButton.interactable = viewData.canConfirm;
        }
    }

    private void HandleConfirmClicked()
    {
        if(currentViewData == null || !currentViewData.canConfirm || string.IsNullOrWhiteSpace(currentViewData.buildId))
        {
            return;
        }

        ConfirmRequested?.Invoke(currentViewData.buildId);
    }

    // 취소와 배경 클릭은 동일한 닫기 요청으로 처리한다.
    private void HandleCancelClicked()
    {
        CancelRequested?.Invoke();
        Hide();
    }

    // Popup은 최초 Awake에서 한 번 초기화한 뒤 숨길 때 GameObject까지 비활성화한다.
    // Binding은 별도 활성 Runtime 오브젝트에 있으므로 다음 Show 호출에서 안전하게 다시 활성화할 수 있다.
    private void SetVisible(bool visible)
    {
        if(visible && !gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if(canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        if(!visible && gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    // 완성형 한글의 종성 인덱스를 이용해 마지막 글자의 받침 여부를 확인한다.
    private static bool HasFinalConsonant(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        char lastCharacter = value[value.Length - 1];

        const int hangulStart = 0xAC00;
        const int hangulEnd = 0xD7A3;

        if(lastCharacter < hangulStart || lastCharacter > hangulEnd)
        {
            return false;
        }

        int syllableIndex = lastCharacter - hangulStart;

        return syllableIndex % 28 != 0;
    }

}
