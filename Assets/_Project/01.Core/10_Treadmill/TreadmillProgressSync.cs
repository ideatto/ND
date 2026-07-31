// =============================================================================
// TreadmillProgressSync — 트레드밀을 '실제 여행 진행도'로 구동(완벽 동기화)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템 2단계
//
// [역할] 트레드밀을 자기 시계(상수 스크롤+루프)가 아니라 캐러밴의 '실제 진행도(0~1)'로 몬다.
//        매 프레임:
//          - 진행도 p → 길 누적거리(traveled = p×총거리) 지정 → 트레드밀 그리드 = 캐러밴 실제 셀
//          - 도착 직전(남은거리 ≤ arriveWindow)엔 목적지 마을 건물이 그리드 위로 다가옴
//          - p=1(무역 완료) 순간 마을이 정확히 도착 지점에 서며 길·동물 정지
//          - 비: 표시 중 캐러밴 날씨에 동기화(WeatherState.Get(caravanId))
//
// [연결] TreadmillStage(표시 중 캐러밴), TreadmillRoad(구동), TreadmillTownArrival(마을),
//        TreadmillRain(비). 진행도·마을프리팹은 TreadmillRouteSampler가 제공.
//
// [디버그] debugFakeProgress: 실제 여행 없이도 Preview에서 0→1 가짜 진행으로 전 과정 확인.
// =============================================================================

using UnityEngine;

/// <summary>트레드밀을 실제 여행 진행도로 구동해 그리드·마을·도착·비를 동기화한다.</summary>
public class TreadmillProgressSync : MonoBehaviour
{
    [SerializeField] private TreadmillStage stage;         // 비면 자동 검색
    [SerializeField] private TreadmillRoad road;
    [SerializeField] private TreadmillTownArrival arrival;
    [SerializeField] private TreadmillRain rain;
    [Tooltip("목적지 마을이 다가오기 시작하는 '남은 셀 수'. 이 안이면 마을 등장(예: 1.5 = 마지막 1.5칸).")]
    [SerializeField] private float arriveWindowCells = 1.5f;

    [Header("디버그(실제 여행 없이 Preview 확인)")]
    [Tooltip("켜면 실제 진행도 대신 0→1 가짜 진행으로 전 과정 재생.")]
    [SerializeField] private bool debugFakeProgress = false;
    [Tooltip("가짜 진행 소요 시간(초).")]
    [SerializeField] private float fakeSeconds = 12f;
    [Tooltip("가짜 진행에 쓸 route(셀 지형 문자열).")]
    [SerializeField] private string fakeRoute = "GGFFLLGGFF";
    [Tooltip("가짜 진행 도착 마을 건물.")]
    [SerializeField] private GameObject fakeTownPrefab;

    private float fakeT;

    /// <summary>디버그 가짜 진행 on/off(0에서 다시 시작).</summary>
    public void SetFakeProgress(bool on) { debugFakeProgress = on; fakeT = 0f; }

    private void Reset() => FindRefs();
    private void Awake() => FindRefs();

    private void FindRefs()
    {
        if (stage == null) stage = Object.FindFirstObjectByType<TreadmillStage>(FindObjectsInactive.Include);
        if (road == null) road = Object.FindFirstObjectByType<TreadmillRoad>(FindObjectsInactive.Include);
        if (arrival == null) arrival = Object.FindFirstObjectByType<TreadmillTownArrival>(FindObjectsInactive.Include);
        if (rain == null) rain = Object.FindFirstObjectByType<TreadmillRain>(FindObjectsInactive.Include);
    }

    private void LateUpdate()   // 스테이지 Update(스크롤/걷기) 뒤에 진행도로 덮어씀
    {
        if (road == null || stage == null) { FindRefs(); if (road == null || stage == null) return; }

        // 1) 진행도 p 얻기(실제 또는 가짜)
        float p = 0f; string cid = "";
        bool have;
        if (debugFakeProgress)
        {
            fakeT += Time.deltaTime / Mathf.Max(0.1f, fakeSeconds);
            p = Mathf.Clamp01(fakeT);
            have = true;
            if (road.RouteCellCount == 0 && !string.IsNullOrEmpty(fakeRoute)) road.SetRoute(fakeRoute);
        }
        else
        {
            cid = stage.CurrentCaravanId();
            have = !string.IsNullOrEmpty(cid) && TreadmillRouteSampler.TryGetProgress(cid, out p);
            if (!have) p = 0f;
        }

        // 이동 중 아님 → 외부 구동 해제(자기시계/디버그 스크롤로 복귀), 마을 정리
        if (!have)
        {
            road.ClearExternalDrive();
            if (arrival != null) arrival.Leave();
            road.SetArrived(false);
            return;
        }

        // 2) route가 아직 안 채워졌으면 대기(스테이지가 곧 SetRoute)
        int cells = road.RouteCellCount;
        if (cells <= 0) return;

        // 3) 진행도 → 그리드(셀=floor(p×셀수)). 지면 시각 스크롤은 길이 알아서.
        road.SetExternalProgress(p);

        // 4) 비: 표시 중 캐러밴 날씨에 동기화(실제 모드만)
        if (rain != null && !debugFakeProgress) rain.SetCaravan(cid);

        // 5) 마을 도착: 남은 셀 ≤ window면 마을이 그리드 위로 다가옴, p=1이면 도착 정지
        GameObject prefab = debugFakeProgress ? fakeTownPrefab : null; string tid;
        bool hasTown = debugFakeProgress
            ? (prefab != null)
            : TreadmillRouteSampler.TryGetArrivalTownPrefab(cid, out prefab, out tid);

        float remainingCells = (1f - p) * cells;
        if (arrival != null && hasTown && prefab != null && remainingCells <= arriveWindowCells)
        {
            arrival.EnsureTown(prefab);
            arrival.PlaceByRemaining(remainingCells / Mathf.Max(0.01f, arriveWindowCells));   // 1=등장, 0=도착
            road.SetArrived(p >= 0.999f);          // 완료 순간 정지 = 완벽 도착
        }
        else
        {
            if (arrival != null) arrival.Leave();
            road.SetArrived(false);
        }
    }
}
