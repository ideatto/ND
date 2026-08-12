// =============================================================================
// BuildingListPanel — 정보창 "마을 건물" 스크롤 리스트 (마을 씬 연동)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 마을 씬의 VillageBuildingRegistry에서 건물 목록을 받아 스크롤 리스트로
//        표시한다. 항목 클릭 → 마을 씬 건물 하이라이트. 맨 아래 [+] → 건물 추가.
//
// [구조] content(ScrollRect Content, VerticalLayoutGroup + ContentSizeFitter)에
//        항목 버튼들 + [+] 버튼을 쌓는다. 세로 배치·높이는 레이아웃 그룹이 담당.
//
// [씬 로드 타이밍] 마을 씬(Registry)이 additive로 나중에 로드되므로 대기 후 생성.
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ND.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>마을 건물 스크롤 리스트 UI(클릭 하이라이트 + [+] 추가).</summary>
public class BuildingListPanel : MonoBehaviour
{
    [SerializeField] private RectTransform content;   // ScrollRect Content
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float itemHeight = 56f;
    // 건물별 기능은 이 패널에 직접 결합하지 않는다. 동적으로 생성되는 행에 대한
    // 범용 Badge 상태만 보관하고, 오두막·빵집 등의 연결부가 공개 API로 상태를 전달한다.
    private readonly Dictionary<string, Button> rowsByBuildingName =
        new Dictionary<string, Button>(StringComparer.Ordinal);
    private readonly Dictionary<string, BuildingBadgeState> badgeStates =
        new Dictionary<string, BuildingBadgeState>(StringComparer.Ordinal);
    private readonly Dictionary<string, GameObject> badgeObjects =
        new Dictionary<string, GameObject>(StringComparer.Ordinal);
    [SerializeField] private BuildingAddPopup addPopup;   // [+] 가 여는 건물 추가 팝업
    [SerializeField] private Button rowTemplate;          // [편집형] 행 템플릿(지정 시 복제, 없으면 코드 생성)
    [SerializeField] private BuildingPlacementController placementController; // 편집 모드 신호원(비면 런타임 탐색)
    private bool reorderMode;   // 편집 모드 = 리스트 순서 변경 가능(행에 ▲▼ 버튼 표시)

    /// <summary>
    /// 동적으로 생성된 건물 블록이 선택된 뒤 표시 이름을 전달한다.
    /// 목록은 특정 건물 기능을 알지 않고 외부 연결부가 필요한 동작만 선택하도록 한다.
    /// </summary>
    public event Action<string> BuildingClicked;

    /// <summary>
    /// 특정 건물 행의 범용 알림 Badge를 설정한다. 행이 아직 생성되지 않았으면 상태만
    /// 기억했다가 다음 Rebuild에서 적용하므로 기능 연결부가 UI 생성 순서를 알 필요가 없다.
    /// </summary>
    public void SetBuildingBadge(string buildingName, Sprite icon, bool visible)
    {
        if (string.IsNullOrWhiteSpace(buildingName)) return;
        badgeStates[buildingName] = new BuildingBadgeState(icon, visible);
        ApplyBadge(buildingName);
    }


