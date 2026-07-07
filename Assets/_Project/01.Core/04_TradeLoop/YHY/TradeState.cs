/// <summary>
/// 무역(상단)의 진행 상태. 팀 마일스톤 문서(M1)에서 확정된 흐름.
/// Prepare → Ready → Traveling → Arrived → SettlementPending → Completed → (다시 Prepare)
/// </summary>
public enum TradeState
{
    Prepare,            // 준비: 상단 구성 중 (상품·식량·마차·동물·용병 채우는 단계)
    Ready,              // 출발 준비 완료: 검증을 통과해 떠날 수 있는 상태
    Traveling,          // 이동 중: 무역로를 따라 진행 (이벤트 발생 구간)
    Arrived,            // 도착: 무역 도시에 도착
    SettlementPending,  // 정산 대기: 결과 계산을 기다리는 상태
    Completed           // 완료: 정산까지 끝남 (이후 다시 Prepare)
}