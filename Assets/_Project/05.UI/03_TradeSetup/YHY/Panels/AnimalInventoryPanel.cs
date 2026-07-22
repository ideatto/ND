// =============================================================================
// AnimalInventoryPanel — 상단 구성 패널 (3번 화면: 왼쪽 웨건 + 오른쪽 동물)
// =============================================================================
// [담당] Core Gameplay (윤호영)
// [구성] 왼쪽 = 웨건 영역:
//          · 웨건 없음 → [Edit] 버튼 → 웨건 선택 팝업(WagonSelectPopup)
//          · 웨건 있음 → 이미지 + 동물 슬롯(설치가능 수만큼) + 정보 + [Remove wagon]
//        오른쪽 = 동물 인벤토리(그리드):
//          · 칸 클릭 = 바로 1마리 웨건 슬롯에 추가(잔량 차감), 슬롯 클릭 = 1마리 빼기
//          · 칸 마우스오버 = 동물 정보 툴팁
//
// [역할] 웨건 넣기/빼기 + 동물 편성 + 요구량 검증(min~max) 표시. 순수 UI.
//   저장/복원은 상위(드라이버)가 이벤트·RestoreComposition으로 처리.
//   실제 편성 규칙(같은 종류 등)은 상위/CaravanValidator가 최종 확인.
// =============================================================================

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>동물 선택 패널 — 인벤토리 클릭→수량팝업→Wagon 슬롯. 요구량 검증. [1차 빌드]</summary>
public class AnimalInventoryPanel : MonoBehaviour
{
    [Header("인벤토리(오른쪽)")]
    [SerializeField] private Transform inventoryContainer;   // 소지 동물 버튼 목록 부모
    [SerializeField] private Button inventoryButtonPrefab;   // 인벤토리 동물 버튼 (자식 TMP_Text)

    [Header("Wagon Info(왼쪽 위) — 웨건 상세")]
    [SerializeField] private GameObject wagonDetailRoot;     // 웨건 이미지+슬롯+정보 묶음(웨건 있을 때만 표시)
    [SerializeField] private Transform slotContainer;        // 선택된 동물 슬롯 목록 부모
    [SerializeField] private Button slotButtonPrefab;        // 선택 슬롯 버튼 (클릭 시 취소, 자식 TMP_Text)
    [SerializeField] private TMP_Text wagonInfoText;         // 요구량/현재/검증 상태 표시

    [Header("Wagon 없을 때 — Edit 버튼")]
    [SerializeField] private Button editWagonButton;         // 웨건 없을 때 표시(클릭→웨건 선택 팝업)
    [SerializeField] private WagonSelectPopup wagonPopup;    // 웨건 선택 팝업
    [SerializeField] private Button removeWagonButton;       // 웨건 있을 때 하단 표시(클릭→웨건 빼기)

    [Header("마우스오버 툴팁")]
    [SerializeField] private AnimalTooltip tooltip;          // 동물 정보 툴팁

    /// <summary>선택이 바뀔 때. 인자 = (선택목록, 요구량 충족 여부).</summary>
    public event Action<IReadOnlyList<AnimalPick>, bool> OnSelectionChanged;
    /// <summary>웨건을 넣었을 때. 인자 = 선택된 웨건.</summary>
    public event Action<TransportSelectPanel.TransportEntry> OnWagonSelected;
    /// <summary>웨건을 뺐을 때(Remove). 상위가 상태 동기화(저장 해제 등)에 사용.</summary>
    public event Action OnWagonRemoved;

    private readonly List<AnimalEntry> animals = new List<AnimalEntry>();
    private readonly List<Button> invButtons = new List<Button>();
    private readonly List<Button> wagonSlots = new List<Button>();          // 고정 웨건 슬롯(설치가능 수만큼)
    private readonly List<string> slotAssign = new List<string>();          // 각 슬롯에 담긴 동물ID(""=빈칸)
    private readonly Dictionary<string, int> counts = new Dictionary<string, int>();   // 동물ID → 선택 수량
    private readonly List<TransportSelectPanel.TransportEntry> wagonInventory = new List<TransportSelectPanel.TransportEntry>();
    private int minReq, maxReq;
    private float wagonBaseSpeed;   // 선택된 마차 기본 이동속도(WagonWithAnimals면 0)
    private string wagonName = "";  // 선택된 마차 이름(Wagon Info 표시용)
    private bool hasWagon;          // 웨건을 넣었는가
    private TransportSelectPanel.TransportEntry currentWagon;
    private bool allowEditing = true;
    private bool wired;

