// =============================================================================
// MinimapWind — 기압 기반 바람 시뮬 (미니맵 격자)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 프로토타입 (프레임워크 독립, 표시/연출용)
//
// [원리] 기압원(pressure source)들의 가우시안 합 = 기압장 P(위치).
//        바람 = -∇P (고기압→저기압) 을 살짝 회전(소용돌이) 시킨 벡터.
//        - 계절 기본: currentSeasonId를 읽어 고/저기압 배치(여름↔겨울 반전) = 주풍
//        - 떠돌이: 천천히 이동하는 기압존 몇 개 = 계절 안 변화
//        - 이벤트: 전쟁/불(저기압, 수렴) · 메테오(고기압, 발산). 수명 지나면 소멸
//
// [비침습] 계절값은 읽기만 한다(프레임워크 미수정). 바람은 저장 안 하고 런타임 계산.
// [부착] 미니맵 렌더 루트(WorldMapRenderRootV2). MinimapGrid 필요.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>기압원 기반 바람 필드(계절 기본 + 떠돌이 + 이벤트).</summary>
public class MinimapWind : MonoBehaviour
{
    public enum SourceKind { Seasonal, Ambient, Event }

    private class Source
    {
        public Vector2 pos;
        public float strength;      // 현재 세기(+고기압 / -저기압)
        public float strength0;     // 최초 세기(이벤트 감쇠용)
        public float radius;
        public float age;
        public float life;          // 수명(초). <0 이면 영구
        public Vector2 drift;       // 이동(떠돌이)
        public SourceKind kind;
    }

    [SerializeField] private MinimapGrid grid;

    [Header("바람")]
    [Tooltip("소용돌이 각도(0=고→저 직진, 90=완전 순환/태풍)")]
    [SerializeField] private float swirlAngleDeg = 70f;
    [SerializeField] private float windScale = 1f;

    [Header("계절 기본 기압")]
    [SerializeField] private float seasonalStrength = 1.0f;

    [Header("떠도는 기압존")]
    [SerializeField] private int ambientCount = 3;
    [SerializeField] private float ambientStrength = 0.6f;
    [SerializeField] private float ambientDriftSpeed = 0.25f;

    [Header("지형(산) 기압")]
    [Tooltip("산 셀에 더할 고기압(바람이 부딪히면 돌아감). 0이면 지형 무시")]
    [SerializeField] private float mountainPressure = 2.6f;   // 산=고기압(계절풍과 맞먹는 +1.0대) → 바람이 산을 또렷이 우회
    [SerializeField] private int terrainBlur = 2;   // 산 기압을 부드럽게(그라디언트용)

    private readonly List<Source> sources = new List<Source>();
    private string curSeason = "";
    private Vector2 areaMin, areaMax;
    private float zPlane, eps;
    private bool ready;
    private float[,] terrainP;   // 셀별 지형 기압(산=고기압), 블러된 값

    private void Awake()
    {
        if (grid == null) grid = GetComponent<MinimapGrid>() ?? GetComponentInChildren<MinimapGrid>(true);
    }

    private void Start()
    {
        EnsureArea();
    }

    private bool EnsureArea()
    {
        if (ready) return true;
        if (grid == null) return false;
        Vector3 bl = grid.CellToWorld(0, 0);
        Vector3 tr = grid.CellToWorld(grid.Rows - 1, grid.Cols - 1);
        if (!grid.IsReady) return false;
        Vector2 half = grid.CellSize() * 0.5f;
        areaMin = (Vector2)bl - half;
        areaMax = (Vector2)tr + half;
        zPlane = bl.z;
        eps = (areaMax.x - areaMin.x) / Mathf.Max(1, grid.Cols) * 0.5f;
        RebuildSeasonal();
        SpawnAmbient();
        BuildTerrainPressure();
        ready = true;
        return true;
    }

    /// <summary>산 셀 = 고기압으로 지형 기압장을 만든다(블러로 부드럽게 → 바람이 산을 우회).</summary>
    public void BuildTerrainPressure()
    {
        if (grid == null || mountainPressure == 0f) { terrainP = null; return; }
        int R = grid.Rows, C = grid.Cols;
        var t = new float[R, C];
        for (int r = 0; r < R; r++)
            for (int c = 0; c < C; c++)
            {
                var cell = grid.GetCell(r, c);
                if (cell != null && cell.terrain == TerrainType.Mountain) t[r, c] = mountainPressure;
            }
        // 박스 블러(경계 부드럽게)
        for (int pass = 0; pass < terrainBlur; pass++)
        {
            var n = new float[R, C];
            for (int r = 0; r < R; r++)
                for (int c = 0; c < C; c++)
                {
                    float sum = 0f; int cnt = 0;
                    for (int dr = -1; dr <= 1; dr++)
                        for (int dc = -1; dc <= 1; dc++)
                        {
                            int rr = r + dr, cc = c + dc;
                            if (rr < 0 || rr >= R || cc < 0 || cc >= C) continue;
                            sum += t[rr, cc]; cnt++;
                        }
                    n[r, c] = sum / cnt;
                }
            t = n;
        }
        terrainP = t;
    }

