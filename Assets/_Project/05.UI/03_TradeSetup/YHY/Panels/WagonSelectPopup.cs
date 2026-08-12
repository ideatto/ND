using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WagonSelectPopup : MonoBehaviour
{
    [Header("목록")]
    [SerializeField] private Transform listContainer;
    [SerializeField] private ScrollRect listScrollRect;
    [SerializeField] private Button buttonPrefab;
    [SerializeField] private WagonInstanceRowView instanceRowPrefab;

    [Header("버튼")]
    [SerializeField] private Button cancelButton;

    private readonly List<Button> headerPool = new List<Button>();
    private readonly List<WagonInstanceRowView> rowPool = new List<WagonInstanceRowView>();
    private IReadOnlyList<TransportSelectPanel.TransportEntry> entries;
    private Action<TransportSelectPanel.TransportEntry> onSelect;
    private bool wired;
    private int usedHeaders;
    private int usedRows;
    private string expandedContentId;

    private void EnsureWired()
    {
        if (wired) return;
        wired = true;
        if (cancelButton != null) cancelButton.onClick.AddListener(Close);
    }

    public void Open(IReadOnlyList<TransportSelectPanel.TransportEntry> wagons,
        Action<TransportSelectPanel.TransportEntry> selectCallback)
    {
        EnsureWired();
        onSelect = selectCallback;
        // Activate before rebuilding so layout components include every pooled row in the
        // content height. Rebuilding under an inactive popup can leave the final row outside
        // ScrollRect's cached bounds until another layout pass happens.
        gameObject.SetActive(true);
        Rebuild(wagons, null);
        RefreshScrollLayout(true);
    }

    private void Rebuild(IReadOnlyList<TransportSelectPanel.TransportEntry> wagons,
        string expandedContentId)
    {
        ResetPools();
        entries = wagons;
        this.expandedContentId = expandedContentId;
        if (listContainer == null || buttonPrefab == null || wagons == null) return;

        foreach (var group in wagons.GroupBy(wagon => wagon.id, StringComparer.Ordinal))
        {
            TransportSelectPanel.TransportEntry first = group.First();
            Button header = GetHeader($"{first.name}  [{TypeLabel(first.type)}]  x{group.Count()}");
            string contentId = group.Key;
            header.onClick.AddListener(() => Expand(contentId));

            if (!string.Equals(contentId, expandedContentId, StringComparison.Ordinal) ||
                instanceRowPrefab == null)
                continue;

            int number = 1;
            foreach (TransportSelectPanel.TransportEntry wagon in group)
            {
                WagonInstanceRowView row = GetRow();
                row.Bind(number++, wagon, Choose);
            }
        }
    }

    private void Expand(string contentId)
    {
        Rebuild(entries,
            string.Equals(expandedContentId, contentId, StringComparison.Ordinal)
                ? null
                : contentId);
        RefreshScrollLayout(false);
    }

    private void RefreshScrollLayout(bool moveToTop)
    {
        if (listContainer is RectTransform content)
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();

        if (moveToTop && listScrollRect != null)
            listScrollRect.verticalNormalizedPosition = 1f;
    }

    private Button GetHeader(string label)
    {
        Button button;
        if (usedHeaders < headerPool.Count)
            button = headerPool[usedHeaders];
        else
        {
            button = Instantiate(buttonPrefab, listContainer);
            headerPool.Add(button);
        }

        usedHeaders++;
        button.gameObject.SetActive(true);
        button.transform.SetAsLastSibling();
        button.onClick.RemoveAllListeners();
        TMP_Text text = button.GetComponentInChildren<TMP_Text>();
        if (text != null) text.text = label;
        return button;
    }

    private WagonInstanceRowView GetRow()
    {
        WagonInstanceRowView row;
        if (usedRows < rowPool.Count)
            row = rowPool[usedRows];
        else
        {
            row = Instantiate(instanceRowPrefab, listContainer);
            rowPool.Add(row);
        }

        usedRows++;
        row.gameObject.SetActive(true);
        row.transform.SetAsLastSibling();
        return row;
    }

    private static string TypeLabel(TransportType type)
    {
        if (type == TransportType.Wagon) return "마차";
        if (type == TransportType.Mount) return "탈것";
        return "도보";
    }

    private void Choose(TransportSelectPanel.TransportEntry wagon)
    {
        Action<TransportSelectPanel.TransportEntry> callback = onSelect;
        onSelect = null;
        gameObject.SetActive(false);
        callback?.Invoke(wagon);
    }

    public void Close()
    {
        onSelect = null;
        gameObject.SetActive(false);
    }

    private void ResetPools()
    {
        usedHeaders = 0;
        usedRows = 0;
        foreach (Button header in headerPool)
            if (header != null) header.gameObject.SetActive(false);
        foreach (WagonInstanceRowView row in rowPool)
            if (row != null) row.gameObject.SetActive(false);
    }
}
