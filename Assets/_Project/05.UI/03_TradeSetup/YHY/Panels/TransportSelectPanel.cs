// =============================================================================
// TransportSelectPanel — 이동수단 선택 패널 (무역 준비 2단계)
// =============================================================================
// [담당] Core Gameplay (윤호영)
// [와이어프레임] 루트 선택 후, 이동수단을 고르는 단계. None / 마차A / 마차B / 자동차를
//   가로로 나열, 클릭으로 토글 선택, 선택 시 Next 활성. 좌우 슬라이드.
//   Next: 타입이 Wagon(동물 필요)이면 동물 선택으로, None/Mount면 적재로 분기.
//
// [역할] 이동수단 카드 목록을 가로로 만들고, 하나 선택 시 OnSelectionChanged로 알린다.
//   선택된 이동수단의 타입(TransportType)이 뒤 단계 분기의 근거가 된다.
//   (좌우 슬라이드는 가로 스크롤로 대체 가능 — 여기선 가로 배치만, 스크롤은 씬에서)
// =============================================================================

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>이동수단 타입 — 뒤 단계 분기 근거. Wagon만 동물 선택이 필요하다.</summary>
public enum TransportType
{
    None,    // 도보 등 — 동물 없음, 바로 적재
    Wagon,   // 마차(동물 견인) — 동물 선택 필요
    Mount    // 자동차/탈것 — 동물 없음, 바로 적재
}

/// <summary>이동수단 선택 패널 — 카드 가로 목록 + 단일 선택 이벤트. [1차 빌드]</summary>
public class TransportSelectPanel : MonoBehaviour
{
    [Header("카드 생성")]
    [SerializeField] private Transform cardContainer;   // 카드가 가로로 담기는 부모 (Horizontal)
    [SerializeField] private Button cardPrefab;         // 이동수단 카드 프리팹 (자식 TMP_Text)

    [Header("선택 색")]
    [SerializeField] private Color normalColor = new Color(0.80f, 0.83f, 0.92f);
    [SerializeField] private Color selectedColor = new Color(0.45f, 0.70f, 0.45f);
    [SerializeField] private Color emptyColor = new Color(0.82f, 0.82f, 0.84f, 0.5f);   // 미소지 빈 슬롯

    /// <summary>선택이 바뀔 때. 인자 = 선택된 이동수단(없으면 hasValue=false).</summary>
    public event Action<TransportEntry?> OnSelectionChanged;

    private readonly List<TransportEntry> items = new List<TransportEntry>();
    private readonly List<Button> spawned = new List<Button>();
    private readonly List<bool> selectable = new List<bool>();   // 소지(선택 가능) 여부
    private int selectedIndex = -1;

    /// <summary>이동수단 목록으로 카드를 만든다(미선택 상태). 소지 안 한 슬롯은 빈 칸·비활성.</summary>
    public void Populate(IReadOnlyList<TransportEntry> source)
    {
        Clear();
        if (source != null) items.AddRange(source);
        if (cardContainer == null || cardPrefab == null) return;

        for (int i = 0; i < items.Count; i++)
        {
            TransportEntry e = items[i];
            bool canPick = IsOwned(e);   // 도보(None)는 항상, 마차는 소지>0일 때만
            Button card = Instantiate(cardPrefab, cardContainer);
            TMP_Text t = card.GetComponentInChildren<TMP_Text>();
            if (t != null) t.text = BuildLabel(e);

            if (canPick)
            {
                SetColor(card, normalColor);
                card.interactable = true;
                int idx = i;   // for 캡처 방지
                card.onClick.AddListener(() => Select(idx));
            }
            else
            {
                SetColor(card, emptyColor);
                card.interactable = false;   // 미소지 → 클릭 불가(빈 슬롯)
            }
            selectable.Add(canPick);
            spawned.Add(card);
        }
    }

    public void Populate(TradePrepareViewData viewData)
    {
        List<TransportEntry> entries = new List<TransportEntry>();
        if (viewData != null && viewData.wagons != null)
            foreach (WagonViewData wagon in viewData.wagons)
                if (wagon != null) entries.Add(new TransportEntry(wagon));
        Populate(entries);
    }

    /// <summary>도보(None)는 항상 선택 가능, 그 외(마차/자동차)는 소지 개수>0일 때만.</summary>
    private static bool IsOwned(TransportEntry e)
    {
        return e.canSelect && (e.type == TransportType.None || e.owned > 0);
    }

