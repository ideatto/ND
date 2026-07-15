using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class MercenaryHirePanelController : MonoBehaviour
{
    [Serializable]
    public sealed class MercenaryOffer
    {
        public string displayName = "용병";
        [Min(0)] public int combatPower;
        [Min(0)] public long hireCost;
    }

    [SerializeField] private int expectedRisk = 75;
    [SerializeField] private MercenaryOffer[] offers = Array.Empty<MercenaryOffer>();
    [SerializeField] private UnityEvent onConfirmed = new UnityEvent();

    private static readonly Color32 FrameColor = new Color32(184, 177, 177, 255);
    private static readonly Color32 ContentColor = new Color32(242, 239, 237, 255);
    private static readonly Color32 DarkColor = new Color32(105, 94, 93, 255);
    private static readonly Color32 DetailColor = new Color32(196, 169, 169, 255);
    private static readonly Color32 ActionColor = new Color32(255, 232, 70, 255);
    private static readonly Color32 TextColor = new Color32(45, 40, 39, 255);

    private CargoLoadingPanelController cargoPanel;
    private RectTransform panelRect;
    private CanvasGroup canvasGroup;
    private readonly List<Button> offerButtons = new List<Button>();
    private TMP_Text riskSummaryText;
    private TMP_Text moneySummaryText;
    private Button confirmButton;
    private int selectedOfferIndex = -1;
    private long availableGold;
    private Coroutine animationRoutine;
    private bool built;

    public int SelectedCombatPower => selectedOfferIndex < 0 ? 0 : offers[selectedOfferIndex].combatPower;
    public long SelectedHireCost => selectedOfferIndex < 0 ? 0L : offers[selectedOfferIndex].hireCost;
    public bool CanConfirm => SelectedHireCost <= availableGold;

    public static MercenaryHirePanelController CreateForCargo(CargoLoadingPanelController cargo)
    {
        GameObject go = new GameObject("MercenaryHirePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        go.SetActive(false);
        go.transform.SetParent(cargo.transform.parent, false);

        MercenaryHirePanelController controller = go.AddComponent<MercenaryHirePanelController>();
        controller.Initialize(cargo);
        return controller;
    }

    public void Initialize(CargoLoadingPanelController cargo)
    {
        cargoPanel = cargo;
        panelRect = transform as RectTransform;
        canvasGroup = GetComponent<CanvasGroup>();
        BuildIfNeeded();
        gameObject.SetActive(false);
    }

    public void Show(long gold)
    {
        BuildIfNeeded();
        availableGold = Math.Max(0L, gold);
        selectedOfferIndex = -1;
        Refresh();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        panelRect.localScale = Vector3.zero;
        canvasGroup.alpha = 0f;
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);
        animationRoutine = StartCoroutine(AnimateVisible(true, null));
    }

    private void Awake()
    {
        panelRect = transform as RectTransform;
        canvasGroup = GetComponent<CanvasGroup>();
        BuildIfNeeded();
    }

    private void BuildIfNeeded()
    {
        if (built || panelRect == null)
            return;

        built = true;
        EnsureDefaultOffers();

        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(1176f, 740f);
        GetComponent<Image>().color = FrameColor;

        RectTransform header = CreatePanel(transform, "Header", new Vector2(1176f, 104f), Vector2.zero, FrameColor);
        RectTransform titleBackground = CreatePanel(header, "TitleBackground", new Vector2(460f, 76f), new Vector2(358f, -14f), new Color32(248, 246, 243, 255));
        CreateText(titleBackground, "TitleText", "용병 고용", 34f, new Vector2(440f, 68f), new Vector2(230f, -38f), TextAlignmentOptions.Center, FontStyles.Bold);
        Button back = CreateButton(header, "BackButton", "뒤로", new Vector2(116f, 56f), new Vector2(76f, -52f), DarkColor);
        Button cancel = CreateButton(header, "CloseButton", "무역 취소", new Vector2(116f, 56f), new Vector2(1100f, -52f), new Color32(190, 87, 87, 255));
        back.onClick.AddListener(BackToCargo);
        cancel.onClick.AddListener(CancelTrade);

        RectTransform cardArea = CreatePanel(transform, "MercenaryCardArea", new Vector2(1116f, 412f), new Vector2(32f, -104f), DarkColor);
        for (int i = 0; i < offers.Length; i++)
        {
            int index = i;
            float x = 18f + i * 218f;
            Button card = CreateOfferCard(cardArea, offers[i], new Vector2(x, -20f));
            card.onClick.AddListener(() => ToggleOffer(index));
            offerButtons.Add(card);
        }

        RectTransform summary = CreatePanel(transform, "HireSummaryArea", new Vector2(900f, 172f), new Vector2(32f, -536f), ContentColor);
        riskSummaryText = CreateText(summary, "RiskSummaryText", string.Empty, 23f, new Vector2(840f, 48f), new Vector2(450f, -48f), TextAlignmentOptions.Left, FontStyles.Bold);
        moneySummaryText = CreateText(summary, "MoneySummaryText", string.Empty, 23f, new Vector2(840f, 48f), new Vector2(450f, -112f), TextAlignmentOptions.Left, FontStyles.Bold);

        RectTransform action = CreatePanel(transform, "ActionArea", new Vector2(184f, 172f), new Vector2(964f, -536f), ContentColor);
        confirmButton = CreateButton(action, "ConfirmButton", "확인", new Vector2(148f, 52f), new Vector2(92f, -118f), ActionColor);
        confirmButton.onClick.AddListener(Confirm);
        CreateText(action, "OptionalText", "용병 고용은 선택입니다", 17f, new Vector2(160f, 56f), new Vector2(92f, -48f), TextAlignmentOptions.Center, FontStyles.Normal);

        Refresh();
    }

    private void EnsureDefaultOffers()
    {
        if (offers != null && offers.Length > 0)
            return;

        offers = new[]
        {
            NewOffer("정찰병", 0, 0),
            NewOffer("가도 경비대", 10, 120),
            NewOffer("베테랑 2인조", 40, 420),
            NewOffer("강철 용병단", 100, 980),
            NewOffer("단독 여행", 0, 0)
        };
    }

    private static MercenaryOffer NewOffer(string name, int power, long cost)
    {
        return new MercenaryOffer { displayName = name, combatPower = power, hireCost = cost };
    }

    private Button CreateOfferCard(RectTransform parent, MercenaryOffer offer, Vector2 position)
    {
        Button card = CreateButton(parent, offer.displayName, string.Empty, new Vector2(200f, 370f), position, ContentColor, true);
        RectTransform cardRect = card.transform as RectTransform;
        CreatePanel(cardRect, "Portrait", new Vector2(168f, 210f), new Vector2(16f, -16f), new Color32(255, 255, 255, 255));
        CreateText(cardRect, "NameText", offer.displayName, 21f, new Vector2(168f, 54f), new Vector2(100f, -258f), TextAlignmentOptions.Center, FontStyles.Bold);
        RectTransform powerStrip = CreatePanel(cardRect, "PowerStrip", new Vector2(168f, 44f), new Vector2(16f, -286f), DetailColor);
        CreateText(powerStrip, "PowerText", $"전투력  {offer.combatPower}", 19f, new Vector2(160f, 40f), new Vector2(84f, -22f), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateText(cardRect, "CostText", offer.hireCost == 0 ? "무료" : $"{offer.hireCost:N0} G", 18f, new Vector2(168f, 34f), new Vector2(100f, -348f), TextAlignmentOptions.Center, FontStyles.Normal);
        return card;
    }

    private void ToggleOffer(int index)
    {
        selectedOfferIndex = selectedOfferIndex == index ? -1 : index;
        Refresh();
    }

    private void Refresh()
    {
        if (riskSummaryText != null)
            riskSummaryText.text = $"예상 위험도  {expectedRisk}   /   고용 전투력  {SelectedCombatPower}";
        if (moneySummaryText != null)
            moneySummaryText.text = $"소지 금액  {availableGold:N0} G   /   고용 비용  {SelectedHireCost:N0} G";

        for (int i = 0; i < offerButtons.Count; i++)
        {
            Image image = offerButtons[i].targetGraphic as Image;
            if (image != null)
                image.color = i == selectedOfferIndex ? new Color32(255, 235, 150, 255) : ContentColor;
        }

        if (confirmButton != null)
            confirmButton.interactable = CanConfirm;
    }

    private void BackToCargo()
    {
        Hide(() => cargoPanel?.ReturnFromMercenaryHire());
    }

    private void CancelTrade()
    {
        Hide(() => cargoPanel?.CancelTradeFromMercenaryHire());
    }

    private void Confirm()
    {
        if (!CanConfirm)
            return;
        Hide(() => onConfirmed.Invoke());
    }

    private void Hide(Action completed)
    {
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);
        animationRoutine = StartCoroutine(AnimateVisible(false, () =>
        {
            completed?.Invoke();
            gameObject.SetActive(false);
        }));
    }

    private IEnumerator AnimateVisible(bool visible, Action completed)
    {
        Vector3 startScale = panelRect.localScale;
        Vector3 endScale = visible ? Vector3.one : Vector3.zero;
        float startAlpha = canvasGroup.alpha;
        float endAlpha = visible ? 1f : 0f;
        const float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            panelRect.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
            yield return null;
        }

        panelRect.localScale = endScale;
        canvasGroup.alpha = endAlpha;
        completed?.Invoke();
    }

    private static RectTransform CreatePanel(Transform parent, string name, Vector2 size, Vector2 position, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        go.GetComponent<Image>().color = color;
        return rect;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 size, Vector2 position, Color color, bool topLeft = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = topLeft ? new Vector2(0f, 1f) : new Vector2(0f, 1f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = topLeft ? new Vector2(0f, 1f) : new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = go.GetComponent<Image>();
        image.color = color;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        if (!string.IsNullOrEmpty(label))
            CreateText(rect, "Label", label, 20f, size, Vector2.zero, TextAlignmentOptions.Center, FontStyles.Bold, true);
        return button;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value, float size, Vector2 rectSize, Vector2 position, TextAlignmentOptions alignment, FontStyles style, bool centeredAnchor = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        RectTransform rect = text.rectTransform;
        rect.anchorMin = centeredAnchor ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = rectSize;
        rect.anchoredPosition = position;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = TextColor;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }
}
