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
    [SerializeField] private BuildingAddPopup addPopup;   // [+] 가 여는 건물 추가 팝업

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

    /// <summary>리스트를 처음부터 다시 만든다(추가 후에도 호출).</summary>
    public void Rebuild()
    {
        if (content == null) return;

        // 기존 항목 제거 — Destroy는 프레임 끝에 처리되므로, 먼저 부모에서 분리(즉시)해
        // Rebuild가 옛 항목을 중복으로 쌓지 않게 한다.
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }

        VillageBuildingRegistry reg = VillageBuildingRegistry.Instance;
        if (reg == null) return;

        // 건물 항목들 (이름 + 레벨)
        for (int i = 0; i < reg.Count; i++)
        {
            int idx = i;   // 캡처 방지
            Button item = CreateRow($"{reg.GetName(i)}  Lv.{reg.GetLevel(i)}", new Color(0.76f, 0.77f, 0.73f));
            item.onClick.AddListener(() => reg.Highlight(idx));
        }

        // 맨 아래 [+] 추가 버튼 → 건물 추가 팝업 열기(선택 후 Rebuild)
        Button addBtn = CreateRow("+", new Color(0.6f, 0.7f, 0.55f));
        addBtn.onClick.AddListener(() =>
        {
            if (addPopup != null) addPopup.Open(Rebuild);
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
