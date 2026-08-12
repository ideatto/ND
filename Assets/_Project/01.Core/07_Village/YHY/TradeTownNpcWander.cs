// =============================================================================
// TradeTownNpcWander — 무역 마을 NPC: 거점 마을(VillageNpc) 베이스의 격자 이동
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 거점 마을 NPC(VillageNpc)와 "똑같은 이동감"으로 무역 마을을 배회한다.
//        · 거점과 동일한 A* 격자 길찾기(VillageGrid 재사용)로 셀을 이어 걷고
//        · 빈 셀에 도착하면 잠깐 멈췄다가 다음 목적지를 다시 고른다(순수 배회).
//        속도·회전·도착정밀도를 거점 상수와 동일하게 맞춰, 두 마을 NPC의
//        이동 느낌이 갈리지 않게 한다.
//        ※ 무역 마을 건물은 대부분 장식(환경물)이라, 거점처럼 건물에 다가가
//          까딱거리는 '상호작용'은 하지 않는다(어색함 방지). 건물은 '피하기'만.
//
// [거점과 다른 점(무역 마을 특성상 필요한 최소 차이)]
//        · 거점은 원점 중심 평지 격자 1개. 무역 마을은 여러 곳에 흩어져 있고
//          터레인 경사·강이 있다. → 격자를 "마을 중심 로컬좌표"로 돌려 쓰고,
//          강물/건물/반경 밖 셀을 미리 blocked 로 구워 길찾기가 알아서 피한다.
//        · Y(높이)만 터레인 표면을 샘플해 접지(경사 따라 오르내림).
//
// [경계] 저장 무관(SaveData 안 씀). 연출 전용. 건물 목록은 매 실행 시 현재 씬의
//        실제 건물(tripo_convert_/Building_/Fountain)을 스캔해 구성한다(옛 injected
//        Obstacles 값은 무시). 격자는 마을(center)별로 1번만 굽는다.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>무역 마을 NPC — 거점과 동일한 A* 격자 배회(터레인 접지·강/건물 회피, 상호작용 없음).</summary>
public class TradeTownNpcWander : MonoBehaviour
{
    // ── 거점 마을(VillageNpc)과 "동일"하게 맞춘 이동 상수 ────────────────────
    //    씬 인스턴스마다 옛 값(1.5/8/0.2 등)이 박혀 있어도 무시하고 이 상수로
    //    통일한다(= 두 마을 NPC 움직임 일치). 필요하면 여기 한 곳만 고치면 된다.
    private const float MoveSpeed = 2.5f;        // 거점 VillageNpc.moveSpeed
    private const float WalkTurnSpeed = 10f;     // 이동 중 회전 slerp 계수(거점과 동일)
    private const float ArriveThreshold = 0.05f; // 셀 도착 판정(거점과 동일, 칼같이)
    private const float WanderPause = 0.2f;      // 빈터 도착 시 잠깐 멈춤
    private const float NpcRadius = 0.35f;       // 건물 반경에 더할 몸 두께(격자 굽기용)

    [Header("마을(씬에서 주입되는 값)")]
    [SerializeField] private Vector3 center;                 // 마을 중심(월드)
    [SerializeField] private float radius = 14f;             // 마을 반경(격자 크기·배회 범위)
    [SerializeField] private float groundY = 0f;             // 터레인 없을 때 쓸 바닥 높이
    [SerializeField] private float waterMargin = 0.2f;       // 수면 위 이만큼 확보(그 아래=물로 판정)

    // [사용 안 함/레거시] 예전엔 씬에 구워둔 집 목록을 여기서 읽었지만, 리뉴얼로 위치가
    //   어긋나 지금은 매 실행 시 현재 씬의 실제 건물을 직접 스캔한다(DiscoverBuildings).
    //   기존 인스턴스·API 호환을 위해 필드만 남겨둠(값은 무시됨).
    [SerializeField] private List<Vector4> injectedObstacles = new List<Vector4>();

    // 건물의 XZ 사각 footprint(월드). 셀 blocked 판정에 쓴다.
    private struct BoxXZ { public float cx, cz, ex, ez; }   // 중심(cx,cz) + 반두께(ex,ez)