    private void EnsureWired()
    {
        if (wired) return;
        wired = true;
        if (editWagonButton != null) editWagonButton.onClick.AddListener(OpenWagonPopup);
        if (removeWagonButton != null) removeWagonButton.onClick.AddListener(RemoveWagon);
    }

    /// <summary>웨건 빼기 → 웨건 없음 상태로. 선택 동물도 초기화.</summary>
    private void RemoveWagon()
    {
        if (!allowEditing) return;

        hasWagon = false; minReq = 0; maxReq = 0; wagonBaseSpeed = 0f; wagonName = "";
        currentWagon = default(TransportSelectPanel.TransportEntry);
        List<string> keys = new List<string>(counts.Keys);
        foreach (string k in keys) counts[k] = 0;
        RefreshWagonArea();
        ClearWagonSlots();
        UpdateInventoryLabels();
        OnWagonRemoved?.Invoke();   // 먼저 드라이버 상태 동기화(저장 해제 포함) 후 선택 통지
        OnSelectionChanged?.Invoke(BuildPicks(), IsValid());
    }

    /// <summary>동물 목록 + 소지 웨건 목록으로 채운다. 시작은 "웨건 없음"(Edit 버튼) 상태.</summary>
    public void Populate(IReadOnlyList<AnimalEntry> source,
                         IReadOnlyList<TransportSelectPanel.TransportEntry> ownedWagons)
    {
        ClearAll();
        allowEditing = true;
        EnsureWired();
        if (ownedWagons != null) wagonInventory.AddRange(ownedWagons);

        // 웨건 미선택 상태로 시작
        hasWagon = false; minReq = 0; maxReq = 0; wagonBaseSpeed = 0f; wagonName = "";
        currentWagon = default(TransportSelectPanel.TransportEntry);

        if (source != null)
        {
            foreach (AnimalEntry a in source)
            {
                string selectionKey = SelectionKey(a);
                if (string.IsNullOrEmpty(selectionKey) || counts.ContainsKey(selectionKey))
                {
                    // Duplicate instance IDs would merge two owned animals into one selectable entry.
                    Debug.LogError($"Skipped invalid or duplicate animal selection key: {selectionKey}", this);
                    continue;
                }

                animals.Add(a);
                counts[selectionKey] = 0;

                if (inventoryContainer != null && inventoryButtonPrefab != null)
                {
                    Button b = Instantiate(inventoryButtonPrefab, inventoryContainer);
                    AnimalEntry captured = a;
                    b.onClick.AddListener(() => OnInventoryClick(captured));   // 클릭 → 바로 1마리 슬롯에
                    // 마우스오버 툴팁 부착
                    AnimalTooltipTrigger trig = b.gameObject.AddComponent<AnimalTooltipTrigger>();
                    trig.Init(tooltip,
                        $"{a.name}\n이동속도 {a.moveSpeed:0.#}\n초당 먹이 {a.feedConsumption:0.#}\n" +
                        $"적재+ 평균 {a.incOverLoad:0.#} / 최대 {a.incMaxLoad:0.#}");
                    invButtons.Add(b);
                }
            }
        }
        RefreshWagonArea();
        ClearWagonSlots();       // 웨건 없음 → 슬롯 없음
        UpdateInventoryLabels(); // 잔량 표시
    }

    public void Populate(TradePrepareViewData viewData)
    {
        List<AnimalEntry> animalEntries = new List<AnimalEntry>();
        List<TransportSelectPanel.TransportEntry> wagonEntries = new List<TransportSelectPanel.TransportEntry>();

        if (viewData != null && viewData.draftAnimals != null)
            foreach (DraftAnimalViewData animal in viewData.draftAnimals)
                if (animal != null) animalEntries.Add(new AnimalEntry(animal));

        if (viewData != null && viewData.wagons != null)
            foreach (WagonViewData wagon in viewData.wagons)
                if (wagon != null && wagon.wagonType == WagonType.WagonWithAnimals)
                    wagonEntries.Add(new TransportSelectPanel.TransportEntry(wagon));

        Populate(animalEntries, wagonEntries);
    }

