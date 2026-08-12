using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 코티지 생산 팝업의 정적 UI 동작을 담당한다.
/// 실제 생산 데이터와 수령 처리는 별도 Presenter/Service가 연결한다.
/// </summary>
public sealed class CottageProductionPopupView : MonoBehaviour
{
    [SerializeField] private Button backdropButton;
    [SerializeField] private Button closeButton;
    [Header("Wagon")]
    [SerializeField] private Image wagonIcon;
    [SerializeField] private TMP_Text wagonNameText;
    [SerializeField] private TMP_Text wagonAmountText;
    [SerializeField] private TMP_Text wagonRemainingText;
    [SerializeField] private Button wagonReceiveButton;
    [Header("Draft Animal")]
    [SerializeField] private Image draftAnimalIcon;
    [SerializeField] private TMP_Text draftAnimalNameText;
    [SerializeField] private TMP_Text draftAnimalAmountText;
    [SerializeField] private TMP_Text draftAnimalRemainingText;
    [SerializeField] private Button draftAnimalReceiveButton;
    [Header("All")]
    [SerializeField] private Button receiveAllButton;
    [SerializeField] private Color normalAmountColor = new Color(0.18f, 0.15f, 0.12f);
    [SerializeField] private Color fullAmountColor = new Color(0.25f, 0.62f, 0.22f);

    public event Action WagonReceiveRequested;
    public event Action DraftAnimalReceiveRequested;
    public event Action ReceiveAllRequested;
    public event Action Closed;

    private void OnEnable()
    {
        backdropButton?.onClick.AddListener(Close);
        closeButton?.onClick.AddListener(Close);
        wagonReceiveButton?.onClick.AddListener(RaiseWagonReceive);
        draftAnimalReceiveButton?.onClick.AddListener(RaiseDraftAnimalReceive);
        receiveAllButton?.onClick.AddListener(RaiseReceiveAll);
    }

    private void OnDisable()
    {
        backdropButton?.onClick.RemoveListener(Close);
        closeButton?.onClick.RemoveListener(Close);
        wagonReceiveButton?.onClick.RemoveListener(RaiseWagonReceive);
        draftAnimalReceiveButton?.onClick.RemoveListener(RaiseDraftAnimalReceive);
        receiveAllButton?.onClick.RemoveListener(RaiseReceiveAll);
    }

    public void ConfigureCloseButtons(Button backdrop, Button close)
    {
        backdropButton = backdrop;
        closeButton = close;
    }

    public void ConfigureProductionReferences(
        Image wagonProductIcon,
        TMP_Text wagonName,
        TMP_Text wagonAmount,
        TMP_Text wagonTime,
        Button wagonReceive,
        Image animalProductIcon,
        TMP_Text animalName,
        TMP_Text animalAmount,
        TMP_Text animalTime,
        Button animalReceive,
        Button allReceive)
    {
        wagonIcon = wagonProductIcon;
        wagonNameText = wagonName;
        wagonAmountText = wagonAmount;
        wagonRemainingText = wagonTime;
        wagonReceiveButton = wagonReceive;
        draftAnimalIcon = animalProductIcon;
        draftAnimalNameText = animalName;
        draftAnimalAmountText = animalAmount;
        draftAnimalRemainingText = animalTime;
        draftAnimalReceiveButton = animalReceive;
        receiveAllButton = allReceive;
    }

    public void Bind(CottageProductionViewData data)
    {
        if (data == null) return;
        BindProduct(data.Wagon, wagonIcon, wagonNameText, wagonAmountText,
            wagonRemainingText, wagonReceiveButton);
        BindProduct(data.DraftAnimal, draftAnimalIcon, draftAnimalNameText,
            draftAnimalAmountText, draftAnimalRemainingText, draftAnimalReceiveButton);
        if (receiveAllButton != null)
            receiveAllButton.interactable = data.CanReceiveAll;
    }

    public void Open()
    {
        gameObject.SetActive(true);
    }

    public void Close()
    {
        if (!gameObject.activeSelf) return;
        Closed?.Invoke();
        gameObject.SetActive(false);
    }

    private void BindProduct(
        CottageProductViewData data,
        Image icon,
        TMP_Text nameText,
        TMP_Text amountText,
        TMP_Text remainingText,
        Button receiveButton)
    {
        if (data == null) return;
        if (icon != null)
        {
            icon.sprite = data.Icon;
            icon.enabled = data.Icon != null;
        }
        if (nameText != null) nameText.text = data.DisplayName;
        if (amountText != null)
        {
            amountText.text = $"{data.StoredCount} / {data.Capacity}";
            amountText.color = data.State == CottageProductViewState.Full
                ? fullAmountColor
                : normalAmountColor;
        }
        if (remainingText != null)
            remainingText.text = FormatState(data);
        if (receiveButton != null)
            receiveButton.interactable = data.CanReceive;
    }

    private static string FormatState(CottageProductViewData data)
    {
        switch (data.State)
        {
            case CottageProductViewState.Full:
                return "보관 한도 도달 · 생산 중단";
            case CottageProductViewState.Stopped:
                return "생산 중단";
            case CottageProductViewState.Uninitialized:
                return "생산 준비 중";
            default:
                int seconds = Math.Max(0, data.RemainingSeconds);
                return $"다음 생산까지 {seconds / 60:00}:{seconds % 60:00}";
        }
    }

    private void RaiseWagonReceive() => WagonReceiveRequested?.Invoke();
    private void RaiseDraftAnimalReceive() => DraftAnimalReceiveRequested?.Invoke();
    private void RaiseReceiveAll() => ReceiveAllRequested?.Invoke();
}
