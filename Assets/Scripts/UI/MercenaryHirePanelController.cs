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
        public string mercenaryId;
        public string displayName = "용병";
        public Sprite icon;   // 용병 SO의 아이콘(Portrait에 표시)
        [Min(0)] public int combatPower;
        [Min(0)] public long hireCost;
        public bool canHire = true;
    }

    [SerializeField] private int expectedRisk = 75;
    [SerializeField] private MercenaryOffer[] offers = Array.Empty<MercenaryOffer>();
    [SerializeField] private MercenaryData[] mercenaryCatalog = Array.Empty<MercenaryData>();   // SO 지정 시 아이콘·이름·전투력·가격을 여기서 로드(하드코딩 기본값 대체)
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
    private readonly List<Color> offerBaseColors = new List<Color>();   // [편집형] 카드 원본 색 보존(선택 하이라이트 복원용)
    private RectTransform cardArea;
    private TMP_Text riskSummaryText;
    private TMP_Text moneySummaryText;
    private Button confirmButton;
    private int selectedOfferIndex = -1;
    private long availableGold;
    private Coroutine animationRoutine;
    private bool built;
    private Action backOverride;

    public int SelectedCombatPower => selectedOfferIndex < 0 ? 0 : offers[selectedOfferIndex].combatPower;
    public long SelectedHireCost => selectedOfferIndex < 0 ? 0L : offers[selectedOfferIndex].hireCost;
    public string SelectedMercenaryId => selectedOfferIndex < 0
        ? string.Empty
        : offers[selectedOfferIndex].mercenaryId ?? string.Empty;
    public string SelectedDisplayName => selectedOfferIndex < 0
        ? string.Empty
        : offers[selectedOfferIndex].displayName ?? string.Empty;
    public bool CanConfirm => SelectedHireCost <= availableGold;

    public void SetExpectedRisk(float riskLevel)
    {
        expectedRisk = Mathf.Max(0, Mathf.RoundToInt(riskLevel));
        Refresh();
    }

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
        Show(gold, null);
    }

    public void Show(long gold, Action onBackRequested)
    {
        BuildIfNeeded();
        backOverride = onBackRequested;
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

    public void Populate(IReadOnlyList<MercenaryViewData> viewData)
    {
        BuildIfNeeded();
        var runtimeOffers = new List<MercenaryOffer>();
        if (viewData != null)
        {
            for (int index = 0; index < viewData.Count; index++)
            {
                MercenaryViewData item = viewData[index];
                if (item == null)
                    continue;

                runtimeOffers.Add(new MercenaryOffer
                {
                    mercenaryId = item.mercenaryId ?? string.Empty,
                    displayName = item.displayName ?? string.Empty,
                    icon = item.icon,   // SO 아이콘 전달
                    combatPower = Mathf.Max(0, item.combatCapability),
                    hireCost = Math.Max(0L, item.baseBuyPrice),
                    canHire = item.canHire && !string.IsNullOrWhiteSpace(item.mercenaryId)
                });
            }
        }

        offers = runtimeOffers.ToArray();
        selectedOfferIndex = -1;
        RebuildOfferCards();
        Refresh();
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

        // [편집형] 캔버스에 미리 만든 자식이 있으면 루트의 위치·크기·색을 강제하지 않음(캔버스에서 편집)
        if (transform.childCount == 0)
        {
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(1176f, 740f);
            GetComponent<Image>().color = FrameColor;
        }

        RectTransform header = CreatePanel(transform, "Header", new Vector2(1176f, 104f), Vector2.zero, FrameColor);
        RectTransform titleBackground = CreatePanel(header, "TitleBackground", new Vector2(460f, 76f), new Vector2(358f, -14f), new Color32(248, 246, 243, 255));
        CreateText(titleBackground, "TitleText", "용병 고용", 34f, new Vector2(440f, 68f), new Vector2(230f, -38f), TextAlignmentOptions.Center, FontStyles.Bold);
        Button back = CreateButton(header, "BackButton", "뒤로", new Vector2(116f, 56f), new Vector2(76f, -52f), DarkColor);
        Button cancel = CreateButton(header, "CloseButton", "무역 취소", new Vector2(116f, 56f), new Vector2(1100f, -52f), new Color32(190, 87, 87, 255));
        back.onClick.AddListener(BackToCargo);
        // [닫기=전체 닫기] CloseButton(무역 취소)은 씬에서 FrameworkTradeScreenPresenter.CloseTradeScreen에
        //   persistent로 직접 연결(무역준비 UI 전체를 닫음). 여기서 CancelTrade(도시/루트로 이동)를 붙이지 않는다.
        // cancel.onClick.AddListener(CancelTrade);

        cardArea = CreatePanel(transform, "MercenaryCardArea", new Vector2(1116f, 412f), new Vector2(32f, -104f), DarkColor);
        RebuildOfferCards();

        RectTransform summary = CreatePanel(transform, "HireSummaryArea", new Vector2(900f, 172f), new Vector2(32f, -536f), ContentColor);
        riskSummaryText = CreateText(summary, "RiskSummaryText", string.Empty, 23f, new Vector2(840f, 48f), new Vector2(450f, -48f), TextAlignmentOptions.Left, FontStyles.Bold);
        moneySummaryText = CreateText(summary, "MoneySummaryText", string.Empty, 23f, new Vector2(840f, 48f), new Vector2(450f, -112f), TextAlignmentOptions.Left, FontStyles.Bold);

        RectTransform action = CreatePanel(transform, "ActionArea", new Vector2(184f, 172f), new Vector2(964f, -536f), ContentColor);
        confirmButton = CreateButton(action, "ConfirmButton", "확인", new Vector2(148f, 52f), new Vector2(92f, -118f), ActionColor);
        confirmButton.onClick.AddListener(Confirm);
        CreateText(action, "OptionalText", "용병 고용은 선택입니다", 17f, new Vector2(160f, 56f), new Vector2(92f, -48f), TextAlignmentOptions.Center, FontStyles.Normal);

        Refresh();
    }

    private void RebuildOfferCards()
    {
        for (int index = 0; index < offerButtons.Count; index++)
        {
            Button oldCard = offerButtons[index];
            if (oldCard == null)
                continue;
            oldCard.gameObject.SetActive(false);
            Destroy(oldCard.gameObject);
        }
        offerButtons.Clear();
        offerBaseColors.Clear();

        if (cardArea == null)
            return;

        // [편집형] 템플릿은 복제 원본이므로 런타임엔 항상 숨김(편집 중 켜둬도 게임엔 안 나옴)
        Transform cardTemplate = cardArea.Find("CardTemplate");
        if (cardTemplate != null)
            cardTemplate.gameObject.SetActive(false);

        for (int i = 0; i < offers.Length; i++)
        {
            int index = i;
            float x = 18f + i * 218f;
            Button card = CreateOfferCard(cardArea, offers[i], new Vector2(x, -20f));
            card.onClick.AddListener(() => ToggleOffer(index));
            card.interactable = offers[i].canHire;
            offerButtons.Add(card);
            Image cardImage = card.targetGraphic as Image;
            offerBaseColors.Add(cardImage != null ? cardImage.color : (Color)ContentColor);
        }
    }

    private void EnsureDefaultOffers()
    {
        // TODO(PRODUCTION): Remove this hard-coded fallback after every entry path injects
        // MercenaryViewData built from MercenaryData SOs. Production selection must always retain
        // the SO mercenaryId through Draft, Summary, and departure Commit.
        if (offers != null && offers.Length > 0)
            return;

        // SO 카탈로그가 지정돼 있으면 하드코딩 대신 SO에서 오퍼(아이콘 포함)를 구성한다.
        if (mercenaryCatalog != null && mercenaryCatalog.Length > 0)
        {
            var fromCatalog = new List<MercenaryOffer>();
            foreach (MercenaryData data in mercenaryCatalog)
            {
                if (data == null)
                    continue;
                fromCatalog.Add(new MercenaryOffer
                {
                    mercenaryId = data.MercenaryId,
                    displayName = data.DisplayName,
                    icon = data.Icon,
                    combatPower = data.CombatCapability,
                    hireCost = data.BaseBuyPrice,
                    canHire = true
                });
            }
            if (fromCatalog.Count > 0)
            {
                offers = fromCatalog.ToArray();
                return;
            }
        }

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
        // [편집형] 카드영역에 CardTemplate가 있으면 복제해서 사용(레이아웃/폰트/색을 캔버스에서 편집)
        Transform template = parent.Find("CardTemplate");
        if (template != null)
        {
            GameObject clone = Instantiate(template.gameObject, parent);
            clone.name = offer.displayName;
            clone.SetActive(true);
            RectTransform cloneRect = clone.transform as RectTransform;
            cloneRect.anchoredPosition = position;

            SetCardText(clone, "NameText", offer.displayName);
            SetCardText(clone, "PowerText", $"전투력  {offer.combatPower}");
            SetCardText(clone, "CostText", offer.hireCost == 0 ? "무료" : $"{offer.hireCost:N0} G");
            SetCardIcon(clone, "Portrait", offer.icon);   // SO 아이콘을 Portrait에 표시(null이면 템플릿 그림 유지)

            Button cloneButton = clone.GetComponent<Button>();
            if (cloneButton == null)
                cloneButton = clone.AddComponent<Button>();
            return cloneButton;
        }

        // [폴백] 템플릿이 없으면 기존 코드 생성 방식으로 카드를 만든다
        Button card = CreateButton(parent, offer.displayName, string.Empty, new Vector2(200f, 370f), position, ContentColor, true, false);
        RectTransform cardRect = card.transform as RectTransform;
        RectTransform portrait = CreatePanel(cardRect, "Portrait", new Vector2(168f, 210f), new Vector2(16f, -16f), new Color32(255, 255, 255, 255));
        if (offer.icon != null)
        {
            Image portraitImage = portrait.GetComponent<Image>();
            if (portraitImage != null)
            {
                portraitImage.sprite = offer.icon;
                portraitImage.color = Color.white;
            }
        }
        CreateText(cardRect, "NameText", offer.displayName, 21f, new Vector2(168f, 54f), new Vector2(100f, -258f), TextAlignmentOptions.Center, FontStyles.Bold);
        RectTransform powerStrip = CreatePanel(cardRect, "PowerStrip", new Vector2(168f, 44f), new Vector2(16f, -286f), DetailColor);
        CreateText(powerStrip, "PowerText", $"전투력  {offer.combatPower}", 19f, new Vector2(160f, 40f), new Vector2(84f, -22f), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateText(cardRect, "CostText", offer.hireCost == 0 ? "무료" : $"{offer.hireCost:N0} G", 18f, new Vector2(168f, 34f), new Vector2(100f, -348f), TextAlignmentOptions.Center, FontStyles.Normal);
        return card;
    }

    // [편집형] 복제한 카드에서 이름으로 자식 텍스트를 찾아 값 채우기(PowerText 등 중첩 자식 포함)
    private static void SetCardText(GameObject cardRoot, string childName, string value)
    {
        foreach (TMP_Text text in cardRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == childName)
            {
                text.text = value;
                return;
            }
        }
    }

    // [편집형] 복제 카드에서 이름으로 Image를 찾아 아이콘 스프라이트 적용(null이면 기존 유지)
    private static void SetCardIcon(GameObject cardRoot, string childName, Sprite sprite)
    {
        if (sprite == null)
            return;
        foreach (Image image in cardRoot.GetComponentsInChildren<Image>(true))
        {
            if (image.name == childName)
            {
                image.sprite = sprite;
                image.color = Color.white;
                return;
            }
        }
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
            {
                // [편집형] 선택 시 하이라이트, 해제 시 캔버스에서 편집한 원본 색으로 복원
                Color baseColor = i < offerBaseColors.Count ? offerBaseColors[i] : (Color)ContentColor;
                image.color = i == selectedOfferIndex ? (Color)new Color32(255, 235, 150, 255) : baseColor;
            }
        }

        if (confirmButton != null)
            confirmButton.interactable = CanConfirm;
    }

    private void BackToCargo()
    {
        Action requestedBack = backOverride;
        backOverride = null;
        Hide(() =>
        {
            if (requestedBack != null)
                requestedBack();
            else
                cargoPanel?.ReturnFromMercenaryHire();
        });
    }

    // (미사용) 예전엔 CloseButton이 이걸 호출해 Cargo 취소로 갔음.
    //   지금은 CloseButton을 씬에서 CloseTradeScreen(전체 닫기)에 직접 연결한다.
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

    // [편집형] parent 하위 전체(중첩 포함)에서 이름으로 오브젝트 탐색 — 레이아웃용 컨테이너로 감싸도 찾도록
    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null)
            return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
                return child;
            Transform found = FindDeep(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    private static RectTransform CreatePanel(Transform parent, string name, Vector2 size, Vector2 position, Color color)
    {
        // [편집형] 캔버스에 같은 이름 오브젝트가 있으면 그대로 사용(위치·색을 캔버스에서 편집 가능)
        Transform existingPanel = FindDeep(parent, name);
        if (existingPanel != null)
            return existingPanel as RectTransform;

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

    private Button CreateButton(Transform parent, string name, string label, Vector2 size, Vector2 position, Color color, bool topLeft = false, bool reuseExisting = true)
    {
        // [편집형] 패널 전체(루트)에서 같은 이름 버튼을 찾아 재사용 — Header 밖 등 어디로 옮겨도 그대로 연결.
        //   (용병 카드는 reuseExisting=false로 항상 새로 생성)
        if (reuseExisting)
        {
            Transform existingButton = FindDeep(transform, name);
            if (existingButton != null)
            {
                Button existingComp = existingButton.GetComponent<Button>();
                if (existingComp != null)
                    return existingComp;
            }
        }

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
        // [편집형] 캔버스에 같은 이름 텍스트가 있으면 재사용(위치·내용 캔버스 유지, 동적 텍스트는 Refresh가 갱신)
        Transform existingText = FindDeep(parent, name);
        if (existingText != null)
        {
            TMP_Text existingComp = existingText.GetComponent<TMP_Text>();
            if (existingComp != null)
                return existingComp;
        }

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