    private IEnumerator Start()
    {
        float timeout = 5f;
        while (VillageBuildingRegistry.Instance == null && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        // 편집 모드 신호원 연결(비면 씬에서 탐색). 편집 진입/종료 시 재정렬 버튼을 켜고 끈다.
        if (placementController == null)
            placementController = FindAnyObjectByType<BuildingPlacementController>(FindObjectsInactive.Include);
        if (placementController != null)
        {
            reorderMode = placementController.IsEditMode;
            placementController.EditModeChanged += OnEditModeChanged;
        }

        if (VillageBuildingRegistry.Instance != null)
            Rebuild();
    }

    private void OnDestroy()
    {
        if (placementController != null)
            placementController.EditModeChanged -= OnEditModeChanged;
    }

    // 편집 모드 토글 시: 행에 ▲▼ 재정렬 버튼을 붙이거나 떼기 위해 다시 그린다.
    private void OnEditModeChanged(bool editing)
    {
        reorderMode = editing;
        Rebuild();
    }

    /// <summary>Registry의 현재 건물 상태를 기준으로 리스트를 처음부터 다시 만든다.</summary>
public void Rebuild()
    {
        if (content == null) return;
        rowsByBuildingName.Clear();
        badgeObjects.Clear();

        // Destroy는 frame 끝에 처리되므로 먼저 부모에서 분리해 연속 Rebuild에도 중복 행이 남지 않게 한다.
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }

        VillageBuildingRegistry reg = VillageBuildingRegistry.Instance;
        if (reg == null) return;

        // 표시 순서 = 저장된 villageBuildings 순서(그 목록에서의 위치로 정렬, 없으면 뒤로).
        //   OrderBy는 안정 정렬이라 동순위(미등록)는 레지스트리 순서를 유지한다.
        List<string> savedOrder = GetSavedOrder();
        List<BuildingEntry> entries = new List<BuildingEntry>();
        for (int i = 0; i < reg.Count; i++)
            entries.Add(new BuildingEntry(reg.GetName(i), reg.GetLevel(i), i));
        entries = entries.OrderBy(e => OrderIndex(savedOrder, e.Name)).ToList();

        for (int e = 0; e < entries.Count; e++)
        {
            BuildingEntry entry = entries[e];
            int regIdx = entry.RegIndex;         // 하이라이트는 원래 레지스트리 인덱스로(표시 순서와 무관)
            string buildingName = entry.Name;
            Button item = CreateRow(
                $"{buildingName}  Lv.{entry.Level}",
                new Color(0.76f, 0.77f, 0.73f));
            rowsByBuildingName[buildingName] = item;
            item.onClick.AddListener(() =>
            {
                // 기존 하이라이트는 유지하고 추가 기능은 건물 이름 이벤트를 구독한 연결부에 위임한다.
                reg.Highlight(regIdx);
                BuildingClicked?.Invoke(buildingName);
            });

            // 편집 모드면 행 오른쪽에 ▲▼ 재정렬 버튼(맨 위=▲비활성, 맨 아래=▼비활성).
            if (reorderMode)
                AddReorderButtons(item.transform, buildingName, e == 0, e == entries.Count - 1);
        }

        Button addBtn = CreateRow("+", new Color(0.6f, 0.7f, 0.55f));
        addBtn.onClick.AddListener(() =>
        {
            if (addPopup != null) addPopup.Open();
        });
        foreach (string buildingName in badgeStates.Keys)
            ApplyBadge(buildingName);
    }

