using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using ND.Economy;

public sealed class PaymentPanelController : MonoBehaviour
{
    [Header("Payment presentation")]
    [SerializeField] private Sprite receiptBackgroundSprite;
    [SerializeField] private Sprite stampSprite;
    [SerializeField] private TMP_FontAsset uiFont;
    [SerializeField, Min(0.05f)] private float stampDuration = 0.48f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip stampSound;
    [SerializeField] private AudioClip completeSound;
    [SerializeField] private UnityEvent onPaymentCompleted = new UnityEvent();

    [SerializeField, HideInInspector] private RectTransform panelRect;
    [SerializeField, HideInInspector] private CanvasGroup canvasGroup;
    [SerializeField, HideInInspector] private TMP_Text routeText;
    [SerializeField, HideInInspector] private TMP_Text elapsedText;
    [SerializeField, HideInInspector] private TMP_Text summaryText;
    [SerializeField, HideInInspector] private Button stampButton;
    [SerializeField, HideInInspector] private RectTransform stampRect;
    [SerializeField, HideInInspector] private RectTransform paperRect;
    private Coroutine animationRoutine;
    private EconomyM1SettlementViewData currentViewData;
    private bool currentCanConfirm;

    public UnityEvent PaymentCompleted => onPaymentCompleted;

    private void Awake()
    {
        if (!HasRequiredReferences())
        {
            Debug.LogError(
                "[PaymentPanel] Prefab references are missing. Regenerate or repair the prefab in the Editor.",
                this);
            enabled = false;
            return;
        }
        WireViewEvents();
    }

    private bool HasRequiredReferences()
    {
        return panelRect != null && canvasGroup != null && paperRect != null
            && routeText != null && elapsedText != null && summaryText != null
            && stampButton != null && stampRect != null && audioSource != null;
    }

    public void Show(EconomyM1SettlementViewData viewData, RouteData route, float elapsedSeconds)
    {
        string title = route == null ? "출발지 → 목적지" : $"{DisplayTown(route.FromTownName, route.FromTownId)} → {DisplayTown(route.ToTownName, route.ToTownId)}";
        Show(viewData, title, elapsedSeconds, viewData != null && viewData.Success);
    }

    public void Show(EconomyM1SettlementViewData viewData, string routeTitle, float elapsedSeconds, bool canConfirm)
    {
        if (!enabled || !HasRequiredReferences())
            return;
        currentViewData = viewData;
        routeText.text = string.IsNullOrWhiteSpace(routeTitle) ? "출발지 → 목적지" : routeTitle;
        elapsedText.text = $"소요 시간  {FormatElapsed(elapsedSeconds)}";
        summaryText.text = BuildSummary(viewData);
        currentCanConfirm = canConfirm && viewData != null && viewData.Settlement != null;
        stampButton.interactable = currentCanConfirm;
        stampRect.localScale = Vector3.one;
        stampRect.localRotation = Quaternion.identity;
        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        PlaySound(openSound);
    }

