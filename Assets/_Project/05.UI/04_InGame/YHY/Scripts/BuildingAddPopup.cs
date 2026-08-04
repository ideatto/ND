using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// `마을 꾸미기` 팝업의 카탈로그를 프리팹 설정과 런타임 Registry로 조합한다.
/// 버튼 GameObject를 프리팹에 고정하지 않고 Section 정의에서 생성하여 카테고리 확장을 허용한다.
/// </summary>
/// <remarks>
/// RegistryBuildings는 기존 건설 흐름을 유지하고, InspectorEntries는 아직 데이터 타입이 확정되지 않은
/// 환경 메뉴의 임시 확장 지점이다. 실제 환경 SO 정책이 정해지면 개별 UnityEvent 대신 공통 선택 처리기로
/// 데이터를 전달하되, ScrollView와 Section 생성 책임은 이 클래스에 남긴다.
/// </remarks>
public class BuildingAddPopup : MonoBehaviour, IPointerClickHandler
{
    /// <summary>Section 항목을 어디서 구성할지 결정하며 UI가 데이터 원본을 추측하지 않게 한다.</summary>
    public enum SectionContentSource
    {
        RegistryBuildings,
        InspectorEntries
    }

    [Serializable]
    /// <summary>
    /// InspectorEntries가 생성할 표시용 항목이다. 현재 selected는 임시 연결 지점이며,
    /// 환경 데이터 형식 확정 후 안정적인 ID와 데이터 참조 기반 선택 계약으로 교체할 예정이다.
    /// </summary>
    public sealed class MenuEntry
    {
        public string label = "새 항목";
        public bool interactable = true;
        public UnityEvent selected = new UnityEvent();
    }

    [Serializable]
    public sealed class MenuSection
    {
        public string title = "새 카테고리";
        public bool initiallyExpanded = true;
        public SectionContentSource contentSource;
        public List<MenuEntry> entries = new List<MenuEntry>();
    }

    [Header("목록")]
    [SerializeField] private RectTransform content;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float itemHeight = 60f;
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private List<MenuSection> sections = new List<MenuSection>();

    [Header("편집 모드 버튼 Prefab")]
    [SerializeField] private Button editModeButtonPrefab;

    [Header("건물 상세")]
    [SerializeField] private BuildingPopupRuntimeBinding popupRuntimeBinding;

    private readonly Dictionary<int, bool> expandedSections = new Dictionary<int, bool>();
    private BuildingPlacementController placementController;
    private Button headerEditModeButton;
    private bool useImmediateAdd;
    private Action onAdded;

    private void Awake()
    {
        placementController = FindAnyObjectByType<BuildingPlacementController>();
        EnsureDefaultSections();
        ResolveHeader();
    }

    /// <summary>팝업 인스턴스가 재사용되어도 이전 펼침 상태가 다음 진입에 누출되지 않게 한다.</summary>
    private void OnDisable()
    {
        // Reset transient accordion state whenever the popup is closed.
        expandedSections.Clear();
    }

    public void Open(Action onAddedCallback)
    {
        CancelPlacementSelection();
        useImmediateAdd = true;
        onAdded = onAddedCallback;
        OpenInternal();
    }

