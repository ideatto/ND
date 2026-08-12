using System;
using System.Collections;
using System.Collections.Generic;
using ND.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CaravanCombatSequencePanel : MonoBehaviour
{
    [Header("View")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform cardRoot;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image stageImage;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Transform vfxRoot;

    [Header("Stage Sprites (Optional)")]
    [SerializeField] private Sprite encounterSprite;
    [SerializeField] private Sprite victorySprite;
    [SerializeField] private Sprite defeatSprite;

    [Header("Stage VFX Prefabs (Optional)")]
    [SerializeField] private GameObject encounterVfxPrefab;
    [SerializeField] private GameObject victoryVfxPrefab;
    [SerializeField] private GameObject defeatVfxPrefab;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float refreshInterval = 0.1f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.2f;
    [SerializeField, Min(0f)] private float encounterDuration = 0.6f;
    [SerializeField, Min(0f)] private float outcomeTransitionDuration = 0.2f;
    [SerializeField, Min(0f)] private float outcomeDuration = 1.5f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.3f;

    [Header("Stage Colors")]
    [SerializeField] private Color encounterColor = new Color(0.18f, 0.16f, 0.13f, 0.94f);
    [SerializeField] private Color victoryColor = new Color(0.18f, 0.42f, 0.20f, 0.95f);
    [SerializeField] private Color defeatColor = new Color(0.48f, 0.12f, 0.12f, 0.95f);

    private readonly Queue<CombatPresentation> presentationQueue =
        new Queue<CombatPresentation>();
    private readonly Dictionary<string, Queue<CaravanActivityLogEntrySaveData>>
        pendingEncounters =
            new Dictionary<string, Queue<CaravanActivityLogEntrySaveData>>();

    private ND.Framework.SaveData observedSaveData;
    private Coroutine playbackCoroutine;
    private long lastObservedSequence;
    private float nextRefreshTime;
    private Vector2 initialCardPosition;
    private GameObject activeStageVfx;

    public Sprite EncounterSprite
    {
        get => encounterSprite;
        set => encounterSprite = value;
    }

    public Sprite VictorySprite
    {
        get => victorySprite;
        set => victorySprite = value;
    }

    public Sprite DefeatSprite
    {
        get => defeatSprite;
        set => defeatSprite = value;
    }

    public GameObject EncounterVfxPrefab
    {
        get => encounterVfxPrefab;
        set => encounterVfxPrefab = value;
    }

    public GameObject VictoryVfxPrefab
    {
        get => victoryVfxPrefab;
        set => victoryVfxPrefab = value;
    }

    public GameObject DefeatVfxPrefab
    {
        get => defeatVfxPrefab;
        set => defeatVfxPrefab = value;
    }

    private void Awake()
    {
        if (cardRoot != null)
        {
            initialCardPosition = cardRoot.anchoredPosition;
        }
        HideImmediately();
    }

    private void OnEnable()
    {
        FrameworkEvents.RouteCombatResolved += OnRouteCombatResolved;
        observedSaveData = GetCurrentSaveData();
        lastObservedSequence = GetLatestSequence(observedSaveData);
        nextRefreshTime = Time.unscaledTime;
        presentationQueue.Clear();
        pendingEncounters.Clear();
        HideImmediately();
    }

    private void OnDisable()
    {
        FrameworkEvents.RouteCombatResolved -= OnRouteCombatResolved;
        if (playbackCoroutine != null)
        {
            StopCoroutine(playbackCoroutine);
            playbackCoroutine = null;
        }
        presentationQueue.Clear();
        pendingEncounters.Clear();
        HideImmediately();
    }

    private void OnRouteCombatResolved(string caravanId, bool victory)
    {
        var saveData = GetCurrentSaveData();
        observedSaveData = saveData;
        // The committed save already contains the encounter/outcome pair. Advance the polling
        // cursor so the fallback scanner cannot enqueue the same presentation a second time.
        lastObservedSequence = GetLatestSequence(saveData);
        pendingEncounters.Clear();
        presentationQueue.Enqueue(new CombatPresentation(
            ResolveCaravanName(saveData, caravanId),
            victory));
        if (playbackCoroutine == null && isActiveAndEnabled)
        {
            playbackCoroutine = StartCoroutine(PlayQueuedPresentations());
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }
        nextRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, refreshInterval);

        var currentSaveData = GetCurrentSaveData();
        if (!ReferenceEquals(observedSaveData, currentSaveData))
        {
            // Trade transactions replace FrameworkRoot.CurrentSaveData with a committed clone.
            // Keep the last sequence actually consumed by this panel; adopting the clone's latest
            // sequence here would incorrectly mark the newly committed combat logs as already seen.
            observedSaveData = currentSaveData;
        }

        ScanNewCombatLogs(currentSaveData);
        if (playbackCoroutine == null && presentationQueue.Count > 0)
        {
            playbackCoroutine = StartCoroutine(PlayQueuedPresentations());
        }
    }

    private void ScanNewCombatLogs(ND.Framework.SaveData saveData)
    {
        var logs = saveData?.caravanActivityLogs;
        if (logs == null)
        {
            return;
        }

        for (var index = 0; index < logs.Count; index++)
        {
            var entry = logs[index];
            if (entry == null || entry.sequence <= lastObservedSequence)
            {
                continue;
            }

            lastObservedSequence = Math.Max(lastObservedSequence, entry.sequence);
            var key = BuildCombatKey(entry);
            if (entry.eventType == CaravanActivityLogType.CombatEncounter)
            {
                if (!pendingEncounters.TryGetValue(key, out var encounters))
                {
                    encounters = new Queue<CaravanActivityLogEntrySaveData>();
                    pendingEncounters.Add(key, encounters);
                }
                encounters.Enqueue(entry);
                continue;
            }

            if (entry.eventType != CaravanActivityLogType.CombatVictory
                && entry.eventType != CaravanActivityLogType.CombatDefeat)
            {
                continue;
            }

            if (!pendingEncounters.TryGetValue(key, out var pending)
                || pending.Count == 0)
            {
                continue;
            }

            var encounter = pending.Dequeue();
            if (pending.Count == 0)
            {
                pendingEncounters.Remove(key);
            }
            presentationQueue.Enqueue(new CombatPresentation(
                ResolveCaravanName(saveData, encounter.caravanId),
                entry.eventType == CaravanActivityLogType.CombatVictory));
        }
    }

    private IEnumerator PlayQueuedPresentations()
    {
        while (presentationQueue.Count > 0)
        {
            yield return PlayPresentation(presentationQueue.Dequeue());
        }
        playbackCoroutine = null;
    }

    private IEnumerator PlayPresentation(CombatPresentation presentation)
    {
        ApplyStage(
            encounterSprite,
            encounterColor,
            $"{presentation.CaravanName}이(가) 산적과 조우했습니다!",
            encounterVfxPrefab);
        if (cardRoot != null)
        {
            cardRoot.anchoredPosition = initialCardPosition;
            cardRoot.localScale = Vector3.one * 0.88f;
        }
        yield return FadeCanvas(0f, 1f, fadeInDuration, true);
        yield return WaitUnscaled(encounterDuration);

        yield return FadeCanvas(1f, 0f, outcomeTransitionDuration * 0.5f, false);
        ApplyStage(
            presentation.IsVictory ? victorySprite : defeatSprite,
            presentation.IsVictory ? victoryColor : defeatColor,
            presentation.IsVictory
                ? $"{presentation.CaravanName}이(가) 전투에서 승리했습니다!"
                : $"{presentation.CaravanName}이(가) 산적에게 패배했습니다.",
            presentation.IsVictory ? victoryVfxPrefab : defeatVfxPrefab);
        yield return FadeCanvas(0f, 1f, outcomeTransitionDuration * 0.5f, false);

        if (presentation.IsVictory)
        {
            yield return PlayVictoryMotion(outcomeDuration);
        }
        else
        {
            yield return PlayDefeatMotion(outcomeDuration);
        }

        yield return FadeCanvas(1f, 0f, fadeOutDuration, false);
        HideImmediately();
    }

    private void ApplyStage(
        Sprite sprite,
        Color backgroundColor,
        string message,
        GameObject vfxPrefab)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = backgroundColor;
        }
        if (stageImage != null)
        {
            stageImage.sprite = sprite;
            stageImage.enabled = sprite != null;
            stageImage.preserveAspect = true;
        }
        if (messageText != null)
        {
            messageText.text = message ?? string.Empty;
        }
        PlayStageVfx(vfxPrefab);
    }

    private void PlayStageVfx(GameObject prefab)
    {
        ClearStageVfx();
        if (prefab == null)
        {
            return;
        }

        var parent = vfxRoot != null ? vfxRoot : cardRoot;
        activeStageVfx = Instantiate(prefab, parent, false);
        activeStageVfx.transform.localPosition = Vector3.zero;
        activeStageVfx.transform.localRotation = Quaternion.identity;
    }

    private void ClearStageVfx()
    {
        if (activeStageVfx == null)
        {
            return;
        }

        activeStageVfx.SetActive(false);
        Destroy(activeStageVfx);
        activeStageVfx = null;
    }

    private IEnumerator FadeCanvas(float from, float to, float duration, bool scaleCard)
    {
        if (canvasGroup == null)
        {
            yield break;
        }

        var elapsed = 0f;
        duration = Mathf.Max(0f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var progress = Mathf.Clamp01(elapsed / duration);
            var eased = Mathf.SmoothStep(0f, 1f, progress);
            canvasGroup.alpha = Mathf.Lerp(from, to, eased);
            if (scaleCard && cardRoot != null)
            {
                cardRoot.localScale = Vector3.one * Mathf.Lerp(0.88f, 1f, eased);
            }
            yield return null;
        }
        canvasGroup.alpha = to;
        if (scaleCard && cardRoot != null)
        {
            cardRoot.localScale = Vector3.one;
        }
    }

    private IEnumerator PlayVictoryMotion(float duration)
    {
        var elapsed = 0f;
        duration = Mathf.Max(0f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var progress = Mathf.Clamp01(elapsed / duration);
            if (cardRoot != null)
            {
                var pulse = 1f + Mathf.Sin(progress * Mathf.PI) * 0.08f;
                cardRoot.localScale = Vector3.one * pulse;
            }
            yield return null;
        }
        ResetCardTransform();
    }

    private IEnumerator PlayDefeatMotion(float duration)
    {
        var elapsed = 0f;
        duration = Mathf.Max(0f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var progress = Mathf.Clamp01(elapsed / duration);
            if (cardRoot != null)
            {
                var strength = (1f - progress) * 14f;
                cardRoot.anchoredPosition = initialCardPosition
                    + Vector2.right * Mathf.Sin(progress * Mathf.PI * 12f) * strength;
            }
            yield return null;
        }
        ResetCardTransform();
    }

    private static IEnumerator WaitUnscaled(float duration)
    {
        var endTime = Time.unscaledTime + Mathf.Max(0f, duration);
        while (Time.unscaledTime < endTime)
        {
            yield return null;
        }
    }

    private void HideImmediately()
    {
        ClearStageVfx();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        ResetCardTransform();
    }

    private void ResetCardTransform()
    {
        if (cardRoot != null)
        {
            cardRoot.anchoredPosition = initialCardPosition;
            cardRoot.localScale = Vector3.one;
        }
    }

    private static ND.Framework.SaveData GetCurrentSaveData()
    {
        return FrameworkRoot.Instance != null
            ? FrameworkRoot.Instance.CurrentSaveData
            : null;
    }

    private static long GetLatestSequence(ND.Framework.SaveData saveData)
    {
        long latest = 0L;
        var logs = saveData?.caravanActivityLogs;
        if (logs == null)
        {
            return latest;
        }
        for (var index = 0; index < logs.Count; index++)
        {
            if (logs[index] != null)
            {
                latest = Math.Max(latest, logs[index].sequence);
            }
        }
        return latest;
    }

    private static string BuildCombatKey(CaravanActivityLogEntrySaveData entry)
    {
        return string.Concat(
            entry.caravanId ?? string.Empty,
            "|",
            entry.tradeId ?? string.Empty,
            "|",
            entry.routeEventId ?? string.Empty);
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

    private readonly struct CombatPresentation
    {
        public CombatPresentation(string caravanName, bool isVictory)
        {
            CaravanName = caravanName ?? "캐러반";
            IsVictory = isVictory;
        }

        public string CaravanName { get; }
        public bool IsVictory { get; }
    }
}
