// =============================================================================
// TradePrepareFlowDemo — 무역 준비 플로우 데모 드라이버 (Test 씬 전용)
// =============================================================================
// [담당] Core Gameplay (윤호영)  /  [용도] 와이어프레임 확인용 데모. 실제 빌드엔 안 씀.
//
// [플로우] (Section 9 와이어프레임 + 팀 결정 반영)
//   ① 도시+루트  : 아코디언(도시 클릭 → 아래로 루트 펼침). 루트 클릭 → ②.
//                  도시 롱프레스(0.5초) → 도시 정보 팝업.
//   ② 상단 슬롯  : 내 상단 슬롯(새 게임=전부 빈칸). 슬롯 선택 → 아래 Edit 노출 + Next 활성.
//                  · Edit  → 항상 ③(구성/편집. 저장된 슬롯이면 웨건+동물 복원)
//                  · Next  → 빈 슬롯=③(구성), 저장된 슬롯=④(적재, 저장 구성 사용)
//   ③ 동물(구성) : 왼쪽=웨건(Edit로 넣기/Remove로 빼기, 이미지+슬롯), 오른쪽=동물 인벤토리(그리드).
//                  동물 클릭=바로 1마리 슬롯에, 슬롯 클릭=1마리 빼기. 우상단 Save 체크=슬롯에 저장.
//   ④ 적재       : 아이템 수량 적재(웨건 칸 수만큼 종류 제한). Next → ⑤.
//   ⑤ 요약       : 고른 값 집계 + 출발(데모 로그).
//
// [데이터] 하드코딩 없음 — 더미 값은 TradePrepareDemoData(SO) 에셋에서 읽는다.
//          규칙 검증은 각 패널/CaravanValidator가 담당(여긴 흐름만).
// =============================================================================

using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>무역 준비 플로우를 더미 SO 데이터로 이어주는 데모 드라이버. [Test 씬 전용]</summary>
public class TradePrepareFlowDemo : MonoBehaviour
{
    [Header("더미 데이터(SO)")]
    [SerializeField] private TradePrepareDemoData data;   // 도시/이동수단/동물/아이템 더미 값

    [Header("화면 패널")]
    [SerializeField] private TownRoutePanel townRoutePanel;        // ① 도시+루트
    [SerializeField] private CaravanSlotPanel caravanSlotPanel;    // ② 상단 슬롯
    [SerializeField] private AnimalInventoryPanel animalPanel;     // ③ 동물(구성)
    [SerializeField] private ItemLoadPanel itemPanel;              // ④ 적재
    [SerializeField] private GameObject summaryPanel;              // ⑤ 요약(루트 오브젝트)

    [Header("상단 슬롯")]
    [SerializeField] private int caravanSlotCount = 4;   // 슬롯 개수(새 게임=전부 빈칸)

    [Header("구성 저장")]
    [SerializeField] private Toggle saveToggle;          // 3번 우상단 "구성 저장" 체크박스

    [Header("진행(Next) 버튼")]
    [SerializeField] private Button slotNext;        // ② 슬롯 선택 후 하단 Next
    [SerializeField] private Button animalNext;      // ③ → ④
    [SerializeField] private Button itemNext;        // ④ → ⑤
    [SerializeField] private Button departButton;    // ⑤ 출발

    [Header("뒤로(Back) 버튼")]
    [SerializeField] private Button slotBack;        // ② → ①
    [SerializeField] private Button animalBack;      // ③ → ②
    [SerializeField] private Button itemBack;        // ④ → ③
    [SerializeField] private Button summaryBack;     // ⑤ → ④

    [Header("요약")]
    [SerializeField] private TMP_Text summaryText;

    // ── 선택 상태 ────────────────────────────────
    private string townName = "-", routeName = "-";
    private float distanceKm;
    private bool hasTransport;
    private TransportSelectPanel.TransportEntry transport;
    private string[] caravanSlots;      // 슬롯별 표시 요약(""=빈칸)
    private SlotComp[] slotData;        // 슬롯별 구조화 저장(웨건+동물)
    private int composingSlotIndex = -1; // 지금 구성 중인 슬롯
    private bool itemsFromSavedSlot;     // 적재(④)에 저장슬롯 Next로 직행했나 → Back 목적지 분기

