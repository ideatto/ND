// =============================================================================
// TradePrepareUIManager — 무역 준비 플로우 UI 매니저 (제품 코드)
// =============================================================================
// [담당] Core Gameplay (윤호영)
// [역할] 무역 준비 화면 전환·상태·데이터 전달을 총괄한다.
//   ① 도시+루트(TownRoutePanel) → ② 상단 슬롯(CaravanSlotPanel)
//   → ③ 상단 구성(AnimalInventoryPanel) → ④ 물품 적재(CargoLoadingPanelController·정헌님)
//   → ⑤ 용병 고용(MercenaryHirePanelController·정헌님) → ⑥ 요약
//
// [데이터 주입] 매니저는 데이터 출처를 모른다 — Func 프로바이더로 주입받는다.
//   (데모는 TradePrepareFlowDemo가 더미 SO로, 실제 게임은 게임 데이터 소스가 주입)
//
// [정헌님 패널 연결 규약] ★씬(인스펙터) 설정 필요
//   · Cargo 진입 시 매니저가 Configure(골드, 최대적재량, 필요먹이, 상점아이템, 재고) 호출 후 활성화.
//   · Cargo/용병의 이벤트는 private UnityEvent라 코드 구독 불가 → 씬에서 퍼시스턴트 리스너로 연결:
//       CargoLoadingPanelController.onBackRequested   → OnCargoBackRequested
//       CargoLoadingPanelController.onTradeCancelled  → OnTradeCancelledByPanels
//       MercenaryHirePanelController.onConfirmed      → OnMercenaryConfirmed
//   · Cargo의 mercenaryStepPanel(인스펙터)에 씬의 용병 패널을 지정해야
//     Cargo가 우리 용병 패널을 Show(예산)로 띄운다. (용병 UI는 컴포넌트가 코드로 생성)
//   · ④→⑤ 전환, ⑤에서 Back(④ 복귀)은 정헌님 패널이 자체 처리 — 매니저는 관여하지 않는다.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>무역 준비 플로우의 화면 전환과 단계 간 데이터 전달을 총괄하는 매니저.</summary>
public class TradePrepareUIManager : MonoBehaviour
{
    // ══ 외부 계약(주입 데이터 형식) ═══════════════════════════════

    /// <summary>④ 적재(Cargo) 화면에 넘길 값 묶음. 최대 적재량은 매니저가 상단 구성에서 계산한다.</summary>
    public struct CargoConfig
    {
        public long gold;                 // 현재 소지 골드
        public int requiredFood;          // 반드시 적재해야 하는 먹이 수
        public TradeItemData[] shopItems; // 상점 판매 아이템(정헌님 Cargo 계약)
        public int[] stocks;              // 아이템별 재고(shopItems와 같은 순서)
    }

    /// <summary>⑥ 요약 계산 질의 — 매니저가 아는 확정 선택값. 계산은 데이터 소스가 담당.</summary>
    public struct SummaryQuery
    {
        public string routeId;            // 선택 루트
        public float distanceKm;          // 루트 거리
        public bool hasTransport;         // 이동수단 유무
        public TransportSelectPanel.TransportEntry transport; // 선택 이동수단
        public List<AnimalInventoryPanel.AnimalPick> animals; // 편성 동물
        public TradeItemBundle[] cargo;   // 적재 확정 물품
        public int loadedFood;            // 적재한 먹이 수
    }

    /// <summary>⑥ 요약 계산 결과 — 데이터 소스(데모=Core 계산기)가 채워서 돌려준다.</summary>
    public struct SummaryStats
    {
        public string fromTownName;   // 출발 도시(루트 SO의 FromTown)
        public string viaText;        // 경유 도시 표기(비면 "없음" 처리)
        public int expectedRisk;      // 루트 내 최고 습격 이벤트 값(없으면 0)
        public float expectedFood;    // 예상 음식 소모량
        public float durationSeconds; // 예상 소요 시간(초)
    }

