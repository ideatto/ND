// =============================================================================
// PlayerMainManager — 플레이어 데이터 메인 매니저 (SaveData 접근 창구)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 플레이어의 런타임 데이터(소지금·인벤토리·마을 등)를 관리하는 메인 매니저.
//        지금까지 각 시스템이 SaveData를 직접 참조하던 것을, 앞으로 이 매니저를
//        거치게 바꿔서 진실을 한 곳으로 모은다.
//
// [현 단계]
//   · 소지금·마차 화물(caravan.cargo)은 SaveData를 직접 참조 → 진실은 SaveData 하나.
//   · 거점 인벤토리는 SaveData에 아직 없어 매니저가 직접 소유(진실). 저장은 추후 편입.
//   나중에 무역·정산 등 다른 코드가 이 매니저를 참조하도록 전환하면 매니저가 최종 진실.
//   거점 창고·마차 화물 모두 같은 CargoEntrySaveData 타입이라 서로 이동이 자연스럽다.
//
// [주의] SaveData 타입은 반드시 ND.Framework.SaveData 풀네임을 쓴다.
//        정헌님(_LJH)의 전역 SaveData와 이름이 겹치기 때문(alias·using으로는 충돌).
//
// [폴백] FrameworkRoot(SaveData 소유자)가 없는 테스트 씬에서는 임시 SaveData로 동작.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using ND.Framework;

/// <summary>플레이어 데이터 메인 매니저. SaveData를 감싸 게임플레이에 노출한다.</summary>
public class PlayerMainManager : MonoBehaviour
{
    public static PlayerMainManager Instance { get; private set; }

    /// <summary>FrameworkRoot가 없는 테스트 씬용 임시 SaveData.</summary>
    private ND.Framework.SaveData fallbackSave;

    /// <summary>현재 SaveData — FrameworkRoot 있으면 CurrentSaveData(진실), 없으면 폴백.</summary>
    private ND.Framework.SaveData Save
    {
        get
        {
            FrameworkRoot root = FrameworkRoot.Instance;
            if (root != null && root.CurrentSaveData != null)
                return root.CurrentSaveData;
            return fallbackSave ?? (fallbackSave = new ND.Framework.SaveData());
        }
    }

