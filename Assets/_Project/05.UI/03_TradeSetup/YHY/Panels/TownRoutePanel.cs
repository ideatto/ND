// =============================================================================
// TownRoutePanel — 도시+루트 선택 패널 (무역 준비 1단계, 아코디언)
// =============================================================================
// [담당] Core Gameplay (윤호영)
// [와이어프레임] 도시 리스트에서 도시를 누르면 "그 도시 버튼 아래로" 루트 버튼이 펼쳐지고,
//   아래 도시들은 밀려 내려간다(아코디언). 다른 도시를 누르면 접히고 그 도시가 펼쳐진다.
//   루트를 고르면 이 화면을 닫고 이동수단 선택으로 넘어간다.
//
// [역할] 도시+루트를 "한 화면"에서 처리. (예전 TownSelectPanel/RouteSelectPanel을 합침)
//   - 도시 클릭 → 해당 도시의 루트를 바로 아래에 삽입(펼침), 다시 누르면 접힘.
//   - 루트 클릭 → OnRouteSelected(도시ID, 루트ID, 거리km).
//   데이터는 바깥이 TownEntry(루트 목록 포함)로 넘긴다. SO에 안 묶임.
//
// [애니메이션] 펼침/접힘 연출은 미구현(구조 우선). 지금은 즉시 재배치로 동일한 결과.
// =============================================================================

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>도시+루트 아코디언 패널 — 도시 클릭 시 루트를 아래로 펼침. [1차 빌드]</summary>
public class TownRoutePanel : MonoBehaviour
{
    [Header("리스트 생성")]
    [SerializeField] private Transform listContainer;    // 도시/루트 버튼이 세로로 쌓이는 부모
    [SerializeField] private Button townButtonPrefab;    // 도시 버튼 프리팹 (자식 TMP_Text)
    [SerializeField] private Button routeButtonPrefab;   // 루트 버튼 프리팹 (자식 TMP_Text, 살짝 다른 색)

    [Header("도시 정보 팝업(롱프레스)")]
    [SerializeField] private TownInfoPopup townInfoPopup;   // 마을 0.5초 이상 누르면 표시
    [SerializeField] private float longPressSeconds = 0.5f;

    /// <summary>루트가 선택됐을 때. 인자 = (도시ID, 루트ID, 거리km).</summary>
    public event Action<string, string, float> OnRouteSelected;

    private readonly List<TownEntry> towns = new List<TownEntry>();
    private readonly List<GameObject> spawned = new List<GameObject>();
    private string expandedTownId;   // 현재 펼쳐진 도시(없으면 null)

    /// <summary>도시 목록으로 리스트를 만든다(모두 접힌 상태).</summary>
    public void Populate(IReadOnlyList<TownEntry> source)
    {
        towns.Clear();
        if (source != null) towns.AddRange(source);
        expandedTownId = null;
        Rebuild();
    }

    /// <summary>Framework가 만든 무역 준비 ViewData를 도시/경로 목록에 연결한다.</summary>
    public void Populate(TradePrepareViewData viewData)
    {
        towns.Clear();
        expandedTownId = null;

        if (viewData == null || viewData.towns == null)
        {
            Rebuild();
            return;
        }

        foreach (TownViewData townView in viewData.towns)
        {
            if (townView == null) continue;

            TownEntry town = new TownEntry(townView.townId, townView.displayName, new List<RouteEntry>())
            {
                description = townView.description,
                unlocked = townView.isUnlocked,
                canSelect = townView.canSelect,
                disabledReason = townView.disabledReason
            };

            if (viewData.routes != null)
            {
                foreach (RouteViewData routeView in viewData.routes)
                {
                    if (routeView == null || !IsRouteForTown(routeView, viewData.currentTownId, townView.townId))
                        continue;
                    town.routes.Add(new RouteEntry(routeView));
                }
            }

            towns.Add(town);
        }

        Rebuild();
    }

    /// <summary>현재 상태(펼친 도시 반영)로 버튼 리스트를 다시 만든다.</summary>
    private void Rebuild()
    {
        Clear();
        if (listContainer == null || townButtonPrefab == null) return;

        foreach (TownEntry town in towns)
        {
            // 도시 버튼 — 짧게 탭=선택(펼침), 길게=정보 팝업. 잠긴 도시는 [잠김] 표기.
            Button tb = Instantiate(townButtonPrefab, listContainer);
            string townState = !town.unlocked ? " [잠김]" : (!town.canSelect ? " [선택 불가]" : string.Empty);
            SetLabel(tb, $"{town.name}{townState}");
            string tid = town.id;         // foreach 캡처 방지
            TownEntry captured = town;    // 팝업용 전체 정보 캡처
            LongPressTrigger lp = tb.gameObject.AddComponent<LongPressTrigger>();
            lp.Init(() => ToggleTown(tid), () => ShowTownInfo(captured), longPressSeconds);
            spawned.Add(tb.gameObject);

            // 펼쳐진 도시면 바로 아래에 루트 버튼들 삽입
            if (town.id == expandedTownId && town.routes != null)
            {
                foreach (RouteEntry route in town.routes)
                {
                    Button rb = Instantiate(routeButtonPrefab != null ? routeButtonPrefab : townButtonPrefab, listContainer);
                    string availability = route.canSelect ? string.Empty : $"\n{FallbackReason(route.disabledReason)}";
                    SetLabel(rb,
                        $"└ {route.name} ({route.distanceKm:0.#}km / {route.estimatedTime:0.#}시간)" +
                        $"\n식량 {route.requiredFood} · 전투력 {route.requiredMercenaryPower} · 위험도 {route.riskLevel:0.#}" +
                        availability);
                    string rTid = town.id; string rid = route.id; float dist = route.distanceKm;
                    rb.onClick.AddListener(() => OnRouteSelected?.Invoke(rTid, rid, dist));
                    rb.interactable = route.canSelect;
                    spawned.Add(rb.gameObject);
                }
            }
        }
    }

