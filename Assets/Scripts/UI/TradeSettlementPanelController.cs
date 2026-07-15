using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ND.Economy;
using ND.Framework;

public sealed class TradeSettlementPanelController : MonoBehaviour, IPointerClickHandler, ISettlementView
{
    [Header("Presentation")]
    [SerializeField] private Sprite receiptBackgroundSprite;
    [SerializeField] private TMP_FontAsset uiFont;
    [SerializeField, Min(0.001f)] private float characterInterval = 0.018f;
    [SerializeField, Min(0.01f)] private float slideDuration = 0.28f;
    [SerializeField] private PaymentPanelController paymentPanel;
    [SerializeField] private UnityEvent onSettlementShown = new UnityEvent();
    [SerializeField] private UnityEvent onPaymentRequested = new UnityEvent();
    [SerializeField] private UnityEvent onPaymentCompleted = new UnityEvent();

    [SerializeField, HideInInspector] private RectTransform panelRect;
    [SerializeField, HideInInspector] private CanvasGroup canvasGroup;
    [SerializeField, HideInInspector] private TMP_Text routeText;
    [SerializeField, HideInInspector] private TMP_Text elapsedText;
    [SerializeField, HideInInspector] private TMP_Text receiptText;
    [SerializeField, HideInInspector] private TMP_Text errorText;
    [SerializeField, HideInInspector] private Button paymentButton;
    [SerializeField, HideInInspector] private RectTransform paperRect;
    private Coroutine presentationRoutine;
    private string completeReceipt = string.Empty;
    private bool typing;
    private EconomyM1SettlementViewData currentViewData;
    private RouteData currentRoute;
    private float currentElapsedSeconds;
    private bool currentCanClaim;
    private string currentRouteTitle = "출발지 → 목적지";
    private string currentFailureMessage = string.Empty;
    private bool paymentCompletionWired;

    public bool IsTyping => typing;
    public EconomyM1SettlementViewData CurrentViewData => currentViewData;

    private void Awake()
    {
        if (!HasRequiredReferences())
        {
            Debug.LogError(
                "[TradeSettlementPanel] Prefab references are missing. Regenerate or repair the prefab in the Editor.",
                this);
            enabled = false;
            return;
        }
        WireViewEvents();
    }

    private bool HasRequiredReferences()
    {
        return panelRect != null && canvasGroup != null && paperRect != null
            && routeText != null && elapsedText != null && receiptText != null
            && errorText != null && paymentButton != null;
    }

    public void Show(EconomyM1LoopResult result, RouteData route, float elapsedSeconds)
    {
        Show(EconomyM1SettlementViewAdapter.Create(result), route, elapsedSeconds);
    }

    public void Show(EconomyM1SettlementViewData viewData, RouteData route, float elapsedSeconds)
    {
        currentCanClaim = viewData != null && viewData.Success;
        currentFailureMessage = viewData != null && !viewData.Success ? viewData.ErrorCode : string.Empty;
        currentRouteTitle = BuildRouteTitle(route);
        ShowInternal(viewData, route, elapsedSeconds);
    }

    public void ShowSettlement(SettlementViewData viewData)
    {
        if (viewData == null) { ShowNoSettlement("No settlement result."); return; }
        currentCanClaim = viewData.CanClaim;
        currentFailureMessage = viewData.IsFailed ? $"실패 사유  {viewData.FailureReason}" : string.Empty;
        currentRouteTitle = ResolveFrameworkRouteTitle();
        var settlement = new SettlementBreakdown {
            TradeId = viewData.TradeId, TotalRevenue = viewData.Revenue,
            TotalExpense = viewData.Cost, GrossTradeProfit = viewData.Revenue - viewData.Cost,
            NetProfit = viewData.NetProfit
        };
        if (viewData.Revenue > 0) settlement.Entries.Add(new SettlementEntry { EntryType = SettlementEntryType.ItemSaleRevenue, Amount = viewData.Revenue, IsPositive = true, SourceId = "trade" });
        if (viewData.Cost > 0) settlement.Entries.Add(new SettlementEntry { EntryType = SettlementEntryType.ItemPurchaseCost, Amount = viewData.Cost, IsPositive = false, SourceId = "trade" });
        ShowInternal(new EconomyM1SettlementViewData { Success = true, Settlement = settlement }, null, viewData.TravelSeconds);
    }

    public void ShowNoSettlement(string reason)
    {
        currentCanClaim = false; currentFailureMessage = reason ?? string.Empty; currentRouteTitle = "출발지 → 목적지";
        ShowInternal(new EconomyM1SettlementViewData { Success = false, ErrorCode = currentFailureMessage }, null, 0f);
    }