    public void Open()
    {
        CancelPlacementSelection();
        useImmediateAdd = false;
        onAdded = null;
        OpenInternal();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.pointerCurrentRaycast.gameObject == gameObject)
            Close();
    }

    private void OpenInternal()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ResolveHeader();
        BuildCatalog();
    }

    private void ResolveHeader()
    {
        if (headerText == null)
        {
            foreach (TMP_Text candidate in GetComponentsInChildren<TMP_Text>(true))
            {
                if (candidate != null && candidate.text == "건물 추가")
                {
                    headerText = candidate;
                    break;
                }
            }
        }

        if (headerText != null)
            headerText.text = "마을 꾸미기";
    }

    private void EnsureDefaultSections()
    {
        if (sections == null)
            sections = new List<MenuSection>();
        if (sections.Count > 0)
            return;

        sections.Add(new MenuSection
        {
            title = "건물",
            initiallyExpanded = false,
            contentSource = SectionContentSource.RegistryBuildings
        });
        sections.Add(new MenuSection
        {
            title = "환경",
            initiallyExpanded = false,
            contentSource = SectionContentSource.InspectorEntries
        });
    }

    private void CancelPlacementSelection()
    {
        ResolvePlacementController();
        placementController?.CancelPlacementSelection();
    }

    private void ResolvePlacementController()
    {
        if (placementController == null)
            placementController = FindAnyObjectByType<BuildingPlacementController>();
    }

    private void BuildCatalog()
    {
        if (content == null)
            return;

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }

        ResolvePlacementController();
        CreateHeaderEditModeControls();

        for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            MenuSection section = sections[sectionIndex];
            if (section == null)
                continue;

            if (!expandedSections.TryGetValue(sectionIndex, out bool expanded))
            {
                expanded = section.initiallyExpanded;
                expandedSections[sectionIndex] = expanded;
            }

            int capturedSection = sectionIndex;
            Button header = CreateRow(
                $"{(expanded ? "▼" : "▶")}  {section.title}",
                new Color(0.47f, 0.55f, 0.45f),
                false);
            header.onClick.AddListener(() =>
            {
                expandedSections[capturedSection] = !expandedSections[capturedSection];
                BuildCatalog();
            });

            if (!expanded)
                continue;

            if (section.contentSource == SectionContentSource.RegistryBuildings)
                CreateRegistryBuildingRows();

            CreateInspectorRows(section);
        }
    }

    /// <summary>
    /// 편집 진입 버튼은 스크롤 Content가 아닌 헤더에 생성한다.
    /// 상태 종료/저장은 별도 UI가 맡도록 여기서는 EnterEditMode와 팝업 닫기만 수행한다.
    /// </summary>
    private void CreateHeaderEditModeControls()
    {
        bool editing = placementController != null && placementController.IsEditMode;
        if (headerEditModeButton == null)
            // Keep the entry button visually paired with the existing Close button (120 x 56).
            // A narrower width wraps the Korean label and makes the header controls look unrelated.
            headerEditModeButton = CreateHeaderButton(editModeButtonPrefab, "\uD3B8\uC9D1\uBAA8\uB4DC", 120f);
        headerEditModeButton.onClick.RemoveAllListeners();
        headerEditModeButton.onClick.AddListener(() =>
        {
            ResolvePlacementController();
            placementController?.EnterEditMode();
            // Close the catalog immediately so it does not block village edit input.
            Close();
        });
        headerEditModeButton.gameObject.SetActive(true);
        headerEditModeButton.interactable = !editing;
    }

    private Button CreateHeaderButton(Button prefab, string label, float width)
    {
        Transform header = headerText != null ? headerText.transform.parent : transform;
        Button button = prefab != null
            ? Instantiate(prefab, header, false)
            : CreateRow(label, new Color(0.31f, 0.61f, 0.35f), false);
        button.name = prefab != null ? prefab.name : label;
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.SetParent(header, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(16f, -16f);
        rect.sizeDelta = new Vector2(width, 56f);
        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout != null)
            Destroy(layout);
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = label;
            text.fontSize = 22f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 16f;
            text.fontSizeMax = 22f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = TextAlignmentOptions.Center;
            if (font != null)
                text.font = font;
        }
        return button;
    }    private void CreateRegistryBuildingRows()
    {
        VillageBuildingRegistry registry = VillageBuildingRegistry.Instance;
        if (registry == null)
            return;

        for (int i = 0; i < registry.CatalogCount; i++)
        {
            int catalogIndex = i;
            Button row = CreateRow(
                $"{registry.GetCatalogName(i)}  Lv.{registry.GetCatalogLevel(i)}",
                new Color(0.78f, 0.79f, 0.75f),
                true);
            row.onClick.AddListener(() => SelectRegistryBuilding(registry, catalogIndex));
        }
    }

    private void CreateInspectorRows(MenuSection section)
    {
        if (section.entries == null)
            return;

        foreach (MenuEntry entry in section.entries)
        {
            if (entry == null)
                continue;

            Button row = CreateRow(
                $"{entry.label}",
                new Color(0.72f, 0.76f, 0.70f),
                true);
            row.interactable = entry.interactable;
            row.onClick.AddListener(() => entry.selected?.Invoke());
        }
    }

    private void SelectRegistryBuilding(VillageBuildingRegistry registry, int catalogIndex)
    {
        if (useImmediateAdd)
        {
            registry.AddOrUpgrade(catalogIndex);
            onAdded?.Invoke();
            Close();
            return;
        }

        BuildData buildData = registry.GetCatalogBuildData(catalogIndex);
        if (buildData == null)
        {
            Debug.LogError($"BuildingAddPopup: no BuildData is assigned to catalog index {catalogIndex}.", this);
            return;
        }

        if (popupRuntimeBinding == null)
        {
            Debug.LogError("BuildingAddPopup: BuildingPopupRuntimeBinding is not assigned.", this);
            return;
        }

        popupRuntimeBinding.OpenDetail(buildData, registry.GetCatalogLevel(catalogIndex));
        Close();
    }

    private Button CreateFromPrefab(Button prefab, string label, Color fallbackColor)
    {
        if (prefab == null)
            return CreateRow(label, fallbackColor, false);

        Button button = Instantiate(prefab, content, false);
        button.gameObject.SetActive(true);
        button.name = prefab.name;
        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout == null)
            layout = button.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = itemHeight;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = label;
            if (font != null)
                text.font = font;
        }
        return button;
    }

    private Button CreateRow(string label, Color background, bool childRow)
    {
        GameObject go = new GameObject(
            childRow ? "CategoryItem" : "CategoryHeader",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        go.transform.SetParent(content, false);
        go.GetComponent<LayoutElement>().minHeight = itemHeight;

        Image image = go.GetComponent<Image>();
        image.color = background;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(go.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(6f, 0f);
        labelRect.offsetMax = new Vector2(-6f, 0f);

        TextMeshProUGUI text = labelObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = label;
        text.fontSize = childRow ? 27f : 29f;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 18f;
        text.fontSizeMax = childRow ? 27f : 29f;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.color = new Color(0.16f, 0.18f, 0.16f);
        text.raycastTarget = false;
        return button;
    }
}