    public void ConfirmPayment()
    {
        if (currentViewData == null || !currentCanConfirm || currentViewData.Settlement == null)
            return;
        stampButton.interactable = false;
        PlaySound(stampSound);
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);
        animationRoutine = StartCoroutine(StampRoutine());
    }

    private IEnumerator StampRoutine()
    {
        // TODO(DOTween): Implement the receipt shake and stamp impact sequence here.
        // Suggested targets: paperRect.DOShakeAnchorPos(...), stampRect.DOPunchScale(...).
        // Keep the sequence independent of Time.timeScale and invoke completion after it finishes.
        yield return null;

        PlaySound(completeSound);
        onPaymentCompleted.Invoke();
        currentViewData = null;
        gameObject.SetActive(false);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }

    private static string BuildSummary(EconomyM1SettlementViewData viewData)
    {
        if (viewData == null || viewData.Settlement == null)
            return "결제할 정산 데이터가 없습니다.";

        SettlementBreakdown settlement = viewData.Settlement;
        long foodCost = SumEntry(settlement, SettlementEntryType.FoodCost);
        long mercenaryCost = SumEntry(settlement, SettlementEntryType.MercenaryCost);
        long repairCost = SumEntry(settlement, SettlementEntryType.CartRepairCost);
        return
            $"먹이 비용       -{foodCost:N0} G\n" +
            $"용병 고용 비용  -{mercenaryCost:N0} G\n" +
            $"마차 수리 비용  -{repairCost:N0} G\n\n" +
            $"총 사용 금액    {settlement.TotalExpense:N0} G\n" +
            $"총 수익         {settlement.TotalRevenue:N0} G\n" +
            $"순이익          {(settlement.NetProfit >= 0 ? "+" : string.Empty)}{settlement.NetProfit:N0} G";
    }

    private static long SumEntry(SettlementBreakdown settlement, SettlementEntryType type)
    {
        return settlement.Entries?
            .Where(entry => entry != null && entry.EntryType == type)
            .Sum(entry => entry.Amount) ?? 0L;
    }

    private static string DisplayTown(string displayName, string id)
    {
        return string.IsNullOrWhiteSpace(displayName) ? id : displayName;
    }

    private static string FormatElapsed(float seconds)
    {
        TimeSpan time = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
        return time.TotalHours >= 1d
            ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
    }

#if UNITY_EDITOR
    private void BuildViewIfNeeded()
    {
        if (panelRect != null && paperRect != null && stampButton != null)
            return;

        panelRect = transform as RectTransform;
        if (panelRect == null)
            panelRect = gameObject.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        EconomySettlementUiFactory.EnsureImage(gameObject, new Color(0.08f, 0.07f, 0.06f, 0.72f));
        canvasGroup = EconomySettlementUiFactory.EnsureCanvasGroup(gameObject);
        paperRect = EconomySettlementUiFactory.CreateRect(panelRect, "PaymentPaper", new Vector2(440f, 660f), Vector2.zero);
        Image paperImage = EconomySettlementUiFactory.EnsureImage(paperRect.gameObject, Color.white);
        Shadow paperShadow = paperRect.gameObject.AddComponent<Shadow>();
        paperShadow.effectColor = new Color(0f, 0f, 0f, 0.78f);
        paperShadow.effectDistance = new Vector2(12f, -12f);
        if (receiptBackgroundSprite != null)
        {
            paperImage.sprite = receiptBackgroundSprite;
            paperImage.preserveAspect = true;
            paperImage.useSpriteMesh = true;
        }

        routeText = EconomySettlementUiFactory.CreateText(paperRect, "RouteText", 34f, FontStyles.Bold,
            new Vector2(310f, 44f), new Vector2(0f, 238f), TextAlignmentOptions.Center, new Color32(43, 39, 37, 255));
        elapsedText = EconomySettlementUiFactory.CreateText(paperRect, "ElapsedText", 20f, FontStyles.Normal,
            new Vector2(310f, 30f), new Vector2(0f, 202f), TextAlignmentOptions.Center, new Color32(90, 82, 77, 255));
        summaryText = EconomySettlementUiFactory.CreateText(paperRect, "SummaryText", 25f, FontStyles.Normal,
            new Vector2(300f, 360f), new Vector2(0f, -30f), TextAlignmentOptions.TopLeft, new Color32(45, 41, 39, 255));

        stampButton = EconomySettlementUiFactory.CreateButton(paperRect, "StampButton", string.Empty, new Vector2(108f, 108f), new Vector2(70f, -215f));
        WireViewEvents();
        stampRect = stampButton.transform as RectTransform;
        Image stampImage = stampButton.GetComponent<Image>();
        stampImage.color = Color.white;
        if (stampSprite != null)
        {
            stampImage.sprite = stampSprite;
            stampImage.preserveAspect = true;
            stampImage.useSpriteMesh = true;
        }
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        EconomySettlementUiFactory.ApplyFont(panelRect, uiFont);
    }
#endif

    private void WireViewEvents()
    {
        if (stampButton == null)
            return;
        stampButton.onClick.RemoveListener(ConfirmPayment);
        stampButton.onClick.AddListener(ConfirmPayment);
    }

}
