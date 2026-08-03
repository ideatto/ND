// =============================================================================
// WeatherRainField — seed로 '이 위치·이 시간의 비'를 바로 계산하는 결정론 함수(공식)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 날씨 → 이동시간(속도) 연동
//
// [왜] 화면 구름은 '움직이는 시뮬'이라 미래 시각을 알려면 굴려야 한다. 반면 속도감속용은
//      연출이 필요 없으니, seed+위치+시간을 넣으면 바로 나오는 '순수 공식'으로 둔다.
//      → 무역 출발 시 route를 시간축 따라 훑어 '언제 어디서 비/진흙인지' 즉시 계산 가능.
//      결정론이라 오프라인/재현 자동. 화면 구름과는 별개(연출용 아님, 시간 계산 전용).
//
// [무엇] Intensity(pos, time)=0~1 비 세기(공간 노이즈가 시간에 따라 흐름). Wetness=비+진흙
//        (최근 비의 감쇠 잔여 = 질퍽거림). 상수는 밸런스용(비 빈도·이동속도·진흙 지속).
// =============================================================================

using UnityEngine;

/// <summary>seed+위치+시간 → 비/진흙을 즉시 계산하는 결정론 공식(속도감속 전용, 화면 구름과 별개).</summary>
public static class WeatherRainField
{
    // ── 밸런스 상수(추후 조절) ──
    private const float Seed = 12345f;         // 월드 씨앗(오프셋으로 사용)
    private const float SpaceScale = 0.02f;    // 공간 변화 촘촘함(작을수록 넓은 비구름대)
    private const float DriftX = 0.03f;        // 비구름이 시간에 따라 흐르는 속도(x)
    private const float DriftY = 0.017f;       // (y)
    private const float RainThreshold = 0.62f; // 노이즈가 이 값 이상인 곳만 비(대부분 맑음)

    /// <summary>그 위치·그 시각의 비 세기(0=맑음 ~ 1=폭우). 결정론(같은 seed·위치·시간이면 같은 값).</summary>
    public static float Intensity(Vector2 worldPos, double timeSec)
    {
        // 공간 노이즈를 시간에 따라 흘려(=비구름 이동). Perlin은 결정론.
        float sx = (worldPos.x + Seed) * SpaceScale + (float)(timeSec * DriftX);
        float sy = (worldPos.y + Seed * 1.7f) * SpaceScale + (float)(timeSec * DriftY);
        float n = Mathf.PerlinNoise(sx, sy);                 // 0~1
        return Mathf.InverseLerp(RainThreshold, 1f, n);      // 임계 미만=0(맑음), 이상=비 세기
    }

    /// <summary>그 위치·그 시각의 '젖음(비+진흙)'. 지금 비가 오거나 최근 비 왔으면(감쇠 잔여) 젖음.
    /// mudSeconds = 비 그친 뒤 질퍽함이 남는 시간(밸런스).</summary>
    public static float Wetness(Vector2 worldPos, double timeSec, float mudSeconds)
    {
        float wet = Intensity(worldPos, timeSec);            // 지금 내리는 비
        if (mudSeconds > 0.01f)
        {
            const int steps = 4;                             // 최근 몇 지점을 되돌아봄(진흙)
            for (int k = 1; k <= steps; k++)
            {
                double back = timeSec - (double)mudSeconds * k / steps;   // 과거로
                float decay = 1f - (float)k / (steps + 1);                // 오래될수록 약하게
                float past = Intensity(worldPos, back) * decay;
                if (past > wet) wet = past;                  // 최근 최댓값 = 남은 질퍽함
            }
        }
        return Mathf.Clamp01(wet);
    }
}