    /// <summary>Builds and restores S3 from one Caravan-specific Provider snapshot.</summary>
    public bool Populate(CaravanSettingViewData viewData)
    {
        if (viewData == null || string.IsNullOrWhiteSpace(viewData.caravanId))
        {
            // Detached edits require an explicit identity so another Caravan cannot be edited by accident.
            Debug.LogError("Cannot populate Caravan settings without a valid caravanId.", this);
            ClearAll();
            return false;
        }

        var animalEntries = new List<AnimalEntry>();
        var wagonEntries = new List<TransportSelectPanel.TransportEntry>();

        if (viewData.draftAnimals != null)
        {
            foreach (DraftAnimalViewData animal in viewData.draftAnimals)
                if (animal != null) animalEntries.Add(new AnimalEntry(animal));
        }

        if (viewData.wagons != null)
        {
            foreach (WagonViewData wagon in viewData.wagons)
            {
                // Walking is represented by removing the wagon instead of assigning a fake owned asset.
                if (wagon != null && wagon.wagonType != WagonType.None)
                    wagonEntries.Add(new TransportSelectPanel.TransportEntry(wagon));
            }
        }

        Populate(animalEntries, wagonEntries);
        allowEditing = viewData.canEdit;

        bool snapshotValid = true;
        if (!string.IsNullOrWhiteSpace(viewData.selectedWagonInstanceId))
        {
            int wagonIndex = wagonInventory.FindIndex(wagon =>
                string.Equals(
                    wagon.instanceId,
                    viewData.selectedWagonInstanceId,
                    StringComparison.Ordinal));
            if (wagonIndex < 0)
            {
                Debug.LogError(
                    $"Caravan {viewData.caravanId} references missing wagon instance {viewData.selectedWagonInstanceId}.",
                    this);
                snapshotValid = false;
            }
            else
            {
                ApplyWagon(wagonInventory[wagonIndex], false);
            }
        }

        if (viewData.selectedAnimalInstanceIds != null)
        {
            var restoredIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string selectedInstanceId in viewData.selectedAnimalInstanceIds)
            {
                string normalizedId = string.IsNullOrWhiteSpace(selectedInstanceId)
                    ? string.Empty
                    : selectedInstanceId.Trim();
                if (string.IsNullOrEmpty(normalizedId) || !restoredIds.Add(normalizedId))
                {
                    Debug.LogError(
                        $"Caravan {viewData.caravanId} contains an invalid or duplicate animal instance ID.",
                        this);
                    snapshotValid = false;
                    continue;
                }

                int animalIndex = animals.FindIndex(animal =>
                    string.Equals(animal.instanceId, normalizedId, StringComparison.Ordinal));
                if (animalIndex < 0)
                {
                    Debug.LogError(
                        $"Caravan {viewData.caravanId} references missing animal instance {normalizedId}.",
                        this);
                    snapshotValid = false;
                    continue;
                }

                string selectionKey = SelectionKey(animals[animalIndex]);
                counts[selectionKey] = 1;
            }
        }

        if (!hasWagon && TotalSelected() > 0)
        {
            // Animals without a wagon indicate an inconsistent authoritative snapshot.
            Debug.LogError($"Caravan {viewData.caravanId} has assigned animals but no wagon.", this);
            snapshotValid = false;
        }

