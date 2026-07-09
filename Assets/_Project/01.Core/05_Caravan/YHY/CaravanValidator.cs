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
//  · 적재량은 Core 소유 CaravanCalculator.GetCurrentLoad()로 계산한다(계산 로직 일원화).
//  · null 상단이 들어와도 예외 없이 canDepart=false로 안전하게 반환한다.
//  · static 클래스 / UnityEngine 의존 없음 → 순수 로직 테스트 가능.
//    호출은  CaravanValidator.Validate(상단)  형태로 한다.
//
// [주의]   데이터(CaravanData)는 공용 데이터라 정의 소유자는 UI & Data(이종현님).
//          지금 필드는 확정 전 "임시 초안"이며, 최종 확정되면 데이터만 교체한다.
// =============================================================================

using System.Collections.Generic;

/// <summary>출발 불가 사유 (UI에 넘길 "출발 불가 원인") — 초안</summary>
public enum DepartureBlockReason
{
    NoWagon,          // 마차 없음
    NotEnoughAnimals, // 견인 동물 수 부족 (minAnimals 미만)
    TooManyAnimals,   // 견인 동물 수 초과 (maxAnimals 초과) → 출발 불가
    Overloaded,       // 물리 상한(maxLoad) 초과 → 출발 불가
    NoCargo,          // 실은 무역품 없음
    BrokenWagon       // 마차 내구도 0 → 수리 전 출발 불가 [M2]
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

        // ⓪ null 방어: 상단 자체가 없으면 출발 불가(사유 없음). NRE 방지.
        //    (플레이 중엔 발생하지 않는 프로그래밍 오류 상황이므로 사유 목록엔 넣지 않는다)
        if (caravan == null)
        {
            result.canDepart = false;
            return result;
        }

        // ① 마차 없음
        if (caravan.wagon == null)
        {
            result.reasons.Add(DepartureBlockReason.NoWagon);
        }
        else
        {
            // ② 견인 동물 수 검사 (마차가 있어야 최소/최대를 알 수 있음)
            //    부족: minAnimals 미만 → 출발 불가
            //    초과: maxAnimals 초과 → 출발 불가.
            //          정상적으론 UI가 max까지만 붙이게 막지만, Core는 안전장치로 여기서도 막는다.
            if (caravan.animals.Count < caravan.wagon.minAnimals)
                result.reasons.Add(DepartureBlockReason.NotEnoughAnimals);
            else if (caravan.animals.Count > caravan.wagon.maxAnimals)
                result.reasons.Add(DepartureBlockReason.TooManyAnimals);

            // ③ 물리 상한 초과 → 출발 불가 (현재 무게 > maxLoad).
            //    참고: overLoad(기준선) 초과는 여기서 막지 않는다. 그건 출발은 되되
            //    TravelCalculator에서 속도만 감소시킨다. 여기서 막는 건 maxLoad(물리 상한)뿐.
            if (CaravanCalculator.GetCurrentLoad(caravan) > caravan.wagon.maxLoad)
                result.reasons.Add(DepartureBlockReason.Overloaded);

            // 마차 내구도 소진(0 이하) → 수리 전 출발 불가 [M2]
            if (caravan.currentDurability <= 0)
                result.reasons.Add(DepartureBlockReason.BrokenWagon);
        }

        // ④ 실은 무역품이 하나도 없음
        if (caravan.cargo.Count == 0)
            result.reasons.Add(DepartureBlockReason.NoCargo);

        // 막힌 사유가 하나도 없으면 출발 가능
        result.canDepart = (result.reasons.Count == 0);

        return result;
    }
}