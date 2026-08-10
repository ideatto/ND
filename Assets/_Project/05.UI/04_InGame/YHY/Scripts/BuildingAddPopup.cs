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
        RegistryEnvironment,   // 레지스트리 항목 중 '환경'만 표시(건물과 동일 배치·비용 파이프라인, 필터만 다름)
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
    [SerializeField] private Button rowTemplate;   // [편집형] 카탈로그 행 템플릿(지정 시 복제, 없으면 코드 생성)

    [Header("편집 모드 버튼 Prefab")]
    [SerializeField] private Button editModeButtonPrefab;
    [SerializeField] private Button editModeButton;   // [편집형] 씬에 직접 둔 편집모드 버튼(지정 시 코드 생성 대신 이걸 사용)
    [SerializeField] private Sprite editModeIcon;     // [편집형] 편집모드(진입 전) 버튼 이미지
    [SerializeField] private Sprite editDoneIcon;     // [편집형] 편집완료(편집 중) 버튼 이미지

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
            contentSource = SectionContentSource.RegistryEnvironment   // 레지스트리의 환경 항목을 건물과 동일 방식으로 표시
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
            // [편집형] rowTemplate은 복제 원본이므로 파괴하지 않고 숨겨만 둔다(에디터에선 보이게 편집 가능).
            if (rowTemplate != null && child == rowTemplate.transform)
            {
                child.gameObject.SetActive(false);
                continue;
            }
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
                CreateRegistryBuildingRows(environmentOnly: false);       // 건물 탭: 환경 아님만
            else if (section.contentSource == SectionContentSource.RegistryEnvironment)
                CreateRegistryBuildingRows(environmentOnly: true);        // 환경 탭: 환경만(같은 파이프라인)
            else
                CreateInspectorRows(section);                            // InspectorEntries(임시 placeholder)
        }
    }

    /// <summary>
    /// 편집 진입 버튼은 스크롤 Content가 아닌 헤더에 생성한다.
    /// 상태 종료/저장은 별도 UI가 맡도록 여기서는 EnterEditMode와 팝업 닫기만 수행한다.
    /// </summary>
    private void CreateHeaderEditModeControls()
    {
        ResolvePlacementController();
        if (headerEditModeButton == null)
            // \uAE30\uC874 \uC704\uCE58\u00B7\uC2A4\uD0C0\uC77C \uADF8\uB300\uB85C. \uB77C\uBCA8\uC740 \uC0C1\uD0DC\uC5D0 \uB530\uB77C \uD3B8\uC9D1\uBAA8\uB4DC/\uD3B8\uC9D1\uC644\uB8CC\uB85C \uD1A0\uAE00\uB41C\uB2E4.
            headerEditModeButton = editModeButton != null
                ? editModeButton   // [\uD3B8\uC9D1\uD615] \uC52C\uC5D0 \uC9C1\uC811 \uB454 \uBC84\uD2BC \uC0AC\uC6A9(\uC704\uCE58\u00B7\uBAA8\uC591 \uCE94\uBC84\uC2A4\uC5D0\uC11C \uD3B8\uC9D1)
                : CreateHeaderButton(editModeButtonPrefab, "\uD3B8\uC9D1\uBAA8\uB4DC", 120f);
        headerEditModeButton.onClick.RemoveAllListeners();
        headerEditModeButton.onClick.AddListener(() =>
        {
            ResolvePlacementController();
            placementController?.ToggleEditMode();   // \uD3B8\uC9D1 \uC9C4\uC785 \u2194 \uC885\uB8CC
            UpdateEditModeButtonLabel();             // \uAC19\uC740 \uBC84\uD2BC \uB77C\uBCA8\uB9CC \uAC31\uC2E0(\uC790\uB9AC \uC720\uC9C0)
        });
        headerEditModeButton.gameObject.SetActive(true);
        headerEditModeButton.interactable = true;
        UpdateEditModeButtonLabel();
    }

    // \uD3B8\uC9D1 \uC0C1\uD0DC\uC5D0 \uB9DE\uCDB0 \uD3B8\uC9D1 \uBC84\uD2BC \uB77C\uBCA8\uC744 '\uD3B8\uC9D1\uBAA8\uB4DC'/'\uD3B8\uC9D1\uC644\uB8CC'\uB85C \uAC31\uC2E0(\uBC84\uD2BC\uC740 \uADF8\uB300\uB85C \uB450\uACE0 \uD14D\uC2A4\uD2B8\uB9CC).
    // [\uD3B8\uC9D1\uD615] \uD3B8\uC9D1 \uC0C1\uD0DC\uC5D0 \uB530\uB77C \uBC84\uD2BC \uC774\uBBF8\uC9C0\uB97C \uAD50\uCCB4\uD55C\uB2E4(\uD3B8\uC9D1\uBAA8\uB4DC \uC544\uC774\uCF58 \u2194 \uD3B8\uC9D1\uC644\uB8CC \uC544\uC774\uCF58).
    //   \uB77C\uBCA8 \uD14D\uC2A4\uD2B8\uB294 \uCE94\uBC84\uC2A4\uC5D0\uC11C \uC815\uD55C \uB300\uB85C \uB450\uACE0, \uC0C1\uD0DC\uB294 \uC774\uBBF8\uC9C0\uB85C \uD45C\uC2DC\uD55C\uB2E4.
    //   \uB450 \uC2A4\uD504\uB77C\uC774\uD2B8\uAC00 \uBE44\uC5B4 \uC788\uC73C\uBA74(\uBBF8\uC9C0\uC815) \uC774\uBBF8\uC9C0\uB97C \uAC74\uB4DC\uB9AC\uC9C0 \uC54A\uB294\uB2E4(\uCE94\uBC84\uC2A4 \uC774\uBBF8\uC9C0 \uC720\uC9C0).
    private void UpdateEditModeButtonLabel()
    {
        if (headerEditModeButton == null) return;
        bool editing = placementController != null && placementController.IsEditMode;
        Image icon = headerEditModeButton.targetGraphic as Image;
        if (icon == null) icon = headerEditModeButton.GetComponent<Image>();
        if (icon == null) return;

        Sprite next = editing ? editDoneIcon : editModeIcon;
        if (next != null) icon.sprite = next;
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
    }

    private void CreateRegistryBuildingRows(bool environmentOnly)
    {
        VillageBuildingRegistry registry = VillageBuildingRegistry.Instance;
        if (registry == null)
            return;

        for (int i = 0; i < registry.CatalogCount; i++)
        {
            if (registry.GetCatalogIsEnvironment(i) != environmentOnly)
                continue;   // 이 섹션(건물/환경) 카테고리에 맞는 항목만 표시
            int catalogIndex = i;
            BuildData buildData = registry.GetCatalogBuildData(i);
            int currentLevel = registry.GetCatalogLevel(i);
            string rowLabel = $"{registry.GetCatalogName(i)}  Lv.{currentLevel}";
            bool rowInteractable = true;

            // EndingItem remains visible as a progression goal, but it cannot open before
            // BaseCamp Lv.5 or after its single construction level has been completed.
            // The locked row uses its color instead of a long suffix to communicate that state,
            // keeping the catalog readable while BaseCamp UI owns the detailed unlock guidance.
            if (IsEndingBuilding(buildData))
            {
                bool unlocked = IsEndingBuildingUnlocked();
                bool completed = currentLevel >= 1;
                rowInteractable = unlocked && !completed;
                rowLabel = completed
                    ? $"{registry.GetCatalogName(i)}  Lv.1  (건설 완료)"
                    : unlocked
                        ? $"{registry.GetCatalogName(i)}  Lv.0"
                        : registry.GetCatalogName(i);
            }

            Button row = CreateRow(
                rowLabel,
                new Color(0.78f, 0.79f, 0.75f),
                true);
            row.interactable = rowInteractable;
            if (!rowInteractable)
                ApplyDisabledRowPresentation(row);
            row.onClick.AddListener(() => SelectRegistryBuilding(registry, catalogIndex));
        }
    }

    /// <summary>
    /// Makes a disabled catalog entry recognizable before the player attempts to click it.
    /// The Button stays non-interactable; this method only strengthens its static presentation.
    /// </summary>
    private static void ApplyDisabledRowPresentation(Button row)
    {
        if (row == null)
            return;

        ColorBlock colors = row.colors;
        colors.disabledColor = new Color(0.66f, 0.64f, 0.59f, 1f);
        colors.colorMultiplier = 1f;
        row.colors = colors;

        TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.color = new Color(0.25f, 0.24f, 0.22f, 1f);
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
        BuildData selectedBuildData = registry.GetCatalogBuildData(catalogIndex);
        if (IsEndingBuilding(selectedBuildData)
            && (!IsEndingBuildingUnlocked() || registry.GetCatalogLevel(catalogIndex) >= 1))
        {
            // Fail closed even if an external caller bypasses the disabled row Button.
            return;
        }

        if (useImmediateAdd)
        {
            registry.AddOrUpgrade(catalogIndex);
            onAdded?.Invoke();
            Close();
            return;
        }

        BuildData buildData = selectedBuildData;
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

    private static bool IsEndingBuilding(BuildData buildData)
    {
        return buildData != null
            && string.Equals(
                buildData.BuildId,
                ND.Framework.BaseCampBuildingLevelPolicy.EndingBuildingId,
                StringComparison.Ordinal);
    }

    private static bool IsEndingBuildingUnlocked()
    {
        ND.Framework.SaveData saveData =
            ND.Framework.FrameworkRoot.Instance?.CurrentSaveData;
        return ND.Framework.BaseCampProgressionPolicy.IsEndingBuildingUnlocked(
            saveData?.player?.villageBuildings);
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
        // [편집형] rowTemplate이 지정되면 복제해서 사용(모양·폰트·높이·이미지를 씬에서 편집).
        //   헤더/항목 구분용 색·폰트크기만 코드가 지정하고, 스프라이트 등은 템플릿 그대로 유지.
        if (rowTemplate != null)
        {
            Button row = Instantiate(rowTemplate, content);
            row.gameObject.SetActive(true);
            row.name = childRow ? "CategoryItem" : "CategoryHeader";
            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            if (rowLayout == null) rowLayout = row.gameObject.AddComponent<LayoutElement>();
            rowLayout.minHeight = itemHeight;
            // [편집형] Image 색·스프라이트는 RowTemplate에서 정한 그대로 유지(코드가 background로 덮어쓰지 않음).
            //   금색 스프라이트에 코드 색을 곱하면 탁해지므로, 색은 템플릿에서만 관리한다.
            //   헤더/항목 구분은 라벨 접두(▼/▶ vs Lv.)로 됨. 색으로 구분이 필요하면 별도 요청.
            TMP_Text rowText = row.GetComponentInChildren<TMP_Text>(true);
            if (rowText != null)
            {
                rowText.text = label;
                rowText.fontSize = childRow ? 27f : 29f;
            }
            return row;
        }

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
