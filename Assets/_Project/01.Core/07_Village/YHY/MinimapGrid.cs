// =============================================================================
// MinimapGrid — 미니맵을 기준으로 격자(그리드)를 나눠 좌표를 생성한다
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 프로토타입 — 프레임워크 독립, 자체 생성
//
// [역할] 미니맵 배경(맵 아트)의 실제 bounds를 cols×rows 격자로 나누고,
//        "셀(row,col) ↔ 월드좌표" 양방향 변환을 제공한다. 이 좌표를 기준으로
//        날씨 이벤트·상단(캐러밴) 이동 등을 얹는다.
//
// [기준] 배경 스프라이트(렌더 루트 안 가장 큰 SpriteRenderer)의 bounds를 격자 영역으로
//        삼으므로, 렌더 루트를 어느 위치에 두든(프리팹 드롭) 좌표가 맞는다.
//
// [오버레이] drawOverlay=true면 격자선을 LineRenderer로 그려 미니맵에서 눈으로 보이게 한다.
//
// [부착] 미니맵 렌더 루트(WorldMapRenderRootV2)에 붙인다.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>미니맵 배경을 격자로 나눠 셀↔월드좌표 변환을 제공한다(프로토타입).</summary>
public class MinimapGrid : MonoBehaviour
{
    [Header("격자 크기")]
    [SerializeField] private int cols = 24;   // 가로 셀 수
    [SerializeField] private int rows = 16;   // 세로 셀 수

    [Header("영역 기준")]
    [SerializeField] private Transform renderRoot;   // 배경 탐색 범위(비면 자기 자신)

    [Header("오버레이(격자선)")]
    [SerializeField] private bool drawOverlay = true;
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private float lineWidth = 0.03f;
    [SerializeField] private int sortingOrder = 5;   // 배경(0)보다 위, 마을(10)보다 아래

    [Header("셀 정보(땅정보)")]
    // 저장/편집용 flat 리스트(Unity는 2D 배열을 직렬화 못 하므로). 각 셀에 row,col,terrain 포함.
    [SerializeField] private List<MinimapCell> savedCells = new List<MinimapCell>();

    public int Cols => cols;
    public int Rows => rows;
    public int CellCount => cols * rows;
    public bool IsReady { get; private set; }
    public bool CellsBuilt { get; private set; }

    private Bounds area;          // 격자 영역(배경 bounds)
    private Transform overlayRoot;
    private MinimapCell[,] cells; // 런타임 빠른 접근용 2D 배열

    private void Awake()
    {
        if (renderRoot == null) renderRoot = transform;
    }

    private void Start()
    {
        if (!EnsureArea()) return;
        BuildCells();
        if (drawOverlay) BuildOverlay();
    }

    // ------------------------------------------------------------------ 좌표 변환

    /// <summary>셀(row,col) 중심의 월드 좌표. 범위를 벗어난 인덱스는 clamp.</summary>
    public Vector3 CellToWorld(int row, int col)
    {
        EnsureArea();
        col = Mathf.Clamp(col, 0, cols - 1);
        row = Mathf.Clamp(row, 0, rows - 1);
        float cw = area.size.x / cols;
        float ch = area.size.y / rows;
        float x = area.min.x + (col + 0.5f) * cw;
        float y = area.min.y + (row + 0.5f) * ch;
        return new Vector3(x, y, area.center.z);
    }

    /// <summary>월드 좌표 → 셀(row,col). 반환값은 영역 안이면 true.</summary>
    public bool WorldToCell(Vector3 world, out int row, out int col)
    {
        EnsureArea();
        float u = (world.x - area.min.x) / area.size.x;
        float v = (world.y - area.min.y) / area.size.y;
        col = Mathf.FloorToInt(u * cols);
        row = Mathf.FloorToInt(v * rows);
        bool inside = col >= 0 && col < cols && row >= 0 && row < rows;
        col = Mathf.Clamp(col, 0, cols - 1);
        row = Mathf.Clamp(row, 0, rows - 1);
        return inside;
    }

    /// <summary>셀 하나의 크기(월드 단위, 가로·세로).</summary>
    public Vector2 CellSize()
    {
        EnsureArea();
        return new Vector2(area.size.x / cols, area.size.y / rows);
    }

    // ------------------------------------------------------------------ 셀 정보(땅정보)

