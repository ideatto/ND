// =============================================================================
// CaravanConfig — 상단 계산 튜닝 값 모음 (Core 소유)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 이동·식량 계산에 쓰는 밸런싱 상수를 한 곳에 모은다.
//        밸런싱은 "이 파일만" 만지면 된다. 계산 로직은 CaravanCalculator 에 있다.
//        (나중에 RouteData / 밸런스 SO 로 옮길 수 있음 — 지금은 상수 초안)
//
// [주의] 지금 const 라서, 값이 상수로 고정되면 컴파일러가 일부 방어 코드를
//        "도달 불가"로 경고할 수 있다(CS0162). 데이터로 옮길 땐 static readonly 등으로.
// =============================================================================

/// <summary>상단 이동·식량 계산 튜닝 값.</summary>
public static class CaravanConfig
{
    // ── 기준 속도 ─────────────────────────────────────────────
    // 견인 동물 1마리로 BaseDistanceKm 를 BaseSeconds 에 이동.  (예: 100Km→10초 = 10Km/초)
    public const float BaseDistanceKm = 100f;   // 기준 거리 (Km)             ← 수정 가능
    public const float BaseSeconds = 10f;    // 위 거리를 1마리로 갈 때(초)  ← 수정 가능

    // ── 동물 수 효율 ──────────────────────────────────────────
    // efficiency = 1.0 + PerExtraAnimal × (마리수 - 1)   (1→1.0 / 2→1.5 / 3→2.0)
    public const float PerExtraAnimal = 0.5f;   // 한 마리 늘 때마다 +0.5배     ← 수정 가능
    public const float MaxEfficiency = 0f;     // 효율 상한. 0 = 무제한        ← 필요 시 켜기
                                               // (마차 maxAnimals 가 자연 상한이라 보통 0으로 둠)

    // ── 적재 무게 → 속도 감소 ─────────────────────────────────
    // overLoad(기준선) 이하는 100%. 넘으면 초과분에 비례해 감속.
    //   적재효율 = 1 - LoadPenalty × (초과무게 / overLoad)   (LoadFactorMin 밑으론 안 감)
    public const float LoadPenalty = 0.5f;   // 기준선 100% 초과당 -0.5배    ← 수정 가능
    public const float LoadFactorMin = 0.2f;   // 감속 하한(멈추지 않게)       ← 수정 가능
}