    /// <summary>슬롯 하나에 저장된 상단 구성(구조화).</summary>
    private class SlotComp
    {
        public bool filled;
        public bool hasWagon;
        public TransportSelectPanel.TransportEntry wagon;
        public List<AnimalInventoryPanel.AnimalPick> animals = new List<AnimalInventoryPanel.AnimalPick>();
    }
    private readonly List<AnimalInventoryPanel.AnimalPick> pickedAnimals = new List<AnimalInventoryPanel.AnimalPick>();
    private readonly List<ItemLoadPanel.ItemLoad> pickedItems = new List<ItemLoadPanel.ItemLoad>();

    // 이름표(요약에 id 대신 이름) — data에서 자동 생성
    private readonly Dictionary<string, string> townNames = new Dictionary<string, string>();
    private readonly Dictionary<string, string> routeNames = new Dictionary<string, string>();
    private readonly Dictionary<string, string> animalNames = new Dictionary<string, string>();
    private readonly Dictionary<string, string> itemNames = new Dictionary<string, string>();

    private void Start()
    {
        if (data == null)
        {
            Debug.LogWarning("[Prepare Demo] TradePrepareDemoData(SO)가 비었음 — FlowDemo의 data 필드에 더미 에셋을 연결하세요.");
            return;
        }

        // ── 구독 ──
        if (townRoutePanel != null) townRoutePanel.OnRouteSelected += OnRouteSelected;
        if (caravanSlotPanel != null) { caravanSlotPanel.OnSlotSelected += OnSlotSelected; caravanSlotPanel.OnEditRequested += OnEditRequested; }
        if (animalPanel != null)
        {
            animalPanel.OnSelectionChanged += OnAnimalsChanged;
            animalPanel.OnWagonSelected += OnWagonPlaced;
            animalPanel.OnWagonRemoved += OnWagonRemoved;
        }
        if (itemPanel != null)      itemPanel.OnLoadChanged += OnItemsChanged;
        if (saveToggle != null)    saveToggle.onValueChanged.AddListener(OnSaveToggle);

        // ── Next ──
        if (slotNext != null)      slotNext.onClick.AddListener(FromSlotNext);   // 슬롯 선택 후 진행
        if (animalNext != null)    animalNext.onClick.AddListener(GoItems);
        if (itemNext != null)      itemNext.onClick.AddListener(GoSummary);
        if (departButton != null)  departButton.onClick.AddListener(Depart);

        // ── Back ──
        if (slotBack != null)      slotBack.onClick.AddListener(GoTownRoute);
        if (animalBack != null)    animalBack.onClick.AddListener(GoCaravanSlot);   // 동물 → 상단 슬롯
        if (itemBack != null)      itemBack.onClick.AddListener(BackFromItems);
        if (summaryBack != null)   summaryBack.onClick.AddListener(GoItems);

        // ── 슬롯 저장소 초기화(전부 빈칸) ──
        int n = Mathf.Max(1, caravanSlotCount);
        caravanSlots = new string[n];                                // 표시 요약
        slotData = new SlotComp[n];                                  // 구조화 저장
        for (int i = 0; i < n; i++) slotData[i] = new SlotComp();

        BuildNameTables();

        // ① 도시+루트는 시작 시 한 번 채운다(진짜 SO → DTO 변환).
        if (townRoutePanel != null) townRoutePanel.Populate(BuildTownEntries());

        GoTownRoute();
    }

    // ══ 어댑터: 진짜 SO → 패널 DTO ═══════════════════
    // 패널은 이종현님 SO 타입을 모른다(중립 DTO만 받음). 변환은 데모 드라이버가 담당.