    /// <summary>idx 카드를 선택(같은 걸 다시 누르면 해제). 색 갱신 + 통지.</summary>
    private void Select(int idx)
    {
        selectedIndex = (selectedIndex == idx) ? -1 : idx;
        for (int i = 0; i < spawned.Count; i++)
        {
            if (!selectable[i]) { SetColor(spawned[i], emptyColor); continue; }   // 빈 슬롯은 그대로
            SetColor(spawned[i], i == selectedIndex ? selectedColor : normalColor);
        }

        if (selectedIndex >= 0) OnSelectionChanged?.Invoke(items[selectedIndex]);
        else OnSelectionChanged?.Invoke(null);
    }

    /// <summary>현재 선택된 이동수단(없으면 null).</summary>
    public TransportEntry? Selected => selectedIndex >= 0 ? items[selectedIndex] : (TransportEntry?)null;

    /// <summary>선택을 모두 해제한다(화면 재진입 시 이전 선택 잔상 제거). 통지 포함.</summary>
    public void ResetSelection()
    {
        selectedIndex = -1;
        for (int i = 0; i < spawned.Count; i++)
            SetColor(spawned[i], selectable[i] ? normalColor : emptyColor);
        OnSelectionChanged?.Invoke(null);
    }

    /// <summary>카드·상태를 모두 비운다.</summary>
    public void Clear()
    {
        foreach (Button b in spawned)
            if (b != null) Destroy(b.gameObject);
        spawned.Clear();
        items.Clear();
        selectable.Clear();
        selectedIndex = -1;
    }

    private static void SetColor(Button b, Color c)
    {
        Image img = b.GetComponent<Image>();
        if (img != null) img.color = c;
    }

    private static string BuildLabel(TransportEntry e)
    {
        // 카드 안 요약: 이름 / 타입 / 칸 / (마차면 동물 요구) / 소지
        string line = $"{e.name}\n[{e.type}]\nslots {e.slotCount}";
        if (e.type == TransportType.Wagon) line += $"\nanimals {e.minAnimals}~{e.maxAnimals}";
        if (e.type == TransportType.None) line += "\n(on foot)";              // 도보는 소지 개념 없음
        else line += e.owned > 0 ? $"\nowned {e.owned}" : "\n(empty)";        // 미소지 = 빈 슬롯
        if (e.maxDurability > 0) line += $"\ndurability {e.currentDurability}/{e.maxDurability}";
        line += $"\nload {e.overLoad:0.#}/{e.maxLoad:0.#}";
        if (!e.canSelect && !string.IsNullOrWhiteSpace(e.disabledReason)) line += $"\n{e.disabledReason}";
        return line;
    }

    /// <summary>이동수단 하나의 입력 데이터.</summary>
    [Serializable]
    public struct TransportEntry
    {
        public string id;
        public string name;
        public TransportType type;
        public int slotCount;     // 아이템 슬롯 수
        public float maxLoad;     // 최대 적재량(무게)
        public int minAnimals;    // 최소 요구 동물 수 (Wagon만 의미)
        public int maxAnimals;    // 최대 요구 동물 수 (Wagon만 의미)
        public float baseMoveSpeed; // 마차 기본 이동속도 (Wagon은 0, 동물 화면 속도계산에 사용)
        public int owned;         // 소지 개수(0 = 미소지 빈 슬롯). 도보(None)는 무시하고 항상 사용 가능
        public float overLoad;
        public int currentDurability;
        public int maxDurability;
        public bool canSelect;
        public string disabledReason;

        public TransportEntry(string id, string name, TransportType type, int slotCount, float maxLoad,
                              int minAnimals, int maxAnimals, float baseMoveSpeed, int owned)
        {
            this.id = id;
            this.name = name;
            this.type = type;
            this.slotCount = slotCount;
            this.maxLoad = maxLoad;
            this.minAnimals = minAnimals;
            this.maxAnimals = maxAnimals;
            this.baseMoveSpeed = baseMoveSpeed;
            this.owned = owned;
            overLoad = 0f;
            currentDurability = 0;
            maxDurability = 0;
            canSelect = true;
            disabledReason = "";
        }

        public TransportEntry(WagonViewData viewData)
        {
            id = viewData.wagonId;
            name = viewData.displayName;
            type = viewData.wagonType == WagonType.None ? TransportType.None :
                   viewData.wagonType == WagonType.Mount ? TransportType.Mount : TransportType.Wagon;
            slotCount = viewData.inventorySlotCount;
            maxLoad = viewData.maxLoad;
            minAnimals = viewData.minRequireAnimals;
            maxAnimals = viewData.maxPullAnimals;
            baseMoveSpeed = viewData.baseMoveSpeed;
            owned = viewData.ownedAmount;
            overLoad = viewData.overLoad;
            currentDurability = viewData.currentDurability;
            maxDurability = viewData.maxDurability;
            canSelect = viewData.canSelect && (viewData.isOwned || viewData.wagonType == WagonType.None);
            disabledReason = viewData.disabledReason;
        }
    }
}
