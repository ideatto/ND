// =============================================================================
// CaravanData — 상단(캐러밴) 데이터 (공용 데이터, 초안)
// =============================================================================
// [작성] 윤호영 (요구사항 초안)
// [영역] 공용 데이터 — 정의 소유자는 UI & Data(이종현님).
//         지금 필드는 확정 전 "임시 초안"이며, 최종 확정되면 데이터만 교체한다.
//
// [핵심 포인트]
//  · 상단 하나의 전체 구성 = 마차 + 견인동물 + 용병 + 적재(무역품/식량) + 현재 무역 상태.
//  · 출발 검증·이동·정산이 모두 이 데이터를 기준으로 동작한다.
//  · GetCurrentLoad() = 현재 적재 총 무게 = 마일스톤의 "CurrentLoad"(UI에 넘길 값).
//    (MaxLoad는 wagon.maxLoad)
//  · 리스트 3개(animals·mercenaries·cargo)로 여러 개를 담을 수 있다.
// =============================================================================

using System;
using System.Collections.Generic;

/// <summary>적재한 무역품 한 종류 (아이템 + 수량) — 초안</summary>
[Serializable]
public class CargoEntry
{
    public TradeItemData item;  // 어떤 무역품인지
    public int quantity;        // 몇 개 실었는지
}

/// <summary>상단 데이터. 자세한 설명은 상단 주석 참고.</summary>
[Serializable]
public class CaravanData
{
    public WagonData wagon;                                       // 마차 (1대)
    public List<AnimalData> animals = new List<AnimalData>();      // 견인 동물 목록
    public List<MercenaryData> mercenaries = new List<MercenaryData>(); // 용병 목록
    public List<CargoEntry> cargo = new List<CargoEntry>();       // 적재한 무역품 목록
    public int foodAmount;                                        // 실은 식량 수량
    public float foodUnitWeight = 1f;                             // 식량 1개당 무게

    public TradeState state = TradeState.Prepare;                 // 현재 무역 상태

    /// <summary>
    /// 현재 적재 총 무게 = 무역품 무게 합 + 식량 무게 합.
    /// (마일스톤 인터페이스의 CurrentLoad로 UI에 넘길 값)
    /// </summary>
    public float GetCurrentLoad()
    {
        float total = 0f;

        // 무역품: 각 아이템 무게 × 수량
        foreach (CargoEntry entry in cargo)
        {
            if (entry.item != null)
                total += entry.item.weight * entry.quantity;
        }

        // 식량: 수량 × 1개당 무게
        total += foodAmount * foodUnitWeight;

        return total;
    }
}