    /// <summary>TownData[] → 도시+루트 DTO (루트는 TownData.AvailableRoutes에서).</summary>
    private List<TownRoutePanel.TownEntry> BuildTownEntries()
    {
        List<TownRoutePanel.TownEntry> result = new List<TownRoutePanel.TownEntry>();
        if (data.towns == null) return result;
        foreach (TownData t in data.towns)
        {
            if (t == null) continue;
            List<TownRoutePanel.RouteEntry> routes = new List<TownRoutePanel.RouteEntry>();
            foreach (RouteData r in t.AvailableRoutes)
                if (r != null)
                    routes.Add(new TownRoutePanel.RouteEntry(r.RouteId, r.DisplayName, r.Distance, 0)); // via수는 SO에 없음 → 0

            // 정보 팝업용 상세 (TownData에서)
            TownRoutePanel.TownEntry entry = new TownRoutePanel.TownEntry(t.TownId, t.DisplayName, routes);
            entry.description = t.Description;
            entry.unlocked = t.UnlockedByDefault;
            entry.contributionMax = t.MaximumContributionLimit;
            entry.contributionCurrent = 0f;                 // 현재 기여도는 런타임값 → 데모는 0
            entry.specialties = DummySpecialties(t.TownId); // 특산품(데모 더미)
            result.Add(entry);
        }
        return result;
    }

    /// <summary>WagonData[] → 이동수단 DTO (WagonType → TransportType 매핑).</summary>
    private List<TransportSelectPanel.TransportEntry> BuildTransportEntries()
    {
        List<TransportSelectPanel.TransportEntry> result = new List<TransportSelectPanel.TransportEntry>();
        if (data.transports == null) return result;
        for (int i = 0; i < data.transports.Length; i++)
        {
            WagonData w = data.transports[i];
            if (w == null) continue;
            // 소지 개수: transportOwned 배열에서(없으면 0=빈 슬롯). 도보(None)는 패널이 항상 허용.
            int owned = (data.transportOwned != null && i < data.transportOwned.Length) ? data.transportOwned[i] : 0;
            result.Add(new TransportSelectPanel.TransportEntry(
                w.WagonId, w.DisplayName, MapType(w.WagonType),
                w.InventorySlotCount, w.MaxLoad, w.MinRequireAnimals, w.MaxPullAnimals,
                w.BaseMoveSpeed, owned));
        }
        return result;
    }

    /// <summary>도시별 특산품 더미(데모). 이름 + 마우스오버 툴팁(아이템 정보).</summary>
    private List<TownRoutePanel.Specialty> DummySpecialties(string townId)
    {
        string[] names;
        if (townId == "town_a") names = new string[] { "Wheat", "Wool" };
        else if (townId == "town_b") names = new string[] { "Fish", "Silk" };
        else if (townId == "town_c") names = new string[] { "Ore", "Gems" };
        else names = new string[0];

        List<TownRoutePanel.Specialty> result = new List<TownRoutePanel.Specialty>();
        foreach (string n in names) result.Add(new TownRoutePanel.Specialty(n, ItemTooltip(n)));
        return result;
    }

    /// <summary>아이템 이름으로 TradeItemData를 찾아 툴팁 문자열을 만든다(없으면 일반).</summary>
    private string ItemTooltip(string itemName)
    {
        if (data.items != null)
            foreach (TradeItemData it in data.items)
                if (it != null && it.DisplayName == itemName)
                    return $"{it.DisplayName}\nWeight {it.Weight:0.#}\nCategory {it.Category}\n" +
                           $"Buy {it.BaseBuyPrice} / Sell {it.BaseSellPrice}";
        return $"{itemName}\n(specialty item)";
    }

    /// <summary>웨건 선택 팝업용 목록 — 도보(None)는 항상, 마차/자동차(Mount)는 소지분만.
    /// None/Mount는 동물 요구량 0~0이라 동물 없이 바로 요구량 충족(OK)이 된다.</summary>
    private List<TransportSelectPanel.TransportEntry> BuildOwnedWagons()
    {
        List<TransportSelectPanel.TransportEntry> result = new List<TransportSelectPanel.TransportEntry>();
        foreach (TransportSelectPanel.TransportEntry t in BuildTransportEntries())
        {
            if (t.type == TransportType.None) result.Add(t);   // 도보 — 소지 개념 없음
            else if (t.owned > 0) result.Add(t);               // 마차·자동차 — 소지한 것만
        }
        return result;
    }

