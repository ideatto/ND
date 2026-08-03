// =============================================================================
// WeatherTravelPenalty — 비/진흙으로 무역 이동시간이 늘어나는 '배율'을 출발 시 계산
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 날씨 → 이동시간 연동
//
// [무엇] RouteFactor(routeId, 출발시각, 기본이동초): route를 여러 지점으로 나눠, 각 지점을
//        '마차가 지날 그 시각'의 비/진흙(WeatherRainField)으로 구간 속도를 낮춰 배율을 낸다.
//        → seed+route+시각으로 "어디서 어디까지 얼마나 느려지는지"를 출발 때 즉시 계산.
//        flat 아님(마른 구간=1.0, 비/진흙 구간만 커짐). 결과 배율(≥1)을 이동시간에 곱한다.
//
// [왜 출발 때 계산으로 충분한가] 날씨가 결정론이라 '출발 때 미리 계산한 비 = 실제로 겪을 비'가
//        100% 같다. 이동시간은 출발 시 1회 계산·저장(expectedTradeEndUtcTick)되고 안 바뀌므로,
//        이 배율을 곱해두면 → 저장됨 → 오프라인 자동(되감기·시뮬 굴림 불필요).
//
// [경계] 실제 시간 적용(expectedSeconds *= factor)은 이동시간 계산 쪽(TradeStartService)에서.
//        화면 구름과는 별개의 순수 계산(연출 아님, 시간 전용).
// =============================================================================

using UnityEngine;
using ND.UI.WorldMap;

/// <summary>route를 시간축 따라 훑어 비/진흙 감속을 반영한 '이동시간 배율(≥1)'을 돌려주는 정적 헬퍼.</summary>
public static class WeatherTravelPenalty
{
    /// <summary>이 무역의 이동시간 배율(≥1). 마른 route=1.0, 비/진흙 구간이 많을수록 큼.
    /// route 없으면 1.0. ★출발 시 1회 호출 → expectedSeconds에 곱해 저장(오프라인 자동).</summary>
    /// <param name="routeId">무역로 id.</param>
    /// <param name="startTimeSec">출발 게임시각(초, UTC ticks/TicksPerSecond). 각 구간 통과 시각 계산용.</param>
    /// <param name="baseTravelSeconds">날씨 반영 전 기본 이동시간(초). 구간별 통과 시각 = 출발+진행도×이것.</param>
    /// <param name="slowdownRate">젖음 1.0당 감속 배율 증가(예: 0.3 → 완전 젖은 구간은 시간 ×1.3).</param>
    /// <param name="mudSeconds">비 그친 뒤 질퍽함이 남는 시간(초). 0이면 진흙 없이 '지금 비'만.</param>
    /// <param name="maxFactor">배율 상한(폭우 route가 과도하게 길어지지 않게).</param>
    /// <param name="samples">route 샘플 점 수(구간 해상도).</param>
    public static float RouteFactor(
        string routeId, double startTimeSec, float baseTravelSeconds,
        float slowdownRate = 0.3f, float mudSeconds = 120f, float maxFactor = 2f, int samples = 64)
    {
        if (string.IsNullOrEmpty(routeId)) return 1f;

        // route(경로) 찾기. 같은 routeId가 여러 개(월드맵+미니맵)일 수 있으나, 위치 샘플만 필요하니
        // 첫 번째 유효 RouteVisual을 쓴다(월드좌표는 어느 것이든 그 route 경로).
        RouteVisual route = null;
        foreach (var rv in Object.FindObjectsByType<RouteVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (rv != null && rv.RouteId == routeId) { route = rv; break; }
        if (route == null) return 1f;

        int n = Mathf.Max(2, samples);
        double sum = 0.0;
        for (int i = 0; i < n; i++)
        {
            float p = (float)i / (n - 1);                        // 진행도 0~1
            // ★그 구간을 '마차가 지날 시각' = 출발 + 진행도 × 기본이동시간. → 그 시각의 비/진흙을 봄.
            double passTime = startTimeSec + (double)p * Mathf.Max(0f, baseTravelSeconds);
            Vector3 pos = route.EvaluatePosition(p);
            float wet = WeatherRainField.Wetness(new Vector2(pos.x, pos.y), passTime, mudSeconds);
            sum += 1.0 + Mathf.Max(0f, wet) * Mathf.Max(0f, slowdownRate);   // 젖은 구간만 느려짐
        }
        float factor = (float)(sum / n);
        return Mathf.Clamp(factor, 1f, Mathf.Max(1f, maxFactor));   // 최소 1(빨라지진 않음)
    }
}
