// =============================================================================
// TreadmillRoad — 트레드밀 길 컨트롤러(지형별 독립 길을 무대에 올리고 교체)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템 2단계
//
// [구조] 길을 한 줄로 이어붙이지 않는다. 지형 종류마다 '독립된 길'(TerrainRoadStrip)을
//        따로 만들어 서로 멀찌감치(X로 벌려) 대기시킨다. 현재 그리드 셀의 지형 길만 무대
//        (원점, 마차 아래)에 올려 제자리 루프시킨다. 그래서 그 셀을 지나는 동안엔 그 지형만
//        화면을 가득 채우고, 앞에 다른 지형이 미리 보이지 않는다.
//
// [교체] 셀이 다음 지형으로 바뀌면(예: 풀→숲) 그제서야 숲 길을 현재 길 '앞'에 이어붙여
//        같이 흘린다(Slide). 숲이 지평선에서 들어와 자연스럽게 이어지고, 마차가 건너가
//        풀 길이 뒤로 완전히 빠지면 숲 길이 무대를 넘겨받아 다시 루프한다.
//
// [진행] route(셀 지형 순서)와 cellLength(한 셀을 지나는 거리)로 진행. 실제 게임에선
//        캐러밴이 실제 지나는 그리드 셀/이동거리로 연결한다. loopSingle에 한 글자를 넣으면
//        그 지형만 무한 루프(단일 지형 확인용).
//
// [부착] 프리뷰 씬의 길 루트에 붙인다(에디트에선 지형별 길이 X로 나란히 보여 각각 확인 가능).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>지형별 독립 길을 무대에 올려 루프시키고, 셀이 바뀌면 다음 길로 교체하는 컨트롤러.</summary>
[ExecuteAlways]
public class TreadmillRoad : MonoBehaviour
{
    [Header("진행")]
    [Tooltip("그리드 셀들의 지형 순서(P평지 R강 B강변 D다리 M산 W호수 F숲 L논밭 G풀). 순환된다.")]
    [SerializeField] private string route = "GFPLMDRBW";
    [Tooltip("한 그리드 셀을 지나는 거리(m). 이만큼 같은 지형이 루프된 뒤 다음 지형으로 교체.")]
    [SerializeField] private float cellLength = 120f;
    [Tooltip("비우면 route로 진행. 한 글자(예: F)를 넣으면 그 지형만 무한 루프(단일 지형 확인용).")]
    [SerializeField] private string loopSingle = "";
    [SerializeField] private float scrollSpeed = 3f;   // 흐름 속도(m/s)
    [SerializeField] private bool scroll = true;       // 흐름 on/off (캐러밴이 이동 중일 때만 켬)

    [Header("길 모양")]
    [SerializeField] private float segLength = 10f;    // 조각(루프 단위) 길이
    [SerializeField] private float roadWidth = 12f;    // 길 너비
    [SerializeField] private int piecesPerStrip = 8;   // 한 지형 길의 조각 수(총 길이=이×segLength)
    [SerializeField] private float curvature = 0.0025f;
    [SerializeField] private float viewBack = -10f;    // 루프 창 뒤끝(마차 뒤로 남길 여유)
    [SerializeField] private float parkSpacingX = 60f; // 대기 중인 지형 길들을 X로 벌려두는 간격

    private readonly List<TerrainType> distinct = new List<TerrainType>();
    private readonly Dictionary<TerrainType, TerrainRoadStrip> strips = new Dictionary<TerrainType, TerrainRoadStrip>();
    private TerrainRoadStrip active;     // 무대에서 루프 중인 길
    private TerrainRoadStrip incoming;   // 교체 중 앞에서 들어오는 길
    private bool transitioning;
    private float traveled;              // 누적 이동거리(m)

    /// <summary>스크롤(흐름) on/off. 캐러밴이 이동 중일 때만 켜도록 외부에서 제어.</summary>
    public void SetScrollEnabled(bool on) => scroll = on;

    /// <summary>지형 순서를 캐러밴 실제 루트로 교체하고 다시 만든다(비면 무시).</summary>
    public void SetRoute(string routeChars)
    {
        if (string.IsNullOrEmpty(routeChars)) return;
        route = routeChars;
        loopSingle = "";
        Rebuild();
    }

    private void OnEnable() => Rebuild();

