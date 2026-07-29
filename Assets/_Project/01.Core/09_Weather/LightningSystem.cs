// =============================================================================
// LightningSystem — 먹구름 아래 번개 → 숲/풀이면 큰불 → 저기압(기압 변화)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 날씨 시스템 — 월드 이벤트(캐러밴 무관)
//
// [역할] 주기마다 먹구름(세기 충분) 아래 셀 중 하나에 '번개'를 친다(결정론 hash).
//        번개 맞은 셀이 가연 지형(숲/풀)이면 → 그 자리에 '큰불' + 바람에 저기압 이벤트를
//        주입(wind.DropEventAt)해서 기압이 변하게 한다. 비가연 지형이면 섬광만.
//
// [연결] 불의 저기압 → 바람이 불로 수렴 → 구름이 끌림 → (동적 기압 = 정적 뭉침 완화).
//        날씨↔지형↔불↔기압이 서로 영향 주는 살아있는 루프.
//
// [부착] V2 렌더 루트(WorldMapRenderRootV2). MinimapGrid·MinimapClouds·MinimapWind 필요(자동 탐색).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>먹구름 아래 번개 → 가연지형(숲/풀)에 큰불 + 저기압 발생.</summary>
public class LightningSystem : MonoBehaviour
{
    [Header("참조 (비면 자동 탐색)")]
    [SerializeField] private Transform renderRoot;
    [SerializeField] private MinimapGrid grid;
    [SerializeField] private MinimapClouds clouds;
    [SerializeField] private MinimapWind wind;

    [Header("번개")]
    [Tooltip("이 주기(초)마다 번개 시도")]
    [SerializeField] private float strikeInterval = 2.5f;
    [Range(0f, 1f)]
    [Tooltip("시도당 번개 발동 확률")]
    [SerializeField] private float strikeChance = 0.6f;
    [Tooltip("이 세기(강수량×크기) 이상 먹구름 아래에서만 번개")]
    [SerializeField] private float minStrikeIntensity = 0.4f;
    [SerializeField] private uint worldSeed = 777u;   // 결정론 씨앗

    [Header("큰불")]
    [Tooltip("불 지속(초). 이 동안 저기압도 유지")]
    [SerializeField] private float fireDuration = 8f;
    [Tooltip("불의 저기압 세기(절댓값)")]
    [SerializeField] private float firePressureStrength = 1.6f;
    [Tooltip("불 저기압 반경(맵 폭 대비)")]
    [SerializeField] private float fireRadiusFactor = 0.12f;

    private Transform fxRoot;
    private float timer;
    private long lightningStep;

    private class Fx { public Transform t; public SpriteRenderer sr; public float age; public float life; public bool isFire; }
    private readonly List<Fx> fxs = new List<Fx>();
    private readonly List<MinimapCell> candidates = new List<MinimapCell>();

    private static readonly Color BoltCol = new Color(1f, 1f, 0.75f);   // 번개 섬광(밝은 노랑흰)
    private static readonly Color FireCol = new Color(1f, 0.45f, 0.1f);  // 불(주황)

    private void Awake()
    {
        if (renderRoot == null) renderRoot = transform;
        if (grid == null) grid = GetComponentInChildren<MinimapGrid>(true);
        if (clouds == null) clouds = GetComponentInChildren<MinimapClouds>(true);
        if (wind == null) wind = GetComponentInChildren<MinimapWind>(true);
    }

