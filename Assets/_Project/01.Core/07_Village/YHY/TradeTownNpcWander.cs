// =============================================================================
// TradeTownNpcWander — 무역 마을 NPC: 건물 방문·상호작용 + 건물 관통 방지
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 그리드/네브메시 없이도 거점마을 NPC(VillageNpc)처럼 "의도가 보이는" 행동:
//        · 주변 건물(Building_* / 분수대) 중 하나를 골라 그 '앞'까지 걸어가
//        · 건물을 향해 서서 잠깐 까딱(상호작용 표현)한 뒤
//        · 다음 목적지(다른 건물 또는 빈터)를 다시 고른다.
//        이동 중에는 모든 건물을 '원형 장애물'로 보고 반경 밖으로 밀어내
//        건물을 뚫고 지나가지 않는다.
//
// [경계] 저장 무관(SaveData 안 씀). 평지(바닥 y 고정) 전제. 연출 전용.
//        건물 목록은 실행 시 이름(Building_/Fountain)으로 자동 탐색(마을별 캐시).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>무역 마을 NPC — 건물 방문·상호작용, 건물 관통 방지 배회.</summary>
public class TradeTownNpcWander : MonoBehaviour
{
    [Header("배회 영역")]
    [SerializeField] private Vector3 center;       // 마을 중심(월드)
    [SerializeField] private float radius = 14f;   // 빈터 배회 반경
    [SerializeField] private float groundY = 0f;   // 바닥 높이(평지)

    [Header("이동")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float turnSpeed = 8f;
    [SerializeField] private float arriveThreshold = 0.2f;
    [SerializeField] private float npcRadius = 0.35f;   // 건물 반경에 더할 여유(몸 두께)

    [Header("상호작용(건물 앞 까딱)")]
    [SerializeField] private float interactSeconds = 2.5f;
    [SerializeField] private float bobHeight = 0.12f;
    [SerializeField] private float bobSpeed = 7f;
    [SerializeField] private float approachGap = 1.0f;   // 건물 가장자리에서 이만큼 앞에 선다

    [Header("멈춤(빈터 도착 시)")]
    [SerializeField] private float pauseMin = 0.6f;
    [SerializeField] private float pauseMax = 1.6f;

    [Header("행동 비율")]
    [Range(0f, 1f)]
    [SerializeField] private float visitChance = 0.75f;  // 건물 방문 확률(나머지는 빈터 배회)

    // [주입] 이름으로 못 찾는 집(tripo 등)은 스포너가 직접 넣어준다.
    //        각 원소: xyz=월드 중심, w=반경. 비어있으면 이름 기반 자동탐색으로 폴백.
    [SerializeField] private List<Vector4> injectedObstacles = new List<Vector4>();

    // 건물 = 원형 장애물 + 방문 대상
    private struct Building { public Vector2 center; public float radius; }
    // 마을(center 키)별로 한 번만 탐색해 공유
    private static Dictionary<Vector3, List<Building>> buildingCache;

    /// <summary>스포너가 집 목록(월드중심 xyz + 반경 w)을 직접 주입한다.</summary>
    public void SetObstacles(List<Vector4> obstacles)
    {
        injectedObstacles = obstacles != null ? obstacles : new List<Vector4>();
    }

    private List<Building> buildings;
    private enum State { Walking, Interacting, Pausing }
    private State state = State.Walking;
    private Vector3 target;          // 현재 목표 지점
    private int targetBuilding = -1; // 향하는 건물 index(-1이면 빈터)
    private float timer;             // 상호작용/멈춤 잔여 시간
    private float baseY;

    // 지형/물 인식 — 강 있는 마을에서 NPC가 물로 걸어 들어가지 않게
    private Terrain groundTerrain;   // 이 마을이 올라탄 터레인
    private float terrainBaseY;      // 터레인 y 오프셋
    private bool hasWater;           // 이 마을에 강이 있는가
    private float waterY;            // 강 수면 높이
    [SerializeField] private float waterMargin = 0.2f;   // 수면 위 이만큼 확보(그 아래=물로 판정)

    /// <summary>스포너가 중심·반경·바닥높이를 주입.</summary>
    public void Configure(Vector3 wanderCenter, float wanderRadius, float ground)
    {
        center = wanderCenter; radius = wanderRadius; groundY = ground;
        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, groundY, p.z);
    }

