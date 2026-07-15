// =============================================================================
// TradePrepareFlowDemo — 무역 준비 데모 데이터 공급자 (Test 씬 전용)
// =============================================================================
// [담당] Core Gameplay (윤호영)  /  [용도] 와이어프레임 확인용 데모. 실제 빌드엔 안 씀.
//
// [역할] 화면 전환·상태 관리는 전부 TradePrepareUIManager(제품 코드)가 한다.
//        여기는 "더미 SO → 패널 DTO 어댑터"와 프로바이더 주입, Begin() 호출만 담당.
//        실제 게임 통합 시 이 파일 대신 진짜 데이터 소스가 같은 프로바이더를 주입하면 된다.
//
// [데이터] 하드코딩 없음 — 더미 값은 TradePrepareDemoData(SO) 에셋에서 읽는다.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>무역 준비 플로우에 더미 SO 데이터를 공급하는 데모 드라이버. [Test 씬 전용]</summary>
public class TradePrepareFlowDemo : MonoBehaviour
{
    [Header("더미 데이터(SO)")]
    [SerializeField] private TradePrepareDemoData data;   // 도시/이동수단/동물/아이템/골드 더미 값

    [Header("UI 매니저(제품 코드)")]
    [SerializeField] private TradePrepareUIManager ui;    // 플로우 총괄 매니저

    // id → 표시 이름 표(도시·루트·동물·아이템 통합, 요약 표시용)
    private readonly Dictionary<string, string> names = new Dictionary<string, string>();

    private void Start()
    {
        if (data == null || ui == null)
        {
            Debug.LogWarning("[Prepare Demo] data(SO) 또는 ui(매니저)가 비었음 — FlowDemo 인스펙터를 확인하세요.");
            return;
        }

        BuildNameTable();

        // ── 매니저에 데이터 프로바이더 주입 ──
        ui.TownProvider = BuildTownEntries;
        ui.AnimalProvider = BuildAnimalEntries;
        ui.OwnedWagonProvider = BuildOwnedWagons;
        ui.CargoProvider = BuildCargoConfig;
        ui.SummaryStatsProvider = BuildSummaryStats;   // ⑥ 요약 계산(Core 계산기 사용)
        ui.NameResolver = Resolve;

        // ── 결과 이벤트(데모는 로그만) ──
        ui.OnDepart += OnDepart;
        ui.OnCancelled += OnCancelled;
        ui.OnJourneyFinished += OnJourneyFinished;

        ui.Begin();
    }

    // ══ 어댑터: 진짜 SO → 패널 DTO ═══════════════════════════════
    // 패널은 이종현님 SO 타입을 모른다(중립 DTO만 받음). 변환은 여기서 담당.

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

    /// <summary>웨건 선택 팝업용 목록 — 도보(None)는 항상, 마차/자동차(Mount)는 소지분만.</summary>
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

    /// <summary>④ 적재(Cargo·정헌님)용 값 묶음 — 골드·먹이·상점 아이템·재고.</summary>
    private TradePrepareUIManager.CargoConfig BuildCargoConfig()
    {
        TradePrepareUIManager.CargoConfig cfg = new TradePrepareUIManager.CargoConfig();
        cfg.gold = data.gold;
        cfg.requiredFood = data.requiredFood;
        cfg.shopItems = data.items;
        // 재고: itemStocks에서(부족하면 10으로 간주)
        int n = data.items != null ? data.items.Length : 0;
        cfg.stocks = new int[n];
        for (int i = 0; i < n; i++)
            cfg.stocks[i] = (data.itemStocks != null && i < data.itemStocks.Length) ? data.itemStocks[i] : 10;
        return cfg;
    }

    // ══ ⑥ 요약 계산 (와이어프레임 6번) ═══════════════════════════
    // 시간·음식은 진짜 Core 계산기(CaravanCalculator)를 쓴다.
    // SO(이종현님) → imsi(Core 초안 타입) 매핑은 데모(여기)가 담당 — 매핑 규칙은 주석 참고.