    /// <summary>DraftAnimalData[] → 동물 DTO (소지 수는 SO에 없어 MaxCount를 더미로 사용).</summary>
    private List<AnimalInventoryPanel.AnimalEntry> BuildAnimalEntries()
    {
        List<AnimalInventoryPanel.AnimalEntry> result = new List<AnimalInventoryPanel.AnimalEntry>();
        if (data.animals == null) return result;
        foreach (DraftAnimalData a in data.animals)
            if (a != null)
                result.Add(new AnimalInventoryPanel.AnimalEntry(
                    a.DraftAnimalId, a.DisplayName, a.MaxCount,
                    a.BaseMoveSpeed, a.IncreaseOverLoad, a.IncreaseMaxLoad, a.FeedConsumption));
        return result;
    }

    /// <summary>TradeItemData[] → 아이템 DTO.</summary>
    private List<ItemLoadPanel.ItemEntry> BuildItemEntries()
    {
        List<ItemLoadPanel.ItemEntry> result = new List<ItemLoadPanel.ItemEntry>();
        if (data.items == null) return result;
        foreach (TradeItemData it in data.items)
            if (it != null)
                result.Add(new ItemLoadPanel.ItemEntry(it.ItemId, it.DisplayName, it.Weight, it.MaxCount));
        return result;
    }

    /// <summary>WagonType(이종현님) → TransportType(데모 분기용) 매핑.</summary>
    private static TransportType MapType(WagonType t)
    {
        if (t == WagonType.WagonWithAnimals) return TransportType.Wagon;
        if (t == WagonType.Mount) return TransportType.Mount;
        return TransportType.None;
    }

    /// <summary>SO에서 id→이름 표를 만든다(요약 표시용).</summary>
    private void BuildNameTables()
    {
        townNames.Clear(); routeNames.Clear(); animalNames.Clear(); itemNames.Clear();
        if (data.towns != null)
            foreach (TownData t in data.towns)
            {
                if (t == null) continue;
                townNames[t.TownId] = t.DisplayName;
                foreach (RouteData r in t.AvailableRoutes)
                    if (r != null) routeNames[r.RouteId] = r.DisplayName;
            }
        if (data.animals != null)
            foreach (DraftAnimalData a in data.animals)
                if (a != null) animalNames[a.DraftAnimalId] = a.DisplayName;
        if (data.items != null)
            foreach (TradeItemData it in data.items)
                if (it != null) itemNames[it.ItemId] = it.DisplayName;
    }

    // ── ① 루트 선택 → ② 이동수단 ──
    private void OnRouteSelected(string townId, string routeId, float distance)
    {
        distanceKm = distance;
        townName = NameOrId(townNames, townId);
        routeName = $"{NameOrId(routeNames, routeId)} ({distance:0.#}km)";
        GoCaravanSlot();
    }

    // ── ② 상단 슬롯 선택 → 하단 Next만 활성(자동 진행 안 함). Edit는 별도 버튼. ──
    private void OnSlotSelected(int slotIndex)
    {
        composingSlotIndex = slotIndex;   // 구성 대상 슬롯 기억
        if (slotNext != null) slotNext.interactable = slotIndex >= 0;
    }

    // 선택 슬롯의 [Edit] → 항상 3번(구성/편집). 빈 슬롯이든 저장된 슬롯이든.
    private void OnEditRequested(int slotIndex)
    {
        hasTransport = false;   // 3번에서 웨건부터 다시 넣음(편집)
        GoAnimals();
    }

    // 하단 [Next] → 빈 슬롯이면 3번(구성), 저장된 슬롯이면 4번(적재).
    private void FromSlotNext()
    {
        bool empty = caravanSlotPanel != null && caravanSlotPanel.IsSelectedEmpty();
        if (empty)
        {
            hasTransport = false;
            GoAnimals();   // 빈 슬롯 → 3번(구성)
        }
        else
        {
            // 저장된 슬롯 → 4번(적재). "그 슬롯에 저장된" 웨건·동물을 그대로 사용.
            SlotComp s = (slotData != null && composingSlotIndex >= 0 && composingSlotIndex < slotData.Length)
                         ? slotData[composingSlotIndex] : null;
            hasTransport = s != null && s.filled && s.hasWagon;
            if (hasTransport) transport = s.wagon;
            pickedAnimals.Clear();
            if (s != null) pickedAnimals.AddRange(s.animals);   // 요약이 저장된 동물을 보여주도록
            itemsFromSavedSlot = true;                          // 적재 Back → 슬롯화면으로
            GoItems();
        }
    }

