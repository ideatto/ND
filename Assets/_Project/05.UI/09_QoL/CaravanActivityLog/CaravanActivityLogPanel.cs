using System;
using System.Collections;
using System.Collections.Generic;
using ND.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CaravanActivityLogPanel : MonoBehaviour
{
    [Header("View")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private CaravanActivityLogItemView itemPrefab;

    [Header("Behavior")]
    [SerializeField, Min(1)] private int maxVisibleEntries = 100;
    [SerializeField, Range(0f, 0.1f)] private float bottomThreshold = 0.02f;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;
    [SerializeField, Min(0f)] private float autoReturnDelay = 5f;
    [SerializeField, Min(0f)] private float autoReturnDuration = 0.2f;

    [Header("Caravan Colors")]
    [SerializeField] private Color[] caravanColors =
    {
        new Color(0.25f, 0.58f, 0.96f, 1f),
        new Color(0.96f, 0.48f, 0.28f, 1f),
        new Color(0.38f, 0.76f, 0.46f, 1f),
        new Color(0.68f, 0.48f, 0.90f, 1f)
    };

    private readonly List<CaravanActivityLogItemView> spawnedItems =
        new List<CaravanActivityLogItemView>();
    private long lastSequence = long.MinValue;
    private int lastCount = -1;
    private float nextRefreshTime;
    private bool initialScrollPending;
    private bool waitingForAutoReturn;
    private float lastScrollbarInteractionTime;
    private Coroutine autoReturnCoroutine;
    private bool suppressScrollbarInteraction;

    private void OnEnable()
    {
        lastSequence = long.MinValue;
        lastCount = -1;
        initialScrollPending = true;
        waitingForAutoReturn = false;
        autoReturnCoroutine = null;
        if (scrollRect?.verticalScrollbar != null)
        {
            scrollRect.verticalScrollbar.onValueChanged.AddListener(
                HandleScrollbarValueChanged);
        }
        Refresh(force: true);
    }

    private void OnDisable()
    {
        if (scrollRect?.verticalScrollbar != null)
        {
            scrollRect.verticalScrollbar.onValueChanged.RemoveListener(
                HandleScrollbarValueChanged);
        }
        if (autoReturnCoroutine != null)
        {
            StopCoroutine(autoReturnCoroutine);
            autoReturnCoroutine = null;
        }
    }

    private void Update()
    {
        if (waitingForAutoReturn
            && Time.unscaledTime - lastScrollbarInteractionTime >= autoReturnDelay
            && autoReturnCoroutine == null)
        {
            autoReturnCoroutine = StartCoroutine(ReturnToBottom());
        }

        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.unscaledTime + refreshInterval;
        Refresh(force: false);
    }

    private void LateUpdate()
    {
        if (!initialScrollPending)
        {
            return;
        }

        initialScrollPending = false;
        ScrollToBottom();
    }

    public void RefreshNow()
    {
        Refresh(force: true);
    }

    public void NotifyScrollbarInteraction()
    {
        lastScrollbarInteractionTime = Time.unscaledTime;
        waitingForAutoReturn = true;
        initialScrollPending = false;
        if (autoReturnCoroutine != null)
        {
            StopCoroutine(autoReturnCoroutine);
            autoReturnCoroutine = null;
        }
    }

    private void HandleScrollbarValueChanged(float value)
    {
        if (!suppressScrollbarInteraction)
        {
            NotifyScrollbarInteraction();
        }
    }

    private void Refresh(bool force)
    {
        var saveData = FrameworkRoot.Instance != null
            ? FrameworkRoot.Instance.CurrentSaveData
            : null;
        var logs = saveData?.caravanActivityLogs;
        var count = logs?.Count ?? 0;
        var newestSequence = count > 0 && logs[count - 1] != null
            ? logs[count - 1].sequence
            : 0L;
        if (!force && count == lastCount && newestSequence == lastSequence)
        {
            return;
        }

        var shouldFollowBottom = initialScrollPending || IsAtBottom();
        suppressScrollbarInteraction = true;
        Rebuild(saveData, logs);
        suppressScrollbarInteraction = false;
        lastCount = count;
        lastSequence = newestSequence;

        if (shouldFollowBottom)
        {
            initialScrollPending = true;
        }
    }

    private void Rebuild(
        ND.Framework.SaveData saveData,
        List<CaravanActivityLogEntrySaveData> logs)
    {
        ClearItems();
        if (contentRoot == null || itemPrefab == null || logs == null)
        {
            return;
        }

        var startIndex = Mathf.Max(0, logs.Count - Mathf.Max(1, maxVisibleEntries));
        for (var index = startIndex; index < logs.Count; index++)
        {
            var entry = logs[index];
            if (entry == null)
            {
                continue;
            }

            var item = Instantiate(itemPrefab, contentRoot);
            item.gameObject.SetActive(true);
            item.Bind(
                FormatMessage(saveData, entry),
                ResolveCaravanColor(saveData, entry.caravanId));
            spawnedItems.Add(item);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
    }

    private void ClearItems()
    {
        for (var index = 0; index < spawnedItems.Count; index++)
        {
            if (spawnedItems[index] != null)
            {
                Destroy(spawnedItems[index].gameObject);
            }
        }
        spawnedItems.Clear();
    }

    private bool IsAtBottom()
    {
        return scrollRect == null
            || !scrollRect.vertical
            || scrollRect.verticalNormalizedPosition <= bottomThreshold;
    }

    private void ScrollToBottom()
    {
        if (scrollRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        if (contentRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        }
        suppressScrollbarInteraction = true;
        scrollRect.verticalNormalizedPosition = 0f;
        scrollRect.StopMovement();
        suppressScrollbarInteraction = false;
    }

    private IEnumerator ReturnToBottom()
    {
        if (scrollRect == null)
        {
            waitingForAutoReturn = false;
            autoReturnCoroutine = null;
            yield break;
        }

        Canvas.ForceUpdateCanvases();
        var start = scrollRect.verticalNormalizedPosition;
        var duration = Mathf.Max(0f, autoReturnDuration);
        if (duration <= 0f)
        {
            ScrollToBottom();
        }
        else
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                suppressScrollbarInteraction = true;
                scrollRect.verticalNormalizedPosition =
                    Mathf.Lerp(start, 0f, Mathf.SmoothStep(0f, 1f, progress));
                suppressScrollbarInteraction = false;
                yield return null;
            }
            ScrollToBottom();
        }

        waitingForAutoReturn = false;
        autoReturnCoroutine = null;
    }

    private string FormatMessage(
        ND.Framework.SaveData saveData,
        CaravanActivityLogEntrySaveData entry)
    {
        var caravanName = ResolveCaravanName(saveData, entry.caravanId);
        var townName = ResolveTownName(entry.townId);
        switch (entry.eventType)
        {
            case CaravanActivityLogType.Departure:
                return string.IsNullOrEmpty(townName)
                    ? $"{caravanName}이(가) 무역을 위해 출발했습니다."
                    : $"{caravanName}이(가) {townName}(으)로 무역을 출발했습니다.";
            case CaravanActivityLogType.CombatEncounter:
                return $"{caravanName}이(가) 산적과 전투 중입니다.";
            case CaravanActivityLogType.CombatVictory:
                return $"{caravanName}이(가) 산적과의 전투에서 승리했습니다.";
            case CaravanActivityLogType.CombatDefeat:
                return $"{caravanName}이(가) 산적과의 전투에서 패배했습니다.";
            case CaravanActivityLogType.Arrival:
                return string.IsNullOrEmpty(townName)
                    ? $"{caravanName}이(가) 무역 목적지에 도착했습니다."
                    : $"{caravanName}이(가) {townName}에 도착했습니다.";
            default:
                return $"{caravanName}의 상태가 변경되었습니다.";
        }
    }

    private static string ResolveCaravanName(
        ND.Framework.SaveData saveData,
        string caravanId)
    {
        if (SaveDataLookup.TryGetCaravan(saveData, caravanId, out var caravan)
            && caravan != null)
        {
            return $"캐러반 {caravan.slotIndex + 1}";
        }
        return "캐러반";
    }

    private static string ResolveTownName(string townId)
    {
        var sharedData = FrameworkRoot.Instance != null
            ? FrameworkRoot.Instance.SharedGameData
            : null;
        if (sharedData != null
            && sharedData.TryGetTown(townId, out var town)
            && town != null
            && !string.IsNullOrWhiteSpace(town.DisplayName))
        {
            return town.DisplayName;
        }
        return townId ?? string.Empty;
    }

    private Color ResolveCaravanColor(
        ND.Framework.SaveData saveData,
        string caravanId)
    {
        var palette = caravanColors;
        if (palette == null || palette.Length == 0)
        {
            return Color.white;
        }

        var slotIndex = 0;
        if (SaveDataLookup.TryGetCaravan(saveData, caravanId, out var caravan)
            && caravan != null)
        {
            slotIndex = Mathf.Max(0, caravan.slotIndex);
        }
        return palette[slotIndex % palette.Length];
    }
}