    /// <summary>지형별 독립 길들을 새로 만든다(문자열/치수 변경 시 우클릭 → Rebuild).</summary>
    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        for (int i = transform.childCount - 1; i >= 0; i--) DestroyObj(transform.GetChild(i).gameObject);
        strips.Clear(); distinct.Clear();
        transitioning = false; incoming = null; traveled = 0f;

        BuildDistinctList();
        for (int slot = 0; slot < distinct.Count; slot++)
        {
            TerrainType t = distinct[slot];
            var go = new GameObject("Road_" + t);
            go.transform.SetParent(transform, false);
            var strip = go.AddComponent<TerrainRoadStrip>();
            strip.BuildFor(t, segLength, roadWidth, piecesPerStrip, curvature, viewBack);
            strips[t] = strip;
            Park(strip, slot);   // 에디트에선 X로 나란히(각 지형 길 따로 확인)
        }

        active = strips.Count > 0 ? strips[distinct[0]] : null;
        if (Application.isPlaying && active != null) MoveToStage(active);
    }

    private void Update()
    {
        if (!Application.isPlaying || active == null) return;
        if (!scroll) return;   // 정지(출발 전 Prepare 등) — 길 안 흐름

        float dz = scrollSpeed * Time.deltaTime;
        traveled += dz;

        // 현재 셀 지형이 활성 길과 다르면 교체 시작
        if (!transitioning)
        {
            TerrainType want = CurrentCellTerrain();
            if (want != active.Terrain && strips.ContainsKey(want))
                BeginTransition(want);
        }

        if (transitioning)
        {
            active.Advance(dz, false);     // 퇴장(뒤로 슬라이드)
            incoming.Advance(dz, false);   // 등장(앞에서 슬라이드)
            if (active.MaxCenterZ < viewBack)   // 활성 길이 완전히 뒤로 빠짐 → 교체 완료
            {
                ParkByType(active);
                incoming.SnapToLoopWindow();
                active = incoming; incoming = null; transitioning = false;
            }
        }
        else
        {
            active.Advance(dz, true);      // 제자리 무한 루프
        }
    }

    // ── 교체 ──

    private void BeginTransition(TerrainType want)
    {
        incoming = strips[want];
        MoveRootToStageX(incoming);
        incoming.PlaceStartingAt(active.FrontEdgeZ + segLength * 0.5f);   // 활성 길 앞 끝에 이어붙여 등장
        transitioning = true;
    }

    // ── 배치 ──

    private void MoveToStage(TerrainRoadStrip strip)
    {
        MoveRootToStageX(strip);
        strip.SnapToLoopWindow();
    }

    private void MoveRootToStageX(TerrainRoadStrip strip) =>
        strip.transform.localPosition = Vector3.zero;   // 무대(원점)

    private void Park(TerrainRoadStrip strip, int slot) =>
        strip.transform.localPosition = new Vector3(parkSpacingX * slot, 0f, 0f);

    private void ParkByType(TerrainRoadStrip strip)
    {
        int slot = distinct.IndexOf(strip.Terrain);
        strip.transform.localPosition = new Vector3(parkSpacingX * (slot + 1), 0f, 0f);   // 무대(0) 피해 멀리
    }

    // ── 진행 ──

    private TerrainType CurrentCellTerrain()
    {
        if (!string.IsNullOrEmpty(loopSingle)) return MinimapCell.FromChar(loopSingle[0]);
        if (string.IsNullOrEmpty(route)) return TerrainType.Plain;
        int cell = Mathf.FloorToInt(traveled / Mathf.Max(0.001f, cellLength));
        int idx = ((cell % route.Length) + route.Length) % route.Length;
        return MinimapCell.FromChar(route[idx]);
    }

    private void BuildDistinctList()
    {
        distinct.Clear();
        void Add(char ch)
        {
            TerrainType t = MinimapCell.FromChar(ch);
            if (!distinct.Contains(t)) distinct.Add(t);
        }
        if (!string.IsNullOrEmpty(loopSingle)) Add(loopSingle[0]);
        if (!string.IsNullOrEmpty(route)) foreach (char ch in route) Add(ch);
        if (distinct.Count == 0) distinct.Add(TerrainType.Plain);
    }

    private static void DestroyObj(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
    }
}
