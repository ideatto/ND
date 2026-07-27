// =============================================================================
// VillageGrid — 마을 격자(셀 점유) 관리
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 마을 바닥을 정수 격자(1칸=1m)로 보고, "어느 칸이 찼는지"를 2D 배열로 관리한다.
//        건물 배치의 겹침 판정을 물리(OverlapBox) 대신 이 배열 조회로 처리한다.
//        → 물리보다 견고하고, 저장도 셀 좌표(int)로 가벼워진다.
//        → 나중에 NPC 길찾기(A*)의 "막힌 칸/빈 칸" 지도로 그대로 재사용 가능.
//
// [좌표 규칙] 월드 원점(0,0)을 격자 중심으로 본다. 마을이 width×height 칸이면
//        셀 x는 [-width/2 .. width/2), z도 동일. 월드좌표 → 셀은 Floor로 변환한다.
//        (셀 (cx,cz)의 중심 월드좌표는 (cx+0.5, 0, cz+0.5))
//
// [비-MonoBehaviour] 순수 C# 로직. 씬 오브젝트(컨트롤러)가 인스턴스를 들고 쓴다.
// =============================================================================

using UnityEngine;

/// <summary>마을 격자의 칸 점유 상태를 관리한다(1칸=1m, 순수 로직).</summary>
public class VillageGrid
{
    public const float CellSize = 1.25f;   // 한 칸의 월드 크기(m). 건물은 2×2칸(=2.5m)을 차지한다.

    private readonly int width;         // 가로 칸 수
    private readonly int height;        // 세로 칸 수
    private readonly int minX;          // 셀 x 최소값(음수, 격자 중심 기준)
    private readonly int minZ;          // 셀 z 최소값
    private readonly object[,] occupant; // 각 칸을 차지한 주인(건물 Transform 등). null=빈 칸

    /// <summary>width×height 칸 격자를 만든다(원점을 격자 중심으로).</summary>
    public VillageGrid(int width, int height)
    {
        this.width = Mathf.Max(1, width);
        this.height = Mathf.Max(1, height);
        this.minX = -this.width / 2;
        this.minZ = -this.height / 2;
        this.occupant = new object[this.width, this.height];
    }

    // ── 월드 ↔ 셀 변환 ─────────────────────────────────────────────
    /// <summary>월드좌표 → 셀 좌표(Floor 기준).</summary>
    public void WorldToCell(Vector3 world, out int cx, out int cz)
    {
        cx = Mathf.FloorToInt(world.x / CellSize);
        cz = Mathf.FloorToInt(world.z / CellSize);
    }

    /// <summary>
    /// 셀 좌표(왼쪽아래 기준) + 점유 칸 수 → 건물 피벗이 놓일 월드좌표(점유 영역의 중심, y=0).
    /// 피벗이 바닥-중심이므로 sizeX·sizeZ 영역의 한가운데로 맞춘다.
    /// </summary>
    public Vector3 CellToWorldCenter(int cx, int cz, int sizeX, int sizeZ)
    {
        float wx = (cx + sizeX * 0.5f) * CellSize;
        float wz = (cz + sizeZ * 0.5f) * CellSize;
        return new Vector3(wx, 0f, wz);
    }

    // ── 점유 조회/설정 ─────────────────────────────────────────────
    /// <summary>셀 좌표가 격자 범위 안인가.</summary>
    public bool InBounds(int cx, int cz)
    {
        return cx >= minX && cx < minX + width
            && cz >= minZ && cz < minZ + height;
    }

    /// <summary>해당 칸이 비었는가(범위 밖이면 false).</summary>
    public bool IsFree(int cx, int cz)
    {
        if (!InBounds(cx, cz)) return false;
        return occupant[cx - minX, cz - minZ] == null;
    }

    /// <summary>
    /// (cx,cz)를 왼쪽아래로 하는 sizeX×sizeZ 영역이 전부 비었는가.
    /// ignore(자기 자신)가 이미 차지한 칸은 "빈 것"으로 취급 → 자기 자리로의 이동 허용.
    /// </summary>
    public bool CanPlace(int cx, int cz, int sizeX, int sizeZ, object ignore = null)
    {
        for (int dx = 0; dx < sizeX; dx++)
            for (int dz = 0; dz < sizeZ; dz++)
            {
                int x = cx + dx, z = cz + dz;
                if (!InBounds(x, z)) return false;                 // 격자 밖
                object o = occupant[x - minX, z - minZ];
                if (o != null && o != ignore) return false;        // 남이 차지한 칸
            }
        return true;
    }