    /// <summary>출발 시점에 확정된 준비 내용(요약 표시·출발 API 연결용).</summary>
    public struct DepartData
    {
        public string townId;             // 목적지 도시
        public string routeId;            // 선택 루트
        public float distanceKm;          // 루트 거리
        public bool hasTransport;         // 이동수단 선택 여부
        public TransportSelectPanel.TransportEntry transport; // 선택 이동수단
        public List<AnimalInventoryPanel.AnimalPick> animals; // 편성 동물
        public TradeItemBundle[] cargo;   // 적재 확정 물품(정헌님 계약)
        public long cargoCost;            // 물품 구매 비용
        public int mercenaryPower;        // 고용 용병 전투력(미고용 0)
        public long mercenaryCost;        // 용병 고용 비용(미고용 0)
    }

    // ══ 인스펙터 참조 ═══════════════════════════════════════════

    [Header("화면 패널 (윤호영)")]
    [SerializeField] private TownRoutePanel townRoutePanel;      // ① 도시+루트
    [SerializeField] private CaravanSlotPanel caravanSlotPanel;  // ② 상단 슬롯
    [SerializeField] private AnimalInventoryPanel animalPanel;   // ③ 상단 구성
    [SerializeField] private GameObject summaryPanel;            // ⑥ 요약(루트 오브젝트)

    [Header("화면 패널 (정헌님)")]
    [SerializeField] private CargoLoadingPanelController cargoPanel;     // ④ 물품 적재
    [SerializeField] private MercenaryHirePanelController mercenaryPanel; // ⑤ 용병 고용

    [Header("진행 화면 (⑦ 와이어프레임 7번)")]
    [SerializeField] private TradeProgressPanel progressPanel;       // ⑦ 무역 진행 중
    [SerializeField] private TradeCancelWarningPopup cancelWarning;  // ⑦-1 취소 경고창
    [SerializeField] private Button progressCancel;                 // ⑦ 하단 무역 취소
    [SerializeField] private Button warningReturn;                  // ⑦-1 돌아가기
    [SerializeField] private Button warningConfirm;                 // ⑦-1 무역 취소(확정)
    [SerializeField, Min(0f)] private float progressDemoSeconds = 20f; // 데모 관찰용 진행 시간(0=실제 계산값 사용)

    [Header("상단 슬롯")]
    [SerializeField] private int caravanSlotCount = 4;   // 슬롯 개수(새 게임=전부 빈칸)

    [Header("구성 저장")]
    [SerializeField] private Toggle saveToggle;          // ③ 우상단 "구성 저장" 체크박스

    [Header("진행(Next) 버튼")]
    [SerializeField] private Button slotNext;        // ② 슬롯 선택 후 하단 Next
    [SerializeField] private Button animalNext;      // ③ → ④
    [SerializeField] private Button departButton;    // ⑥ 출발

    [Header("뒤로(Back) 버튼")]
    [SerializeField] private Button slotBack;        // ② → ①
    [SerializeField] private Button animalBack;      // ③ → ②
    [SerializeField] private Button summaryBack;     // ⑥ → ⑤(용병 다시 열기)

    [Header("요약(⑥ 와이어프레임 6번)")]
    [SerializeField] private TradeSummaryPanel summaryView;   // 요약 표시 패널(순수 UI)
    [SerializeField] private Button summaryCancel;            // ⑥ 무역 취소

    // ══ 데이터 프로바이더(코드에서 주입) ═════════════════════════

    /// <summary>① 도시+루트 목록 공급자.</summary>
    public Func<List<TownRoutePanel.TownEntry>> TownProvider;
    /// <summary>③ 동물 인벤토리 공급자(스탯 포함 — 최대 적재량 계산에도 사용).</summary>
    public Func<List<AnimalInventoryPanel.AnimalEntry>> AnimalProvider;
    /// <summary>③ 웨건 선택 팝업용 소지 이동수단 공급자.</summary>
    public Func<List<TransportSelectPanel.TransportEntry>> OwnedWagonProvider;
    /// <summary>④ 적재 화면 값(골드·먹이·상점) 공급자.</summary>
    public Func<CargoConfig> CargoProvider;
    /// <summary>⑥ 요약 계산(출발도시·위험도·음식·시간) 공급자 — 데모는 Core 계산기 사용.</summary>
    public Func<SummaryQuery, SummaryStats> SummaryStatsProvider;
    /// <summary>id → 표시 이름 변환(요약용). 없으면 id 그대로 표시.</summary>
    public Func<string, string> NameResolver;