        FillSlots();
        ApplyInteractionState();
        return snapshotValid;
    }

    /// <summary>[Edit] → 웨건 선택 팝업 열기.</summary>
    private void OpenWagonPopup()
    {
        if (!allowEditing) return;
        if (wagonPopup != null) wagonPopup.Open(wagonInventory, OnWagonChosen);
    }

    /// <summary>팝업에서 웨건 선택 → 요구량/정보 세팅 + 상세 표시.</summary>
    private void OnWagonChosen(TransportSelectPanel.TransportEntry w)
    {
        if (!allowEditing) return;
        ApplyWagon(w, true);
    }

    private void ApplyWagon(TransportSelectPanel.TransportEntry w, bool notifyChanges)
    {
        hasWagon = true;
        currentWagon = w;
        minReq = w.minAnimals;
        maxReq = Mathf.Max(w.minAnimals, w.maxAnimals);
        wagonBaseSpeed = w.baseMoveSpeed;
        wagonName = w.name;
        // 웨건 바뀌면 요구량이 달라지니 선택 동물 초기화
        List<string> keys = new List<string>(counts.Keys);
        foreach (string k in keys) counts[k] = 0;
        RefreshWagonArea();
        BuildWagonSlots(maxReq);   // 설치가능 동물 수(maxAnimals)만큼 슬롯 생성
        if (notifyChanges)
        {
            OnWagonSelected?.Invoke(w);
            OnSelectionChanged?.Invoke(BuildPicks(), IsValid());
        }
    }

    /// <summary>저장된 구성(웨건+동물)을 그대로 복원한다(Edit로 열 때).</summary>
    public void RestoreComposition(TransportSelectPanel.TransportEntry wagon, IReadOnlyList<AnimalPick> picks)
    {
        ApplyWagon(wagon, true);   // 웨건 넣기(슬롯 생성, counts 초기화)
        if (picks != null)
            foreach (AnimalPick p in picks)
            {
                string selectionKey = p.SelectionKey;
                if (counts.ContainsKey(selectionKey)) counts[selectionKey] = p.count;
            }
        FillSlots();
        OnSelectionChanged?.Invoke(BuildPicks(), IsValid());
    }

    /// <summary>웨건 유무에 따라 Edit 버튼 / 웨건 상세를 토글.</summary>
    private void RefreshWagonArea()
    {
        if (editWagonButton != null) editWagonButton.gameObject.SetActive(!hasWagon);
        if (wagonDetailRoot != null) wagonDetailRoot.SetActive(hasWagon);
        ApplyInteractionState();
    }

    private void ApplyInteractionState()
    {
        if (editWagonButton != null) editWagonButton.interactable = allowEditing;
        if (removeWagonButton != null) removeWagonButton.interactable = allowEditing;
        foreach (Button slot in wagonSlots)
            if (slot != null) slot.interactable = allowEditing;
    }

    /// <summary>인벤토리 동물 클릭 → 팝업 없이 바로 1마리 슬롯에 넣는다.</summary>
    private void OnInventoryClick(AnimalEntry a)
    {
        if (!allowEditing) return;
        if (!hasWagon) return;                          // 웨건부터 넣어야
        string selectionKey = SelectionKey(a);
        int placed = counts.TryGetValue(selectionKey, out int c) ? c : 0;
        if (placed >= a.ownedCount) return;             // 소지 다 씀
        if (TotalSelected() >= maxReq) return;          // 슬롯 꽉 참
        counts[selectionKey] = placed + 1;
        FillSlots();                                    // 슬롯 채움 + 인벤토리 잔량 갱신
        OnSelectionChanged?.Invoke(BuildPicks(), IsValid());
    }

    /// <summary>인벤토리 각 칸 라벨을 "이름 / 잔량"으로 갱신하고, 다 쓰면/꽉 차면 비활성.</summary>
    private void UpdateInventoryLabels()
    {
        bool full = hasWagon && TotalSelected() >= maxReq;
        for (int i = 0; i < animals.Count && i < invButtons.Count; i++)
        {
            AnimalEntry a = animals[i];
            string selectionKey = SelectionKey(a);
            int placed = counts.TryGetValue(selectionKey, out int c) ? c : 0;
            int remain = a.ownedCount - placed;
            SetLabel(invButtons[i], $"{a.name}\n(x{remain})");
            if (invButtons[i] != null)
                invButtons[i].interactable = allowEditing && hasWagon && a.canSelect && remain > 0 && !full;
        }
    }

    private int TotalSelected()
    {
        int total = 0;
        foreach (KeyValuePair<string, int> kv in counts) total += kv.Value;
        return total;
    }

    /// <summary>웨건의 설치가능 동물 수(n)만큼 고정 슬롯을 만든다(빈칸).</summary>
    private void BuildWagonSlots(int n)
    {
        ClearWagonSlots();
        if (slotContainer == null || slotButtonPrefab == null) return;
        for (int i = 0; i < n; i++)
        {
            Button slot = Instantiate(slotButtonPrefab, slotContainer);
            TMP_Text st = slot.GetComponentInChildren<TMP_Text>();
            if (st != null) st.fontSize = 20;   // 작은 칸용 폰트
            int idx = i;   // 캡처 방지
            slot.onClick.AddListener(() => RemoveAtSlot(idx));   // 슬롯 클릭 → 그 칸 동물 1마리 빼기
            slot.interactable = allowEditing;
            wagonSlots.Add(slot);
            slotAssign.Add("");
        }
        FillSlots();
    }

    /// <summary>현재 선택 동물을 고정 슬롯에 채운다(1칸=1마리). 남는 칸은 빈칸.</summary>
    private void FillSlots()
    {
        // 선택 동물을 flat하게 펼침 (Horse x2 → [Horse, Horse])
        List<string> flat = new List<string>();
        foreach (AnimalEntry a in animals)
        {
            string selectionKey = SelectionKey(a);
            int c = counts.TryGetValue(selectionKey, out int v) ? v : 0;
            for (int k = 0; k < c; k++) flat.Add(selectionKey);
        }
        for (int i = 0; i < wagonSlots.Count; i++)
        {
            string id = i < flat.Count ? flat[i] : "";
            slotAssign[i] = id;
            SetLabel(wagonSlots[i], string.IsNullOrEmpty(id) ? "-" : NameOf(id));
        }
        UpdateInfo();
        UpdateInventoryLabels();   // 넣은 만큼 잔량 반영
    }

    /// <summary>슬롯 i의 동물 1마리 빼기(그 종류 수량 -1).</summary>
    private void RemoveAtSlot(int i)
    {
        if (!allowEditing) return;
        if (i < 0 || i >= slotAssign.Count) return;
        string id = slotAssign[i];
        if (string.IsNullOrEmpty(id)) return;
        if (counts.TryGetValue(id, out int c) && c > 0) counts[id] = c - 1;
        FillSlots();
        OnSelectionChanged?.Invoke(BuildPicks(), IsValid());
    }

    /// <summary>웨건 정보 텍스트(요구량·속도·적재증가)를 현재 선택 기준으로 갱신.</summary>
    private void UpdateInfo()
    {
        int total = 0;
        float sumSpeed = 0f, sumOver = 0f, sumMax = 0f;
        foreach (AnimalEntry a in animals)
        {
            int c = counts.TryGetValue(SelectionKey(a), out int v) ? v : 0;
            if (c <= 0) continue;
            total += c;
            sumSpeed += a.moveSpeed * c;
            sumOver += a.incOverLoad * c;
            sumMax += a.incMaxLoad * c;
        }
        if (wagonInfoText != null)
        {
            float curSpeed = wagonBaseSpeed + sumSpeed;
            string state = IsValid() ? "충족" : (total < minReq ? "부족" : "초과");
            wagonInfoText.text =
                $"[{wagonName}]  동물 {minReq}~{maxReq} · 현재 {total} · {state}\n" +
                $"이동속도 {curSpeed:0.#}  ·  적재+ 평균 {sumOver:0.#} / 최대 {sumMax:0.#}";
        }
    }

    /// <summary>고정 슬롯을 모두 제거한다(즉시 파괴 — 재구성 시 한 프레임 중복 방지).</summary>
    private void ClearWagonSlots()
    {
        foreach (Button b in wagonSlots) if (b != null) DestroyImmediate(b.gameObject);
        wagonSlots.Clear();
        slotAssign.Clear();
    }

    private string NameOf(string id)
    {
        foreach (AnimalEntry a in animals) if (SelectionKey(a) == id) return a.name;
        return id;
    }

    private static string SelectionKey(AnimalEntry animal)
    {
        // Legacy trade preparation groups by content ID; detached Caravan editing uses instance ID.
        return string.IsNullOrWhiteSpace(animal.instanceId) ? animal.id : animal.instanceId;
    }

    /// <summary>요구량 충족 여부(웨건 있음 & min ≤ 합 ≤ max).</summary>
    public bool IsValid()
    {
        if (!hasWagon) return false;   // 웨건 없으면 진행 불가
        int total = 0;
        foreach (KeyValuePair<string, int> kv in counts) total += kv.Value;
        return total >= minReq && total <= maxReq;
    }

    /// <summary>Validates detached Caravan settings where no wagon represents walking.</summary>
    public bool CanConfirmCaravanSetting()
    {
        // Walking is valid only when no animal remains assigned to a removed wagon.
        return hasWagon ? IsValid() : TotalSelected() == 0;
    }

    private List<AnimalPick> BuildPicks()
    {
        List<AnimalPick> list = new List<AnimalPick>();
        foreach (AnimalEntry animal in animals)
        {
            string selectionKey = SelectionKey(animal);
            int count = counts.TryGetValue(selectionKey, out int selectedCount) ? selectedCount : 0;
            if (count > 0)
                list.Add(new AnimalPick(animal.id, animal.instanceId, count));
        }
        return list;
    }

    /// <summary>Creates a UI Draft snapshot without mutating runtime or SaveData.</summary>
    public bool TryCreateSettingDraft(string caravanId, out CaravanSettingDraft draft)
    {
        draft = null;
        string normalizedCaravanId = string.IsNullOrWhiteSpace(caravanId)
            ? string.Empty
            : caravanId.Trim();
        if (string.IsNullOrEmpty(normalizedCaravanId))
            return false;

        var result = new CaravanSettingDraft
        {
            caravanId = normalizedCaravanId,
            selectedWagonInstanceId = hasWagon ? currentWagon.instanceId ?? string.Empty : string.Empty
        };

        // Detached edits must use stable instance IDs; aggregate legacy selections cannot be committed here.
        if (hasWagon && string.IsNullOrWhiteSpace(result.selectedWagonInstanceId))
            return false;

        foreach (AnimalPick pick in BuildPicks())
        {
            if (pick.count <= 0)
                continue;
            if (string.IsNullOrWhiteSpace(pick.animalInstanceId) || pick.count != 1)
                return false;
            if (!result.SelectAnimal(pick.animalInstanceId))
                return false;
        }

        draft = result;
        return true;
    }

    /// <summary>전체(인벤토리 버튼·슬롯·상태) 비우기.</summary>
    public void ClearAll()
    {
        foreach (Button b in invButtons) if (b != null) Destroy(b.gameObject);
        invButtons.Clear();
        ClearWagonSlots();
        animals.Clear();
        counts.Clear();
        wagonInventory.Clear();
        hasWagon = false;
        currentWagon = default(TransportSelectPanel.TransportEntry);
    }

    private static void SetLabel(Button b, string text)
    {
        TMP_Text t = b.GetComponentInChildren<TMP_Text>();
        if (t != null) t.text = text;
    }

    /// <summary>인벤토리 동물 하나 (id + 이름 + 소지 수 + 스탯: 이동속도·적재량 증가치).</summary>
    [Serializable]
    public struct AnimalEntry
    {
        // Shared content ID used to resolve DraftAnimalData and presentation assets.
        public string id;

        // Stable owned-instance ID used by multi-Caravan persistence and asset locking.
        public string instanceId;

        public string name;
        public int ownedCount;
        public float moveSpeed;      // 기본 이동속도 (현재 이동속도 계산에 합산)
        public float incOverLoad;    // 평균(적정) 적재량 증가치
        public float incMaxLoad;     // 최대 적재량 증가치
        public float feedConsumption; // 초당 음식 소모량(툴팁 표시용)
        public bool canSelect;
        public string disabledReason;

        public AnimalEntry(string id, string name, int ownedCount,
                           float moveSpeed, float incOverLoad, float incMaxLoad, float feedConsumption)
        {
            this.id = id;
            instanceId = "";
            this.name = name;
            this.ownedCount = ownedCount;
            this.moveSpeed = moveSpeed;
            this.incOverLoad = incOverLoad;
            this.incMaxLoad = incMaxLoad;
            this.feedConsumption = feedConsumption;
            canSelect = true;
            disabledReason = "";
        }

        public AnimalEntry(DraftAnimalViewData viewData)
        {
            id = viewData.draftAnimalId;
            instanceId = viewData.draftAnimalInstanceId;
            name = viewData.displayName;
            // Detached setting options describe one owned instance per entry.
            ownedCount = string.IsNullOrEmpty(viewData.draftAnimalInstanceId) ? viewData.ownedAmount : 1;
            moveSpeed = viewData.baseMoveSpeed;
            incOverLoad = viewData.increaseOverLoad;
            incMaxLoad = viewData.increaseMaxLoad;
            feedConsumption = viewData.feedConsumption;
            canSelect = viewData.canSelect && viewData.isEligibleForSelectedWagon;
            disabledReason = viewData.disabledReason;
        }
    }

    /// <summary>선택 결과 한 항목 (동물ID + 수량).</summary>
    public struct AnimalPick
    {
        public string animalId;
        public string animalInstanceId;
        public int count;

        public string SelectionKey => string.IsNullOrWhiteSpace(animalInstanceId)
            ? animalId
            : animalInstanceId;

        public AnimalPick(string animalId, int count)
            : this(animalId, "", count)
        {
        }

        public AnimalPick(string animalId, string animalInstanceId, int count)
        {
            this.animalId = animalId;
            this.animalInstanceId = animalInstanceId;
            this.count = count;
        }
    }
}
