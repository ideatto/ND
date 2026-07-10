// =============================================================================
// JourneyResultData — 무역 결과 데이터 (공용 데이터, 초안)
// =============================================================================
// [작성] 윤호영 (요구사항 초안)  /  [영역] 공용 데이터 — 정의 소유는 UI & Data 모듈.
//
// [핵심 포인트]
//  · 무역 한 사이클이 끝나면 나오는 "하나의 결과". 정산 화면·다음 무역 선택으로 넘긴다.
//  · 성공/실패가 갈래로 나뉘지 않는다. 결과는 항상 하나고, 그 안의 grade가 등급을 말한다.
//      - Success        : 손실 없이 완주
//      - PartialSuccess : 완주는 했으나 도중 손실(전투 실패로 무역품 감소 등)  ← "중간 성공"
//      - Failed         : 완주 못 하고 거점 복귀(식량 고갈·동물 상실 등)
//  · Core가 채우는 값 : grade, failureReason, cargoLost, durabilityLost,
//                     그리고 계산값(travelSeconds·foodConsumed·departureLoad·finalEfficientLoad·overloadRatio). [M2]
//  · Progression이 채우는 값 : revenue, cost, netProfit (Core는 뼈대만 만들어 넘김).
// =============================================================================

using System;

/// <summary>무역 결과 등급.</summary>
public enum JourneyResultGrade
{
    Success,         // 완전 성공 (손실 없음)
    PartialSuccess,  // 부분 성공 (완주 + 도중 손실)
    Failed           // 실패 (완주 못 함 → 거점 복귀)
}

/// <summary>무역 실패 사유 (Failed일 때만 의미). 출발 전 사유(DepartureBlockReason)와 별개.</summary>
public enum JourneyFailureReason
{
    None,             // 실패 아님
    FoodDepleted,     // 식량 부족
    AnimalsLost,      // 견인 동물 상실
    NotEnoughAnimals  // 마차 최소 견인 동물 수 미달
}

/// <summary>무역 한 사이클의 결과.</summary>
[Serializable]
public class JourneyResultData
{
    // ── Core가 채움 ──────────────────────────────
    public JourneyResultGrade grade;                              // 결과 등급
    public JourneyFailureReason failureReason = JourneyFailureReason.None; // 실패 시 사유
    public int cargoLost;                                       // 잃은 무역품 수량 (부분성공/실패)
    public float durabilityLost;                                // 마차 내구도 손실           [M2]

    // ── Core가 채우는 계산값 (M2 완료기준: 정산 데이터에 포함) ──
    public float travelSeconds;        // 실제 이동한 시간(초)
    public float foodConsumed;         // 총 식량 소모
    public float departureLoad;        // 출발 시 적재량(짐무게)
    public float finalEfficientLoad;   // 최종 적정 적재량
    public float overloadRatio;        // 과적 비율 (적정 이하면 0)

    // ── Progression이 채움 (M1~M2) ───────────────
    public long revenue;    // 판매 수익 (돈=long, 팀 결정)
    public long cost;       // 유지비 등 비용 (돈=long, 팀 결정)
    public long netProfit;  // 순이익 (돈=long, 팀 결정)
}