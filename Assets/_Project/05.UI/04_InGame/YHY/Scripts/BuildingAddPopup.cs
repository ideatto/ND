// =============================================================================
// BuildingAddPopup — 건물 추가 팝업 (카탈로그에서 선택)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 건물 리스트의 [+] 버튼이 여는 팝업. 추가 가능한 건물 종류(카탈로그)를
//        버튼 리스트로 보여주고, 하나를 선택하면 마을에 그 건물을 추가한다.
//        추가 후 onAdded 콜백으로 건물 리스트를 갱신하고 팝업을 닫는다.
// =============================================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>건물 추가 팝업 — 카탈로그 리스트에서 선택.</summary>
public class BuildingAddPopup : MonoBehaviour
{
    [SerializeField] private RectTransform content;   // 카탈로그 항목이 쌓일 곳
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float itemHeight = 60f;

    private Action onAdded;

    /// <summary>팝업 열기(추가 완료 콜백 전달).</summary>
    public void Open(Action onAddedCallback)
    {
        onAdded = onAddedCallback;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        BuildCatalog();
    }

    /// <summary>팝업 닫기.</summary>
    public void Close()
    {
        gameObject.SetActive(false);
    }

    private void BuildCatalog()
    {
        if (content == null) return;
        // 기존 항목 즉시 제거
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }

        VillageBuildingRegistry reg = VillageBuildingRegistry.Instance;
        if (reg == null) return;

        for (int i = 0; i < reg.CatalogCount; i++)
        {
            int idx = i;   // 캡처 방지
            // 있는 건물 = Lv.n, 없는 건물 = Lv.0
            Button row = CreateRow($"{reg.GetCatalogName(i)}  Lv.{reg.GetCatalogLevel(i)}");
            row.onClick.AddListener(() =>
            {
                reg.AddOrUpgrade(idx);   // 있으면 레벨업, 없으면 신축(Lv.1)
                if (onAdded != null) onAdded.Invoke();
                Close();
            });
        }
    }

    private Button CreateRow(string label)
    {
        GameObject go = new GameObject("CatalogRow",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(content, false);
        go.GetComponent<LayoutElement>().minHeight = itemHeight;

        Image img = go.GetComponent<Image>();
        img.color = new Color(0.78f, 0.79f, 0.75f);
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
        t.fontSize = 30f;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.2f, 0.2f, 0.2f);
        t.raycastTarget = false;

        return btn;
    }
}
