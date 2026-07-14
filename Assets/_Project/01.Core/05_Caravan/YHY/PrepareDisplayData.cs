// =============================================================================
// PrepareDisplayData — 무역 준비 화면 표시용 계산값 묶음 (Core 제공)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] UI(이종현님)가 준비 화면에 표시할 Core 계산값을 "한 번에" 받도록 묶은 DTO.
//        UI는 CaravanCalculator.BuildPrepareDisplay(...) 한 번 호출 → 이 객체를 바인딩만 하면 됨.
//        정산 쪽 SettlementViewData(천성욱님)와 같은 패턴.
//
// [매핑] 이종현님 TradePrepareViewData ← 이 DTO
//        currentLoad → currentLoad · overloadLimit → overloadLimit · maxLoad → maxLoad
//        requiredFoodQuantity → requiredFood(반올림) 등
//
// [주의] 순수 계산 결과 홀더. 씬·프리팹·저장과 무관. 값의 의미는 각 필드 주석 참고.
// =============================================================================

/// <summary>준비 화면 표시용 Core 계산값 묶음. UI가 받아 바인딩. [1차 빌드]</summary>
public class PrepareDisplayData
{
    // ── 적재 (무게) ──────────────────────────────
    public float currentLoad;        // 현재 짐무게 (무역품 + 식량)
    public float cargoWeight;        // 무역품 무게
    public float foodWeight;         // 식량 무게
    public float overloadLimit;      // 적정 한계 — 이 무게 이하면 정상 속도 (GetFinalEfficientLoad)
    public float maxLoad;            // 최대 한계 — 초과 시 출발 불가 (GetMaxLoad)
    public bool  isOverloaded;       // 과적 상태인가 (현재 > 적정)
    public float overloadRatio;      // 과적 비율 (0 = 적정 이하)
    public float loadSpeedModifier;  // 과적 감속 배수 (1 = 감속 없음)

    // ── 슬롯 (칸) ────────────────────────────────
    public int usedSlots;            // 사용 중인 칸
    public int maxSlots;             // 마차 최대 칸

    // ── 개수 · 식량 · 시간 ───────────────────────
    public int   cargoCount;             // 무역품 총 개수
    public float requiredFood;           // 예상 필요 식량 (인게임 시간 기준)
    public float estimatedTravelSeconds; // 예상 이동 시간 (현실 초)
}