    // 동물 화면에서 웨건을 넣었을 때 — 이후 적재 슬롯 수 계산에 사용
    private void OnWagonPlaced(TransportSelectPanel.TransportEntry w)
    {
        transport = w;
        hasTransport = true;
    }

    // 동물 화면에서 웨건을 뺐을 때 — 드라이버 상태 동기화.
    // 웨건 없는 구성은 저장 대상이 아니므로, 저장돼 있었으면 저장도 해제한다.
    private void OnWagonRemoved()
    {
        hasTransport = false;
        if (saveToggle != null && saveToggle.isOn)
        {
            saveToggle.SetIsOnWithoutNotify(false);
            OnSaveToggle(false);   // 슬롯 저장 해제(빈칸으로)
        }
    }

    // ── ③ 동물 선택 변경 → Next(요구량 충족 시) ──
    private void OnAnimalsChanged(IReadOnlyList<AnimalInventoryPanel.AnimalPick> picks, bool valid)
    {
        pickedAnimals.Clear();
        pickedAnimals.AddRange(picks);
        if (animalNext != null) animalNext.interactable = valid;
        // 저장 체크돼 있으면 슬롯 요약도 실시간 갱신
        if (saveToggle != null && saveToggle.isOn) OnSaveToggle(true);
    }

    // ── ④ 적재 변경 ──
    private void OnItemsChanged(IReadOnlyList<ItemLoadPanel.ItemLoad> load)
    {
        pickedItems.Clear();
        pickedItems.AddRange(load);
    }

    // ══ 화면 전환 ══════════════════════════════════
    private void GoTownRoute() { ShowOnly(0); }

    private void GoCaravanSlot()
    {
        if (caravanSlotPanel != null)
        {
            List<string> slots = new List<string>();
            for (int i = 0; i < caravanSlotCount; i++)
                slots.Add((caravanSlots != null && i < caravanSlots.Length) ? (caravanSlots[i] ?? "") : "");
            caravanSlotPanel.Populate(slots);   // 저장된 슬롯은 요약 표시, 나머지는 빈칸
            caravanSlotPanel.ResetSelection();
        }
        if (slotNext != null) slotNext.interactable = false;   // 슬롯 고르기 전엔 Next 잠금
        ShowOnly(1);
    }


    private void GoAnimals()
    {
        itemsFromSavedSlot = false;   // 동물(구성)을 거치면 적재 Back은 동물로
        pickedAnimals.Clear();
        if (animalNext != null) animalNext.interactable = false;   // 기본 잠금(복원되면 아래서 다시 켜짐)

        if (animalPanel != null)
        {
            animalPanel.Populate(BuildAnimalEntries(), BuildOwnedWagons());   // 웨건은 화면 안 Edit로 선택
            // 저장된 슬롯을 Edit로 열면 웨건+동물 그대로 복원 (OnAnimalsChanged로 pickedAnimals·Next 갱신됨)
            if (slotData != null && composingSlotIndex >= 0 && composingSlotIndex < slotData.Length
                && slotData[composingSlotIndex].filled && slotData[composingSlotIndex].hasWagon)
            {
                animalPanel.RestoreComposition(slotData[composingSlotIndex].wagon, slotData[composingSlotIndex].animals);
            }
        }
        // 저장 체크박스: 이 슬롯이 이미 저장돼 있으면 체크 상태로(알림 없이)
        if (saveToggle != null)
        {
            bool saved = composingSlotIndex >= 0 && composingSlotIndex < caravanSlots.Length
                         && !string.IsNullOrEmpty(caravanSlots[composingSlotIndex]);
            saveToggle.SetIsOnWithoutNotify(saved);
        }
        ShowOnly(2);
    }