    /// <summary>위치의 지형 기압(셀 격자 bilinear 샘플).</summary>
    private float SampleTerrainP(Vector2 p)
    {
        if (terrainP == null) return 0f;
        int R = grid.Rows, C = grid.Cols;
        float u = (p.x - areaMin.x) / Mathf.Max(1e-4f, areaMax.x - areaMin.x);
        float v = (p.y - areaMin.y) / Mathf.Max(1e-4f, areaMax.y - areaMin.y);
        float fx = u * C - 0.5f, fy = v * R - 0.5f;
        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float tx = fx - x0, ty = fy - y0;
        float p00 = TP(y0, x0), p10 = TP(y0, x0 + 1), p01 = TP(y0 + 1, x0), p11 = TP(y0 + 1, x0 + 1);
        return Mathf.Lerp(Mathf.Lerp(p00, p10, tx), Mathf.Lerp(p01, p11, tx), ty);
    }

    private float TP(int r, int c)
    {
        r = Mathf.Clamp(r, 0, grid.Rows - 1);
        c = Mathf.Clamp(c, 0, grid.Cols - 1);
        return terrainP[r, c];
    }

    private void Update()
    {
        if (!EnsureArea()) return;
        float dt = Time.deltaTime;

        // 계절 바뀌면 기본 기압 재구성(여름↔겨울 반전)
        if (ReadSeason() != curSeason) RebuildSeasonal();

        for (int i = sources.Count - 1; i >= 0; i--)
        {
            var s = sources[i];
            if (s.kind == SourceKind.Ambient)
            {
                s.pos += s.drift * dt;
                // 영역 밖이면 튕기기
                if (s.pos.x < areaMin.x || s.pos.x > areaMax.x) s.drift.x = -s.drift.x;
                if (s.pos.y < areaMin.y || s.pos.y > areaMax.y) s.drift.y = -s.drift.y;
                s.pos.x = Mathf.Clamp(s.pos.x, areaMin.x, areaMax.x);
                s.pos.y = Mathf.Clamp(s.pos.y, areaMin.y, areaMax.y);
            }
            else if (s.kind == SourceKind.Event)
            {
                s.age += dt;
                float t = s.life > 0f ? Mathf.Clamp01(1f - s.age / s.life) : 1f;
                s.strength = s.strength0 * t;   // 시간 지나며 감쇠
                if (s.life > 0f && s.age >= s.life) { sources.RemoveAt(i); }
            }
        }
    }

    // ------------------------------------------------------------------ 기압/바람 조회

    /// <summary>위치의 기압(기압원 가우시안 합).</summary>
    public float PressureAt(Vector2 p)
    {
        float sum = 0f;
        for (int i = 0; i < sources.Count; i++)
        {
            var s = sources[i];
            float dx = p.x - s.pos.x, dy = p.y - s.pos.y;
            float r2 = s.radius * s.radius;
            if (r2 <= 0f) continue;
            sum += s.strength * Mathf.Exp(-(dx * dx + dy * dy) / (2f * r2));
        }
        return sum + SampleTerrainP(p);   // + 산 등 지형 기압
    }

    /// <summary>위치의 바람 벡터(-∇P 회전).</summary>
    public Vector2 WindAt(Vector2 p)
    {
        if (!EnsureArea()) return Vector2.zero;
        float dPdx = (PressureAt(p + new Vector2(eps, 0f)) - PressureAt(p - new Vector2(eps, 0f))) / (2f * eps);
        float dPdy = (PressureAt(p + new Vector2(0f, eps)) - PressureAt(p - new Vector2(0f, eps))) / (2f * eps);
        Vector2 w = new Vector2(-dPdx, -dPdy);   // 고기압 → 저기압
        float a = swirlAngleDeg * Mathf.Deg2Rad;
        float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
        w = new Vector2(w.x * ca - w.y * sa, w.x * sa + w.y * ca);   // 소용돌이 회전
        return w * windScale;
    }

