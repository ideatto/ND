// =============================================================================
// TownInfoPopup — 도시 정보 팝업 (1번 화면, 마을 롱프레스 시)
// =============================================================================
// [담당] Core Gameplay (윤호영)
// [와이어프레임] 도시 패널을 0.5초 이상 누르면: 도시 이름·설명·이미지·활성여부·
//   특산품 리스트·현재/최대 기여도를 보여준다. 확인 선택 시 닫힘.
//
// [역할] 도시 정보(TownRoutePanel.TownEntry)를 받아 표시. 순수 표시 UI.
// =============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>도시 정보 팝업 — 이름·설명·활성·특산품·기여도. [1차 빌드]</summary>
public class TownInfoPopup : MonoBehaviour
{
    [Header("표시")]
    [SerializeField] private TMP_Text nameText;        // 도시 이름
    [SerializeField] private TMP_Text descText;        // 설명(왼쪽 이미지 아래)
    [SerializeField] private TMP_Text statusText;      // 활성 여부(옵션)
    [SerializeField] private TMP_Text contribText;     // 현재/최대 기여도(오른쪽 위)

    [Header("특산품 아이콘 그리드(오른쪽 아래)")]
    [SerializeField] private Transform specialtyContainer;   // 특산품 칸이 담기는 부모(Grid)
    [SerializeField] private Button specialtyCellPrefab;     // 특산품 칸 프리팹(없으면 코드로 생성)
    [SerializeField] private AnimalTooltip tooltip;          // 아이템 정보 마우스오버 툴팁(공용)

    [Header("버튼")]
    [SerializeField] private Button confirmButton;     // 확인(닫기)

    private bool wired;
    private readonly List<GameObject> specialtyCells = new List<GameObject>();

    private void EnsureWired()
    {
        if (wired) return;
        wired = true;
        if (confirmButton != null) confirmButton.onClick.AddListener(Hide);
    }

    /// <summary>도시 정보를 채워 팝업을 연다.</summary>
    public void Show(TownRoutePanel.TownEntry town)
    {
        EnsureWired();
        if (nameText != null) nameText.text = town.name;
        if (descText != null) descText.text = string.IsNullOrEmpty(town.description) ? "(설명 없음)" : town.description;
        if (statusText != null) statusText.text = town.unlocked ? "개방" : "잠김";
        if (contribText != null)
            contribText.text = $"기여도\n{town.contributionCurrent:0} / {town.contributionMax:0}";
        BuildSpecialties(town.specialties);
        gameObject.SetActive(true);
    }

    /// <summary>특산품을 인벤토리 칸(아이콘 자리+이름)으로 만든다. 각 칸에 마우스오버 툴팁 부착.</summary>
    private void BuildSpecialties(List<TownRoutePanel.Specialty> specialties)
    {
        foreach (GameObject go in specialtyCells) if (go != null) DestroyImmediate(go);
        specialtyCells.Clear();
        if (specialtyContainer == null || specialties == null) return;

        foreach (TownRoutePanel.Specialty s in specialties)
        {
            GameObject cell = BuildCell(s.name);
            // 마우스오버 → 아이템 정보 툴팁
            AnimalTooltipTrigger trig = cell.AddComponent<AnimalTooltipTrigger>();
            trig.Init(tooltip, s.tooltip);
            specialtyCells.Add(cell);
        }
    }

    /// <summary>특산품 칸 하나를 코드로 만든다(아이콘 박스 + 이름).</summary>
    private GameObject BuildCell(string label)
    {
        GameObject cell = new GameObject("Specialty", typeof(RectTransform));
        cell.transform.SetParent(specialtyContainer, false);
        Image icon = cell.AddComponent<Image>();
        icon.color = new Color(0.85f, 0.82f, 0.70f);   // 아이콘 자리(더미)

        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(cell.transform, false);
        RectTransform lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        TextMeshProUGUI t = labelGO.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        t.text = label; t.fontSize = 18; t.color = Color.black; t.alignment = TextAlignmentOptions.Center; t.enableWordWrapping = true;
        return cell;
    }

    /// <summary>확인 → 닫기.</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
