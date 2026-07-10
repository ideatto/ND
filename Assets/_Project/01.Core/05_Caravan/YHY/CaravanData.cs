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
    public float elapsedInGameSeconds;              // [인게임시간] 이번 무역 누적 인게임 경과(초). 식량 소모 기준. 바깥(Framework/테스트)이 채움
    public bool settlementClaimed;                   // 정산 수령 여부 (true면 중복 보상 방지)

    // ── 이번 무역 손실 누적 (출발 시 초기화, 이동 중 이벤트가 채움) ──
    public int runCargoLost;                                       // 이번 무역 무역품 손실 누적
    public float runFoodLost;                                      // 이번 무역 식량 이벤트 차감 누적(도난 등)
    public JourneyFailureReason runFatalReason = JourneyFailureReason.None; // 치명 상태(실패 확정) 사유

    // ── 마차 내구도 & 전투 (M2) ───────────────────────────────
    public int currentDurability;     // 현재 마차 내구도 (무역 거듭하며 감소, 무역 간 유지)
    public int runDurabilityLost;     // 이번 무역 약탈 내구도 손실 누적 (손실상한 캡 기준)
    public int runBattlesFought;      // 이번 무역 전투 횟수 (용병 방어 판정용)
    public int runStartDurability;    // 이번 무역 출발 시 내구도 (정산 손실 = 출발 - 도착) [M2 거리마모]
    public float runWearRemainder;    // 거리 마모 소수점 이월(1 미만 마모 누적) [M2 거리마모]

    // ── 식량 고갈 제한시간 (M2) ────────────────────────────────
    public bool runFoodDepleted;          // 이번 무역 식량 바닥 여부 (바닥나면 제한시간 시작)
    public float runFoodDepletedProgress; // 식량이 바닥난 시점의 진행도(0~1)
    public float starveGraceSeconds;      // 식량 바닥 후 도착 제한 시간(초). 초과+미도착 시 실패 [임시값]

    // ── 손실 상한 (M2, 정헌 LossLimitRate) ─────────────────────
    public float lossLimitRate = 1f;      // 손실 상한율(0~1). 1=무제한. 정헌 CoreRuntimeStatModifier.LossLimitRate로 설정
    public bool limitRaidDurability = true;   // 약탈 내구도 손실에 손실상한 적용? false=전량 적용 [M2]
    public int runOriginalCargoCount;     // 출발 시 원래 무역품 개수 (손실 상한 계산 기준)
    public float runDepartureLoad;        // 출발 시 짐무게 (정산 데이터용) [M2]

    // TODO(M2): remainingCombatPower(상행 단위 전투력 잔량) — 전투력 소진 판정/저장용.
    //           용병별 combatPower 합산 규칙 확정 후 추가.
}