    public void SetClaimInteractable(bool interactable)
    {
        currentCanClaim = interactable;
        if (paymentButton != null) paymentButton.interactable = interactable;
    }

    private void ShowInternal(EconomyM1SettlementViewData viewData, RouteData route, float elapsedSeconds)
    {
        if (!enabled || !HasRequiredReferences())
            return;
        currentViewData = viewData ?? new EconomyM1SettlementViewData
        {
            Success = false,
            ErrorCode = "NULL_SETTLEMENT_VIEW_DATA"
        };
        currentRoute = route;
        currentElapsedSeconds = Mathf.Max(0f, elapsedSeconds);
        completeReceipt = BuildReceipt(currentViewData);

        routeText.text = currentRouteTitle;
        elapsedText.text = $"소요 시간  {FormatElapsed(currentElapsedSeconds)}";
        receiptText.text = string.Empty;
        errorText.text = !string.IsNullOrEmpty(currentFailureMessage) ? currentFailureMessage
            : (currentViewData.Success ? string.Empty : $"실패 사유  {currentViewData.ErrorCode}");
        paymentButton.gameObject.SetActive(false);

        gameObject.SetActive(true);
        if (presentationRoutine != null)
            StopCoroutine(presentationRoutine);
        presentationRoutine = StartCoroutine(PresentRoutine());
    }

    public void Hide()
    {
        if (!gameObject.activeInHierarchy)
            return;
        if (presentationRoutine != null)
            StopCoroutine(presentationRoutine);
        presentationRoutine = StartCoroutine(HideRoutine());
    }

    public void CompleteTyping()
    {
        if (!typing)
            return;
        typing = false;
        receiptText.text = completeReceipt;
        paymentButton.gameObject.SetActive(currentViewData != null && currentCanClaim);
        paymentButton.interactable = currentCanClaim;
    }

    public void OpenPayment()
    {
        if (typing || currentViewData == null || !currentCanClaim)
            return;

        if (paymentPanel == null)
        {
            Debug.LogError(
                "[TradeSettlementPanel] PaymentPanel reference is missing. Assign it in the integrated UI prefab.",
                this);
            return;
        }

        WirePaymentCompletion();

        paymentPanel.Show(currentViewData, currentRouteTitle, currentElapsedSeconds, currentCanClaim);
        onPaymentRequested.Invoke();
        gameObject.SetActive(false);
    }

    private void WirePaymentCompletion()
    {
        if (paymentPanel == null || paymentCompletionWired)
            return;
        paymentPanel.PaymentCompleted.AddListener(onPaymentCompleted.Invoke);
        paymentCompletionWired = true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        CompleteTyping();
    }

    private IEnumerator PresentRoutine()
    {
        typing = true;
        Vector2 destination = Vector2.zero;
        Vector2 start = new Vector2(0f, panelRect.rect.height + 120f);
        panelRect.anchoredPosition = start;
        canvasGroup.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            panelRect.anchoredPosition = Vector2.LerpUnclamped(start, destination, eased);
            canvasGroup.alpha = eased;
            yield return null;
        }

        panelRect.anchoredPosition = destination;
        canvasGroup.alpha = 1f;
        onSettlementShown.Invoke();

        for (int length = 1; length <= completeReceipt.Length && typing; length++)
        {
            receiptText.text = completeReceipt.Substring(0, length);
            yield return new WaitForSecondsRealtime(characterInterval);
        }

