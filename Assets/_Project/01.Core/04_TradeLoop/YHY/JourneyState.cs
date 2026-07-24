// =============================================================================
// JourneyState — 무역(상단) 진행 "단계" enum
// =============================================================================
// [작성] 윤호영
//
// [핵심 포인트]
//  · 이 enum은 "지금 무역이 어느 단계냐"만 담는다. (성공/실패 판정 아님)
//  · 성공/부분성공/실패는 JourneyResultData의 등급(JourneyResultGrade)으로 처리한다.
//    → 실패해도 단계는 Settling을 거쳐 Completed로 간다. "무슨 결과였나"는 결과 데이터가 말한다.
//  · 논리 흐름: Prepare → Traveling → Settling → (Selling) → Completed → (다시 Prepare)
//    - Selling(정산 중)은 UI가 판매/정산 화면을 연 동안의 단계. 선택적이다 —
//      Framework의 한방 정산은 Settling에서 바로 Completed로 갈 수 있다(JourneyRunner 참고).
//  · 출발 검증은 "출발 시도" 한 동작에서 처리하므로 Ready 단계를 따로 두지 않는다.
//
// [저장 호환 — 중요]
//  · 이 값은 JsonUtility로 "정수"로 직렬화된다(Prepare=0 … Completed=3, Selling=4).
//  · 새 값은 반드시 "맨 끝"에 추가한다. 중간에 끼우면 뒤 값들의 정수가 밀려
//    옛 저장의 Completed(3)가 다른 단계로 오독된다. Selling을 논리상 Completed 앞이지만
//    선언은 끝에 둔 이유가 이것 — 선언 순서 ≠ 실행 순서다.
//  · 값을 지우거나 순서를 바꿔야 하면 SaveData.CurrentVersion 상향 + 마이그레이션 필요(천성욱님 협의).
// =============================================================================

/// <summary>무역(상단)의 진행 단계. (선언 순서 = 저장 정수값 — 새 값은 끝에만 추가)</summary>
public enum JourneyState
{
    Prepare,     // 0  준비: 상단 구성 중
    Traveling,   // 1  이동 중: 시간 경과 + 이벤트 발생 구간
    Settling,    // 2  정산 대기: 도착(또는 실패 확정) 후 결과 계산·수령 대기
    Completed,   // 3  완료: 정산 수령까지 끝남 (이후 다시 Prepare)
    Selling      // 4  정산 중: 판매/정산 화면을 연 동안 (논리상 Settling과 Completed 사이)
}