    /// <summary>소지금이 바뀌면 알림(재화 HUD 등이 구독).</summary>
    public event Action<long> OnGoldChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SanitizeHomeInventory();   // 직렬화로 딸려온 무효 항목(빈 id·count 0) 제거
    }

    /// <summary>거점 인벤토리에서 유효하지 않은 항목(빈 id 또는 count 0 이하)을 제거한다.</summary>
    private void SanitizeHomeInventory()
    {
        for (int i = homeInventory.Count - 1; i >= 0; i--)
        {
            ND.Framework.CargoEntrySaveData e = homeInventory[i];
            if (e == null || e.item == null || string.IsNullOrEmpty(e.item.itemId) || e.quantity <= 0)
                homeInventory.RemoveAt(i);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── 소지금 (지금은 SaveData 참조) ────────────
    /// <summary>현재 소지금(무역 재화).</summary>
    public long Gold => Save.player.tradingCurrency;

    /// <summary>소지금 증가.</summary>
    public void AddGold(long amount)
    {
        if (amount == 0) return;
        Save.player.tradingCurrency += amount;
        OnGoldChanged?.Invoke(Save.player.tradingCurrency);
    }

    /// <summary>소지금 지불(부족하면 false, 차감 안 함).</summary>
    public bool SpendGold(long amount)
    {
        if (amount <= 0) return true;
        if (Save.player.tradingCurrency < amount) return false;
        Save.player.tradingCurrency -= amount;
        OnGoldChanged?.Invoke(Save.player.tradingCurrency);
        return true;
    }

    // ── 거점 인벤토리 (매니저가 진실로 소유) ──────
    // 거점 창고 아이템과 마차 화물(caravan.cargo)은 "서로 오가는 같은 물건"이라
    // 표현을 통일한다: Framework 공용 DTO CargoEntrySaveData(item + quantity)를 그대로 사용.
    // → 창고 ↔ 마차 이동, 무역·정산과 데이터 일관. 저장은 나중에 SaveData에 편입.
    // (SaveData에 없는 신규 데이터라 지금은 매니저가 직접 들고 있는 게 진실.)
    [SerializeField] private List<ND.Framework.CargoEntrySaveData> homeInventory
        = new List<ND.Framework.CargoEntrySaveData>();

    /// <summary>거점 인벤토리 조회(읽기 전용, UI 바인딩용).</summary>
    public IReadOnlyList<ND.Framework.CargoEntrySaveData> HomeInventory => homeInventory;

    /// <summary>거점 인벤토리가 바뀌면 알림(인벤토리 UI가 구독).</summary>
    public event Action OnHomeInventoryChanged;

    /// <summary>
    /// 거점 인벤토리에 아이템 추가(같은 itemId면 수량 합산).
    /// item은 TradeItemData(SO) → TradeItemSaveData 변환 결과를 넘긴다
    /// (SO→DTO 변환은 데이터 소유자=이종현님 매퍼 몫, Core는 DTO만 소비).
    /// </summary>
    public void AddItem(ND.Framework.TradeItemSaveData item, int count)
    {
        if (item == null || string.IsNullOrEmpty(item.itemId) || count == 0) return;
        ND.Framework.CargoEntrySaveData entry = FindEntry(item.itemId);
        if (entry == null)
        {
            entry = new ND.Framework.CargoEntrySaveData { item = item, quantity = 0 };
            homeInventory.Add(entry);
        }
        entry.quantity += count;
        if (entry.quantity <= 0) homeInventory.Remove(entry);
        OnHomeInventoryChanged?.Invoke();
    }

    /// <summary>거점 인벤토리에서 아이템 제거(부족하면 false, 차감 안 함).</summary>
    public bool RemoveItem(string itemId, int count)
    {
        if (count <= 0) return true;
        ND.Framework.CargoEntrySaveData entry = FindEntry(itemId);
        if (entry == null || entry.quantity < count) return false;
        entry.quantity -= count;
        if (entry.quantity <= 0) homeInventory.Remove(entry);
        OnHomeInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>거점 인벤토리의 아이템 보유 개수(없으면 0).</summary>
    public int GetItemCount(string itemId)
    {
        ND.Framework.CargoEntrySaveData entry = FindEntry(itemId);
        return entry != null ? entry.quantity : 0;
    }

    private ND.Framework.CargoEntrySaveData FindEntry(string itemId)
    {
        foreach (ND.Framework.CargoEntrySaveData e in homeInventory)
            if (e != null && e.item != null && e.item.itemId == itemId) return e;
        return null;
    }

    // ── 마차 인벤토리 (지금은 SaveData 참조) ──────
    // 소지금과 같은 방식: 진실은 SaveData.caravan.cargo, 매니저는 창구만 제공.
    // 거점 인벤토리와 같은 CargoEntrySaveData 타입이라 창고 ↔ 마차 이동이 자연스럽다.
    // [주의] 마차 화물은 평소 무역 준비/정산 시스템이 채운다. 매니저 조작은 전환기용 창구.

    /// <summary>마차에 적재된 화물 조회(읽기 전용, SaveData 참조).</summary>
    public IReadOnlyList<ND.Framework.CargoEntrySaveData> CaravanCargo => Save.caravan.cargo;

    /// <summary>마차 화물이 바뀌면 알림(마차 UI가 구독).</summary>
    public event Action OnCaravanCargoChanged;

    /// <summary>마차에 화물 적재(같은 itemId면 수량 합산).</summary>
    public void AddCargo(ND.Framework.TradeItemSaveData item, int count)
    {
        if (item == null || string.IsNullOrEmpty(item.itemId) || count == 0) return;
        List<ND.Framework.CargoEntrySaveData> cargo = Save.caravan.cargo;
        ND.Framework.CargoEntrySaveData entry = FindCargoEntry(item.itemId);
        if (entry == null)
        {
            entry = new ND.Framework.CargoEntrySaveData { item = item, quantity = 0 };
            cargo.Add(entry);
        }
        entry.quantity += count;
        if (entry.quantity <= 0) cargo.Remove(entry);
        OnCaravanCargoChanged?.Invoke();
    }

    /// <summary>마차에서 화물 하역(부족하면 false, 차감 안 함).</summary>
    public bool RemoveCargo(string itemId, int count)
    {
        if (count <= 0) return true;
        ND.Framework.CargoEntrySaveData entry = FindCargoEntry(itemId);
        if (entry == null || entry.quantity < count) return false;
        entry.quantity -= count;
        if (entry.quantity <= 0) Save.caravan.cargo.Remove(entry);
        OnCaravanCargoChanged?.Invoke();
        return true;
    }

    /// <summary>마차 화물의 특정 아이템 수량(없으면 0).</summary>
    public int GetCargoCount(string itemId)
    {
        ND.Framework.CargoEntrySaveData entry = FindCargoEntry(itemId);
        return entry != null ? entry.quantity : 0;
    }

    private ND.Framework.CargoEntrySaveData FindCargoEntry(string itemId)
    {
        foreach (ND.Framework.CargoEntrySaveData e in Save.caravan.cargo)
            if (e != null && e.item != null && e.item.itemId == itemId) return e;
        return null;
    }

    // ── 도메인 매니저 창구 (B: 메인이 총괄) ──────
    // 마을·건물 도메인은 마을 매니저(VillageBuildingRegistry)가 소유·관리한다.
    // 메인 매니저는 그걸 "소유"하지 않고, 다른 코드가 한 창구로 접근하게 "참조만" 제공한다.
    // 마을 매니저는 마을 씬에 속해 씬과 함께 생겼다 사라지므로, 들고 있지 말고 호출 시점에 조회한다.

    /// <summary>마을(건물) 매니저 창구. 마을 씬이 로드 안 됐으면 null. (예: PlayerMainManager.Instance.Village?.AddOrUpgrade(i))</summary>
    public VillageBuildingRegistry Village => VillageBuildingRegistry.Instance;
}
