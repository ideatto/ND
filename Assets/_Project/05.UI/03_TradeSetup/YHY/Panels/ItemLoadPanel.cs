// =============================================================================
// ItemLoadPanel — 아이템 적재 패널 (무역 준비 5단계)
// =============================================================================
// [담당] Core Gameplay (윤호영)
// [와이어프레임] ⑤ 동물을 고른 뒤, 마차 슬롯에 무역품을 종류·수량 선택 — 선택 시 출발(요약)로.
//
// [역할] 아이템 목록을 행(QuantityRow, 공용)으로 만들고, 각 행의 수량을 모아
//        OnLoadChanged(선택목록)로 상위에 알린다. 선택목록 = (아이템ID, 수량) 중 수량>0.
//        · 슬롯 규칙: "수량>0인 종류 수 ≤ 마차 슬롯 수" — 슬롯이 꽉 차면 새 종류 추가는 막는다.
//        · 무게/과적 최종 판정은 CaravanCalculator·CaravanValidator가 담당(여긴 슬롯만 즉시 제어).
//
// [쓰는 법 — 유니티]
//   1) 패널 루트에 이 스크립트를 붙인다.
//   2) rowContainer(스크롤 Content) · rowPrefab(QuantityRow 붙은 프리팹) 연결.
//   3) Populate(아이템목록, 마차슬롯수) 호출 → 행 생성. 수량 변화는 OnLoadChanged로 받는다.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>아이템 적재 패널 — 아이템별 수량 행 + 슬롯 제한 + 적재 변경 이벤트. [1차 빌드]</summary>
public class ItemLoadPanel : MonoBehaviour
{
    [Header("행 생성")]
    [SerializeField] private Transform rowContainer;   // 행이 담길 부모 (ScrollView Content 등)
    [SerializeField] private QuantityRow rowPrefab;    // QuantityRow(공용) 컴포넌트가 붙은 행 프리팹

    /// <summary>적재 수량이 하나라도 바뀌면 호출. 인자 = 수량>0인 (아이템ID, 수량) 목록.</summary>
    public event Action<IReadOnlyList<ItemLoad>> OnLoadChanged;

    private readonly List<QuantityRow> spawned = new List<QuantityRow>();
    private readonly List<string> rowIds = new List<string>();                          // spawned와 같은 순서의 아이템ID
    private readonly Dictionary<string, int> counts = new Dictionary<string, int>();   // 아이템ID → 현재 수량

    private int maxSlots;   // 마차 슬롯 수 = 실을 수 있는 "종류"의 상한

    /// <summary>아이템 목록으로 행을 다시 만든다. (id, 이름, 단위무게, 상한) + 마차 슬롯 수.</summary>
    public void Populate(IReadOnlyList<ItemEntry> items, int wagonSlots)
    {
        Clear();
        maxSlots = wagonSlots;
        if (items == null || rowContainer == null || rowPrefab == null) return;

        foreach (ItemEntry it in items)
        {
            QuantityRow row = Instantiate(rowPrefab, rowContainer);
            row.Setup(it.id, $"{it.name} (each {it.unitWeight:0.#})", 0, it.maxCount);   // 시작 수량 0
            row.OnChanged = HandleRowChanged;
            counts[it.id] = 0;
            spawned.Add(row);
            rowIds.Add(it.id);   // 행-아이템 매칭 기록(롤백 시 사용)
        }
    }

    /// <summary>행 수량이 바뀔 때 — 슬롯(종류 수) 초과면 되돌리고, 아니면 갱신 후 통지.</summary>
    private void HandleRowChanged(string itemId, int count)
    {
        // 이번 변경으로 "새로운 종류"가 생기는 경우에만 슬롯 검사.
        bool wasZero = counts.TryGetValue(itemId, out int prev) && prev == 0;
        if (count > 0 && wasZero && UsedSlots() >= maxSlots)
        {
            // 슬롯이 이미 꽉 참 → 이 종류는 담을 수 없음. 값·행을 0으로 되돌린다.
            // (세밀한 "슬롯 가득" 안내 UX는 상위/디자인에서 보강)
            counts[itemId] = 0;
            SyncRowSilently(itemId, 0);
            OnLoadChanged?.Invoke(BuildLoad());   // 롤백 결과 통지
            return;
        }

        counts[itemId] = count;
        OnLoadChanged?.Invoke(BuildLoad());
    }

    /// <summary>수량>0인 종류 수(사용 슬롯).</summary>
    private int UsedSlots()
    {
        int n = 0;
        foreach (KeyValuePair<string, int> kv in counts)
            if (kv.Value > 0) n++;
        return n;
    }

    /// <summary>특정 아이템 행의 표시 수량을 강제로 맞춘다(슬롯 롤백용, 알림 없음).</summary>
    private void SyncRowSilently(string itemId, int value)
    {
        int index = rowIds.IndexOf(itemId);   // 생성 순서 기록에서 id로 행 찾기(Dictionary 순서 의존 X)
        if (index >= 0 && index < spawned.Count && spawned[index] != null)
            spawned[index].SetCountSilently(value);
    }

    /// <summary>현재 수량>0인 것만 모아 적재목록으로 만든다.</summary>
    private List<ItemLoad> BuildLoad()
    {
        List<ItemLoad> result = new List<ItemLoad>();
        foreach (KeyValuePair<string, int> kv in counts)
            if (kv.Value > 0) result.Add(new ItemLoad(kv.Key, kv.Value));
        return result;
    }

    /// <summary>생성된 행을 모두 제거하고 수량 기록도 비운다.</summary>
    public void Clear()
    {
        foreach (QuantityRow r in spawned)
            if (r != null) Destroy(r.gameObject);
        spawned.Clear();
        rowIds.Clear();
        counts.Clear();
    }

    /// <summary>아이템 행 하나의 입력 데이터 (id + 이름 + 단위무게 + 상한).</summary>
    [Serializable]
    public struct ItemEntry
    {
        public string id;
        public string name;
        public float unitWeight;   // 개당 무게(표시용) — 최종 과적 판정은 CaravanCalculator
        public int maxCount;       // 이 아이템의 최대 선택 수량(보유/재고 등)

        public ItemEntry(string id, string name, float unitWeight, int maxCount)
        {
            this.id = id;
            this.name = name;
            this.unitWeight = unitWeight;
            this.maxCount = maxCount;
        }
    }

    /// <summary>적재 결과 한 항목 (아이템ID + 수량).</summary>
    public struct ItemLoad
    {
        public string itemId;
        public int count;

        public ItemLoad(string itemId, int count)
        {
            this.itemId = itemId;
            this.count = count;
        }
    }
}
