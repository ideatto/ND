// =============================================================================
// CaravanValidator — 상단 출발 검증 로직
// =============================================================================
// [담당]   Core Gameplay (윤호영)
// [역할]   상단(CaravanData)이 출발할 수 있는지 검증한다.
//          Validate(상단)을 호출하면 → 출발 가능 여부 + 안 되는 이유 목록을 돌려준다.
//
// [핵심 포인트]
//  · 검사 4가지 : ① 마차 없음  ② 견인 동물 수 부족  ③ 적재량 초과  ④ 무역품 없음
//  · 반환값 DepartureValidationResult = 마일스톤의 "출발 불가 원인" 값
//                                       → UI & Data(이종현님)에게 넘길 데이터.
//  · static 클래스라 씬/오브젝트에 붙이지 않는다.
//    호출은  CaravanValidator.Validate(상단)  형태로 한다.
//  · M0 완료 기준 "누락 구성과 적재량 초과를 코드에서 판별한다" 를 충족한다.
//
// [주의]   데이터(CaravanData)는 공용 데이터라 정의 소유자는 UI & Data(이종현님).
//          지금 필드는 확정 전 "임시 초안"이며, 최종 확정되면 데이터만 교체한다.
// =============================================================================

using System.Collections.Generic;

/// <summary>출발 불가 사유 (UI에 넘길 "출발 불가 원인") — 초안</summary>
public enum DepartureBlockReason
{
    NoWagon,          // 마차 없음
    NotEnoughAnimals, // 견인 동물 수 부족
    Overloaded,       // 적재량 초과
    NoCargo           // 실은 무역품 없음
}

/// <summary>출발 검증 결과 = 가능 여부 + 막힌 사유 목록</summary>
public class DepartureValidationResult
{
    public bool canDepart;  // 출발 가능한가?
    public List<DepartureBlockReason> reasons = new List<DepartureBlockReason>(); // 막힌 사유들
}

/// <summary>
/// 상단 출발 검증 로직. 자세한 설명은 파일 상단 주석 블록 참고.
/// (데이터 CaravanData는 UI & Data(이종현님) 확정 전 임시 초안 기준)
/// </summary>
public static class CaravanValidator
{
    public static DepartureValidationResult Validate(CaravanData caravan)
    {
        DepartureValidationResult result = new DepartureValidationResult();

        // ① 마차 없음
        if (caravan.wagon == null)
        {
            result.reasons.Add(DepartureBlockReason.NoWagon);
        }
        else
        {
            // ② 견인 동물 수 부족 (마차가 있어야 최소 필요 수를 알 수 있음)
            if (caravan.animals.Count < caravan.wagon.minAnimals)
                result.reasons.Add(DepartureBlockReason.NotEnoughAnimals);

            // ③ 적재량 초과 (현재 적재 무게 > 마차 최대 적재량)
            if (caravan.GetCurrentLoad() > caravan.wagon.maxLoad)
                result.reasons.Add(DepartureBlockReason.Overloaded);
        }

        // ④ 실은 무역품이 하나도 없음
        if (caravan.cargo.Count == 0)
            result.reasons.Add(DepartureBlockReason.NoCargo);

        // 막힌 사유가 하나도 없으면 출발 가능
        result.canDepart = (result.reasons.Count == 0);

        return result;
    }
}