    // ── 마을(center 키)별 공유 자원 — 여러 NPC가 같은 격자를 함께 쓴다 ──────
    private class TownData
    {
        public VillageGrid grid;            // 이 마을 격자(로컬좌표 기준, 원점=마을중심)
        public List<BoxXZ> obstacles;       // 건물 footprint(막을 대상, 작은 소품 포함)
        public float effRadius;             // 실제 건물 퍼짐에 맞춘 유효 반경(격자·배회 범위)
        public Terrain terrain;             // 이 마을이 올라탄 터레인(없을 수 있음)
        public float terrainBaseY;          // 터레인 y 오프셋
        public float groundLevel;           // 터레인 없을 때 쓸 지면 높이(건물 바닥 median)
        public bool hasGroundLevel;         // groundLevel 을 구했는가
        public bool hasWater;               // 강이 있는가
        public float waterY;                // 강 수면 높이
    }
    private static Dictionary<Vector3, TownData> townCache;

    // 건물 판정 기준(반두께 max)
    private const float BlockMinExtent = 0.30f;   // 이보다 작으면 막지도 않음(잔풀·소품)
    private const float BuildMaxExtent = 4.0f;    // 이보다 크면 건물 아님(바닥/Env/NPCs 등 컨테이너)

    // 격자 크기 산정용(거점과 동일한 셀 크기 사용)
    private const float CellSize = VillageGrid.CellSize;

    private TownData town;                               // 내가 속한 마을 공유 자원
    private readonly List<Vector2Int> path = new List<Vector2Int>();  // A* 경로(로컬 셀)
    private int pathIndex;                               // 현재 밟는 경로 인덱스
    private float baseY;                                 // 현재 접지 높이

    private enum State { Walking, Pausing }
    private State state = State.Walking;
    private float timer;                                 // 멈춤 잔여 시간

    /// <summary>스포너가 집 목록(월드중심 xyz + 반경 w)을 직접 주입한다(선택).</summary>
    public void SetObstacles(List<Vector4> obstacles)
    {
        injectedObstacles = obstacles != null ? obstacles : new List<Vector4>();
    }

    /// <summary>스포너가 중심·반경·바닥높이를 주입한다(선택).</summary>
    public void Configure(Vector3 wanderCenter, float wanderRadius, float ground)
    {
        center = wanderCenter; radius = wanderRadius; groundY = ground;
    }

    private void Start()
    {
        if (center == Vector3.zero) center = transform.position;

        town = GetOrBuildTown();     // 마을 격자·건물·터레인·강 준비(마을당 1번만 굽고 공유)

        SnapToNearestFreeCell();     // 시작 위치가 blocked면 가장 가까운 빈 셀로 스냅
        Vector3 p = transform.position;
        baseY = GroundY(p.x, p.z);
        transform.position = new Vector3(p.x, baseY, p.z);

        Decide();                    // 첫 목적지 결정
    }

    // ── 마을 격자 준비(굽기) ───────────────────────────────────────────────

    /// <summary>내 마을(center)의 공유 자원을 가져오거나, 없으면 새로 굽는다.</summary>
    private TownData GetOrBuildTown()
    {
        if (townCache == null) townCache = new Dictionary<Vector3, TownData>();
        if (townCache.TryGetValue(center, out TownData cached)) return cached;

        var t = new TownData();
        FindTerrainAndWater(t);              // 터레인·강 먼저 찾고(높이/물 판정에 필요)
        DiscoverBuildings(t);                // 현재 씬의 실제 건물을 스캔(footprint·방문중심·유효반경)
        t.grid = BakeGrid(t);                // 건물 박스·강·반경밖을 blocked로 구운 격자

        townCache[center] = t;
        return t;
    }

    /// <summary>이 마을이 올라탄 터레인과(있다면) 가까운 강 수면을 찾는다.</summary>
    private void FindTerrainAndWater(TownData t)
    {
        foreach (Terrain terr in Terrain.activeTerrains)
        {
            Vector3 tp = terr.transform.position; Vector3 sz = terr.terrainData.size;
            if (center.x >= tp.x && center.x <= tp.x + sz.x && center.z >= tp.z && center.z <= tp.z + sz.z)
            { t.terrain = terr; t.terrainBaseY = tp.y; break; }
        }

        Transform[] allT = Object.FindObjectsOfType<Transform>();
        float best = 70f * 70f;              // 마을에서 70m 안의 RiverWater만 이 마을 강으로 인정
        foreach (Transform tr in allT)
        {
            if (tr.name != "RiverWater") continue;
            float sq = (new Vector2(tr.position.x, tr.position.z) - new Vector2(center.x, center.z)).sqrMagnitude;
            if (sq < best) { best = sq; t.hasWater = true; t.waterY = tr.position.y; }
        }
    }

