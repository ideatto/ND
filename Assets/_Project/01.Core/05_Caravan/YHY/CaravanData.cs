// =============================================================================
// CaravanData — 상단(캐러밴) 데이터 (공용 데이터, 초안)
// =============================================================================
// [작성] 윤호영 (요구사항 초안)  /  [영역] 공용 데이터 — 정의 소유는 UI & Data 모듈.
//
// [핵심 포인트]
//  · 상단 하나의 전체 구성 = 마차 + 견인동물 + 용병 + 적재(무역품/식량) + 현재 무역 단계.
//  · 순수 데이터 홀더로 유지. 적재량 계산은 Core 소유 CaravanLoad로 분리했다.
//  · 시각(start/end)·정산수령·이번 무역 손실 누적은 이동/정산이 참조하는 "런타임 상태"다.
//    이 값들은 Framework의 저장·오프라인 정산에 그대로 넘어간다.
// =============================================================================

using System;
using System.Collections.Generic;

/// <summary>적재한 무역품 한 종류 (아이템 + 수량) — 초안</summary>
[Serializable]
public class CargoEntry
{
    public imsiTradeItemData item;  // 어떤 무역품인지
    public int quantity;        // 몇 개 실었는지
}

/// <summary>상단 데이터. 자세한 설명은 상단 주석 참고.</summary>
[Serializable]
public class CaravanData
{
    // ── 구성 (준비 단계에서 채움) ──────────────────────────────
    public imsiWagonData wagon;                                              // 마차 (1대)
    public List<imsiAnimalData> animals = new List<imsiAnimalData>();            // 견인 동물 목록
    public List<imsiMercenaryData> mercenaries = new List<imsiMercenaryData>();  // 용병 목록
    public List<CargoEntry> cargo = new List<CargoEntry>();             // 적재한 무역품 목록
    public int foodAmount;                                              // 실은 식량 수량
    public float foodUnitWeight = 1f;                                   // 식량 1개당 무게

    // ── 런타임/저장 상태 ─────────────────────────────────────
    // [시간은 출처를 안 정한다] 진행도(progress01)만 들고 있는다.
    //  이 값을 무엇으로 채우는지(델타타임 / 타임스탬프 / 게임시간 / 오프라인 정산)는
    //  바깥(지금은 테스트, 나중엔 Framework)이 정한다. 여기선 "얼마나 왔나"만 안다.
    public JourneyState state = JourneyState.Prepare;   // 현재 진행 단계
    public float currentDistanceKm;                 // 현재 뛰는 무역 거리(Km) — 출발 시 복사됨
    public float totalSeconds;                      // 이번 무역 총 소요 시간(초) — 출발 시 계산해 저장
    public float progress01;                        // 진행도 0~1 (바깥이 갱신). 1.0이면 도착
    public bool settlementClaimed;                   // 정산 수령 여부 (true면 중복 보상 방지)

    // ── 이번 무역 손실 누적 (출발 시 초기화, 이동 중 이벤트가 채움) ──
    public int runCargoLost;                                       // 이번 무역 무역품 손실 누적
    public float runFoodLost;                                      // 이번 무역 식량 이벤트 차감 누적(도난 등)
    public JourneyFailureReason runFatalReason = JourneyFailureReason.None; // 치명 상태(실패 확정) 사유

    // TODO(M2): remainingCombatPower(상행 단위 전투력 잔량) — 전투력 소진 판정/저장용.
    //           용병별 combatPower 합산 규칙 확정 후 추가.
}