    /// <summary>owner가 차지하던 칸을 전부 비운다(이동/삭제 전에 호출).</summary>
    public void Clear(object owner)
    {
        if (owner == null) return;
        for (int x = 0; x < width; x++)
            for (int z = 0; z < height; z++)
                if (occupant[x, z] == owner) occupant[x, z] = null;
    }

    /// <summary>(cx,cz) 왼쪽아래로 sizeX×sizeZ 영역을 owner가 차지하게 표시.</summary>
    public void Occupy(int cx, int cz, int sizeX, int sizeZ, object owner)
    {
        for (int dx = 0; dx < sizeX; dx++)
            for (int dz = 0; dz < sizeZ; dz++)
            {
                int x = cx + dx, z = cz + dz;
                if (InBounds(x, z)) occupant[x - minX, z - minZ] = owner;
            }
    }

    /// <summary>
    /// (startX,startZ)에서 시작해 바깥으로 나선형 탐색하며, sizeX×sizeZ가 들어갈 첫 빈 자리를 찾는다.
    /// 찾으면 그 왼쪽아래 칸을 out으로 주고 true. 격자 전체가 차 있으면 false.
    /// 겹쳐서 시작한 건물을 근처 빈 칸으로 밀어낼 때 쓴다.
    /// </summary>
    public bool FindNearestFree(int startX, int startZ, int sizeX, int sizeZ, object ignore, out int outX, out int outZ)
    {
        // 반경 0부터 넓혀가며 링(ring)을 훑는다 — 가까운 칸부터 검사돼 "가장 가까운 빈 자리"가 나온다.
        int maxRadius = width + height;
        for (int r = 0; r <= maxRadius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;  // 링 테두리만
                    int cx = startX + dx, cz = startZ + dz;
                    if (CanPlace(cx, cz, sizeX, sizeZ, ignore))
                    {
                        outX = cx; outZ = cz;
                        return true;
                    }
                }
        }
        outX = startX; outZ = startZ;
        return false;
    }

    // ── NPC 길찾기용 ───────────────────────────────────────────────
    /// <summary>셀 하나의 중심 월드좌표(NPC가 그 칸 한가운데 서게). y=0.</summary>
    public Vector3 CellCenter(int cx, int cz)
    {
        return new Vector3((cx + 0.5f) * CellSize, 0f, (cz + 0.5f) * CellSize);
    }

    /// <summary>
    /// 건물 점유 영역(왼쪽아래 bx,bz + 크기 sizeX,sizeZ)의 바로 바깥 둘레에서 빈 칸 하나를 찾는다.
    /// NPC가 "건물 앞"에 서게 하는 용도. 없으면 false.
    /// </summary>
    public bool TryGetAdjacentFreeCell(int bx, int bz, int sizeX, int sizeZ,
        System.Func<int, int> randomInt, out int cx, out int cz)
    {
        // 둘레 칸(위/아래 한 줄 + 좌/우 한 줄)을 모아 무작위로 하나 고른다.
        var candidates = new System.Collections.Generic.List<Vector2Int>();
        for (int x = bx - 1; x <= bx + sizeX; x++)
        {
            if (IsFree(x, bz - 1)) candidates.Add(new Vector2Int(x, bz - 1));           // 아래 줄
            if (IsFree(x, bz + sizeZ)) candidates.Add(new Vector2Int(x, bz + sizeZ));   // 위 줄
        }
        for (int z = bz; z < bz + sizeZ; z++)
        {
            if (IsFree(bx - 1, z)) candidates.Add(new Vector2Int(bx - 1, z));           // 왼쪽 줄
            if (IsFree(bx + sizeX, z)) candidates.Add(new Vector2Int(bx + sizeX, z));   // 오른쪽 줄
        }
        if (candidates.Count == 0) { cx = 0; cz = 0; return false; }
        Vector2Int pick = candidates[randomInt(candidates.Count)];
        cx = pick.x; cz = pick.y;
        return true;
    }

    /// <summary>격자 안의 임의의 빈 칸 하나를 찾는다(NPC 배회 목적지용). 없으면 false.</summary>
    public bool TryGetRandomFreeCell(System.Func<int, int> randomInt, out int cx, out int cz)
    {
        // 몇 번 무작위 시도 후, 실패하면 전체 스캔(대부분 첫 시도에서 성공).
        for (int attempt = 0; attempt < 30; attempt++)
        {
            int x = minX + randomInt(width);
            int z = minZ + randomInt(height);
            if (IsFree(x, z)) { cx = x; cz = z; return true; }
        }
        for (int x = minX; x < minX + width; x++)
            for (int z = minZ; z < minZ + height; z++)
                if (IsFree(x, z)) { cx = x; cz = z; return true; }
        cx = 0; cz = 0;
        return false;
    }

    /// <summary>
    /// A* 길찾기: (sx,sz)에서 (tx,tz)까지 빈 칸만 밟는 최단 경로를 셀 목록으로 반환한다.
    /// 상하좌우 4방향 이동(대각선 금지 — 건물 모서리 관통 방지). 경로 없으면 null.
    /// 결과는 시작칸 제외, 목적지까지의 칸들.
    /// </summary>
    public System.Collections.Generic.List<Vector2Int> FindPath(int sx, int sz, int tx, int tz)
    {
        if (!IsFree(tx, tz) || !InBounds(sx, sz)) return null;
        if (sx == tx && sz == tz) return new System.Collections.Generic.List<Vector2Int>();

        var open = new System.Collections.Generic.List<Vector2Int>();       // 탐색 대기 칸
        var cameFrom = new System.Collections.Generic.Dictionary<Vector2Int, Vector2Int>();
        var gScore = new System.Collections.Generic.Dictionary<Vector2Int, int>();  // 시작~이 칸 실제 비용
        var closed = new System.Collections.Generic.HashSet<Vector2Int>();

        Vector2Int start = new Vector2Int(sx, sz);
        Vector2Int goal = new Vector2Int(tx, tz);
        open.Add(start);
        gScore[start] = 0;

        int[] dxs = { 1, -1, 0, 0 };
        int[] dzs = { 0, 0, 1, -1 };

        while (open.Count > 0)
        {
            // f = g + 휴리스틱(맨해튼 거리)이 가장 작은 칸 선택
            int bestIdx = 0;
            int bestF = int.MaxValue;
            for (int i = 0; i < open.Count; i++)
            {
                int g = gScore[open[i]];
                int h = Mathf.Abs(open[i].x - tx) + Mathf.Abs(open[i].y - tz);
                if (g + h < bestF) { bestF = g + h; bestIdx = i; }
            }
            Vector2Int cur = open[bestIdx];
            if (cur == goal) return Reconstruct(cameFrom, cur);

            open.RemoveAt(bestIdx);
            closed.Add(cur);

            for (int d = 0; d < 4; d++)
            {
                int nx = cur.x + dxs[d], nz = cur.y + dzs[d];
                if (!IsFree(nx, nz)) continue;                 // 건물칸·범위밖 = 못 감
                Vector2Int nb = new Vector2Int(nx, nz);
                if (closed.Contains(nb)) continue;

                int tentative = gScore[cur] + 1;
                if (!gScore.ContainsKey(nb) || tentative < gScore[nb])
                {
                    cameFrom[nb] = cur;
                    gScore[nb] = tentative;
                    if (!open.Contains(nb)) open.Add(nb);
                }
            }
        }
        return null;   // 목적지 도달 불가(막힘)
    }

    private static System.Collections.Generic.List<Vector2Int> Reconstruct(
        System.Collections.Generic.Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int cur)
    {
        var path = new System.Collections.Generic.List<Vector2Int>();
        while (cameFrom.ContainsKey(cur))
        {
            path.Add(cur);
            cur = cameFrom[cur];
        }
        path.Reverse();   // 시작→목적지 순서로
        return path;
    }
}