    /// <summary>
    /// 현재 씬의 실제 건물을 스캔해 footprint(막을 박스)·방문중심·유효반경을 채운다.
    /// 옛 injectedObstacles(리뉴얼 전 구운 값)에 의존하지 않고 지금 위치를 그대로 읽는다.
    /// 규칙(마을마다 계층이 제각각 → 구조 무관 보편 규칙): 마을 반경 안의
    ///   · 이름이 tripo_convert_* / Building_* / *Fountain* 이고
    ///   · 조상에 Env*/NPCs/Grass* 가 없는(장식·NPC·잔풀 제외)
    ///   · 크기 0.3~4m 인 오브젝트 = 건물.
    /// 마을 간 거리(≈137m)가 스캔 반경(≈40m)보다 훨씬 커서 옆 마을은 안 섞인다.
    /// </summary>
    private void DiscoverBuildings(TownData t)
    {
        t.obstacles = new List<BoxXZ>();
        Vector2 c2 = new Vector2(center.x, center.z);
        float maxDist = 0f;                        // 유효반경 산정용(가장 먼 건물까지)
        var baseYs = new List<float>();            // 건물 바닥 Y 모음(터레인 없을 때 지면 추정용)

        float scan = Mathf.Max(radius + 20f, 40f); // 스캔 반경(가장 큰 마을 퍼짐도 덮되 옆 마을엔 못 닿음)
        float scanSq = scan * scan;

        foreach (Transform tr in Object.FindObjectsOfType<Transform>())
        {
            bool isHouse = tr.name.StartsWith("tripo_convert_");
            bool isBld = tr.name.StartsWith("Building_");
            bool isFountain = tr.name.Contains("Fountain");
            if (!isHouse && !isBld && !isFountain) continue;
            if (HasDecorAncestor(tr)) continue;                // 장식(Env)/NPC/잔풀 밑이면 건물 아님
            if (isBld && HasHouseDescendant(tr)) continue;     // Building_ 이 tripo 를 품으면 자식 쪽에서 잡음(중복 방지)

            Renderer[] rs = tr.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) continue;
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            if ((new Vector2(b.center.x, b.center.z) - c2).sqrMagnitude > scanSq) continue;

            AddBuildingBounds(t, b, c2, ref maxDist, baseYs);
        }

        // 유효 반경: 직렬화된 radius를 최소로 두되, 실제 건물 퍼짐을 덮게 확장(과확장 방지 상한)
        t.effRadius = Mathf.Clamp(maxDist + 2f, radius, radius + 25f);