    // 3번 우상단 "구성 저장" 체크박스 → 슬롯에 저장/해제 (구조화 + 표시요약)
    private void OnSaveToggle(bool on)
    {
        if (slotData == null || composingSlotIndex < 0 || composingSlotIndex >= slotData.Length) return;
        SlotComp s = slotData[composingSlotIndex];
        if (on)
        {
            s.filled = true;
            s.hasWagon = hasTransport;
            s.wagon = transport;
            s.animals = new List<AnimalInventoryPanel.AnimalPick>(pickedAnimals);
            caravanSlots[composingSlotIndex] = BuildCaravanSummary();
        }
        else
        {
            s.filled = false; s.hasWagon = false; s.animals.Clear();
            caravanSlots[composingSlotIndex] = "";
        }
    }

    // 지금 구성 중인 상단을 슬롯에 표시할 요약으로.
    private string BuildCaravanSummary()
    {
        string wag = hasTransport ? transport.name : "-";
        if (pickedAnimals.Count == 0) return wag;
        List<string> parts = new List<string>();
        foreach (AnimalInventoryPanel.AnimalPick a in pickedAnimals)
            parts.Add($"{NameOrId(animalNames, a.animalId)}x{a.count}");
        return $"{wag}\n{string.Join(",", parts)}";
    }

    private void GoItems()
    {
        if (itemPanel != null)
            itemPanel.Populate(BuildItemEntries(), hasTransport ? transport.slotCount : 2);
        ShowOnly(3);
    }

    // 적재의 Back — 저장슬롯에서 직행했으면 슬롯화면(②)으로, 구성을 거쳤으면 동물(③)로.
    private void BackFromItems()
    {
        if (itemsFromSavedSlot) GoCaravanSlot();
        else GoAnimals();
    }

    private void GoSummary()
    {
        if (summaryText != null) summaryText.text = BuildSummary();
        ShowOnly(4);
    }

    private void Depart()
    {
        Debug.Log("[Prepare Demo] Depart!\n" + BuildSummary());
    }

    /// <summary>화면 인덱스만 켠다. 0:도시루트 1:상단슬롯 2:동물 3:적재 4:요약</summary>
    private void ShowOnly(int idx)
    {
        SetActive(townRoutePanel, idx == 0);
        SetActive(caravanSlotPanel, idx == 1);
        SetActive(animalPanel, idx == 2);
        SetActive(itemPanel, idx == 3);
        if (summaryPanel != null) summaryPanel.SetActive(idx == 4);
    }

    private static void SetActive(Component panel, bool on)
    {
        if (panel != null) panel.gameObject.SetActive(on);
    }

    /// <summary>고른 값 요약 텍스트.</summary>
    private string BuildSummary()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("== Trade Prepare Summary ==");
        sb.AppendLine($"Town: {townName}");
        sb.AppendLine($"Route: {routeName}");
        sb.AppendLine($"Transport: {(hasTransport ? $"{transport.name} [{transport.type}]" : "-")}");

        sb.Append("Animals: ");
        if (pickedAnimals.Count == 0) sb.AppendLine("(none)");
        else
        {
            List<string> parts = new List<string>();
            foreach (AnimalInventoryPanel.AnimalPick a in pickedAnimals)
                parts.Add($"{NameOrId(animalNames, a.animalId)} x{a.count}");
            sb.AppendLine(string.Join(", ", parts));
        }

        sb.Append("Cargo: ");
        if (pickedItems.Count == 0) sb.AppendLine("(none)");
        else
        {
            List<string> parts = new List<string>();
            foreach (ItemLoadPanel.ItemLoad it in pickedItems)
                parts.Add($"{NameOrId(itemNames, it.itemId)} x{it.count}");
            sb.AppendLine(string.Join(", ", parts));
        }
        return sb.ToString();
    }

    private static string NameOrId(Dictionary<string, string> map, string id)
    {
        return map != null && map.TryGetValue(id, out string n) ? n : id;
    }
}