    /// <summary>⑥ 출발 버튼 — 확정된 준비 내용 전달(출발 API 연결 지점).</summary>
    public event Action<DepartData> OnDepart;
    /// <summary>무역 취소(Cargo/용병의 Cancel, ⑥·⑦-1 취소) — 준비 데이터 초기화 후 ①로 복귀했음을 알림.</summary>
    public event Action OnCancelled;
    /// <summary>⑦ 무역 종료(도착) — 정산(⑧)으로 이어질 지점. 데모는 로그만.</summary>
    public event Action OnJourneyFinished;

    // ══ 진행 상태 ═══════════════════════════════════════════════

    private string selTownId = "", selRouteId = "";
    private float distanceKm;
    private bool hasTransport;
    private TransportSelectPanel.TransportEntry transport;
    private string[] caravanSlots;       // 슬롯별 표시 요약(""=빈칸)
    private SlotComp[] slotData;         // 슬롯별 구조화 저장(웨건+동물)
    private int composingSlotIndex = -1; // 지금 구성 중인 슬롯
    private bool itemsFromSavedSlot;     // ④에 저장슬롯 Next로 직행했나 → Back 목적지 분기
    private string lastFromTownName;     // ⑦ 진행 화면 타이틀용(요약에서 계산한 출발 도시)
    private float lastDuration;          // ⑦ 진행 카운트다운용(요약에서 계산한 소요 초)
    private bool wired;                  // 버튼/이벤트 중복 구독 방지

    /// <summary>슬롯 하나에 저장된 상단 구성(구조화).</summary>
    private class SlotComp
    {
        public bool filled;
        public bool hasWagon;
        public TransportSelectPanel.TransportEntry wagon;
        public List<AnimalInventoryPanel.AnimalPick> animals = new List<AnimalInventoryPanel.AnimalPick>();
    }
    private readonly List<AnimalInventoryPanel.AnimalPick> pickedAnimals = new List<AnimalInventoryPanel.AnimalPick>();

    // ══ 시작 ═══════════════════════════════════════════════════

    /// <summary>플로우 시작 — 프로바이더 주입 후 호출. ① 도시+루트부터 연다.</summary>
    public void Begin()
    {
        WireOnce();

        // 슬롯 저장소 초기화(전부 빈칸)
        int n = Mathf.Max(1, caravanSlotCount);
        caravanSlots = new string[n];
        slotData = new SlotComp[n];
        for (int i = 0; i < n; i++) slotData[i] = new SlotComp();

        ResetTradeState();

        if (townRoutePanel != null && TownProvider != null)
            townRoutePanel.Populate(TownProvider());

        GoTownRoute();
    }

    /// <summary>패널 C# 이벤트·버튼 구독(1회만). 정헌님 패널의 UnityEvent는 씬에서 연결.</summary>
    private void WireOnce()
    {
        if (wired) return;
        wired = true;

        if (townRoutePanel != null) townRoutePanel.OnRouteSelected += OnRouteSelected;
        if (caravanSlotPanel != null)
        {
            caravanSlotPanel.OnSlotSelected += OnSlotSelected;
            caravanSlotPanel.OnEditRequested += OnEditRequested;
        }
        if (animalPanel != null)
        {
            animalPanel.OnSelectionChanged += OnAnimalsChanged;
            animalPanel.OnWagonSelected += OnWagonPlaced;
            animalPanel.OnWagonRemoved += OnWagonRemoved;
        }
        if (saveToggle != null) saveToggle.onValueChanged.AddListener(OnSaveToggle);

        if (slotNext != null) slotNext.onClick.AddListener(FromSlotNext);
        if (animalNext != null) animalNext.onClick.AddListener(FromAnimalsNext);
        if (departButton != null) departButton.onClick.AddListener(Depart);

        if (slotBack != null) slotBack.onClick.AddListener(GoTownRoute);
        if (animalBack != null) animalBack.onClick.AddListener(GoCaravanSlot);
        if (summaryBack != null) summaryBack.onClick.AddListener(BackFromSummary);
        if (summaryCancel != null) summaryCancel.onClick.AddListener(OnTradeCancelledByPanels);   // ⑥ 무역 취소

        // ⑦ 진행 화면 / ⑦-1 경고창
        if (progressPanel != null) progressPanel.OnArrived += OnJourneyArrived;
        if (progressCancel != null) progressCancel.onClick.AddListener(OnProgressCancelClicked);
        if (warningReturn != null) warningReturn.onClick.AddListener(OnWarningReturn);
        if (warningConfirm != null) warningConfirm.onClick.AddListener(OnWarningConfirmCancel);
    }

