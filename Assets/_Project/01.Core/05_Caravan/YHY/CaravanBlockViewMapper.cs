// =============================================================================
// CaravanBlockViewMapper — CaravanData(런타임) → CaravanBlockViewData(화면용) 매핑
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 상단 목록 화면(CaravanOverviewPresenter, 이종현님)이 쓰는 CaravanBlockViewData를
//        Core 런타임 CaravanData에서 채운다. Overview Provider가 슬롯마다 이 매퍼를 부른다.
//
// [채우는 것 — Core가 아는 값]
//   · slotIndex        : 호출자가 지정(고정 슬롯 인덱스)
//   · caravanId        : 상단 식별자
//   · displayName      : 상단 표시명
//   · state            : JourneyState (Prepare/Traveling/Settling/Completed) — Core 소유
//   · wagonContentId   : 마차 콘텐츠 ID (입력으로 받음 — 아래 [열어둠] 참고)
//   · animalIcons[]    : 동물 종류별 개수 요약
//   · cargoIcons[]     : 화물 종류별 개수 요약
//
// [열어둠 — 아직 미확정이라 입력으로 받는다]
//   · slotState / unlockHintText : "상단 해금 조건"이 미정이라 Core가 판정 못 함.
//                                  Occupied면 채워진 상단, Empty/Locked는 호출자가 지정.
//   · *ContentId (아이콘 키)      : 런타임 데이터의 id를 그대로 넣는다. UI 콘텐츠 카탈로그
//                                  키와 매핑이 다르면 호출자가 교체한다. (이종현님과 확인 대기)
//
// [경계] ViewData 타입 정의는 UI(이종현님) 소유. Core는 그걸 소비해 채우기만 한다.
//        상태 변경·저장 없음(읽기 전용 스냅샷 생성).
// =============================================================================

using System.Collections.Generic;

/// <summary>런타임 상단을 화면용 블록 데이터로 채운다.</summary>
public static class CaravanBlockViewMapper
{
    /// <summary>
    /// "구성된(Occupied) 상단"을 CaravanBlockViewData로 채운다.
    /// slotState는 Occupied로 고정하고, 해금/빈 슬롯 표현은 별도 헬퍼(호출자)가 만든다.
    /// </summary>
    /// <param name="caravan">런타임 상단(필수).</param>
    /// <param name="slotIndex">이 상단이 놓인 고정 UI 슬롯 인덱스.</param>
    public static CaravanBlockViewData BuildOccupied(CaravanData caravan, int slotIndex)
    {
        CaravanBlockViewData block = new CaravanBlockViewData
        {
            slotIndex = slotIndex,
            slotState = CaravanSlotState.Occupied,
            unlockHintText = string.Empty
        };
        if (caravan == null) return block;   // 방어: 빈 블록(Occupied지만 내용 없음) 반환

        block.caravanId = caravan.caravanId ?? string.Empty;
        block.displayName = caravan.wagon != null ? caravan.wagon.wagonName : string.Empty;
        block.state = caravan.state;
        block.wagonContentId = caravan.wagon != null ? caravan.wagon.instanceId : string.Empty;
        block.animalIcons = BuildAnimalIcons(caravan);
        block.cargoIcons = BuildCargoIcons(caravan);
        return block;
    }

    /// <summary>빈 슬롯(새 상단을 만들 수 있는 자리)을 표현한다.</summary>
    public static CaravanBlockViewData BuildEmpty(int slotIndex)
    {
        return new CaravanBlockViewData
        {
            slotIndex = slotIndex,
            slotState = CaravanSlotState.Empty
        };
    }

    /// <summary>잠긴 슬롯(아직 해금 안 됨)을 표현한다. 해금 조건은 미정이라 안내문을 입력으로 받는다.</summary>
    public static CaravanBlockViewData BuildLocked(int slotIndex, string unlockHintText)
    {
        return new CaravanBlockViewData
        {
            slotIndex = slotIndex,
            slotState = CaravanSlotState.Locked,
            unlockHintText = unlockHintText ?? string.Empty
        };
    }

    // ── 동물 요약: 종류(animalName)별로 개수를 합쳐 아이콘 목록으로 ──
    private static AnimalIconViewData[] BuildAnimalIcons(CaravanData caravan)
    {
        if (caravan.animals == null || caravan.animals.Count == 0)
            return System.Array.Empty<AnimalIconViewData>();

        // 종류별 개수 집계(순서 유지)
        List<string> order = new List<string>();
        Dictionary<string, int> counts = new Dictionary<string, int>();
        foreach (imsiAnimalData a in caravan.animals)
        {
            if (a == null) continue;
            string key = a.animalName ?? string.Empty;
            if (!counts.ContainsKey(key)) { counts[key] = 0; order.Add(key); }
            counts[key]++;
        }

        AnimalIconViewData[] icons = new AnimalIconViewData[order.Count];
        for (int i = 0; i < order.Count; i++)
            icons[i] = new AnimalIconViewData { animalContentId = order[i], quantity = counts[order[i]] };
        return icons;
    }

    // ── 화물 요약: cargo 항목을 아이콘 목록으로(수량>0만) ──
    private static CargoIconViewData[] BuildCargoIcons(CaravanData caravan)
    {
        if (caravan.cargo == null || caravan.cargo.Count == 0)
            return System.Array.Empty<CargoIconViewData>();

        List<CargoIconViewData> icons = new List<CargoIconViewData>();
        foreach (CargoEntry entry in caravan.cargo)
        {
            if (entry == null || entry.item == null || entry.quantity <= 0) continue;
            icons.Add(new CargoIconViewData
            {
                itemId = entry.item.id ?? string.Empty,
                quantity = entry.quantity
            });
        }
        return icons.ToArray();
    }
}
