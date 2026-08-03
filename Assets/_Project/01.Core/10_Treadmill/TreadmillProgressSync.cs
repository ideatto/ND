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
    private string fedRoute = null;    // 지형을 채워둔 routeId(1회 샘플)
    private string shownTownId = null; // 정박 중 표시 중인 townId

    /// <summary>디버그 가짜 진행 on/off(0에서 다시 시작).</summary>
    public void SetFakeProgress(bool on) { debugFakeProgress = on; fakeT = 0f; }

    private void Reset() => FindRefs();
    private void Awake() => FindRefs();

    private void FindRefs()
    {
        // 레인 안이면 그 레인 형제 부품만 쓴다(다른 레인과 안 엉키게). 레인이 없으면
        // (단일 인스턴스 하위호환) 예전처럼 전역 검색.
        var lane = TreadmillLane.Of(this);
        if (lane != null)
        {
            lane.Resolve();
            if (stage == null) stage = lane.stage;
            if (road == null) road = lane.road;
            if (arrival == null) arrival = lane.arrival;
            if (rain == null) rain = lane.rain;
            return;
        }
        if (stage == null) stage = Object.FindFirstObjectByType<TreadmillStage>(FindObjectsInactive.Include);
        if (road == null) road = Object.FindFirstObjectByType<TreadmillRoad>(FindObjectsInactive.Include);
        if (arrival == null) arrival = Object.FindFirstObjectByType<TreadmillTownArrival>(FindObjectsInactive.Include);
        if (rain == null) rain = Object.FindFirstObjectByType<TreadmillRain>(FindObjectsInactive.Include);
    }

    private void LateUpdate()   // 스테이지 Update(스크롤/걷기) 뒤에 표시상태로 덮어씀
    {
        if (road == null || stage == null) { FindRefs(); if (road == null || stage == null) return; }

        // 표시 상태 결정: 미니맵과 동일 정책(CaravanMapDisplayResolver)
        bool onRoute = false; string routeId = ""; float p = 0f; string townId = ""; string cid = ""; string tradeState = "";
        bool resolved;
        if (debugFakeProgress)
        {
            fakeT += Time.deltaTime / Mathf.Max(0.1f, fakeSeconds);
            p = Mathf.Clamp01(fakeT); onRoute = true; resolved = true;
            if (road.RouteCellCount == 0 && !string.IsNullOrEmpty(fakeRoute)) road.SetRoute(fakeRoute);
        }
        else
        {
            // 레인이 담당 caravanId를 지정했으면 그걸 몬다(레인=마차 고정). 아니면 표시 중 캐러밴.
            var lane = TreadmillLane.Of(this);
            cid = (lane != null && !string.IsNullOrEmpty(lane.caravanId)) ? lane.caravanId : stage.CurrentCaravanId();
            resolved = !string.IsNullOrEmpty(cid)
                     && TreadmillRouteSampler.TryResolveDisplay(cid, out onRoute, out routeId, out p, out townId, out tradeState);
            if (!resolved) onRoute = false;
        }

        if (!resolved) { StopIdle(); return; }

        // 비는 '표시 중 캐러밴' 본인의 날씨에만 동기화한다.
        //  - 이동/정박 모두 여기서 한 번에 지정(정박 분기 DriveTown이 비를 안 켜던 문제 해결).
        //  - 정박 캐러밴은 보통 날씨 보고가 없어 0 → 비 없음 → '멈춰 있음'이 분명해진다.
        //  - 예전엔 정박 시 caravanId가 빈 채로 남아 TreadmillRain이 Max(다른 이동 캐러밴의 비)로
        //    폴백 → 정박 마차에 남의 비가 내려 '이동 중'처럼 보였다.
        if (rain != null && !debugFakeProgress) rain.SetCaravan(cid);

        if (onRoute) DriveRoute(cid, routeId, p);   // 이동 중: 경로 지형 + 진행도 + 목적지 접근
        else DriveTown(townId, tradeState);         // 정박: 대기실 표시 + 정지

        // 대기실 그리드 전환(마지막에 확정). 이동 중이 아니면(마을 진입·대기·파괴) 도로가
        // 지형 그리드를 전부 숨기고 11번째 '대기실 그리드'만 세운다(나무 안 뚫음, 별도 공간).
        if (road != null) road.ShowWaitingRoom(!onRoute);
    }

    // 이동 중: 경로 지형을 진행도로 스크롤하고, 끝에서 목적지 마을이 다가와 정지.
    private void DriveRoute(string cid, string routeId, float p)
    {
        shownTownId = null;
        // 경로 지형 채우기(정확한 routeId로 1회)
        if (!debugFakeProgress && fedRoute != routeId)
        {
            string r = TreadmillRouteSampler.SampleRouteTerrainByRouteId(routeId);
            if (!string.IsNullOrEmpty(r)) { road.SetRoute(r); fedRoute = routeId; }
        }
        int cells = road.RouteCellCount;
        if (cells <= 0) return;

        if (road.IsArrived && p < 0.5f) { road.SetArrived(false); if (arrival != null) arrival.Leave(); }
        road.SetExternalProgress(p);
        // (비 동기화는 LateUpdate에서 이동/정박 공통으로 처리 — 여기선 생략)

        // 목적지 마을 접근(경로 끝 = 목적지)
        GameObject prefab = debugFakeProgress ? fakeTownPrefab : null; string tid;
        bool hasTown = debugFakeProgress ? (prefab != null)
                                         : TreadmillRouteSampler.TryGetArrivalTownPrefab(cid, out prefab, out tid);
        float remainingCells = (1f - p) * cells;
        if (arrival != null && hasTown && prefab != null && remainingCells <= arriveWindowCells)
        {
            arrival.EnsureTown(prefab);
            arrival.PlaceByRemaining(remainingCells / Mathf.Max(0.01f, arriveWindowCells));
            road.SetArrived(p >= 0.999f);
        }
        else
        {
            if (arrival != null) arrival.Leave();
            road.SetArrived(false);
        }
    }

    // 정박(마을 진입·대기·실패/파괴): 완전 정지. 이제 '대기실(마구간)'이 멈춤 연출을 맡으므로
    // 도착 마을 건물은 세우지 않는다(대기실과 겹침 방지). 배경엔 그 마을 지형만 깔아둔다.
    private void DriveTown(string townId, string tradeState)
    {
        road.ClearExternalDrive();
        road.SetScrollEnabled(false);
        fedRoute = null;       // 다음 이동 때 경로 재샘플
        shownTownId = null;

        // 도로 지형은 LateUpdate에서 통째로 숨기므로 여기선 안 건드린다(숨긴 걸 다시 켜지 않게).
        if (arrival != null) arrival.Leave();   // 마을 건물 대신 대기실이 표시됨
        road.SetArrived(true);                  // 완전 정지(스크롤·걷기 off)
    }

    // 표시할 캐러밴이 없음 → 완전 정지·정리
    private void StopIdle()
    {
        road.ClearExternalDrive();
        road.SetScrollEnabled(false);
        road.SetArrived(false);
        fedRoute = null; shownTownId = null;
        if (arrival != null) arrival.Leave();
        if (road != null) road.ShowWaitingRoom(true);   // 표시할 게 없어도 대기실(빈 차고)로
    }
}
