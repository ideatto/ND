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

using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>건물 추가 팝업 — 카탈로그 리스트에서 선택.</summary>
public class BuildingAddPopup : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private RectTransform content;   // 카탈로그 항목이 쌓일 곳
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float itemHeight = 60f;

    // 비용 건설 경로에서는 직접 건물을 추가하지 않고 선택한 BuildData를 Detail/Confirm UI에 전달한다.
    [SerializeField] private BuildingPopupRuntimeBinding popupRuntimeBinding;

    // 기존 무료 즉시 추가 경로와 신규 비용 검증 경로를 함께 보존하기 위한 실행 모드다.
    // true: AddOrUpgrade 즉시 실행, false: Detail Popup 표시
    private bool useImmediateAdd;

    private Action onAdded;

    /// <summary>
    /// 기존 즉시 추가 경로로 Popup을 연다.
    /// 초기 구성, 디버그 또는 명시적인 무료 추가 호출의 기존 동작을 보존한다.
    /// </summary>
    public void Open(Action onAddedCallback)
    {

        CancelPlacementSelection();
        useImmediateAdd = true;
        onAdded = onAddedCallback;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        BuildCatalog();
    }

    /// <summary>
    /// 비용 검증이 필요한 사용자 건설 경로로 Popup을 연다.
    /// 항목 선택 시 AddOrUpgrade를 호출하지 않고 Detail Popup을 표시한다.
    /// </summary>
    public void Open()
    {

        CancelPlacementSelection();
        useImmediateAdd = false;
        // 이전에 즉시 추가 모드로 열었을 때 받은 콜백이 비용 건설 경로에서 실행되지 않게 한다.
        onAdded = null;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        BuildCatalog();
    }

    /// <summary>팝업 닫기.</summary>
    public void Close()
    {
        gameObject.SetActive(false);
    }

/// <summary>
    /// Popup 바깥의 실제 Backdrop(root Image)을 클릭했을 때만 닫는다.
    /// Card와 내부 버튼 클릭은 자식 Graphic이 Raycast를 받으므로 여기서 닫히지 않는다.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.pointerCurrentRaycast.gameObject == gameObject)
        {
            Close();
        }
    }

    private void CancelPlacementSelection()
    {
        // OnGUI 회전 버튼은 Canvas 정렬과 무관하므로 Popup을 열기 전에 선택 자체를 종료해야 한다.
        BuildingPlacementController placementController = FindAnyObjectByType<BuildingPlacementController>();
        placementController?.CancelPlacementSelection();
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
                if (useImmediateAdd)
                {
                    // 기존 호출자를 위한 명시적 무료 추가 경로. 비용 트랜잭션 성공 후에는 사용하지 않는다.
                    reg.AddOrUpgrade(idx); // 있으면 레벨업, 없으면 신축(Lv.1)

                    if (onAdded != null)
                    {
                        onAdded.Invoke();
                    }

                    Close();
                    return;
                }

                // 일반 사용자 건설은 여기서 상태를 변경하지 않고 Popup에 선택 정보만 전달한다.
                BuildData buildData = reg.GetCatalogBuildData(idx);

                if(buildData == null)
                {
                    Debug.LogError($"BuildingAddPopup: no BuildData is assigned to catalog index {idx}.", this);
                    return;
                }

                if(popupRuntimeBinding == null)
                {
                    Debug.LogError("BuildingAddPopup: BuildingPopupRuntimeBinding is not assigned.", this);
                    return;
                }

                int currentLevel = reg.GetCatalogLevel(idx);

                popupRuntimeBinding.OpenDetail(buildData, currentLevel);

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