    private void Start()
    {
        if (center == Vector3.zero) center = transform.position;
        DiscoverBuildings();
        FindTerrainAndWater();
        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, GroundY(p.x, p.z), p.z);   // 지형에 접지
        baseY = transform.position.y;
        Decide();
    }

    /// <summary>이 마을이 올라탄 터레인과(있다면) 강 수면을 찾는다.</summary>
    private void FindTerrainAndWater()
    {
        // center를 품는 터레인
        foreach (Terrain t in Terrain.activeTerrains)
        {
            Vector3 tp = t.transform.position; Vector3 sz = t.terrainData.size;
            if (center.x >= tp.x && center.x <= tp.x + sz.x && center.z >= tp.z && center.z <= tp.z + sz.z)
            { groundTerrain = t; terrainBaseY = tp.y; break; }
        }
        // 가까운 강 수면(RiverWater) — 있으면 물 회피 활성
        Transform[] allT = Object.FindObjectsOfType<Transform>();
        float best = 70f * 70f;
        foreach (Transform t in allT)
        {
            if (t.name != "RiverWater") continue;
            float sq = (new Vector2(t.position.x, t.position.z) - new Vector2(center.x, center.z)).sqrMagnitude;
            if (sq < best) { best = sq; hasWater = true; waterY = t.position.y; }
        }
    }

    /// <summary>지형 표면 높이(터레인 없으면 groundY 고정).</summary>
    private float GroundY(float x, float z)
    {
        if (groundTerrain != null) return terrainBaseY + groundTerrain.SampleHeight(new Vector3(x, 0f, z));
        return groundY;
    }

    /// <summary>그 지점이 뭍인가(강 있는 마을에서 수면 위인지). 강 없으면 항상 뭍.</summary>
    private bool IsLand(float x, float z)
    {
        if (!hasWater) return true;
        return GroundY(x, z) > waterY + waterMargin;
    }

    /// <summary>이름(Building_/Fountain)으로 이 마을 근처 건물을 찾아 장애물+방문대상으로 등록.</summary>
    private void DiscoverBuildings()
    {
        // [주입 우선] 스포너가 집 목록을 넣어줬으면 그걸 쓴다(tripo 마을 등).
        if (injectedObstacles != null && injectedObstacles.Count > 0)
        {
            buildings = new List<Building>();
            foreach (Vector4 o in injectedObstacles)
                buildings.Add(new Building { center = new Vector2(o.x, o.z), radius = o.w });
            return;
        }

        if (buildingCache == null) buildingCache = new Dictionary<Vector3, List<Building>>();
        if (buildingCache.TryGetValue(center, out buildings)) return;

        buildings = new List<Building>();
        Vector2 c2 = new Vector2(center.x, center.z);
        float near = radius + 18f;   // 이 마을 근처만(다른 마을 건물 제외)

        Transform[] allT = Object.FindObjectsOfType<Transform>();   // 활성 오브젝트만
        foreach (Transform t in allT)
        {
            bool isBld = t.name.StartsWith("Building_");
            bool isFountain = t.name.Contains("Fountain");
            if (!isBld && !isFountain) continue;

            // 이 오브젝트의 모든 렌더러를 감싼 전체 바운즈로 반경 계산
            Renderer[] rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) continue;
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

            Vector2 bc = new Vector2(b.center.x, b.center.z);
            if ((bc - c2).sqrMagnitude > near * near) continue;   // 근처 아니면 스킵

            float br = Mathf.Max(b.extents.x, b.extents.z);
            buildings.Add(new Building { center = bc, radius = br });
        }
        buildingCache[center] = buildings;
    }

    /// <summary>다음 목적지 결정: 확률로 건물 방문 또는 빈터 배회.</summary>
    private void Decide()
    {
        state = State.Walking;
        targetBuilding = -1;

        bool canVisit = buildings != null && buildings.Count > 0 && Random.value < visitChance;
        if (canVisit)
        {
            int idx = Random.Range(0, buildings.Count);
            Building b = buildings[idx];
            // 마을 중심(광장) 쪽에서 접근 — 건물 가장자리 밖 approachGap 앞에 선다
            Vector2 dirFromCenter = (b.center - new Vector2(center.x, center.z));
            if (dirFromCenter.sqrMagnitude < 0.01f) dirFromCenter = Random.insideUnitCircle.normalized;
            dirFromCenter.Normalize();
            Vector2 stand = b.center + dirFromCenter * (b.radius + npcRadius + approachGap);
            if (IsLand(stand.x, stand.y))   // 접근점이 뭍일 때만 방문(아니면 빈터 배회로)
            {
                target = new Vector3(stand.x, GroundY(stand.x, stand.y), stand.y);
                targetBuilding = idx;
                return;
            }
        }

        // 빈터: 반경 안에서 "건물 밖 + 뭍"인 랜덤 지점
        for (int tries = 0; tries < 12; tries++)
        {
            Vector2 rnd = Random.insideUnitCircle * radius;
            Vector2 pt = new Vector2(center.x + rnd.x, center.z + rnd.y);
            if (!InsideAnyBuilding(pt, -1) && IsLand(pt.x, pt.y))
            {
                target = new Vector3(pt.x, GroundY(pt.x, pt.y), pt.y);
                return;
            }
        }
        target = new Vector3(center.x, GroundY(center.x, center.z), center.z);   // 실패 시 중심
    }

    private void Update()
    {
        if (state == State.Interacting) { UpdateInteract(); return; }
        if (state == State.Pausing)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f) Decide();
            return;
        }

        // ── Walking: 목표로 이동 + 건물 밀어내기 + 물 회피 ──
        Vector3 pos = transform.position;
        Vector3 flatTarget = new Vector3(target.x, pos.y, target.z);
        Vector3 next = Vector3.MoveTowards(new Vector3(pos.x, pos.y, pos.z), flatTarget, moveSpeed * Time.deltaTime);
        next = PushOutOfBuildings(next);   // 건물 반경 밖으로 밀어내 관통 방지
        if (!IsLand(next.x, next.z)) { Decide(); return; }   // 물로 들어가려 하면 새 목적지
        next.y = GroundY(next.x, next.z);   // 지형 높이 따라가기(경사 접지)
        transform.position = next;
        baseY = next.y;

        // 진행 방향 바라보기
        Vector3 dir = flatTarget - new Vector3(pos.x, pos.y, pos.z); dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);

        // 도착 판정
        Vector3 d = new Vector3(transform.position.x - flatTarget.x, 0f, transform.position.z - flatTarget.z);
        if (d.sqrMagnitude <= arriveThreshold * arriveThreshold)
        {
            if (targetBuilding >= 0) { state = State.Interacting; timer = interactSeconds; }
            else { state = State.Pausing; timer = Random.Range(pauseMin, pauseMax); }
        }
    }

    /// <summary>건물 앞에서 그 건물을 향해 서서 까딱(상호작용 표현).</summary>
    private void UpdateInteract()
    {
        if (targetBuilding >= 0 && targetBuilding < buildings.Count)
        {
            Vector2 bc = buildings[targetBuilding].center;
            Vector3 look = new Vector3(bc.x - transform.position.x, 0f, bc.y - transform.position.z);
            if (look.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), turnSpeed * Time.deltaTime);
        }
        // 까딱거림(제자리 위아래)
        float bob = Mathf.Abs(Mathf.Sin(Time.time * bobSpeed)) * bobHeight;
        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, baseY + bob, p.z);

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            transform.position = new Vector3(p.x, baseY, p.z);
            Decide();
        }
    }

    /// <summary>위치가 어떤 건물 반경 안이면 그 경계 밖으로 민 좌표를 돌려준다.</summary>
    private Vector3 PushOutOfBuildings(Vector3 worldPos)
    {
        if (buildings == null) return worldPos;
        Vector2 p = new Vector2(worldPos.x, worldPos.z);
        for (int i = 0; i < buildings.Count; i++)
        {
            Building b = buildings[i];
            float rr = b.radius + npcRadius;
            Vector2 diff = p - b.center;
            float sq = diff.sqrMagnitude;
            if (sq < rr * rr && sq > 0.0001f)
                p = b.center + diff.normalized * rr;   // 경계로 밀어냄
        }
        return new Vector3(p.x, groundY, p.y);
    }

    private bool InsideAnyBuilding(Vector2 p, int except)
    {
        if (buildings == null) return false;
        for (int i = 0; i < buildings.Count; i++)
        {
            if (i == except) continue;
            float rr = buildings[i].radius + npcRadius;
            if ((p - buildings[i].center).sqrMagnitude < rr * rr) return true;
        }
        return false;
    }
}
