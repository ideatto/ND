using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 별도 생성 로직 없이 메시지와 확인 버튼만 표시하는 재사용 Popup View입니다.
/// Scene 또는 prefab에 미리 배치한 뒤 Show를 호출해 사용합니다.
/// </summary>
public sealed class ReusableMessagePopup : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text confirmButtonText;

    private Action onConfirmed;

    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(Confirm);
        }
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(Confirm);
        }
    }

    /// <summary>현재 표시 문구를 바꾸되 Popup 활성 상태는 변경하지 않습니다.</summary>
    public void SetContent(string message, string buttonText)
    {
        if (messageText != null)
        {
            messageText.text = message ?? string.Empty;
        }

        if (confirmButtonText != null)
        {
            confirmButtonText.text = buttonText ?? string.Empty;
        }
    }

    /// <summary>전달한 문구와 일회성 확인 callback으로 Popup을 엽니다.</summary>
    public void Show(string message, string buttonText, Action confirmed = null)
    {
        SetContent(message, buttonText);
        onConfirmed = confirmed;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        onConfirmed = null;
        gameObject.SetActive(false);
    }

    private void Confirm()
    {
        Action callback = onConfirmed;
        Hide();
        callback?.Invoke();
    }
}
