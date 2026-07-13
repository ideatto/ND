// =============================================================================
// CaravanSlotPanel — 상단(캐러밴) 슬롯 선택 패널
// =============================================================================
// [담당] Core Gameplay (윤호영)
// [역할] 내 상단 슬롯 목록을 만들고, 하나 고르면:
//        - 선택한 슬롯 아래에 슬롯 폭만큼 [Edit] 버튼이 나타난다(그 상단을 구성/편집).
//        - 선택 자체로 자동 진행하지 않는다(Edit 또는 하단 Next로 진행).
//        contents[i]가 비면 빈 슬롯, 값이 있으면 채워진 상단 이름을 표시.
// =============================================================================

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>상단 슬롯 선택 패널 — 슬롯 선택 시 아래에 Edit 버튼 노출. [1차 빌드]</summary>
public class CaravanSlotPanel : MonoBehaviour
{
    [Header("슬롯 생성")]
    [SerializeField] private Transform slotContainer;   // 슬롯 컬럼이 가로로 담기는 부모 (Horizontal)
    [SerializeField] private Button slotPrefab;         // 슬롯 카드 프리팹 (자식 TMP_Text)

    [Header("색")]
    [SerializeField] private Color emptyColor = new Color(0.88f, 0.88f, 0.90f);
    [SerializeField] private Color selectedColor = new Color(0.45f, 0.70f, 0.45f);
    [SerializeField] private Color editColor = new Color(0.95f, 0.85f, 0.55f);

    /// <summary>슬롯이 선택될 때. 인자 = 슬롯 index(해제 시 -1).</summary>
    public event Action<int> OnSlotSelected;
    /// <summary>선택 슬롯의 [Edit] 클릭 시. 인자 = 슬롯 index.</summary>
    public event Action<int> OnEditRequested;

    private readonly List<Button> cards = new List<Button>();
    private readonly List<GameObject> editButtons = new List<GameObject>();
    private readonly List<string> slotContents = new List<string>();   // 각 슬롯 내용(빈 문자열=빈 슬롯)
    private int selectedIndex = -1;

    /// <summary>슬롯을 만든다. contents[i]가 비면 빈 슬롯, 값이 있으면 채워진 상단 이름.</summary>
    public void Populate(IReadOnlyList<string> contents)
    {
        Clear();
        if (slotContainer == null || slotPrefab == null) return;

        int count = contents != null ? contents.Count : 0;
        for (int i = 0; i < count; i++)
        {
            slotContents.Add(contents[i] ?? "");
            // 슬롯 컬럼(세로): [카드] + [Edit 버튼(숨김)]
            GameObject col = new GameObject("SlotCol", typeof(RectTransform));
            col.transform.SetParent(slotContainer, false);
            VerticalLayoutGroup vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8; vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
            LayoutElement colLE = col.AddComponent<LayoutElement>();
            colLE.preferredWidth = 220;   // 슬롯 폭

            // 카드
            string filled = contents[i];
            bool isEmpty = string.IsNullOrEmpty(filled);
            Button card = Instantiate(slotPrefab, col.transform);
            TMP_Text t = card.GetComponentInChildren<TMP_Text>();
            if (t != null) t.text = isEmpty ? $"Slot {i + 1}\n[ Empty ]" : $"Slot {i + 1}\n{filled}";
            SetColor(card, emptyColor);
            int idx = i;   // 캡처 방지
            card.onClick.AddListener(() => Select(idx));
            cards.Add(card);

            // Edit 버튼(슬롯 폭만큼, 아래, 처음엔 숨김)
            GameObject edit = BuildEditButton(col.transform, idx);
            edit.SetActive(false);
            editButtons.Add(edit);
        }
    }

    /// <summary>슬롯 폭만큼의 Edit 버튼을 코드로 만든다(카드 아래에 배치).</summary>
    private GameObject BuildEditButton(Transform parent, int idx)
    {
        GameObject go = new GameObject("EditButton", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>(); img.color = editColor;
        Button btn = go.AddComponent<Button>();
        LayoutElement le = go.AddComponent<LayoutElement>(); le.minHeight = 60; le.preferredHeight = 60;

        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        RectTransform lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        TextMeshProUGUI label = labelGO.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        label.text = "Edit"; label.fontSize = 26; label.color = Color.black; label.alignment = TextAlignmentOptions.Center;

        btn.onClick.AddListener(() => OnEditRequested?.Invoke(idx));
        return go;
    }

    /// <summary>idx 슬롯 선택(다시 누르면 해제). 색·Edit 노출 갱신 + 통지. 자동 진행 안 함.</summary>
    private void Select(int idx)
    {
        selectedIndex = (selectedIndex == idx) ? -1 : idx;
        for (int i = 0; i < cards.Count; i++)
        {
            SetColor(cards[i], i == selectedIndex ? selectedColor : emptyColor);
            editButtons[i].SetActive(i == selectedIndex);   // 선택된 슬롯만 Edit 노출
        }
        OnSlotSelected?.Invoke(selectedIndex);
    }

    /// <summary>현재 선택 슬롯 index(없으면 -1).</summary>
    public int SelectedIndex => selectedIndex;

    /// <summary>현재 선택된 슬롯이 빈 슬롯인가(미선택이면 false).</summary>
    public bool IsSelectedEmpty()
    {
        return selectedIndex >= 0 && selectedIndex < slotContents.Count
               && string.IsNullOrEmpty(slotContents[selectedIndex]);
    }

    /// <summary>선택 해제(재진입 시 잔상 제거).</summary>
    public void ResetSelection()
    {
        selectedIndex = -1;
        for (int i = 0; i < cards.Count; i++)
        {
            SetColor(cards[i], emptyColor);
            editButtons[i].SetActive(false);
        }
        OnSlotSelected?.Invoke(-1);
    }

    /// <summary>슬롯을 모두 제거한다.</summary>
    public void Clear()
    {
        foreach (Button b in cards)
            if (b != null && b.transform.parent != null) DestroyImmediate(b.transform.parent.gameObject);   // 컬럼째 즉시 제거(잔상 방지)
        cards.Clear();
        editButtons.Clear();
        slotContents.Clear();
        selectedIndex = -1;
    }

    private static void SetColor(Button b, Color c)
    {
        Image img = b.GetComponent<Image>();
        if (img != null) img.color = c;
    }
}