    /// <summary>매니저 질의(선택값) → 요약 계산 결과.</summary>
    private TradePrepareUIManager.SummaryStats BuildSummaryStats(TradePrepareUIManager.SummaryQuery q)
    {
        TradePrepareUIManager.SummaryStats st = new TradePrepareUIManager.SummaryStats();
        st.viaText = "";   // 경유 데이터가 SO에 아직 없음(와이어프레임 '경유 기능 필요') → 매니저가 "없음" 처리

        // 출발 도시·예상 위험도 — 루트 SO에서
        RouteData route = FindRoute(q.routeId);
        if (route != null)
        {
            st.fromTownName = route.FromTownName;
            // 예상 위험도: 루트 내 가장 높은 '습격(Combat)' 이벤트 값. 없으면 0 (와이어프레임 규칙)
            int best = 0;
            foreach (RouteEventData ev in route.RouteEvents)
                if (ev != null && ev.eventType == RouteEvent.Combat)
                    best = Mathf.Max(best, Mathf.RoundToInt(ev.eventValue));
            st.expectedRisk = best;
        }

        // 예상 시간·음식 — Core 계산기(속도 공식·초당 소모)를 그대로 사용
        CaravanData cv = BuildCaravanForCalc(q);
        st.durationSeconds = CaravanCalculator.GetTravelSeconds(cv, q.distanceKm);
        st.expectedFood = CaravanCalculator.GetEstimatedFood(cv, q.distanceKm, 1f);   // 인게임 배율 1(데모)
        return st;
    }

    /// <summary>계산용 CaravanData 조립 — SO 값 → imsi(Core 초안 타입) 매핑.
    /// · 동물 speed: imsi는 '말=1 기준 배수' → SO BaseMoveSpeed를 말(6)로 나눠 정규화 [데모 매핑]
    /// · 웨건 speedModifier: SO BaseMoveSpeed는 절대값이라 배수로 못 씀 → 0(중립 1.0) [데모 매핑]</summary>
    private CaravanData BuildCaravanForCalc(TradePrepareUIManager.SummaryQuery q)
    {
        CaravanData cv = new CaravanData();

        // 웨건
        if (q.hasTransport)
        {
            WagonData w = FindWagon(q.transport.id);
            if (w != null)
            {
                imsiWagonData iw = new imsiWagonData();
                iw.wagonName = w.DisplayName;
                iw.overLoad = w.Overload;
                iw.maxLoad = w.MaxLoad;
                iw.minAnimals = w.MinRequireAnimals;
                iw.maxAnimals = w.MaxPullAnimals;
                iw.inventorySlotCount = w.InventorySlotCount;
                cv.wagon = iw;
            }
        }

        // 동물 (수량만큼 1마리씩)
        if (q.animals != null)
            foreach (AnimalInventoryPanel.AnimalPick p in q.animals)
            {
                DraftAnimalData a = FindAnimal(p.animalId);
                if (a == null) continue;
                for (int i = 0; i < p.count; i++)
                {
                    imsiAnimalData ia = new imsiAnimalData();
                    ia.animalName = a.DisplayName;
                    ia.speed = a.BaseMoveSpeed / 6f;         // 말(6)=1.0 기준 정규화 [데모 매핑]
                    ia.foodPerKm = a.FeedConsumption;         // 초당 소모율 (필드명은 Core에서 rename 예정)
                    ia.increaseOverLoad = a.IncreaseOverLoad;
                    ia.increaseMaxLoad = a.IncreaseMaxLoad;
                    ia.animalType = a.AnimalType;
                    cv.animals.Add(ia);
                }
            }

        // 적재 물품
        if (q.cargo != null)
            foreach (TradeItemBundle b in q.cargo)
            {
                TradeItemData it = FindItem(b.itemId);
                if (it == null) continue;
                imsiTradeItemData ii = new imsiTradeItemData();
                ii.id = it.ItemId;
                ii.itemName = it.DisplayName;
                ii.weight = it.Weight;
                ii.basePrice = it.BaseSellPrice;
                ii.maxCount = it.MaxCount;
                CargoEntry ce = new CargoEntry();
                ce.item = ii;
                ce.quantity = b.quantity;
                cv.cargo.Add(ce);
            }
        cv.foodAmount = q.loadedFood;
        return cv;
    }

    // ── SO 조회 헬퍼(데모 데이터에서 id로 찾기) ──
    private RouteData FindRoute(string routeId)
    {
        if (data.towns != null)
            foreach (TownData t in data.towns)
            {
                if (t == null) continue;
                foreach (RouteData r in t.AvailableRoutes)
                    if (r != null && r.RouteId == routeId) return r;
            }
        return null;
    }

