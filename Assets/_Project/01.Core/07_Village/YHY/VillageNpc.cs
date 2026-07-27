// =============================================================================
// VillageNpc — 욕구(니즈) 기반으로 건물을 방문하는 마을 NPC
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 숨은 스탯(허기·체력·심심)을 가진 NPC. 스탯은 시간에 따라 줄고,
//        가장 부족한 스탯을 채워주는 건물을 찾아가 상호작용(까딱)하면 회복된다.
//        랜덤 배회가 아니라 "배고프면 빵집, 지치면 집" 처럼 의도가 보이는 행동.
//
// [경계] 스탯은 저장 안 함(SaveData 무관). 빌리지 씬 안에서만 도는 독립 로직.
//
// [흐름] Decide(가장 낮은 스탯의 건물 선택) → Walking(A* 이동) →
//        Interacting(건물 앞 까딱 + 해당 스탯 회복) → 다시 Decide.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>욕구 기반으로 건물을 방문하며 스탯을 채우는 프로토타입 NPC.</summary>
public class VillageNpc : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float arriveThreshold = 0.05f;

    [Header("상호작용")]
    [SerializeField] private float interactSeconds = 2f;
    [SerializeField] private float bobHeight = 0.25f;
    [SerializeField] private float bobSpeed = 8f;

    [Header("욕구(숨김)")]
    [SerializeField] private float decayPerSecond = 3f;   // 초당 스탯 감소량
    [SerializeField] private float startMin = 40f;        // 시작 스탯 랜덤 하한
    [SerializeField] private float startMax = 90f;        // 시작 스탯 랜덤 상한

    private enum State { Walking, Interacting }
    private State state = State.Walking;

    private VillageGrid grid;
    private List<PlaceableBuilding> buildings;
    private readonly List<Vector2Int> path = new List<Vector2Int>();
    private int pathIndex;
    private float baseY;
    private float interactTimer;
    private Vector3 faceTarget;
    private bool ready;

    // 숨은 스탯 (0~100). 낮을수록 급함.
    private readonly Dictionary<NeedType, float> needs = new Dictionary<NeedType, float>();
    private NeedType currentGoal;                 // 지금 채우러 가는 욕구
    private BuildingNeedProvider goalBuilding;    // 그 욕구를 채워줄 목표 건물

    /// <summary>그리드와 건물 목록을 주입해 활성화한다(배치 컨트롤러가 호출).</summary>
    public void Init(VillageGrid villageGrid, List<PlaceableBuilding> villageBuildings)
    {
        grid = villageGrid;
        buildings = villageBuildings;
        ready = grid != null;
        baseY = transform.position.y;

        // 스탯 랜덤 초기화 (NPC마다 다른 욕구부터 급하게)
        needs[NeedType.Hunger] = Random.Range(startMin, startMax);
        needs[NeedType.Energy] = Random.Range(startMin, startMax);
        needs[NeedType.Fun] = Random.Range(startMin, startMax);

        SnapToNearestFreeCell();
        Decide();
    }

    private void Update()
    {
        if (!ready) return;

        DecayNeeds();   // 시간에 따라 스탯 감소

        if (state == State.Interacting) { UpdateInteract(); return; }

        // ── Walking ──
        if (pathIndex >= path.Count) { EnterInteract(); return; }

        Vector2Int cell = path[pathIndex];
        Vector3 target = grid.CellCenter(cell.x, cell.y);
        Vector3 pos = transform.position;
        target.y = baseY;

        transform.position = Vector3.MoveTowards(pos, target, moveSpeed * Time.deltaTime);

        Vector3 dir = target - pos; dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);

        if ((transform.position - target).sqrMagnitude <= arriveThreshold * arriveThreshold)
            pathIndex++;
    }

    /// <summary>모든 스탯을 시간에 비례해 줄인다(0 밑으론 안 감).</summary>
    private void DecayNeeds()
    {
        float d = decayPerSecond * Time.deltaTime;
        needs[NeedType.Hunger] = Mathf.Max(0f, needs[NeedType.Hunger] - d);
        needs[NeedType.Energy] = Mathf.Max(0f, needs[NeedType.Energy] - d);
        needs[NeedType.Fun] = Mathf.Max(0f, needs[NeedType.Fun] - d);
    }

    /// <summary>
    /// 가장 부족한 스탯을 정하고, 그 스탯을 채워줄 건물을 찾아 경로를 만든다.
    /// 해당 건물이 없거나 경로 실패 시 랜덤 배회로 폴백.
    /// </summary>
    private void Decide()
    {
        path.Clear();
        pathIndex = 0;
        if (grid == null) return;

        // "채워줄 건물이 실제로 있는 스탯" 중 가장 낮은 것을 목표로 (없는 건물로 향해 빈터 배회하는 것 방지)
        currentGoal = LowestSatisfiableNeed(out goalBuilding);

        grid.WorldToCell(transform.position, out int sx, out int sz);

        if (goalBuilding != null)
        {
            PlaceableBuilding pb = goalBuilding.GetComponent<PlaceableBuilding>();
            if (pb != null)
            {
                pb.GetRotatedCells(out int bsx, out int bsz);
                Vector3 corner = pb.transform.position - new Vector3(bsx * 0.5f, 0f, bsz * 0.5f) * VillageGrid.CellSize;
                grid.WorldToCell(corner, out int bx, out int bz);

                if (grid.TryGetAdjacentFreeCell(bx, bz, bsx, bsz, n => Random.Range(0, n), out int tx, out int tz))
                {
                    var found = grid.FindPath(sx, sz, tx, tz);
                    if (found != null && found.Count > 0)
                    {
                        path.AddRange(found);
                        faceTarget = pb.transform.position;
                        return;
                    }
                }
            }
        }

        // 폴백: 랜덤 빈 칸 배회(목표 건물 못 찾음)
        goalBuilding = null;
        if (grid.TryGetRandomFreeCell(n => Random.Range(0, n), out int rx, out int rz))
        {
            var f = grid.FindPath(sx, sz, rx, rz);
            if (f != null) path.AddRange(f);
            faceTarget = grid.CellCenter(rx, rz);
        }
    }

    /// <summary>
    /// "채워줄 건물이 마을에 있는" 스탯들 중 가장 낮은 것을 고르고, 그 건물도 함께 반환한다.
    /// 어떤 스탯도 채울 건물이 없으면 building=null(→ 호출부에서 배회 처리).
    /// </summary>
    private NeedType LowestSatisfiableNeed(out BuildingNeedProvider building)
    {
        NeedType best = NeedType.Hunger;
        float min = float.MaxValue;
        building = null;

        foreach (NeedType need in new[] { NeedType.Hunger, NeedType.Energy, NeedType.Fun })
        {
            BuildingNeedProvider b = FindBuildingFor(need);
            if (b == null) continue;                    // 이 스탯 채울 건물 없음 → 후보에서 제외
            if (needs[need] < min) { min = needs[need]; best = need; building = b; }
        }
        return best;
    }

    /// <summary>해당 욕구를 채워주는 건물 중 하나를 고른다(여러 개면 랜덤). 없으면 null.</summary>
    private BuildingNeedProvider FindBuildingFor(NeedType need)
    {
        var matches = new List<BuildingNeedProvider>();
        if (buildings != null)
            foreach (PlaceableBuilding b in buildings)
            {
                if (b == null) continue;
                var provider = b.GetComponent<BuildingNeedProvider>();
                if (provider != null && provider.satisfies == need) matches.Add(provider);
            }
        if (matches.Count == 0) return null;
        return matches[Random.Range(0, matches.Count)];
    }

    private void EnterInteract()
    {
        state = State.Interacting;
        // 건물 앞이면 정식 상호작용 시간, 배회(빈터)면 잠깐만 멈췄다 바로 다음 목적지.
        interactTimer = (goalBuilding != null) ? interactSeconds : 0.2f;
    }

    /// <summary>건물 앞이면 까딱거리며 상호작용(끝나면 욕구 회복). 배회(빈터)면 까딱 없이 잠깐 대기.</summary>
    private void UpdateInteract()
    {
        bool atBuilding = goalBuilding != null;

        // 건물 쪽 바라보기(배회면 이동 방향 유지)
        Vector3 look = faceTarget - transform.position; look.y = 0f;
        if (atBuilding && look.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 8f * Time.deltaTime);

        // 까딱거림은 "건물 앞"에서만. 빈터에선 가만히 서 있는다.
        var p = transform.position;
        if (atBuilding)
        {
            float bob = Mathf.Abs(Mathf.Sin(Time.time * bobSpeed)) * bobHeight;
            transform.position = new Vector3(p.x, baseY + bob, p.z);
        }

        interactTimer -= Time.deltaTime;
        if (interactTimer <= 0f)
        {
            transform.position = new Vector3(p.x, baseY, p.z);

            // 건물 앞이었으면 그 욕구를 회복 (배회면 회복 없음)
            if (atBuilding && needs.ContainsKey(goalBuilding.satisfies))
                needs[goalBuilding.satisfies] = Mathf.Min(100f, needs[goalBuilding.satisfies] + goalBuilding.restoreAmount);

            state = State.Walking;
            Decide();
        }
    }

    private void SnapToNearestFreeCell()
    {
        if (grid == null) return;
        grid.WorldToCell(transform.position, out int cx, out int cz);
        if (!grid.IsFree(cx, cz))
            grid.FindNearestFree(cx, cz, 1, 1, null, out cx, out cz);
        Vector3 c = grid.CellCenter(cx, cz);
        transform.position = new Vector3(c.x, baseY, c.z);
    }

    // ── 디버그용 (숨은 스탯 확인. 화면엔 안 뜸) ──
    /// <summary>현재 스탯·목표를 문자열로(에디터/리플렉션 확인용).</summary>
    public string DebugStatus()
    {
        return string.Format("허기{0:F0} 체력{1:F0} 심심{2:F0} | 목표={3}({4})",
            needs[NeedType.Hunger], needs[NeedType.Energy], needs[NeedType.Fun],
            currentGoal, goalBuilding != null ? goalBuilding.name : "배회");
    }
}