        // 터레인이 없는 마을(평평한 Ground 위)은 건물 바닥 높이의 median을 지면으로 삼는다.
        // (groundY 직렬화값이 0으로 잘못 박혀 NPC가 땅에 파묻히는 문제 방지)
        if (baseYs.Count > 0)
        {
            baseYs.Sort();
            t.groundLevel = baseYs[baseYs.Count / 2];
            t.hasGroundLevel = true;
        }
    }

    /// <summary>조상 중에 장식(Env*)·NPC(NPCs)·잔풀(Grass*) 컨테이너가 있으면 true(건물 아님).</summary>
    private static bool HasDecorAncestor(Transform tr)
    {
        for (Transform p = tr.parent; p != null; p = p.parent)
            if (p.name.StartsWith("Env") || p.name == "NPCs" || p.name.StartsWith("Grass")) return true;
        return false;
    }

    /// <summary>자손 중에 tripo_convert_ 집 메시가 있으면 true(부모 Building_ 중복 등록 방지용).</summary>
    private static bool HasHouseDescendant(Transform tr)
    {
        foreach (Transform c in tr.GetComponentsInChildren<Transform>())
            if (c != tr && c.name.StartsWith("tripo_convert_")) return true;
        return false;
    }

    /// <summary>건물 바운즈로 footprint 박스를 만들어 목록에 넣는다(너무 작거나 큰 건 제외). 바닥 Y도 모은다.</summary>
    private void AddBuildingBounds(TownData t, Bounds b, Vector2 c2, ref float maxDist, List<float> baseYs)
    {
        float ex = b.extents.x, ez = b.extents.z;
        float exMax = Mathf.Max(ex, ez);
        if (exMax < BlockMinExtent) return;                 // 너무 작은 소품은 무시
        if (exMax > BuildMaxExtent) return;                 // 너무 큰 건 건물 아님(바닥/컨테이너)

        baseYs.Add(b.min.y);                                // 건물 바닥 = 지면 추정에 사용
        Vector2 bc = new Vector2(b.center.x, b.center.z);
        t.obstacles.Add(new BoxXZ { cx = bc.x, cz = bc.y, ex = ex, ez = ez });

        float d = (bc - c2).magnitude + exMax;
        if (d > maxDist) maxDist = d;
    }

    /// <summary>
    /// 마을 중심을 원점으로 하는 격자를 만들고, 건물 박스·강물·반경 밖 셀을 blocked로 굽는다.
    /// (VillageGrid는 원점 중심 전제라, 좌표는 항상 "월드 − center"로 넘겨 로컬로 쓴다.)
    /// 건물은 실제 XZ footprint가 '닿는' 셀을 전부 막아 그리드=건물 정합을 맞춘다(통과 방지).
    /// </summary>
    private VillageGrid BakeGrid(TownData t)
    {
        float rEff = t.effRadius;
        int side = 2 * Mathf.CeilToInt(rEff / CellSize) + 4;   // 유효반경을 덮는 정사각 격자
        var grid = new VillageGrid(side, side);
        object BLOCKED = "BLOCKED";   // 점유 마커(IsFree=false 로 만들기만 하면 됨)

        // 1) 반경 밖 / 물 위 셀 막기
        float rSq = rEff * rEff;
        int half = side / 2;
        for (int cx = -half; cx < side - half; cx++)
            for (int cz = -half; cz < side - half; cz++)
            {
                Vector3 local = grid.CellCenter(cx, cz);          // 로컬 셀 중심(원점=마을중심)
                float wx = center.x + local.x, wz = center.z + local.z;

                if ((new Vector2(wx, wz) - new Vector2(center.x, center.z)).sqrMagnitude > rSq)
                { grid.Occupy(cx, cz, 1, 1, BLOCKED); continue; }

                if (t.hasWater && GroundYOn(t, wx, wz) <= t.waterY + waterMargin)
                { grid.Occupy(cx, cz, 1, 1, BLOCKED); continue; }
            }

        // 2) 건물 박스가 닿는 셀 막기(footprint AABB ∩ 셀영역). cellHalf만큼 여유 → 걸치는 셀도 전부 막음
        const float cellHalf = CellSize * 0.5f;
        const float eps = 0.05f;
        if (t.obstacles != null)
            foreach (BoxXZ o in t.obstacles)
            {
                float exR = o.ex + cellHalf - eps;   // 박스가 이 범위 안 셀중심을 덮으면 그 셀은 걸침
                float ezR = o.ez + cellHalf - eps;
                grid.WorldToCell(new Vector3(o.cx - center.x - exR, 0f, o.cz - center.z - ezR), out int minCx, out int minCz);
                grid.WorldToCell(new Vector3(o.cx - center.x + exR, 0f, o.cz - center.z + ezR), out int maxCx, out int maxCz);
                for (int cx = minCx; cx <= maxCx; cx++)
                    for (int cz = minCz; cz <= maxCz; cz++)
                    {
                        Vector3 local = grid.CellCenter(cx, cz);
                        float wx = center.x + local.x, wz = center.z + local.z;
                        if (Mathf.Abs(wx - o.cx) <= exR && Mathf.Abs(wz - o.cz) <= ezR)
                            grid.Occupy(cx, cz, 1, 1, BLOCKED);
                    }
            }

        return grid;
    }

    // ── 목적지 결정 & 이동(거점 VillageNpc와 동일한 흐름) ────────────────────

    /// <summary>다음 목적지를 정한다: 건물을 피해 빈 셀로 배회(상호작용 없음).</summary>
    private void Decide()
    {
        path.Clear();
        pathIndex = 0;
        state = State.Walking;
        if (town == null || town.grid == null) return;

        WorldToLocalCell(transform.position, out int sx, out int sz);

        // 임의의 빈 셀(건물·물·반경밖은 이미 blocked)로 경로를 만든다.
        if (town.grid.TryGetRandomFreeCell(n => Random.Range(0, n), out int rx, out int rz))
        {
            var f = town.grid.FindPath(sx, sz, rx, rz);
            if (f != null) path.AddRange(f);
        }
    }

    private void Update()
    {
        if (town == null) return;

        if (state == State.Pausing)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f) Decide();
            return;
        }

        // ── Walking: 경로 셀을 하나씩 따라간다(거점과 동일, 각진 이동) ──
        if (pathIndex >= path.Count)   // 경로 끝(빈터 도착) → 잠깐 멈췄다 다음 목적지
        { state = State.Pausing; timer = WanderPause; return; }

        Vector2Int cell = path[pathIndex];
        Vector3 target = LocalCellToWorld(cell.x, cell.y);   // 셀 중심 월드좌표(Y=터레인)
        Vector3 pos = transform.position;

        // XZ로 이동하고 Y는 터레인 높이로 접지
        Vector3 flatTarget = new Vector3(target.x, pos.y, target.z);
        Vector3 next = Vector3.MoveTowards(pos, flatTarget, MoveSpeed * Time.deltaTime);
        next.y = GroundY(next.x, next.z);
        transform.position = next;
        baseY = next.y;

        // 진행 방향 바라보기
        Vector3 dir = new Vector3(target.x - pos.x, 0f, target.z - pos.z);
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), WalkTurnSpeed * Time.deltaTime);

        // 도착 판정(XZ 기준)
        Vector3 d = new Vector3(transform.position.x - target.x, 0f, transform.position.z - target.z);
        if (d.sqrMagnitude <= ArriveThreshold * ArriveThreshold)
            pathIndex++;
    }

    // ── 좌표 변환 & 지형 헬퍼(월드 ↔ 마을 로컬 격자) ─────────────────────────

    /// <summary>월드 위치 → 이 마을 격자의 로컬 셀 좌표.</summary>
    private void WorldToLocalCell(Vector3 world, out int cx, out int cz)
    {
        town.grid.WorldToCell(new Vector3(world.x - center.x, 0f, world.z - center.z), out cx, out cz);
    }

    /// <summary>로컬 셀 좌표 → 셀 중심 월드좌표(Y는 터레인 표면).</summary>
    private Vector3 LocalCellToWorld(int cx, int cz)
    {
        Vector3 local = town.grid.CellCenter(cx, cz);
        float wx = center.x + local.x, wz = center.z + local.z;
        return new Vector3(wx, GroundY(wx, wz), wz);
    }

    /// <summary>지형 표면 높이(내 마을 기준).</summary>
    private float GroundY(float x, float z) => GroundYOn(town, x, z);

    /// <summary>지형 표면 높이(주어진 마을 기준). 터레인 있으면 샘플, 없으면 건물바닥 median, 그것도 없으면 groundY.</summary>
    private float GroundYOn(TownData t, float x, float z)
    {
        if (t != null && t.terrain != null) return t.terrainBaseY + t.terrain.SampleHeight(new Vector3(x, 0f, z));
        if (t != null && t.hasGroundLevel) return t.groundLevel;   // 평지 마을: 건물 바닥 높이를 지면으로
        return groundY;
    }

    /// <summary>시작 위치가 blocked 셀이면 가장 가까운 빈 셀 중심으로 옮긴다.</summary>
    private void SnapToNearestFreeCell()
    {
        if (town == null || town.grid == null) return;
        WorldToLocalCell(transform.position, out int cx, out int cz);
        if (!town.grid.IsFree(cx, cz))
            town.grid.FindNearestFree(cx, cz, 1, 1, null, out cx, out cz);
        Vector3 w = LocalCellToWorld(cx, cz);
        transform.position = w;
    }
}