        if (typing)
        {
            typing = false;
            receiptText.text = completeReceipt;
            paymentButton.gameObject.SetActive(currentViewData.Success);
        }
    }


    private IEnumerator HideRoutine()
    {
        Vector2 start = panelRect.anchoredPosition;
        Vector2 destination = new Vector2(0f, panelRect.rect.height + 120f);
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            panelRect.anchoredPosition = Vector2.LerpUnclamped(start, destination, t);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            yield return null;
        }

        gameObject.SetActive(false);
    }

    private string BuildReceipt(EconomyM1SettlementViewData viewData)
    {
        if (viewData == null || viewData.Settlement == null)
            return "정산 데이터가 없습니다.";

        SettlementBreakdown settlement = viewData.Settlement;
        StringBuilder builder = new StringBuilder(512);
        AppendTradeItems(builder, viewData, SettlementEntryType.ItemPurchaseCost, "구매 상품", false);
        AppendTradeItems(builder, viewData, SettlementEntryType.ItemSaleRevenue, "판매 상품", true);
        AppendEntries(builder, settlement.Entries, SettlementEntryType.EventProfit, "주요 이벤트 수익");
        AppendEntries(builder, settlement.Entries, SettlementEntryType.EventLoss, "주요 이벤트 손실");

        AppendSingle(builder, settlement.Entries, SettlementEntryType.FoodCost, "먹이 비용");
        AppendSingle(builder, settlement.Entries, SettlementEntryType.MercenaryCost, "용병 고용 비용");
        AppendSingle(builder, settlement.Entries, SettlementEntryType.CartRepairCost, "마차 수리 비용");
        AppendSingle(builder, settlement.Entries, SettlementEntryType.LostItemValue, "상품 손실");

        builder.AppendLine();
        builder.AppendLine($"총 사용 금액  {settlement.TotalExpense:N0} G");
        builder.AppendLine($"총 수익       {settlement.TotalRevenue:N0} G");
        builder.AppendLine($"순이익        {Signed(settlement.NetProfit)} G");
        builder.AppendLine($"성장 포인트   +{settlement.DevelopmentCurrencyReward:N0}");
        return builder.ToString().TrimEnd();
    }

    private static void AppendEntries(
        StringBuilder builder,
        IEnumerable<SettlementEntry> entries,
        SettlementEntryType type,
        string heading)
    {
        List<SettlementEntry> matches = entries?.Where(entry => entry != null && entry.EntryType == type).ToList()
            ?? new List<SettlementEntry>();
        if (matches.Count == 0)
            return;

        builder.AppendLine(heading);
        foreach (SettlementEntry entry in matches)
            builder.AppendLine($"  {DisplaySource(entry.SourceId)}  {(entry.IsPositive ? "+" : "-")}{entry.Amount:N0} G");
        builder.AppendLine();
    }

    private static void AppendTradeItems(
        StringBuilder builder,
        EconomyM1SettlementViewData viewData,
        SettlementEntryType type,
        string heading,
        bool useSellPrice)
    {
        List<SettlementEntry> matches = viewData.Settlement.Entries?
            .Where(entry => entry != null && entry.EntryType == type)
            .ToList() ?? new List<SettlementEntry>();
        if (matches.Count == 0)
            return;

        builder.AppendLine(heading);
        foreach (SettlementEntry entry in matches)
        {
            bool hasMatchingPrice = viewData.PriceResult != null
                && string.Equals(viewData.PriceResult.TradeItemId, entry.SourceId, StringComparison.Ordinal);
            if (hasMatchingPrice)
            {
                long unitPrice = useSellPrice
                    ? viewData.PriceResult.UnitSellPrice
                    : viewData.PriceResult.UnitBuyPrice;
                builder.AppendLine(
                    $"  {DisplaySource(entry.SourceId)} x{viewData.PriceResult.Quantity:N0}  " +
                    $"@ {unitPrice:N0} G  = {(entry.IsPositive ? "+" : "-")}{entry.Amount:N0} G");
            }
            else
            {
                builder.AppendLine($"  {DisplaySource(entry.SourceId)}  {(entry.IsPositive ? "+" : "-")}{entry.Amount:N0} G");
            }
        }
        builder.AppendLine();
    }

    private static void AppendSingle(
        StringBuilder builder,
        IEnumerable<SettlementEntry> entries,
        SettlementEntryType type,
        string label)
    {
        long amount = entries?
            .Where(entry => entry != null && entry.EntryType == type)
            .Sum(entry => entry.Amount) ?? 0L;
        builder.AppendLine($"{label,-12} -{amount:N0} G");
    }

    private static string DisplaySource(string sourceId)
    {
        return string.IsNullOrWhiteSpace(sourceId) ? "항목" : sourceId;
    }

    private static string Signed(long amount)
    {
        return amount >= 0 ? $"+{amount:N0}" : $"-{Math.Abs(amount):N0}";
    }

    private static string BuildRouteTitle(RouteData route)
    {
        if (route == null)
            return "출발지 → 목적지";
        string from = string.IsNullOrWhiteSpace(route.FromTownName) ? route.FromTownId : route.FromTownName;
        string to = string.IsNullOrWhiteSpace(route.ToTownName) ? route.ToTownId : route.ToTownName;
        return $"{from} → {to}";
    }

    private static string ResolveFrameworkRouteTitle()
    {
        var root = FrameworkRoot.Instance; var save = root != null ? root.CurrentSaveData : null;
        string routeId = save?.tradeProgress != null ? save.tradeProgress.activeRouteId : string.Empty;
        if (root?.SharedGameData == null || !root.SharedGameData.TryGetRoute(routeId, out SharedRouteDefinition route)) return "출발지 → 목적지";
        string from = root.SharedGameData.TryGetTown(route.FromTownId, out SharedTownDefinition a) ? a.DisplayName : route.FromTownId;
        string to = root.SharedGameData.TryGetTown(route.ToTownId, out SharedTownDefinition b) ? b.DisplayName : route.ToTownId;
        return $"{from} → {to}";
    }

    private static string FormatElapsed(float seconds)
    {
        TimeSpan time = TimeSpan.FromSeconds(seconds);
        return time.TotalHours >= 1d
            ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
    }

