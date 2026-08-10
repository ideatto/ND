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
    [SerializeField] private BuildingAddPopup addPopup;   // [+] 가 여는 건물 추가 팝업    /// <summary>
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
        if (VillageBuildingRegistry.Instance != null)
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

        for (int i = 0; i < reg.Count; i++)
        {
            int idx = i;
            string buildingName = reg.GetName(i);
            Button item = CreateRow(
                $"{buildingName}  Lv.{reg.GetLevel(i)}",
                new Color(0.76f, 0.77f, 0.73f));
            rowsByBuildingName[buildingName] = item;
            item.onClick.AddListener(() =>
            {
                // 기존 하이라이트는 유지하고 추가 기능은 건물 이름 이벤트를 구독한 연결부에 위임한다.
                reg.Highlight(idx);
                BuildingClicked?.Invoke(buildingName);
            });
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
        rect.anchoredPosition = new Vector2(-10f, -8f);
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

    /// <summary>리스트 한 줄(버튼+라벨) 생성. VerticalLayoutGroup이 배치.</summary>
    private Button CreateRow(string label, Color bg)
    {
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
