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
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>마을 건물 스크롤 리스트 UI(클릭 하이라이트 + [+] 추가).</summary>
public class BuildingListPanel : MonoBehaviour
{
    [SerializeField] private RectTransform content;   // ScrollRect Content
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float itemHeight = 56f;
    [SerializeField] private BuildingAddPopup addPopup;   // [+] 가 여는 건물 추가 팝업    /// <summary>
    /// 동적으로 생성된 건물 블록이 선택된 뒤 표시 이름을 전달한다.
    /// 목록은 특정 건물 기능을 알지 않고 외부 연결부가 필요한 동작만 선택하도록 한다.
    /// </summary>
    public event Action<string> BuildingClicked;


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