    private void Update()
    {
        // 핫 리로드 대비 재탐색
        if (renderRoot == null) renderRoot = transform;
        if (grid == null) grid = GetComponentInChildren<MinimapGrid>(true);
        if (clouds == null) clouds = GetComponentInChildren<MinimapClouds>(true);
        if (wind == null) wind = GetComponentInChildren<MinimapWind>(true);
        if (grid == null || clouds == null || wind == null) return;
        if (fxRoot == null) { fxRoot = new GameObject("LightningFx").transform; fxRoot.SetParent(renderRoot, false); }

        float dt = Time.deltaTime;

        // 번개 주기
        timer -= dt;
        if (timer <= 0f) { timer = Mathf.Max(0.2f, strikeInterval); TryStrike(); }

        // Fx 갱신(섬광 페이드 / 불 깜빡임·소멸)
        for (int i = fxs.Count - 1; i >= 0; i--)
        {
            var f = fxs[i];
            f.age += dt;
            if (f.t == null || f.age >= f.life) { if (f.t != null) Destroy(f.t.gameObject); fxs.RemoveAt(i); continue; }
            if (f.isFire)
            {
                float k = 1f - f.age / f.life;                       // 1→0 (끝에 사그라듦)
                float flick = 0.8f + 0.2f * Mathf.Sin(f.age * 22f);  // 깜빡임
                Color c = FireCol; c.a = Mathf.Clamp01(k * 1.5f) * flick;
                f.sr.color = c;
                float sc = (0.9f + 0.15f * Mathf.Sin(f.age * 18f)) * Mathf.Lerp(0.6f, 1f, k);
                f.t.localScale = new Vector3(sc, sc, 1f);
            }
            else
            {
                float a = 1f - f.age / f.life;                        // 섬광 빠르게 사라짐
                Color c = BoltCol; c.a = a;
                f.sr.color = c;
            }
        }
    }

    /// <summary>먹구름(세기 충분) 아래 셀 중 하나에 번개. 결정론 hash로 발동/위치 결정.</summary>
    private void TryStrike()
    {
        candidates.Clear();
        for (int r = 0; r < grid.Rows; r++)
            for (int c = 0; c < grid.Cols; c++)
            {
                var cell = grid.GetCell(r, c);
                if (cell == null) continue;
                if (clouds.RainIntensityAt(grid.CellToWorld(r, c)) >= minStrikeIntensity)
                    candidates.Add(cell);
            }
        if (candidates.Count == 0) return;

        var rng = new DetRng(DetRng.Seed(worldSeed, lightningStep++, 0));
        if (rng.Value() >= strikeChance) return;                    // 이번엔 안 침

        MinimapCell target = candidates[rng.Range(0, candidates.Count)];
        Vector3 pos = grid.CellToWorld(target.row, target.col);

        SpawnFx(pos, false, 0.22f, 1.3f);                           // ⚡ 섬광

        if (IsFlammable(target.terrain))
        {
            SpawnFx(pos, true, fireDuration, 1.1f);                 // 🔥 불
            wind.DropEventAt(new Vector2(pos.x, pos.y), false, firePressureStrength, fireRadiusFactor, fireDuration);  // 저기압 주입
            Debug.Log("[번개] 큰불 발생! 셀(" + target.row + "," + target.col + ") " + target.terrain + " → 저기압 발생");
        }
        else
        {
            Debug.Log("[번개] 셀(" + target.row + "," + target.col + ") " + target.terrain + " (불 안 붙음)");
        }
    }

    /// <summary>가연 지형: 숲·풀.</summary>
    private static bool IsFlammable(TerrainType t) => t == TerrainType.Forest || t == TerrainType.Grass;

    private void SpawnFx(Vector3 pos, bool isFire, float life, float scale)
    {
        var go = new GameObject(isFire ? "Fire" : "Bolt");
        go.transform.SetParent(fxRoot, false);
        go.transform.position = new Vector3(pos.x, pos.y, pos.z);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Blob();
        sr.color = isFire ? FireCol : BoltCol;
        sr.sortingOrder = isFire ? 18 : 45;   // 불은 구름 아래쯤, 섬광은 최상단
        fxs.Add(new Fx { t = go.transform, sr = sr, age = 0f, life = life, isFire = isFire });
    }

    // 부드러운 원형 블롭 스프라이트(코드 베이킹)
    private static Sprite blob;
    private static Sprite Blob()
    {
        if (blob != null) return blob;
        int S = 32;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float nx = (x + 0.5f) / S * 2f - 1f, ny = (y + 0.5f) / S * 2f - 1f;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);   // smoothstep
                px[y * S + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px); tex.Apply();
        blob = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        return blob;
    }
}