    /// <summary>무역 준비 데이터 초기화(저장된 상단 슬롯은 유지).</summary>
    private void ResetTradeState()
    {
        selTownId = ""; selRouteId = "";
        distanceKm = 0f;
        hasTransport = false;
        transport = default(TransportSelectPanel.TransportEntry);
        pickedAnimals.Clear();
        composingSlotIndex = -1;
        itemsFromSavedSlot = false;
    }

    // ══ ① 도시+루트 ═════════════════════════════════════════════

    private void OnRouteSelected(string townId, string routeId, float distance)
    {
        selTownId = townId;
        selRouteId = routeId;
        distanceKm = distance;
        GoCaravanSlot();
    }

    // ══ ② 상단 슬롯 ═════════════════════════════════════════════

    // 슬롯 선택 → 하단 Next만 활성(자동 진행 안 함). Edit는 별도 버튼.
    private void OnSlotSelected(int slotIndex)
    {
        composingSlotIndex = slotIndex;
        if (slotNext != null) slotNext.interactable = slotIndex >= 0;
    }

    // 선택 슬롯의 [Edit] → 항상 ③(구성/편집). 빈 슬롯이든 저장된 슬롯이든.
    private void OnEditRequested(int slotIndex)
    {
        hasTransport = false;   // ③에서 웨건부터 다시 넣음(편집)
        GoAnimals();
    }

    // 하단 [Next] → 빈 슬롯이면 ③(구성), 저장된 슬롯이면 ④(적재).
    private void FromSlotNext()
    {
        bool empty = caravanSlotPanel != null && caravanSlotPanel.IsSelectedEmpty();
        if (empty)
        {
            hasTransport = false;
            GoAnimals();
        }
        else
        {
            // 저장된 슬롯 → ④(적재). "그 슬롯에 저장된" 웨건·동물을 그대로 사용.
            SlotComp s = (slotData != null && composingSlotIndex >= 0 && composingSlotIndex < slotData.Length)
                         ? slotData[composingSlotIndex] : null;
            hasTransport = s != null && s.filled && s.hasWagon;
            if (hasTransport) transport = s.wagon;
            pickedAnimals.Clear();
            if (s != null) pickedAnimals.AddRange(s.animals);
            itemsFromSavedSlot = true;      // ④ Back → ②로
            GoCargo();
        }
    }

    // ══ ③ 상단 구성 ═════════════════════════════════════════════

    // 웨건을 넣었을 때 — 이후 적재량 계산에 사용
    private void OnWagonPlaced(TransportSelectPanel.TransportEntry w)
    {
        transport = w;
        hasTransport = true;
    }

    // 웨건을 뺐을 때 — 상태 동기화. 웨건 없는 구성은 저장 대상이 아니므로 저장도 해제.
    private void OnWagonRemoved()
    {
        hasTransport = false;
        if (saveToggle != null && saveToggle.isOn)
        {
            saveToggle.SetIsOnWithoutNotify(false);
            OnSaveToggle(false);
        }
    }

    // 동물 편성 변경 → Next(요구량 충족 시) 활성
    private void OnAnimalsChanged(IReadOnlyList<AnimalInventoryPanel.AnimalPick> picks, bool valid)
    {
        pickedAnimals.Clear();
        pickedAnimals.AddRange(picks);
        if (animalNext != null) animalNext.interactable = valid;
        // 저장 체크돼 있으면 슬롯 요약도 실시간 갱신
        if (saveToggle != null && saveToggle.isOn) OnSaveToggle(true);
    }

    // ③ 하단 [Next] → ④(적재). 구성을 거쳤으므로 ④ Back은 ③으로.
    private void FromAnimalsNext()
    {
        itemsFromSavedSlot = false;
        GoCargo();
    }

