using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Displays the mercenary selection portion of TradePrepareViewData.
/// All costs and combat power are supplied by TradePrepareViewDataBuilder;
/// this component does not reproduce gameplay calculations.
/// </summary>
public sealed class MercenarySelectionPanel : MonoBehaviour
{
    [Serializable]
    public sealed class MercenarySelectionEvent : UnityEvent<string, bool> { }

    [Header("Summary")]
    [SerializeField] private TMP_Text powerText;
    [SerializeField] private TMP_Text currencyText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text messageText;

    [Header("List")]
    [SerializeField] private RectTransform listContainer;
    [SerializeField] private Button rowTemplate;

    [Header("Actions")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private MercenarySelectionEvent onSelectionRequested = new MercenarySelectionEvent();
    [SerializeField] private UnityEvent onConfirmed = new UnityEvent();

    private readonly List<Button> rows = new List<Button>();

    public MercenarySelectionEvent SelectionRequested => onSelectionRequested;
    public UnityEvent Confirmed => onConfirmed;

    private void Awake()
    {
        if (rowTemplate != null)
            rowTemplate.gameObject.SetActive(false);
        if (confirmButton != null)
            confirmButton.onClick.AddListener(onConfirmed.Invoke);
    }

    /// <summary>
    /// Binds values already calculated by TradePrepareViewDataBuilder.
    /// Mercenary selection is optional for the first build, so confirm remains enabled.
    /// </summary>
    public void Bind(TradePrepareViewData viewData)
    {
        ClearRows();

        if (viewData == null)
        {
            SetText(powerText, "용병 전투력  0 / 0");
            SetText(currencyText, "현재 소지 금액  0 G");
            SetText(costText, "용병 고용 가격  0 G");
            SetText(messageText, "표시할 준비 데이터가 없습니다.");
            if (confirmButton != null) confirmButton.interactable = true;
            return;
        }

        SetText(powerText, $"용병 전투력  {viewData.selectedMercenaryPower:N0} / {viewData.requiredMercenaryPower:N0}");
        SetText(currencyText, $"현재 소지 금액  {viewData.currentTradingCurrency:N0} G");
        SetText(costText, $"용병 고용 가격  {viewData.mercenaryCost:N0} G");
        SetText(messageText, viewData.mercenaryCost > viewData.currentTradingCurrency
            ? "보유 금액이 부족합니다. 선택을 해제하거나 용병 없이 진행하세요."
            : "용병 고용은 선택 사항입니다.");

        MercenaryViewData[] mercenaries = viewData.mercenaries ?? Array.Empty<MercenaryViewData>();
        foreach (MercenaryViewData mercenary in mercenaries)
            AddRow(mercenary);

        if (confirmButton != null)
            confirmButton.interactable = true;
    }

    private void AddRow(MercenaryViewData mercenary)
    {
        if (mercenary == null || rowTemplate == null || listContainer == null)
            return;

        Button row = Instantiate(rowTemplate, listContainer);
        row.gameObject.SetActive(true);
        row.name = $"Mercenary_{mercenary.mercenaryId}";
        TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            string selected = mercenary.isSelected ? "[선택] " : string.Empty;
            label.text = $"{selected}{mercenary.displayName}\n전투력 {mercenary.combatCapability:N0}  /  {mercenary.baseBuyPrice:N0} G";
        }

        row.interactable = mercenary.canHire || mercenary.isSelected;
        string id = mercenary.mercenaryId;
        bool nextSelected = !mercenary.isSelected;
        row.onClick.AddListener(() => onSelectionRequested.Invoke(id, nextSelected));
        rows.Add(row);
    }

    private void ClearRows()
    {
        foreach (Button row in rows)
            if (row != null) Destroy(row.gameObject);
        rows.Clear();
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