    public Vector2 WindAtCell(int row, int col) => WindAt((Vector2)grid.CellToWorld(row, col));
    public float PressureAtCell(int row, int col) => PressureAt((Vector2)grid.CellToWorld(row, col));

    /// <summary>이벤트로 기압원 하나 주입(수명 있음). strength>0=고기압(발산), <0=저기압(수렴).</summary>
    public void DropEvent(Vector2 pos, float strength, float radius, float life)
    {
        sources.Add(new Source
        {
            pos = pos, strength = strength, strength0 = strength,
            radius = radius, age = 0f, life = life, drift = Vector2.zero, kind = SourceKind.Event
        });
    }

    /// <summary>지정한 월드 위치에 이벤트 주입. highPressure=고기압(메테오)/false=저기압(큰불). radiusFactor는 맵 폭 대비 반경 비율.</summary>
    public void DropEventAt(Vector2 world, bool highPressure, float strengthAbs, float radiusFactor, float life)
    {
        if (!EnsureArea()) return;                              // 반경 계산에 area 필요
        float radius = (areaMax.x - areaMin.x) * radiusFactor;
        DropEvent(world, (highPressure ? 1f : -1f) * strengthAbs, radius, life);
    }

    /// <summary>맵 중앙 근처에 이벤트 주입(디버그 편의). kind: fire/war=저기압, meteor=고기압.</summary>
    public void DropEventAtCenter(bool highPressure, float strengthAbs, float radiusFactor, float life)
    {
        if (!EnsureArea()) return;
        Vector2 c = (areaMin + areaMax) * 0.5f;
        Vector2 jitter = Random.insideUnitCircle * (areaMax - areaMin).magnitude * 0.15f;   // 중앙 근처 랜덤
        DropEventAt(c + jitter, highPressure, strengthAbs, radiusFactor, life);
    }

    // ------------------------------------------------------------------ 계절/떠돌이 구성

    private static string ReadSeason()
    {
        var fr = ND.Framework.FrameworkRoot.Instance;
        var s = fr != null && fr.CurrentSaveData != null && fr.CurrentSaveData.world != null
            ? fr.CurrentSaveData.world.currentSeasonId : null;
        return string.IsNullOrEmpty(s) ? "summer" : s;
    }

    /// <summary>계절 기본 기압 재구성. 여름=남고북저 → 주풍 북향, 겨울=반전.</summary>
    private void RebuildSeasonal()
    {
        curSeason = ReadSeason();
        sources.RemoveAll(s => s.kind == SourceKind.Seasonal);

        Vector2 c = (areaMin + areaMax) * 0.5f;
        float h = areaMax.y - areaMin.y;
        Vector2 bottom = new Vector2(c.x, areaMin.y + h * 0.15f);
        Vector2 top = new Vector2(c.x, areaMax.y - h * 0.15f);
        float radius = (areaMax.x - areaMin.x) * 0.7f;

        // dir=+1: 아래 고기압(주풍 북향), dir=-1(겨울): 반전
        float dir = curSeason == "winter" ? -1f : 1f;
        float mag = (curSeason == "spring" || curSeason == "autumn" || curSeason == "fall")
            ? seasonalStrength * 0.4f : seasonalStrength;

        AddSource(bottom, +mag * dir, radius, SourceKind.Seasonal);
        AddSource(top, -mag * dir, radius, SourceKind.Seasonal);
    }

    private void SpawnAmbient()
    {
        sources.RemoveAll(s => s.kind == SourceKind.Ambient);
        for (int i = 0; i < ambientCount; i++)
        {
            Vector2 pos = new Vector2(
                Random.Range(areaMin.x, areaMax.x),
                Random.Range(areaMin.y, areaMax.y));
            float sign = Random.value > 0.5f ? 1f : -1f;
            float str = sign * ambientStrength * Random.Range(0.6f, 1f);
            float radius = (areaMax.x - areaMin.x) * Random.Range(0.18f, 0.35f);
            Vector2 drift = Random.insideUnitCircle.normalized * ambientDriftSpeed;
            var s = new Source { pos = pos, strength = str, strength0 = str, radius = radius, life = -1f, drift = drift, kind = SourceKind.Ambient };
            sources.Add(s);
        }
    }

    private void AddSource(Vector2 pos, float strength, float radius, SourceKind kind)
    {
        sources.Add(new Source { pos = pos, strength = strength, strength0 = strength, radius = radius, life = -1f, drift = Vector2.zero, kind = kind });
    }
}
