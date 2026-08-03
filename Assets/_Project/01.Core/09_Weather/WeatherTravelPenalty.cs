// =============================================================================
// WeatherTravelPenalty — 비/진흙으로 무역 이동시간이 늘어나는 '배율'을 출발 시 계산
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 날씨 → 이동시간 연동
//
// [왜] 비를 맞거나 비 온 뒤 질퍽한 땅을 지날 때 마차가 느려진다 = 이동시간 증가.
//      이동시간은 무역 출발 시 1회 계산돼 저장(expectedTradeEndUtcTick)되고 그 뒤 안 바뀌므로,
//      출발 시 route에 낀 비를 '구간별'로 반영한 배율을 곱해두면 → 저장됨 → 오프라인 자동.
//
// [무엇] RouteFactor(routeId) = route를 여러 점 샘플해 각 점의 비 세기로 (1 + 세기×감속률)를
//        평균낸 배율(≥1). flat 아님 — 마른 구간은 1.0, 비 구간만 커짐. 비 많은 여정=큰 배율.
//        정헌님(TradeStartService)은 expectedSeconds *= RouteFactor(routeId) 한 줄이면 됨.
//
// [경계·한계] ★이 v1은 '출발 시점의 날씨'를 구간별로 본다(결정론·오프라인자동). 여행 도중
//        날씨가 이동하는 것과 '비 온 뒤 진흙 지속(decay)'까지 정확히 반영하려면, 날씨를 여행
//        시간창만큼 앞으로 투영(forward projection)하는 별도 작업이 필요하다(추후 v2).
//        결과는 여기서 만들지 않는다 — 배율만 돌려주고 실제 시간 적용은 이동시간 계산 쪽(협의).
// =============================================================================

using UnityEngine;
using ND.UI.WorldMap;

/// <summary>route에 낀 비를 구간별로 반영해 '이동시간 배율(≥1)'을 돌려주는 정적 헬퍼(출발 시 1회 호출).</summary>
public static class WeatherTravelPenalty
{
    /// <summary>routeId 경로에 대한 이동시간 배율(≥1). 마른 route=1.0, 비 구간이 많을수록 큼.
    /// clouds/route 없으면 1.0(패널티 없음). ★출발 시 1회 호출 → expectedSeconds에 곱해 저장(오프라인 자동).</summary>
    /// <param name="slowdownRate">비 세기 1.0당 감속 배율 증가(예: 0.3 → 세기1.0 구간은 ×1.3 시간).</param>
    /// <param name="maxFactor">배율 상한(폭우 route가 과도하게 길어지지 않게).</param>
    /// <param name="samples">route 샘플 점 수(구간 해상도).</param>
    public static float RouteFactor(string routeId, float slowdownRate = 0.3f, float maxFactor = 2f, int samples = 64)
    {
        if (string.IsNullOrEmpty(routeId)) return 1f;

        // 날씨(구름) 조회 — 우리 미니맵 날씨 시스템. 꺼져 있으면 비 없음 → 배율 1.
        var clouds = Object.FindAnyObjectByType<MinimapClouds>(FindObjectsInactive.Include);
        if (clouds == null) return 1f;

        // routeId에 맞는 RouteVisual을 찾는다. 같은 routeId가 여러 개(월드맵+미니맵 격자)일 수 있어,
        // '비 세기가 실제로 잡히는' 경로를 쓴다(격자에 얹힌 것). 하나만 있으면 그걸 사용.
        RouteVisual chosen = null;
        float best = -1f;
        foreach (var rv in Object.FindObjectsByType<RouteVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rv == null || rv.RouteId != routeId) continue;
            // 이 경로의 중간 지점에서 세기가 잡히면(=격자 위) 후보. 없으면 첫 후보라도.
            float mid = clouds.RainIntensityAt(rv.EvaluatePosition(0.5f));
            if (chosen == null || mid > best) { chosen = rv; best = mid; }
        }
        if (chosen == null) return 1f;

        // 구간별 배율의 평균: 각 샘플점에서 (1 + 세기×감속률). 마른 곳=1, 비 오는 곳만 커짐.
        int n = Mathf.Max(2, samples);
        double sum = 0.0;
        for (int i = 0; i < n; i++)
        {
            float p = (float)i / (n - 1);
            float intensity = clouds.RainIntensityAt(chosen.EvaluatePosition(p));   // 0=비 없음
            sum += 1.0 + Mathf.Max(0f, intensity) * Mathf.Max(0f, slowdownRate);
        }
        float factor = (float)(sum / n);
        return Mathf.Clamp(factor, 1f, Mathf.Max(1f, maxFactor));   // 최소 1(빨라지진 않음)
    }
}