    // ③ 우상단 "구성 저장" 체크박스 → 슬롯에 저장/해제(구조화 + 표시요약)
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

    // 지금 구성 중인 상단을 슬롯에 표시할 요약 문자열로.
    private string BuildCaravanSummary()
    {
        string wag = hasTransport ? transport.name : "-";
        if (pickedAnimals.Count == 0) return wag;
        List<string> parts = new List<string>();
        foreach (AnimalInventoryPanel.AnimalPick a in pickedAnimals)
            parts.Add($"{Resolve(a.animalId)}x{a.count}");
        return $"{wag}\n{string.Join(",", parts)}";
    }

    // ══ ④ 적재 / ⑤ 용병 (정헌님 패널) ═══════════════════════════

    // ④ Cargo의 [Back] — 씬 퍼시스턴트 리스너로 연결(onBackRequested).
    /// <summary>Cargo 뒤로가기 — 저장슬롯 직행이었으면 ②, 구성을 거쳤으면 ③으로.</summary>
    public void OnCargoBackRequested()
    {
        if (itemsFromSavedSlot) GoCaravanSlot();
        else GoAnimals();
    }

    // ④/⑤의 [Cancel](무역 취소) — 씬 퍼시스턴트 리스너로 연결(onTradeCancelled).
    /// <summary>무역 취소 — 준비 데이터 초기화 후 ①로 복귀.</summary>
    public void OnTradeCancelledByPanels()
    {
        ResetTradeState();
        ShowOnly(0);
        OnCancelled?.Invoke();
    }

    // ⑤ 용병의 [Confirm] — 씬 퍼시스턴트 리스너로 연결(onConfirmed).
    /// <summary>용병 고용 확정 → ⑥ 요약.</summary>
    public void OnMercenaryConfirmed()
    {
        GoSummary();
    }

    // ══ 화면 전환 ═══════════════════════════════════════════════

    private void GoTownRoute() { ShowOnly(0); }

    private void GoCaravanSlot()
    {
        if (caravanSlotPanel != null)
        {
            List<string> slots = new List<string>();
            for (int i = 0; i < caravanSlotCount; i++)
                slots.Add((caravanSlots != null && i < caravanSlots.Length) ? (caravanSlots[i] ?? "") : "");
            caravanSlotPanel.Populate(slots);
            caravanSlotPanel.ResetSelection();
        }
        if (slotNext != null) slotNext.interactable = false;   // 슬롯 고르기 전엔 Next 잠금
        ShowOnly(1);
    }

    private void GoAnimals()
    {
        itemsFromSavedSlot = false;
        pickedAnimals.Clear();
        if (animalNext != null) animalNext.interactable = false;

        if (animalPanel != null)
        {
            animalPanel.Populate(
                AnimalProvider != null ? AnimalProvider() : new List<AnimalInventoryPanel.AnimalEntry>(),
                OwnedWagonProvider != null ? OwnedWagonProvider() : new List<TransportSelectPanel.TransportEntry>());
            // 저장된 슬롯을 Edit로 열면 웨건+동물 그대로 복원
            if (slotData != null && composingSlotIndex >= 0 && composingSlotIndex < slotData.Length
                && slotData[composingSlotIndex].filled && slotData[composingSlotIndex].hasWagon)
            {
                animalPanel.RestoreComposition(slotData[composingSlotIndex].wagon, slotData[composingSlotIndex].animals);
            }
        }
        // 저장 체크박스: 이 슬롯이 이미 저장돼 있으면 체크 상태로(알림 없이)
        if (saveToggle != null)
        {
            bool saved = composingSlotIndex >= 0 && caravanSlots != null && composingSlotIndex < caravanSlots.Length
                         && !string.IsNullOrEmpty(caravanSlots[composingSlotIndex]);
            saveToggle.SetIsOnWithoutNotify(saved);
        }
        ShowOnly(2);
    }

    /// <summary>④ 적재로 — Cargo에 골드·최대적재량·먹이·상점을 넘기고 연다.</summary>
    private void GoCargo()
    {
        if (cargoPanel != null && CargoProvider != null)
        {
            CargoConfig cfg = CargoProvider();
            cargoPanel.Configure(cfg.gold, ComputeMaxLoad(), cfg.requiredFood, cfg.shopItems, cfg.stocks);
        }
        ShowOnly(3);
    }

