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

/// <summary>한 종류(풀/돌/덤불/나무…)의 스캐터 배치 설정. 개수·크기를 이 단위로 따로 준다.</summary>
[System.Serializable]
public class ScatterLayer
{
    public string name = "layer";                 // 에디터 식별용(풀/돌/덤불…)
    public GameObject[] prefabs;                   // 이 레이어에서 랜덤으로 뽑을 프리팹들
    public int count = 10;                          // 조각당 개수
    [Range(0.02f, 3f)] public float scale = 1f;    // 스케일 배율(바위는 작게, 나무는 크게)
    [Range(0f, 0.6f)] public float scaleJitter = 0.2f;  // 크기 ± 랜덤(자연스러움)
    [Tooltip("켜면 이 레이어(나무·바위 등)를 캐러밴이 회피기동으로 피한다.")]
    public bool avoid = false;                      // 마차가 피할 장애물인지
    [Tooltip("스캐터에 입힐 색(흰색=원본). 예: 밭 대나무를 노랗게.")]
    public Color tint = Color.white;                // 원본 재질에 곱해질 틴트
    [Tooltip("0=랜덤 배치. >0이면 이 간격(m)의 격자로 줄맞춰 심는다(밭·옥수수농장처럼).")]
    public float gridSpacing = 0f;                  // 줄맞춤 격자 간격
    [Tooltip("가운데를 이 반폭(±m)만큼 비워 마차길(흙길)을 낸다. 0=길 없음(중앙까지 심음).")]
    public float centerPathHalf = 0f;               // 중앙 마차길 반폭
    [Range(0f, 1f)]
    [Tooltip("각 개체를 실제로 심을 확률. 1=항상. 예: 나무 0.5면 절반 조각에만 → 드문드문.")]
    public float spawnChance = 1f;                  // 개체별 스폰 확률(듬성듬성 조절)
    [Tooltip("꽃무리 크기: 한 지점에 몇 송이를 뭉쳐 심을지. 0·1=낱개, 2+=무리(count=무리 개수).")]
    public int clusterSize = 1;                     // 무리당 개체 수
    [Tooltip("꽃무리 반경(m): 무리가 이 반경 안에 흩어져 뭉친다.")]
    public float clusterRadius = 1.2f;              // 무리 퍼짐 반경
}

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
    [SerializeField] private bool scroll = false;      // 흐름 on/off (캐러밴이 실제 이동 중일 때만 켬. 기본 정지)

    [Header("디버그(수동 지형 전환)")]
    [Tooltip("켜면 스타일이 있는 모든 지형의 길을 미리 만들어 둔다(디버그 버튼으로 아무거나 전환 가능).")]
    [SerializeField] private bool debugAllTerrains = false;
    [Tooltip("켜면 route로 자동 전환하지 않고, RequestTerrain(디버그 버튼)으로만 전환한다.")]
    [SerializeField] private bool debugManualTerrain = false;

    [Header("길 모양")]
    [SerializeField] private float segLength = 10f;    // 조각(루프 단위) 길이
    [SerializeField] private float roadWidth = 12f;    // 길 너비
    [Tooltip("모든 지형에서 가운데 '마차 지나는 길'을 이 반폭(±m)만큼 비운다(스캐터 안 생김). 0=안 비움. 각 레이어의 centerPathHalf와 함께 큰 값이 적용됨.")]
    [SerializeField] private float centerPathHalf = 2.6f;   // 중앙 마차길 반폭(전 지형 공통)
    [Tooltip("마차길 흙 텍스처. 지정하면 전 지형 가운데(centerPathHalf 폭)에 이 흙길이 보인다. 비우면 길 안 그림(스캐터만 비움).")]
    [SerializeField] private Texture2D pathTexture;         // 마차길 흙 텍스처(예: TSI_Terrain_Earth_01D)
    [SerializeField] private Color pathTint = Color.white;  // 마차길 색
    [Tooltip("마차길 경계 부드러움(m). 클수록 흙↔지형 경계가 흐릿하게 섞임.")]
    [SerializeField] private float pathSoft = 0.8f;
    [SerializeField] private int piecesPerStrip = 8;   // 한 지형 길의 조각 수(총 길이=이×segLength)
    [SerializeField] private float curvature = 0.0025f;
    [SerializeField] private float viewBack = -10f;    // 루프 창 뒤끝(마차 뒤로 남길 여유)
    [SerializeField] private float parkSpacingX = 60f; // 대기 중인 지형 길들을 X로 벌려두는 간격
    [Tooltip("지형 전환 시 두 길이 겹쳐 노이즈로 디졸브되는 경계 폭(m). 클수록 경계가 더 부드럽게 섞임.")]
    [SerializeField] private float blendBand = 14f;
    private const float DissolveLift = 0.05f;          // 겹침 구간 z-fighting 방지용 살짝 띄우기

    [Header("지형 텍스처 (없으면 단색 폴백)")]
    [SerializeField] private TerrainStyle[] terrainStyles;
    [SerializeField] private float textureTileMeters = 4f;   // 텍스처 1장이 덮는 실제 크기(m)
    [SerializeField] private float noiseScale = 3.5f;        // 풀/흙 얼룩 크기(작을수록 큰 얼룩)

    /// <summary>지형 종류별 바닥: 기본 텍스처 + 섞을(detail) 텍스처 + 틴트 + 스캐터 레이어들.</summary>
    [System.Serializable]
    public class TerrainStyle
    {
        public TerrainType terrain;
        public Texture2D texture;                 // 기본(예: 풀)
        public Color tint = Color.white;
        public Texture2D detailTexture;           // 섞을 것(예: 흙). 없으면 단일 텍스처
        public Color detailTint = Color.white;
        [Range(0f, 1f)] public float detailAmount = 0.45f;   // 얼룩 비율

        [Header("스캐터 레이어(종류별 개수·크기 따로)")]
        public ScatterLayer[] scatterLayers;      // 풀·돌·덤불·나무 등을 각각 다른 개수/크기로
    }

    private readonly List<TerrainType> distinct = new List<TerrainType>();
    private readonly Dictionary<TerrainType, TerrainRoadStrip> strips = new Dictionary<TerrainType, TerrainRoadStrip>();
    private TerrainRoadStrip active;     // 무대에서 루프 중인 길
    private TerrainRoadStrip incoming;   // 교체 중 앞에서 들어오는 길
    private bool transitioning;
    private float traveled;              // 누적 이동거리(m)

    // ── 대기실 그리드(11번째 그리드) ──
    // 지형 10개(Road_Farmland…)와 똑같이 TreadmillRoad의 자식으로 씬에 놓아 둔 '대기실 그리드'.
    // 평소엔 다른 그리드처럼 옆(park)에 대기하다가, 이동 중이 아니면 가운데(무대)로 불러온다.
    // (씬 오브젝트라 에디터에서 자유롭게 편집/교체 가능 — 코드가 생성하지 않는다.)
    [Header("대기실 그리드(11번째)")]
    [Tooltip("씬에 만들어 둔 '대기실 그리드' 오브젝트(TreadmillRoad 자식). 이동 중이 아니면 무대로 나온다.")]
    [SerializeField] private Transform waitingRoomGrid;
    private bool roomMode;   // true면 대기실 그리드가 무대(가운데), 지형 그리드는 옆으로 치움

    /// <summary>대기실 그리드 on/off. 켜면 대기실 그리드를 무대(가운데)로, 지형 그리드는 옆으로 치운다.
    /// → 마을 진입·대기·파괴 시 도로 대신 '차고'에 마차가 서 있는 것처럼 보임(나무 안 뚫음).</summary>
    public void ShowWaitingRoom(bool on)
    {
        if (roomMode == on) return;   // 상태 그대로면 무시(매 프레임 호출돼도 안전)
        roomMode = on;
        PlaceRoom(on);
    }

    // 대기실 그리드를 무대(x=0)로 올리거나 옆(park)으로 치운다. 지형 활성 길은 반대로.
    private void PlaceRoom(bool on)
    {
        if (waitingRoomGrid != null)
            waitingRoomGrid.localPosition = on ? Vector3.zero
                                               : new Vector3(parkSpacingX * Mathf.Max(1, distinct.Count), 0f, 0f);
        if (active != null)
        {
            if (on) ParkByType(active);        // 지형 길을 자기 슬롯(옆)으로 치워 화면에서 사라지게
            else MoveRootToStageX(active);     // 다시 무대(가운데)로
        }
    }

    /// <summary>스크롤(흐름) on/off. 캐러밴이 이동 중일 때만 켜도록 외부에서 제어.</summary>
    public void SetScrollEnabled(bool on) => scroll = on;

    private bool debugForceScroll;   // 디버그에서 강제로 흐르게(캐러밴 이동과 무관)
    /// <summary>디버그: 캐러밴 상태와 무관하게 길을 강제로 흐르게.</summary>
    public void SetDebugScroll(bool on) => debugForceScroll = on;

    private bool arrived;   // 마을 도착 → 길·동물 정지(스크롤 무시)
    /// <summary>마을 도착 정지 on/off. 켜면 스크롤이 멈춘다(동물·덜컹도 정지).</summary>
    public void SetArrived(bool on) => arrived = on;
    /// <summary>도착 정지 상태인지.</summary>
    public bool IsArrived => arrived;

    private bool combatHold;   // 전투 충돌 순간 '잠깐 멈춤'(산적과 부딪혀 정지). 결과 나오면 해제.
    /// <summary>전투 멈춤 on/off. 켜면 시각 스크롤만 잠깐 멈춘다(동물·바닥 정지).
    /// ★시각 전용 게이트라 실제 무역 진행도·도착 타이밍엔 영향 없다(스크롤은 velocity 기반 분리 구동).
    /// 도착(arrived)과 별개 플래그라 도착 접근 로직을 건드리지 않는다.</summary>
    public void SetCombatHold(bool on) => combatHold = on;

    private bool terrainVisible = true;   // 도로 지형(길·나무·풀) 렌더 표시 여부
    /// <summary>도로 지형 렌더 on/off. 대기실 표시 중엔 꺼서 나무·풀이 대기실을 뚫지 않게 한다.
    /// (변경될 때만 렌더러를 훑어 성능 안전. 로직·위치엔 영향 없음, 오직 렌더만.)</summary>
    public void SetTerrainVisible(bool on)
    {
        if (terrainVisible == on) return;
        terrainVisible = on;
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
    }

    // ── 외부 진행도 구동(실제 여행 progress로 길을 몰기) ──
    // 그리드(어느 셀)는 진행도 p로, 시각 지면 스크롤은 기존 scrollSpeed로 '분리' 구동한다.
    // (진행도 총거리가 커도 지면이 블러되지 않게 — 스크롤은 늘 보기 좋은 속도)
    private bool externalDrive;      // 켜지면 진행도 p가 그리드를 정함(자기 시계 대신)
    private float externalP01;       // 외부가 지정한 진행도 0~1
    private float lastP01 = -1f;     // 직전 진행도(전진 판정용)
    private float lastDz;            // 직전 프레임 지면 이동량(걷기 판정용)
    /// <summary>실제 여행 진행도(0~1) 지정. 그리드=floor(p×셀수), 지면은 진행 중일 때만 스크롤. 매 프레임 호출.</summary>
    public void SetExternalProgress(float p01) { externalDrive = true; externalP01 = Mathf.Clamp01(p01); }
    /// <summary>외부 구동 해제 → 자기 시계/디버그 스크롤로 복귀.</summary>
    public void ClearExternalDrive() { externalDrive = false; lastP01 = -1f; }
    /// <summary>route 셀 개수(진행도→셀 매핑용). 없으면 0.</summary>
    public int RouteCellCount => string.IsNullOrEmpty(route) ? 0 : route.Length;

    /// <summary>길이 실제로 흐르는 중인지. 외부 구동이면 '직전 프레임에 지면이 움직였는지'로 판단.</summary>
    public bool IsScrolling => !arrived && (externalDrive ? lastDz > 0.0001f : (scroll || debugForceScroll));
    /// <summary>캐러밴의 유효 전진 속도(m/s). 흐르지 않으면 0. 회피 스티어링이 횡드리프트 계산에 씀.</summary>
    public float ForwardSpeed => IsScrolling ? scrollSpeed : 0f;

    /// <summary>길 커브 계수(먼 곳이 아래로 휨: y=-curvature·z²). 지면 위 오브젝트 높이 맞춤용.</summary>
    public float Curvature => curvature;

    /// <summary>현재 무대에서 루프 중인 지형.</summary>
    public TerrainType CurrentTerrain => active != null ? active.Terrain : TerrainType.Plain;

    /// <summary>현재 무대 길(회피기동이 장애물 목록을 읽는다).</summary>
    public TerrainRoadStrip ActiveStrip => active;

    /// <summary>지형 전환 슬라이드가 진행 중인지.</summary>
    public bool IsTransitioning => transitioning;

    /// <summary>디버그/외부에서 특정 지형으로 '슬라이드 전환'을 요청한다(그리드가 이어지듯 앞에서 들어옴).</summary>
    public void RequestTerrain(TerrainType t)
    {
        if (!Application.isPlaying || active == null) return;
        if (transitioning) return;                 // 전환 중엔 무시(끝난 뒤 다시)
        if (t == active.Terrain) return;           // 이미 그 지형
        if (!strips.ContainsKey(t)) return;        // 그 지형 길이 없음(debugAllTerrains 필요)
        BeginTransition(t);
    }

    /// <summary>슬라이드 없이 즉시 이 지형으로 교체한다(정박 중 마을 주변 지형 표시용).</summary>
    public void SetTerrainImmediate(TerrainType t)
    {
        if (!Application.isPlaying || !strips.ContainsKey(t)) return;
        if (transitioning) { if (incoming != null) ParkByType(incoming); incoming = null; transitioning = false; }
        if (active != null && active.Terrain == t) return;   // 이미 그 지형
        if (active != null) ParkByType(active);
        active = strips[t];
        MoveToStage(active);
    }

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
        // 지형 길 자식만 새로 만든다. '대기실 그리드'(씬에 놓아둔 11번째)는 파괴하지 않고 보존한다.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (waitingRoomGrid != null && child == waitingRoomGrid) continue;
            DestroyObj(child.gameObject);
        }
        strips.Clear(); distinct.Clear();
        transitioning = false; incoming = null; traveled = 0f;

        BuildDistinctList();
        for (int slot = 0; slot < distinct.Count; slot++)
        {
            TerrainType t = distinct[slot];
            var go = new GameObject("Road_" + t);
            go.transform.SetParent(transform, false);
            var strip = go.AddComponent<TerrainRoadStrip>();
            // 지형 스타일(기본+detail 텍스처·틴트 + 스캐터 레이어) 조회
            Texture tex = null, dtex = null; Color tint = Color.white, dtint = Color.white; float amount = 0f;
            ScatterLayer[] layers = null;
            if (terrainStyles != null)
                foreach (var s in terrainStyles)
                    if (s != null && s.terrain == t)
                    { tex = s.texture; tint = s.tint; dtex = s.detailTexture; dtint = s.detailTint; amount = s.detailAmount;
                      layers = s.scatterLayers; break; }
            strip.BuildFor(t, segLength, roadWidth, piecesPerStrip, curvature, viewBack,
                           tex, tint, dtex, dtint, amount, textureTileMeters, noiseScale, layers, centerPathHalf,
                           pathTexture, pathTint, pathSoft);
            strips[t] = strip;
            Park(strip, slot);   // 에디트에선 X로 나란히(각 지형 길 따로 확인)
        }

        active = strips.Count > 0 ? strips[distinct[0]] : null;
        if (Application.isPlaying && active != null) MoveToStage(active);

        // 대기실 그리드(11번째)를 지형 길들 다음 슬롯(옆)에 대기시킨다. 무대로는 ShowWaitingRoom가 부른다.
        if (waitingRoomGrid != null)
            PlaceRoom(roomMode);
    }

    private void Update()
    {
        if (!Application.isPlaying || active == null) return;
        if (roomMode) { lastDz = 0f; return; }   // 대기실 표시 중엔 지형 스크롤·전환 안 함(정지)
        if (combatHold) { lastDz = 0f; return; } // 전투 충돌 순간 잠깐 멈춤(결과 나오면 해제)

        float dz;
        if (externalDrive)
        {
            if (arrived) { lastDz = 0f; return; }
            // 이동 중(외부구동 + 도착 전)이면 진행도 변화량과 무관하게 지면을 '보기 좋은 속도'로 흘린다.
            //  (예전엔 프레임당 진행도 delta > 1e-5 일 때만 스크롤 → 진행이 느린 긴 무역은 delta가
            //   미세해 임계값에 안 걸려 '그리드 셀은 바뀌는데 바닥은 안 흐르는' 문제가 있었다.
            //   시각 스크롤 속도는 어차피 scrollSpeed로 분리 구동이므로, 도착 전엔 항상 흐르게 한다.)
            lastP01 = externalP01;
            dz = scrollSpeed * Time.deltaTime;
            traveled += dz;                                     // 시각용 누적(피스 스크롤)
            lastDz = dz;
        }
        else
        {
            if (!IsScrolling) { lastDz = 0f; return; }          // 정지(출발 전 Prepare 등)
            dz = scrollSpeed * Time.deltaTime;
            traveled += dz;
            lastDz = dz;
        }

        // 현재 셀 지형이 활성 길과 다르면 교체 시작.
        // (디버그 수동모드는 자동전환 안 함 — 단 진행도 구동이면 지형이 progress를 따라야 하므로 전환)
        if (!transitioning && (externalDrive || !debugManualTerrain))
        {
            TerrainType want = CurrentCellTerrain();
            if (want != active.Terrain && strips.ContainsKey(want))
                BeginTransition(want);
        }

        if (transitioning)
        {
            active.Advance(dz, false);     // 퇴장(뒤로 슬라이드)
            incoming.Advance(dz, false);   // 등장(앞에서 슬라이드)
            // 겹침 구간에서 들어오는 길을 노이즈 디졸브 → 옛 지형이 구멍으로 비쳐 경계가 안 생김.
            float edgeWorldZ = transform.position.z + active.FrontEdgeZ - blendBand;
            incoming.SetDissolve(true, edgeWorldZ, blendBand);
            if (active.MaxCenterZ < viewBack)   // 활성 길이 완전히 뒤로 빠짐 → 교체 완료
            {
                ParkByType(active);
                incoming.SetDissolve(false, 0f, blendBand);          // 디졸브 끔
                incoming.transform.localPosition = Vector3.zero;     // 리프트 원복
                // SnapToLoopWindow 제거: 들어온 길을 스냅하면 조각이 순간이동해 '새로 그려지는' 팝이 생김.
                //  이미 조각이 연속 배치라 현재 위치에서 그대로 루프하면 됨(뒤로 넘는 wrap은 카메라 뒤/안개라 안 보임).
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
        // 무대(x=0)에 올리되 z-fighting 방지로 살짝 띄운다.
        incoming.transform.localPosition = new Vector3(0f, DissolveLift, 0f);
        // 활성 길 앞 끝과 blendBand만큼 '겹치게' 배치 → 겹침 구간에서 디졸브로 섞임.
        incoming.PlaceStartingAt(active.FrontEdgeZ - blendBand + segLength * 0.5f);
        float edgeWorldZ = transform.position.z + active.FrontEdgeZ - blendBand;
        incoming.SetDissolve(true, edgeWorldZ, blendBand);
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
        int idx;
        if (externalDrive)   // 진행도 구동: 셀 = floor(p×셀수), 루프 없이 끝 셀에서 멈춤(경로는 편도)
            idx = Mathf.Clamp(Mathf.FloorToInt(externalP01 * route.Length), 0, route.Length - 1);
        else                 // 디버그 자기시계: 누적거리로 셀 계산 + 루프
        {
            int cell = Mathf.FloorToInt(traveled / Mathf.Max(0.001f, cellLength));
            idx = ((cell % route.Length) + route.Length) % route.Length;
        }
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
        // 디버그: 모든 지형의 길을 미리 만들어 둔다(스타일 없으면 단색 폴백). 버튼으로 아무거나 전환.
        if (debugAllTerrains)
            foreach (TerrainType t in System.Enum.GetValues(typeof(TerrainType)))
                if (!distinct.Contains(t)) distinct.Add(t);
        if (distinct.Count == 0) distinct.Add(TerrainType.Plain);
    }

    private static void DestroyObj(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
    }
}