    private void ApplyBadge(string buildingName)
    {
        if (!badgeStates.TryGetValue(buildingName, out BuildingBadgeState state)
            || !rowsByBuildingName.TryGetValue(buildingName, out Button row)
            || row == null)
            return;

        if (!badgeObjects.TryGetValue(buildingName, out GameObject indicator)
            || indicator == null)
        {
            indicator = new GameObject(
                "StatusBadge",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            badgeObjects[buildingName] = indicator;
        }
        indicator.transform.SetParent(row.transform, false);
        RectTransform rect = indicator.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        // 행 오른쪽 위 모서리의 안쪽으로 배치해 패널 마스크 밖에서 잘리지 않게 한다.
        rect.anchoredPosition = new Vector2(-120f, -6f);
        rect.sizeDelta = new Vector2(28f, 28f);
        Image image = indicator.GetComponent<Image>();
        image.sprite = state.Icon;
        image.preserveAspect = true;
        image.raycastTarget = false;
        indicator.SetActive(state.Visible && state.Icon != null);
    }

    private readonly struct BuildingBadgeState
    {
        public BuildingBadgeState(Sprite icon, bool visible)
        {
            Icon = icon;
            Visible = visible;
        }

        public Sprite Icon { get; }
        public bool Visible { get; }
    }

    // 표시 순서 정렬용 임시 항목(이름·레벨·원래 레지스트리 인덱스).
    private readonly struct BuildingEntry
    {
        public BuildingEntry(string name, int level, int regIndex)
        {
            Name = name;
            Level = level;
            RegIndex = regIndex;
        }

        public string Name { get; }
        public int Level { get; }
        public int RegIndex { get; }
    }

    /// <summary>저장된 표시 순서(villageBuildings의 displayName 순서). 없으면 빈 목록.</summary>
    private static List<string> GetSavedOrder()
    {
        List<string> names = new List<string>();
        List<VillageBuildingSaveData> list = FrameworkRoot.Instance?.CurrentSaveData?.player?.villageBuildings;
        if (list != null)
            foreach (VillageBuildingSaveData b in list)
                if (b != null && !string.IsNullOrEmpty(b.displayName)) names.Add(b.displayName);
        return names;
    }

    // 저장 순서에서의 위치(없으면 맨 뒤). 안정 정렬과 함께 써서 미등록 항목은 레지스트리 순서 유지.
    private static int OrderIndex(List<string> order, string name)
    {
        int i = order.IndexOf(name);
        return i < 0 ? int.MaxValue : i;
    }

    /// <summary>건물을 저장 순서(villageBuildings)에서 delta칸 이동 → 저장 → 다시 그린다.</summary>
    private void MoveBuilding(string buildingName, int delta)
    {
        FrameworkRoot root = FrameworkRoot.Instance;
        List<VillageBuildingSaveData> list = root?.CurrentSaveData?.player?.villageBuildings;
        if (list == null) return;

        int idx = list.FindIndex(b => b != null && string.Equals(b.displayName, buildingName, StringComparison.Ordinal));
        int target = idx + delta;
        if (idx < 0 || target < 0 || target >= list.Count) return;   // 경계 밖이면 무시

        VillageBuildingSaveData tmp = list[idx];   // 인접 항목과 swap
        list[idx] = list[target];
        list[target] = tmp;

        root.SaveService?.Save(root.CurrentSaveData);   // 순서 영구 저장
        Rebuild();
    }

    /// <summary>행 오른쪽에 위/아래 이동 버튼을 붙인다(편집 모드 전용). 경계 방향은 비활성.</summary>
    private void AddReorderButtons(Transform row, string buildingName, bool isFirst, bool isLast)
    {
        GameObject holder = new GameObject("ReorderButtons", typeof(RectTransform));
        holder.transform.SetParent(row, false);
        RectTransform hr = holder.GetComponent<RectTransform>();
        hr.anchorMin = new Vector2(1f, 0f);
        hr.anchorMax = new Vector2(1f, 1f);
        hr.pivot = new Vector2(1f, 0.5f);
        hr.sizeDelta = new Vector2(46f, 0f);
        hr.anchoredPosition = new Vector2(-6f, 0f);

        CreateArrowButton(holder.transform, "▲", new Vector2(0.5f, 0.72f), !isFirst, () => MoveBuilding(buildingName, -1));
        CreateArrowButton(holder.transform, "▼", new Vector2(0.5f, 0.28f), !isLast, () => MoveBuilding(buildingName, +1));
    }

    private void CreateArrowButton(Transform parent, string label, Vector2 anchor, bool enabled, UnityEngine.Events.UnityAction onClick)
    {
        GameObject b = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        b.transform.SetParent(parent, false);
        RectTransform r = b.GetComponent<RectTransform>();
        r.anchorMin = anchor; r.anchorMax = anchor; r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(40f, 24f); r.anchoredPosition = Vector2.zero;

        Image img = b.GetComponent<Image>();
        img.color = new Color(0.902f, 0.757f, 0.439f, 1f);   // 금색 톤(UI 팔레트)
        Button btn = b.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.interactable = enabled;
        btn.onClick.AddListener(onClick);

        GameObject lgo = new GameObject("T", typeof(RectTransform));
        lgo.transform.SetParent(b.transform, false);
        RectTransform lr = lgo.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
        TextMeshProUGUI t = lgo.AddComponent<TextMeshProUGUI>();
        t.font = font; t.text = label; t.fontSize = 20f;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.2f, 0.2f, 0.2f);
        t.raycastTarget = false;
    }

    /// <summary>리스트 한 줄(버튼+라벨) 생성. VerticalLayoutGroup이 배치.</summary>
    private Button CreateRow(string label, Color bg)
    {
        // [편집형] rowTemplate이 지정되면 복제해서 사용(모양·폰트·높이·이미지를 씬에서 편집).
        //   ＋행/일반행 구분용 색만 코드가 tint로 지정하고, 스프라이트 등 나머지는 템플릿 그대로 유지.
        if (rowTemplate != null)
        {
            Button row = Instantiate(rowTemplate, content);
            row.gameObject.SetActive(true);
            // [편집형] Image 색·스프라이트는 RowTemplate에서 정한 그대로 유지(코드가 bg로 덮어쓰지 않음).
            //   흰 스프라이트 × Button 색(ColorTint)으로 원하는 색을 낸다. (덮어쓰면 금색이 탁해짐)
            TMP_Text rowText = row.GetComponentInChildren<TMP_Text>(true);
            if (rowText != null) rowText.text = label;
            return row;
        }

        // [폴백] 템플릿이 없으면 기존 코드 생성 방식
        GameObject go = new GameObject("Row",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(content, false);
        go.GetComponent<LayoutElement>().minHeight = itemHeight;

        Image img = go.GetComponent<Image>();
        img.color = bg;
        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        GameObject lgo = new GameObject("Label", typeof(RectTransform));
        lgo.transform.SetParent(go.transform, false);
        RectTransform lr = lgo.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
        TextMeshProUGUI t = lgo.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = label;
        t.fontSize = 26f;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.2f, 0.2f, 0.2f);
        t.raycastTarget = false;

        return btn;
    }
}