    /// <summary>
    /// 셀 2D 배열을 구성한다(좌표 채우고, 저장된 terrain이 있으면 반영).
    /// Unity가 [,]를 직렬화 못 하므로 저장은 savedCells(flat)에, 런타임 접근은 cells[,]에 둔다.
    /// </summary>
    public void BuildCells()
    {
        if (!EnsureArea()) return;
        cells = new MinimapCell[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                cells[r, c] = new MinimapCell(r, c, CellToWorld(r, c));

        // 저장된 땅정보 반영
        if (savedCells != null)
            foreach (var s in savedCells)
                if (s != null && InRange(s.row, s.col))
                    cells[s.row, s.col].terrain = s.terrain;

        CellsBuilt = true;
    }

    /// <summary>셀(row,col) 반환(없으면 null). 범위를 벗어나면 null.</summary>
    public MinimapCell GetCell(int row, int col)
    {
        if (!CellsBuilt) BuildCells();
        return InRange(row, col) ? cells[row, col] : null;
    }

    /// <summary>월드 좌표가 속한 셀을 반환. 영역 밖이면 false.</summary>
    public bool TryGetCellAtWorld(Vector3 world, out MinimapCell cell)
    {
        cell = null;
        int row, col;
        bool inside = WorldToCell(world, out row, out col);
        cell = GetCell(row, col);
        return inside && cell != null;
    }

    /// <summary>셀의 땅정보를 설정하고 저장 리스트에도 반영한다.</summary>
    public void SetTerrain(int row, int col, TerrainType terrain)
    {
        var cell = GetCell(row, col);
        if (cell == null) return;
        cell.terrain = terrain;
        UpsertSaved(cell);
    }

    /// <summary>모든 셀을 순회(읽기용).</summary>
    public IEnumerable<MinimapCell> AllCells()
    {
        if (!CellsBuilt) BuildCells();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                yield return cells[r, c];
    }

    /// <summary>현재 cells[,]의 terrain을 저장 리스트(savedCells)로 평탄화한다(영속화 대비).</summary>
    public void FlushToSaved()
    {
        if (!CellsBuilt) return;
        savedCells = new List<MinimapCell>(rows * cols);
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                savedCells.Add(cells[r, c]);
    }

    private bool InRange(int row, int col) => row >= 0 && row < rows && col >= 0 && col < cols;

    private void UpsertSaved(MinimapCell cell)
    {
        if (savedCells == null) savedCells = new List<MinimapCell>();
        for (int i = 0; i < savedCells.Count; i++)
            if (savedCells[i] != null && savedCells[i].row == cell.row && savedCells[i].col == cell.col)
            { savedCells[i] = cell; return; }
        savedCells.Add(cell);
    }

    // ------------------------------------------------------------------ 영역/오버레이

    /// <summary>배경 스프라이트 bounds를 격자 영역으로 1회 확보한다.</summary>
    private bool EnsureArea()
    {
        if (IsReady) return true;
        Transform root = renderRoot != null ? renderRoot : transform;

        SpriteRenderer bg = null; float best = -1f;
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null || sr.sprite == null) continue;
            Vector3 s = sr.bounds.size;
            float a = s.x * s.y;
            if (a > best) { best = a; bg = sr; }
        }
        if (bg == null) return false;   // 배경 아직 없음 — 다음 기회에
        area = bg.bounds;
        IsReady = true;
        return true;
    }

    /// <summary>격자선을 LineRenderer로 그려 미니맵에 표시한다.</summary>
    private void BuildOverlay()
    {
        if (overlayRoot != null) Destroy(overlayRoot.gameObject);
        var go = new GameObject("GridOverlay");
        overlayRoot = go.transform;
        overlayRoot.SetParent(renderRoot, false);

        float z = area.center.z;
        // 세로선 (cols+1개)
        for (int c = 0; c <= cols; c++)
        {
            float x = area.min.x + area.size.x * c / cols;
            AddLine(new Vector3(x, area.min.y, z), new Vector3(x, area.max.y, z), "V" + c);
        }
        // 가로선 (rows+1개)
        for (int r = 0; r <= rows; r++)
        {
            float y = area.min.y + area.size.y * r / rows;
            AddLine(new Vector3(area.min.x, y, z), new Vector3(area.max.x, y, z), "H" + r);
        }
    }

    private void AddLine(Vector3 a, Vector3 b, string name)
    {
        var go = new GameObject("Line_" + name);
        go.transform.SetParent(overlayRoot, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.widthMultiplier = lineWidth;
        lr.numCapVertices = 0;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = lineColor;
        lr.sortingOrder = sortingOrder;
    }

    /// <summary>런타임에 오버레이 표시를 껐다 켠다.</summary>
    public void SetOverlayVisible(bool visible)
    {
        if (overlayRoot != null) overlayRoot.gameObject.SetActive(visible);
    }
}
