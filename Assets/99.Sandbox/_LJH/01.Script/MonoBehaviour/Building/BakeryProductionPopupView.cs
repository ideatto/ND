using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BakeryProductionPopupView : MonoBehaviour
{
    [SerializeField] private Button backdropButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Image productIcon;
    [SerializeField] private TMP_Text productNameText;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private TMP_Text remainingText;
    [SerializeField] private Button partialReceiveButton;
    [SerializeField] private Button receiveAllButton;
    [Header("Partial Receive Modal")]
    [SerializeField] private GameObject quantityModal;
    [SerializeField] private Button minButton;
    [SerializeField] private Button minusButton;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private Button plusButton;
    [SerializeField] private Button maxButton;
    [SerializeField] private Slider quantitySlider;
    [SerializeField] private Button quantityCancelButton;
    [SerializeField] private Button quantityConfirmButton;
    [SerializeField] private Color normalAmountColor = new Color(0.18f, 0.15f, 0.12f);
    [SerializeField] private Color fullAmountColor = new Color(0.25f, 0.62f, 0.22f);

    public event Action PartialReceiveRequested;
    public event Action ReceiveAllRequested;
    public event Action QuantityMinRequested;
    public event Action QuantityMinusRequested;
    public event Action QuantityPlusRequested;
    public event Action QuantityMaxRequested;
    public event Action<int> QuantitySliderChanged;
    public event Action QuantityConfirmRequested;

    private void OnEnable()
    {
        backdropButton?.onClick.AddListener(Close);
        closeButton?.onClick.AddListener(Close);
        partialReceiveButton?.onClick.AddListener(RaisePartialReceive);
        receiveAllButton?.onClick.AddListener(RaiseReceiveAll);
        minButton?.onClick.AddListener(RaiseQuantityMin);
        minusButton?.onClick.AddListener(RaiseQuantityMinus);
        plusButton?.onClick.AddListener(RaiseQuantityPlus);
        maxButton?.onClick.AddListener(RaiseQuantityMax);
        quantitySlider?.onValueChanged.AddListener(RaiseQuantitySliderChanged);
        quantityCancelButton?.onClick.AddListener(HideQuantityModal);
        quantityConfirmButton?.onClick.AddListener(RaiseQuantityConfirm);
    }

    private void OnDisable()
    {
        backdropButton?.onClick.RemoveListener(Close);
        closeButton?.onClick.RemoveListener(Close);
        partialReceiveButton?.onClick.RemoveListener(RaisePartialReceive);
        receiveAllButton?.onClick.RemoveListener(RaiseReceiveAll);
        minButton?.onClick.RemoveListener(RaiseQuantityMin);
        minusButton?.onClick.RemoveListener(RaiseQuantityMinus);
        plusButton?.onClick.RemoveListener(RaiseQuantityPlus);
        maxButton?.onClick.RemoveListener(RaiseQuantityMax);
        quantitySlider?.onValueChanged.RemoveListener(RaiseQuantitySliderChanged);
        quantityCancelButton?.onClick.RemoveListener(HideQuantityModal);
        quantityConfirmButton?.onClick.RemoveListener(RaiseQuantityConfirm);
    }

    public void Configure(Button backdrop, Button close, Image icon, TMP_Text productName,
        TMP_Text amount, TMP_Text remaining, Button partialReceive, Button receiveAll,
        GameObject modal, Button min, Button minus, TMP_Text selectedQuantity,
        Button plus, Button max, Slider slider, Button cancel, Button confirm)
    {
        backdropButton = backdrop; closeButton = close; productIcon = icon;
        productNameText = productName; amountText = amount;
        remainingText = remaining; partialReceiveButton = partialReceive;
        receiveAllButton = receiveAll; quantityModal = modal; minButton = min;
        minusButton = minus; quantityText = selectedQuantity; plusButton = plus;
        maxButton = max; quantitySlider = slider;
        quantityCancelButton = cancel; quantityConfirmButton = confirm;
    }

    public void Bind(BakeryProductionViewData data)
    {
        if (data == null) return;
        if (productIcon != null) { productIcon.sprite = data.Icon; productIcon.enabled = data.Icon != null; }
        if (productNameText != null) productNameText.text = data.DisplayName;
        if (amountText != null)
        {
            amountText.text = $"{data.StoredCount} / {data.Capacity}";
            amountText.color = data.IsFull ? fullAmountColor : normalAmountColor;
        }
        if (remainingText != null)
            remainingText.text = data.IsFull ? "보관 한도 도달 · 생산 중단"
                : $"다음 생산까지 {data.RemainingSeconds / 60:00}:{data.RemainingSeconds % 60:00}";
        if (partialReceiveButton != null) partialReceiveButton.interactable = data.CanReceive;
        if (receiveAllButton != null) receiveAllButton.interactable = data.CanReceive;
    }

    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);
    public void ShowQuantityModal(int quantity, int maximum)
    {
        if (quantitySlider != null)
        {
            quantitySlider.wholeNumbers = true;
            quantitySlider.minValue = 0;
            quantitySlider.maxValue = Mathf.Max(0, maximum);
        }
        SetQuantity(quantity);
        quantityModal?.SetActive(true);
    }
    public void HideQuantityModal() => quantityModal?.SetActive(false);
    public void SetQuantity(int quantity)
    {
        if (quantityText != null) quantityText.text = quantity.ToString();
        quantitySlider?.SetValueWithoutNotify(quantity);
        if (quantityConfirmButton != null) quantityConfirmButton.interactable = quantity > 0;
    }

    private void RaisePartialReceive() => PartialReceiveRequested?.Invoke();
    private void RaiseReceiveAll() => ReceiveAllRequested?.Invoke();
    private void RaiseQuantityMin() => QuantityMinRequested?.Invoke();
    private void RaiseQuantityMinus() => QuantityMinusRequested?.Invoke();
    private void RaiseQuantityPlus() => QuantityPlusRequested?.Invoke();
    private void RaiseQuantityMax() => QuantityMaxRequested?.Invoke();
    private void RaiseQuantitySliderChanged(float value) =>
        QuantitySliderChanged?.Invoke(Mathf.RoundToInt(value));
    private void RaiseQuantityConfirm() => QuantityConfirmRequested?.Invoke();
}
