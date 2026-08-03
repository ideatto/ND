// =============================================================================
// WeatherTravelPenalty — 비로 무역 이동시간이 늘어나는 '배율'을 출발 시 계산(진짜 시뮬 투영)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 날씨 → 이동시간 연동
//
// [무엇] RouteFactor(routeId, 출발시각, 기본이동초): MinimapClouds.ProjectForward로 '실제 날씨
//        시뮬을 여행 시간창만큼 앞으로 투영'하고, 각 스텝에서 '그 순간 마차가 있는 지점의 비'를
//        샘플해 감속을 적분한 배율(≥1)을 낸다. → 화면 구름·번개·불이 만든 비까지 전부 반영.
//        flat 아님(마른 구간 1.0, 비 구간만 커짐).
//
// [핵심] 날씨가 결정론이라 '출발 때 투영한 비 = 실제로 겪을 비'가 100% 동일. 이동시간은 출발 시
//        1회 계산·저장되므로, 이 배율을 곱해두면 오프라인 자동(되감기 불필요). 투영은 라이브
//        화면/상태를 안 건드린다(스크래치 + 복원).
//
// [경계] 실제 시간 적용(expectedSeconds *= factor)은 이동시간 계산 쪽(TradeStartService)에서.
// =============================================================================

using UnityEngine;
using ND.UI.WorldMap;

/// <summary>실제 날씨 시뮬을 여행 구간에 투영해 '이동시간 배율(≥1)'을 돌려주는 정적 헬퍼(출발 시 1회).</summary>
public static class WeatherTravelPenalty
{
    /// <summary>이 무역의 이동시간 배율(≥1). 마른 route=1.0, 비 구간이 많을수록 큼. route/날씨 없으면 1.0.
    /// ★출발 시 1회 호출 → expectedSeconds에 곱해 저장(오프라인 자동). 진짜 시뮬(불→비 포함)을 씀.</summary>
    /// <param name="startTimeSec">출발 게임시각(초, UTC ticks/TicksPerSecond).</param>
    /// <param name="baseTravelSeconds">날씨 반영 전 기본 이동시간(초). 투영 길이 = 이만큼.</param>
    /// <param name="slowdownRate">비 세기 1.0당 감속 배율 증가(예: 0.3 → 완전 비 구간은 시간 ×1.3).</param>
    /// <param name="maxFactor">배율 상한(폭우 route가 과도하게 길어지지 않게).</param>
    public static float RouteFactor(
        string routeId, double startTimeSec, float baseTravelSeconds,
        float slowdownRate = 0.3f, float maxFactor = 2f)
    {
        if (string.IsNullOrEmpty(routeId) || baseTravelSeconds <= 0f) return 1f;

        var clouds = Object.FindAnyObjectByType<MinimapClouds>(FindObjectsInactive.Include);
        if (clouds == null) return 1f;

        RouteVisual route = null;
        foreach (var rv in Object.FindObjectsByType<RouteVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (rv != null && rv.RouteId == routeId) { route = rv; break; }
        if (route == null) return 1f;

        double sum = 0.0; int count = 0;
        // 실제 시뮬을 여행 시간창만큼 앞으로 투영. 각 스텝: 그 순간 마차 위치의 '투영된 비'로 감속 적분.
        clouds.ProjectForward(startTimeSec, baseTravelSeconds, (simStep, wall) =>
        {
            double elapsed = wall - startTimeSec;
            float p = Mathf.Clamp01((float)(elapsed / baseTravelSeconds));   // 그 시각 마차 진행도
            Vector3 pos = route.EvaluatePosition(p);
            float rain = clouds.RainIntensityAt(pos);                        // 그 미래 시각의 투영된 비
            sum += 1.0 + Mathf.Max(0f, rain) * Mathf.Max(0f, slowdownRate);  // 비 구간만 느려짐
            count++;
        });

        if (count == 0) return 1f;
        return Mathf.Clamp((float)(sum / count), 1f, Mathf.Max(1f, maxFactor));   // 최소 1(빨라지진 않음)
    }
}
