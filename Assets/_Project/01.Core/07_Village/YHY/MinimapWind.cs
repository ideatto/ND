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

    private readonly List<Source> sources = new List<Source>();
    private string curSeason = "";
    private Vector2 areaMin, areaMax;
    private float zPlane, eps;
    private bool ready;

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
        ready = true;
        return true;
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
        return sum;
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

    /// <summary>맵 중앙 근처에 이벤트 주입(디버그 편의). kind: fire/war=저기압, meteor=고기압.</summary>
    public void DropEventAtCenter(bool highPressure, float strengthAbs, float radiusFactor, float life)
    {
        Vector2 c = (areaMin + areaMax) * 0.5f;
        Vector2 jitter = Random.insideUnitCircle * (areaMax - areaMin).magnitude * 0.15f;
        float radius = (areaMax.x - areaMin.x) * radiusFactor;
        DropEvent(c + jitter, (highPressure ? 1f : -1f) * strengthAbs, radius, life);
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