    private WagonData FindWagon(string wagonId)
    {
        if (data.transports != null)
            foreach (WagonData w in data.transports)
                if (w != null && w.WagonId == wagonId) return w;
        return null;
    }

    private DraftAnimalData FindAnimal(string animalId)
    {
        if (data.animals != null)
            foreach (DraftAnimalData a in data.animals)
                if (a != null && a.DraftAnimalId == animalId) return a;
        return null;
    }

    private TradeItemData FindItem(string itemId)
    {
        if (data.items != null)
            foreach (TradeItemData it in data.items)
                if (it != null && it.ItemId == itemId) return it;
        return null;
    }

    /// <summary>도시별 특산품 더미(데모). 이름 + 마우스오버 툴팁(아이템 정보).</summary>
    private List<TownRoutePanel.Specialty> DummySpecialties(string townId)
    {
        string[] specNames;
        if (townId == "town_a") specNames = new string[] { "밀", "양털" };
        else if (townId == "town_b") specNames = new string[] { "생선", "비단" };
        else if (townId == "town_c") specNames = new string[] { "광석", "보석" };
        else specNames = new string[0];

        List<TownRoutePanel.Specialty> result = new List<TownRoutePanel.Specialty>();
        foreach (string n in specNames) result.Add(new TownRoutePanel.Specialty(n, ItemTooltip(n)));
        return result;
    }

    /// <summary>아이템 이름으로 TradeItemData를 찾아 툴팁 문자열을 만든다(없으면 일반).</summary>
    private string ItemTooltip(string itemName)
    {
        if (data.items != null)
            foreach (TradeItemData it in data.items)
                if (it != null && it.DisplayName == itemName)
                    return $"{it.DisplayName}\n무게 {it.Weight:0.#}\n분류 {it.Category}\n" +
                           $"구매 {it.BaseBuyPrice} / 판매 {it.BaseSellPrice}";
        return $"{itemName}\n(특산품)";
    }

    /// <summary>WagonType(이종현님) → TransportType(패널 분기용) 매핑.</summary>
    private static TransportType MapType(WagonType t)
    {
        if (t == WagonType.WagonWithAnimals) return TransportType.Wagon;
        if (t == WagonType.Mount) return TransportType.Mount;
        return TransportType.None;
    }

    /// <summary>SO에서 id→이름 표를 만든다(요약 표시용, 도시·루트·동물·아이템 통합).</summary>
    private void BuildNameTable()
    {
        names.Clear();
        if (data.towns != null)
            foreach (TownData t in data.towns)
            {
                if (t == null) continue;
                names[t.TownId] = t.DisplayName;
                foreach (RouteData r in t.AvailableRoutes)
                    if (r != null) names[r.RouteId] = r.DisplayName;
            }
        if (data.animals != null)
            foreach (DraftAnimalData a in data.animals)
                if (a != null) names[a.DraftAnimalId] = a.DisplayName;
        if (data.items != null)
            foreach (TradeItemData it in data.items)
                if (it != null) names[it.ItemId] = it.DisplayName;
    }

    /// <summary>id → 표시 이름(없으면 id 그대로).</summary>
    private string Resolve(string id)
    {
        return names.TryGetValue(id, out string n) ? n : id;
    }

    // ══ 결과 이벤트(데모는 로그만) ═══════════════════════════════

    private void OnDepart(TradePrepareUIManager.DepartData d)
    {
        Debug.Log($"[Prepare Demo] Depart! town={Resolve(d.townId)} route={Resolve(d.routeId)} " +
                  $"transport={(d.hasTransport ? d.transport.name : "-")} animals={d.animals.Count}종 " +
                  $"cargo={(d.cargo != null ? d.cargo.Length : 0)}종/{d.cargoCost:N0}G merc={d.mercenaryPower}/{d.mercenaryCost:N0}G");
    }

    private void OnCancelled()
    {
        Debug.Log("[Prepare Demo] 무역 취소 — 준비 데이터 초기화, 도시 선택으로 복귀.");
    }

    private void OnJourneyFinished()
    {
        Debug.Log("[Prepare Demo] 무역 종료(도착) — 정산(⑧) 자리. 데모는 도시 선택으로 복귀.");
    }
}
