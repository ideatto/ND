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

    [Header("재현성(결정론)")]
    [Tooltip("월드 씨앗. 같은 씨앗 → 항상 같은 바람. 구름이 자기 씨앗을 밀어넣어 동기화한다.")]
    [SerializeField] private uint worldSeed = 12345u;

    [Header("바람")]
    [Tooltip("소용돌이 각도(0=고→저 직진, 90=완전 순환/태풍)")]
    [SerializeField] private float swirlAngleDeg = 70f;
    [SerializeField] private float windScale = 1f;

    [Header("주풍(탁월풍) — 항상 부는 일정 바람")]
    [Tooltip("죽은 바람 지대(호수 위 등)를 없애고 구름을 한 방향으로 실어냄. 0이면 없음")]
    [SerializeField] private float prevailingStrength = 0.04f;
    [Tooltip("주풍 방향(도). 0=오른쪽(동), 90=위(북), 180=왼쪽(서), 270=아래(남)")]
    [SerializeField] private float prevailingAngleDeg = 90f;

    [Header("계절 기본 기압")]
    [SerializeField] private float seasonalStrength = 1.0f;

    [Header("떠도는 기압존")]
    [SerializeField] private int ambientCount = 3;
    [SerializeField] private float ambientStrength = 0.6f;
    [SerializeField] private float ambientDriftSpeed = 0.25f;

    [Header("지형별 기압 (서늘=고기압 +, 더운 땅=저기압 −)")]
    [Tooltip("산 = 강한 고기압(바람이 부딪히면 우회)")]
    [SerializeField] private float mountainPressure = 2.6f;
    [Tooltip("호수 = 서늘한 고기압(물→뭍으로 바람, 습기 운반). 강은 이 값의 40%")]
    [SerializeField] private float waterPressure = 0.8f;
    [Tooltip("숲 = 그늘·서늘, 약한 고기압")]
    [SerializeField] private float forestPressure = 0.3f;
    [Tooltip("평지 등 더운 땅 = 저기압(음수, 상승기류). 풀/논밭은 이보다 약하게 적용")]
    [SerializeField] private float warmLandPressure = -0.7f;
    [SerializeField] private int terrainBlur = 2;   // 지형 기압을 부드럽게(그라디언트용)

    private readonly List<Source> sources = new List<Source>();
    private string curSeason = "";
    private Vector2 areaMin, areaMax;
    private float zPlane, eps;
    private bool ready;
    private float[,] terrainP;   // 셀별 지형 기압(산=고기압), 블러된 값

    private int eventCounter;          // 이벤트 지터용 카운터(결정론 난수 축)
    private bool externallyDriven;     // true면 자체 Update 시간전진 안 함(구름이 고정스텝으로 몰아줌)

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
        // 핫 리로드로 기압원(sources, 비직렬화)이 비면 ready(bool)만 살아남아 바람이 주풍만 남는다 → sources 있을 때만 '준비됨'으로 보고, 비면 재구성.
        if (ready && sources.Count > 0) return true;
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

    /// <summary>모든 지형에 기압 성향을 부여해 지형 기압장을 만든다(블러로 부드럽게). 지형 경계에서 기압차 → 바람.</summary>
    public void BuildTerrainPressure()
    {
        if (grid == null) { terrainP = null; return; }
        int R = grid.Rows, C = grid.Cols;
        var t = new float[R, C];
        for (int r = 0; r < R; r++)
            for (int c = 0; c < C; c++)
            {
                var cell = grid.GetCell(r, c);
                if (cell != null) t[r, c] = TerrainPressure(cell.terrain);
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

    /// <summary>지형 종류별 기압 성향(+고기압 서늘 / −저기압 더운 땅).</summary>
    private float TerrainPressure(TerrainType terr)
    {
        switch (terr)
        {
            case TerrainType.Mountain: return mountainPressure;         // 산 = 강한 고기압(우회)
            case TerrainType.Water:    return waterPressure;            // 호수 = 서늘 고기압(물→뭍)
            case TerrainType.River:    return waterPressure * 0.4f;     // 강 = 얇은 물
            case TerrainType.Forest:   return forestPressure;           // 숲 = 약한 고기압
            case TerrainType.Plain:    return warmLandPressure;         // 평지 = 더운 저기압
            case TerrainType.Grass:    return warmLandPressure * 0.6f;  // 풀 = 약한 저기압
            case TerrainType.Farmland: return warmLandPressure * 0.5f;  // 논밭
            default:                   return 0f;                       // 다리·강변·구름(맵밖) = 중립
        }
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
        // 구름이 고정스텝(똑딱시계)으로 몰아줄 땐 자체 시간전진을 하지 않는다(중복/비결정 방지).
        // 구름이 꺼져 있을 때만 바람 자체가 실시간으로 흐른다(디버그 화살표 보기용).
        if (externallyDriven) return;
        StepSim(Time.deltaTime);
    }

    /// <summary>바람 소스를 dt만큼 전진(떠돌이 이동·이벤트 감쇠). 밖(구름)에서 고정스텝으로 호출 가능.</summary>
    public void StepSim(float dt)
    {
        if (!EnsureArea()) return;

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

    /// <summary>월드 씨앗 주입(구름이 자기 씨앗으로 바람도 동기화). 바뀌면 떠돌이 재구성.</summary>
    public void SetSeed(uint seed)
    {
        if (seed == worldSeed && ready) return;
        worldSeed = seed;
        if (ready) SpawnAmbient();   // 씨앗이 바뀌었으면 떠돌이 기압존을 새 씨앗으로 다시 배치
    }

    /// <summary>true면 자체 시간전진 정지(구름이 고정스텝으로 몰아줌). 구름 끄면 false로 되돌린다.</summary>
    public void SetExternallyDriven(bool value) => externallyDriven = value;

    /// <summary>바람 소스를 epoch(step 0) 상태로 리셋 — 떠돌이·계절 재시드(DetRng), 드리프트 누적 초기화, 이벤트 제거.
    /// 결정론 리플레이용: 같은 씨앗 → 항상 같은 시작 바람. 구름이 재생성될 때 호출(그 뒤 캐치업이 같이 리플레이).</summary>
    public void ResetSim()
    {
        if (!EnsureArea()) return;
        RebuildSeasonal();                                     // 계절 기압 재구성(결정론)
        SpawnAmbient();                                        // 떠돌이 재시드 → 드리프트 누적 초기화
        sources.RemoveAll(s => s.kind == SourceKind.Event);    // 이벤트(비결정 사용자 배치)는 리플레이에서 제외
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
        w *= windScale;

        // 주풍: 항상 부는 일정 바람을 더한다 → 죽은 바람 지대(호수 위) 제거, 구름을 한 방향으로 실어냄
        float pang = prevailingAngleDeg * Mathf.Deg2Rad;
        w += new Vector2(Mathf.Cos(pang), Mathf.Sin(pang)) * prevailingStrength;
        return w;
    }

    public Vector2 WindAtCell(int row, int col) => WindAt((Vector2)grid.CellToWorld(row, col));
    public float PressureAtCell(int row, int col) => PressureAt((Vector2)grid.CellToWorld(row, col));

    /// <summary>현재 계절(비면 summer). 구름 등 외부가 계절별 날씨 반응에 쓴다.</summary>
    public string Season => string.IsNullOrEmpty(curSeason) ? "summer" : curSeason;

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
        // 중앙 근처 랜덤(결정론): 이벤트마다 카운터를 올려 서로 다른, 그러나 재현 가능한 지터
        var rng = new DetRng(DetRng.Seed(worldSeed, eventCounter++, 200));
        float jang = rng.Range(0f, 2f * Mathf.PI);
        float jrad = Mathf.Sqrt(rng.Value());   // 원판 안 균일 분포
        Vector2 jitter = new Vector2(Mathf.Cos(jang), Mathf.Sin(jang)) * jrad * (areaMax - areaMin).magnitude * 0.15f;
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
        // 떠돌이 기압존을 '번호표'로 배치(같은 씨앗 → 항상 같은 떠돌이). salt 100 = ambient 용도.
        var rng = new DetRng(DetRng.Seed(worldSeed, 0, 100));
        for (int i = 0; i < ambientCount; i++)
        {
            Vector2 pos = new Vector2(
                rng.Range(areaMin.x, areaMax.x),
                rng.Range(areaMin.y, areaMax.y));
            float sign = rng.Value() > 0.5f ? 1f : -1f;
            float str = sign * ambientStrength * rng.Range(0.6f, 1f);
            float radius = (areaMax.x - areaMin.x) * rng.Range(0.18f, 0.35f);
            float dang = rng.Range(0f, 2f * Mathf.PI);   // 드리프트 방향(각도)
            Vector2 drift = new Vector2(Mathf.Cos(dang), Mathf.Sin(dang)) * ambientDriftSpeed;
            var s = new Source { pos = pos, strength = str, strength0 = str, radius = radius, life = -1f, drift = drift, kind = SourceKind.Ambient };
            sources.Add(s);
        }
    }

    private void AddSource(Vector2 pos, float strength, float radius, SourceKind kind)
    {
        sources.Add(new Source { pos = pos, strength = strength, strength0 = strength, radius = radius, life = -1f, drift = Vector2.zero, kind = kind });
    }
}