    /// <summary>현재 상단 구성의 최대 적재량 = 웨건 기본 + Σ(동물 최대적재 증가치 × 마릿수).</summary>
    private float ComputeMaxLoad()
    {
        float load = hasTransport ? transport.maxLoad : 0f;
        if (AnimalProvider != null && pickedAnimals.Count > 0)
        {
            List<AnimalInventoryPanel.AnimalEntry> entries = AnimalProvider();
            foreach (AnimalInventoryPanel.AnimalPick p in pickedAnimals)
                foreach (AnimalInventoryPanel.AnimalEntry e in entries)
                    if (e.id == p.animalId) { load += e.incMaxLoad * p.count; break; }
        }
        return Mathf.Max(1f, load);   // 0이면 아무것도 못 실으므로 최소 1
    }

    /// <summary>⑥ 요약 — 선택값(질의)을 프로바이더에 넘겨 계산 결과를 받고, 패널에 바인딩한다.</summary>
    private void GoSummary()
    {
        if (summaryView != null)
        {
            TradeItemBundle[] bundles = cargoPanel != null ? cargoPanel.BuildTradeItemBundles() : new TradeItemBundle[0];
            int loadedFood = cargoPanel != null ? cargoPanel.LoadedFood : 0;

            // 질의(매니저가 아는 확정 선택값)
            SummaryQuery q = new SummaryQuery();
            q.routeId = selRouteId;
            q.distanceKm = distanceKm;
            q.hasTransport = hasTransport;
            q.transport = transport;
            q.animals = new List<AnimalInventoryPanel.AnimalPick>(pickedAnimals);
            q.cargo = bundles;
            q.loadedFood = loadedFood;
            SummaryStats st = SummaryStatsProvider != null ? SummaryStatsProvider(q) : default(SummaryStats);

            // ⑦ 진행 화면에서 쓸 값 저장(출발 도시·소요 시간)
            lastFromTownName = string.IsNullOrEmpty(st.fromTownName) ? "-" : st.fromTownName;
            lastDuration = st.durationSeconds;

            // 비용·이익(번들에서 직접 집계 — 배율은 배율 데이터 확정 후 적용)
            long cargoCost = 0, profit = 0;
            foreach (TradeItemBundle b in bundles)
            {
                cargoCost += b.purchaseUnitPrice * b.quantity;
                profit += b.sellUnitPrice * b.quantity;
            }
            long mercCost = mercenaryPanel != null ? mercenaryPanel.SelectedHireCost : 0;
            int mercPower = mercenaryPanel != null ? mercenaryPanel.SelectedCombatPower : 0;

            TradeSummaryPanel.SummaryData d = new TradeSummaryPanel.SummaryData();
            d.fromTown = string.IsNullOrEmpty(st.fromTownName) ? "-" : st.fromTownName;
            d.toTown = Resolve(selTownId);
            d.viaText = string.IsNullOrEmpty(st.viaText) ? "없음" : st.viaText;   // 경유 없을 시 '없음'(와이어프레임)
            d.expectedRisk = st.expectedRisk;
            d.mercenaryPower = mercPower;
            d.expectedFood = st.expectedFood;
            d.loadedFood = loadedFood;
            d.prepareCost = cargoCost + mercCost;
            d.expectedProfit = profit;
            d.durationSeconds = st.durationSeconds;
            summaryView.Show(d);
        }
        ShowOnly(4);
    }

    // ⑥ Back → ⑤ 용병 화면 다시 열기(예산은 Cargo가 계산).
    private void BackFromSummary()
    {
        if (summaryPanel != null) summaryPanel.SetActive(false);
        if (mercenaryPanel != null && cargoPanel != null)
            mercenaryPanel.Show(cargoPanel.MercenaryBudget);
        else
            ShowOnly(3);   // 용병 패널이 없으면 ④로라도
    }