#if UNITY_EDITOR
    private void BuildViewIfNeeded()
    {
        if (panelRect != null && paperRect != null && paymentButton != null)
            return;

        panelRect = transform as RectTransform;
        if (panelRect == null)
            panelRect = gameObject.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image frame = EconomySettlementUiFactory.EnsureImage(gameObject, new Color(0.08f, 0.07f, 0.06f, 0.72f));
        frame.raycastTarget = true;
        canvasGroup = EconomySettlementUiFactory.EnsureCanvasGroup(gameObject);

        paperRect = EconomySettlementUiFactory.CreateRect(panelRect, "ReceiptPaper", new Vector2(440f, 660f), Vector2.zero);
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

        RectTransform viewport = EconomySettlementUiFactory.CreateRect(paperRect, "ReceiptViewport", new Vector2(320f, 390f), new Vector2(0f, -20f));
        EconomySettlementUiFactory.EnsureImage(viewport.gameObject, new Color(1f, 1f, 1f, 0.01f));
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        RectTransform content = EconomySettlementUiFactory.CreateRect(viewport, "ReceiptContent", new Vector2(310f, 700f), Vector2.zero);
        content.anchorMin = new Vector2(0.5f, 1f);
        content.anchorMax = new Vector2(0.5f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = new Vector2(0f, 0f);
        receiptText = EconomySettlementUiFactory.CreateText(content, "ReceiptText", 22f, FontStyles.Normal,
            new Vector2(300f, 690f), Vector2.zero, TextAlignmentOptions.TopLeft, new Color32(45, 41, 39, 255));
        receiptText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        receiptText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        receiptText.rectTransform.pivot = new Vector2(0.5f, 1f);
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        errorText = EconomySettlementUiFactory.CreateText(paperRect, "ErrorText", 18f, FontStyles.Bold,
            new Vector2(300f, 30f), new Vector2(0f, -282f), TextAlignmentOptions.Left, new Color32(184, 58, 52, 255));
        paymentButton = EconomySettlementUiFactory.CreateButton(paperRect, "PaymentButton", "결제", new Vector2(120f, 52f), new Vector2(70f, -215f));
        WireViewEvents();
        EconomySettlementUiFactory.ApplyFont(panelRect, uiFont);
    }
#endif

    private void WireViewEvents()
    {
        if (paymentButton == null)
            return;
        paymentButton.onClick.RemoveListener(OpenPayment);
        paymentButton.onClick.AddListener(OpenPayment);
    }

}

#if UNITY_EDITOR
internal static class EconomySettlementUiFactory
{
    public static void ApplyFont(Transform root, TMP_FontAsset font)
    {
        if (root == null || font == null)
            return;
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            text.font = font;
    }

    public static GameObject CreatePanelRoot(Transform parent, string objectName)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);
        return root;
    }

    public static RectTransform CreateRect(Transform parent, string objectName, Vector2 size, Vector2 position)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    public static TMP_Text CreateText(
        Transform parent,
        string objectName,
        float fontSize,
        FontStyles style,
        Vector2 size,
        Vector2 position,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        text.rectTransform.sizeDelta = size;
        text.rectTransform.anchoredPosition = position;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    public static Button CreateButton(Transform parent, string objectName, string label, Vector2 size, Vector2 position)
    {
        RectTransform rect = CreateRect(parent, objectName, size, position);
        Image image = EnsureImage(rect.gameObject, new Color32(95, 183, 113, 255));
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = CreateText(rect, "Label", 23f, FontStyles.Bold, size, Vector2.zero,
            TextAlignmentOptions.Center, new Color32(24, 45, 28, 255));
        text.text = label;
        return button;
    }

    public static Image EnsureImage(GameObject target, Color color)
    {
        Image image = target.GetComponent<Image>();
        if (image == null)
            image = target.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static CanvasGroup EnsureCanvasGroup(GameObject target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        return group == null ? target.AddComponent<CanvasGroup>() : group;
    }
}
#endif