    /// <summary>도시 정보 팝업 표시(롱프레스).</summary>
    private void ShowTownInfo(TownEntry town)
    {
        if (townInfoPopup != null) townInfoPopup.Show(town);
    }

    /// <summary>도시를 펼치거나(다른 도시였으면 교체), 같은 도시면 접는다.</summary>
    private void ToggleTown(string townId)
    {
        expandedTownId = (expandedTownId == townId) ? null : townId;
        Rebuild();
    }

    private static bool IsRouteForTown(RouteViewData route, string currentTownId, string townId)
    {
        if (string.IsNullOrEmpty(townId)) return false;
        if (!string.IsNullOrEmpty(currentTownId))
            return (route.fromTownId == currentTownId && route.toTownId == townId) ||
                   (route.toTownId == currentTownId && route.fromTownId == townId);
        return route.fromTownId == townId || route.toTownId == townId;
    }

    private static string FallbackReason(string reason)
    {
        return string.IsNullOrWhiteSpace(reason) ? "선택할 수 없습니다." : reason;
    }

    /// <summary>생성된 버튼을 모두 제거한다.
    /// 클릭된 버튼이 자기 자신을 지우는 경로라 DestroyImmediate는 위험 —
    /// "즉시 숨김 + 지연 파괴"로 한 프레임 중복 표시만 막는다.</summary>
    public void Clear()
    {
        foreach (GameObject go in spawned)
            if (go != null) { go.SetActive(false); Destroy(go); }
        spawned.Clear();
    }

    private static void SetLabel(Button b, string text)
    {
        TMP_Text t = b.GetComponentInChildren<TMP_Text>();
        if (t != null) t.text = text;
    }

    // ── 입력 데이터 ─────────────────────────────
    /// <summary>도시 하나 (id + 이름 + 루트 + 정보 팝업용 상세).</summary>
    [Serializable]
    public struct TownEntry
    {
        public string id;
        public string name;
        public List<RouteEntry> routes;

        // ── 정보 팝업용(롱프레스) ──
        public string description;
        public bool unlocked;
        public bool canSelect;
        public string disabledReason;
        public float contributionCurrent;
        public float contributionMax;
        public List<Specialty> specialties;

        public TownEntry(string id, string name, List<RouteEntry> routes)
        {
            this.id = id;
            this.name = name;
            this.routes = routes;
            description = "";
            unlocked = true;
            canSelect = true;
            disabledReason = "";
            contributionCurrent = 0f;
            contributionMax = 0f;
            specialties = null;
        }
    }

    /// <summary>특산품 하나 (표시 이름 + 마우스오버 툴팁 내용).</summary>
    [Serializable]
    public struct Specialty
    {
        public string name;
        public string tooltip;

        public Specialty(string name, string tooltip)
        {
            this.name = name;
            this.tooltip = tooltip;
        }
    }

    /// <summary>루트 하나 (id + 이름 + 거리km + 경유 도시 수).</summary>
    [Serializable]
    public struct RouteEntry
    {
        public string id;
        public string name;
        public float distanceKm;
        public int viaCount;   // 경유 도시 수(0 = 직행)
        public float estimatedTime;
        public int requiredFood;
        public int requiredMercenaryPower;
        public float riskLevel;
        public bool canSelect;
        public string disabledReason;

        public RouteEntry(string id, string name, float distanceKm, int viaCount)
        {
            this.id = id;
            this.name = name;
            this.distanceKm = distanceKm;
            this.viaCount = viaCount;
            estimatedTime = 0f;
            requiredFood = 0;
            requiredMercenaryPower = 0;
            riskLevel = 0f;
            canSelect = true;
            disabledReason = "";
        }

        public RouteEntry(RouteViewData viewData)
        {
            id = viewData.routeId;
            name = viewData.displayName;
            distanceKm = viewData.distance;
            viaCount = 0;
            estimatedTime = viewData.estimatedTime;
            requiredFood = viewData.requiredDraftAnimalFoodQuantity;
            requiredMercenaryPower = viewData.requiredMercenaryPower;
            riskLevel = viewData.riskLevel;
            canSelect = viewData.isUnlocked && viewData.canSelect;
            disabledReason = viewData.disabledReason;
        }
    }
}