    /// <summary>⑥ 출발 — 확정 내용을 모아 이벤트로 알린다(출발 API 연결 지점).</summary>
    private void Depart()
    {
        DepartData d = new DepartData();
        d.townId = selTownId;
        d.routeId = selRouteId;
        d.distanceKm = distanceKm;
        d.hasTransport = hasTransport;
        d.transport = transport;
        d.animals = new List<AnimalInventoryPanel.AnimalPick>(pickedAnimals);
        if (cargoPanel != null)
        {
            d.cargo = cargoPanel.BuildTradeItemBundles();
            d.cargoCost = cargoPanel.PendingPurchaseCost;
        }
        if (mercenaryPanel != null)
        {
            d.mercenaryPower = mercenaryPanel.SelectedCombatPower;
            d.mercenaryCost = mercenaryPanel.SelectedHireCost;
        }
        OnDepart?.Invoke(d);
        GoProgress();   // ⑥ → ⑦ 무역 진행 중
    }

    // ══ ⑦ 무역 진행 중 / ⑦-1 취소 경고 ═══════════════════════════

    /// <summary>⑦ 진행 화면으로 — 출발/목적지·소요 시간을 넘겨 카운트다운 시작.</summary>
    private void GoProgress()
    {
        if (cancelWarning != null) cancelWarning.Close();   // 경고창은 닫힌 채로 시작
        if (progressPanel != null)
        {
            // 데모 관찰용 시간(progressDemoSeconds>0)이 있으면 그걸, 없으면 실제 계산값.
            float dur = progressDemoSeconds > 0f ? progressDemoSeconds : Mathf.Max(1f, lastDuration);
            progressPanel.Begin(lastFromTownName, Resolve(selTownId), dur);
        }
        ShowOnly(5);
    }

    // ⑦ 하단 "무역 취소" → ⑦-1 경고창 열기
    private void OnProgressCancelClicked()
    {
        if (cancelWarning != null) cancelWarning.Open();
    }

    // ⑦-1 "돌아가기" → 경고창만 닫기(진행 계속)
    private void OnWarningReturn()
    {
        if (cancelWarning != null) cancelWarning.Close();
    }

    // ⑦-1 "무역 취소"(확정) → 진행 중단. 정산(⑧)은 미구현 → 데모는 ①로 복귀.
    private void OnWarningConfirmCancel()
    {
        if (cancelWarning != null) cancelWarning.Close();
        if (progressPanel != null) progressPanel.StopTimer();
        ResetTradeState();
        ShowOnly(0);
        OnCancelled?.Invoke();
    }

    // ⑦ 남은 시간 0(도착) → 무역 종료. 정산(⑧)은 미구현 → 데모는 ①로 복귀.
    private void OnJourneyArrived()
    {
        OnJourneyFinished?.Invoke();
        ResetTradeState();
        ShowOnly(0);
    }

    /// <summary>화면 인덱스만 켠다. 0:도시루트 1:상단슬롯 2:구성 3:적재(Cargo) 4:요약 5:진행 / -1:전부 끔
    /// ⑤ 용병은 Cargo·용병 패널이 자체 표시/숨김을 관리하므로 여기서 건드리지 않는다.
    /// ⑦-1 경고창은 오버레이라 여기서 켜지 않는다(취소 클릭 시에만 Open).</summary>
    private void ShowOnly(int idx)
    {
        SetActive(townRoutePanel, idx == 0);
        SetActive(caravanSlotPanel, idx == 1);
        SetActive(animalPanel, idx == 2);
        if (cargoPanel != null) cargoPanel.gameObject.SetActive(idx == 3);
        if (summaryPanel != null) summaryPanel.SetActive(idx == 4);
        if (progressPanel != null) progressPanel.gameObject.SetActive(idx == 5);
        if (idx != 5 && cancelWarning != null) cancelWarning.Close();   // 진행 화면 벗어나면 경고창도 닫기
    }

    private static void SetActive(Component panel, bool on)
    {
        if (panel != null) panel.gameObject.SetActive(on);
    }

    // ══ 요약 ═══════════════════════════════════════════════════

    /// <summary>id → 표시 이름(리졸버 없으면 id 그대로).</summary>
    private string Resolve(string id)
    {
        if (NameResolver == null || string.IsNullOrEmpty(id)) return id;
        string n = NameResolver(id);
        return string.IsNullOrEmpty(n) ? id : n;
    }
}
