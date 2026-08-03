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

    [Header("번개(날씨 스텝 = 게임시간 결정론)")]
    [Tooltip("이 '날씨 스텝' 주기마다 번개 시도(실시간 초 아님). 날씨 스텝=0.1게임초 → 25≈2.5게임초.")]
    [SerializeField] private int attemptEverySteps = 25;
    [Range(0f, 1f)]
    [Tooltip("시도당 번개 발동 확률")]
    [SerializeField] private float strikeChance = 0.6f;
    [Tooltip("이 세기(강수량×크기) 이상 먹구름 아래에서만 번개")]
    [SerializeField] private float minStrikeIntensity = 0.4f;
    [SerializeField] private uint worldSeed = 777u;   // 결정론 씨앗(번개 판정 축)

    [Header("큰불")]
    [Tooltip("불 지속(초). 이 동안 저기압도 유지")]
    [SerializeField] private float fireDuration = 8f;
    [Tooltip("불의 저기압 세기(절댓값)")]
    [SerializeField] private float firePressureStrength = 1.6f;
    [Tooltip("불 저기압 반경(맵 폭 대비)")]
    [SerializeField] private float fireRadiusFactor = 0.12f;

    private Transform fxRoot;
    private bool stepSubscribed;                    // clouds.WeatherStepped 구독 여부
    private ND.UI.WorldMap.RouteVisual[] routes;   // 마차 현재 셀 계산용 경로 캐시(최초 1회 탐색)

    private class Fx { public Transform t; public SpriteRenderer sr; public float age; public float life; public bool isFire; }
    private readonly List<Fx> fxs = new List<Fx>();
    private readonly List<Vector3> stormCenters = new List<Vector3>();   // 번개 대상 = 먹구름 '중심'들(가장자리 X)

    private static readonly Color BoltCol = new Color(1f, 1f, 0.75f);   // 번개 섬광(밝은 노랑흰)
    private static readonly Color FireCol = new Color(1f, 0.45f, 0.1f);  // 불(주황)

    private void Awake()
    {
        if (renderRoot == null) renderRoot = transform;
        if (grid == null) grid = GetComponentInChildren<MinimapGrid>(true);
        if (clouds == null) clouds = GetComponentInChildren<MinimapClouds>(true);
        if (wind == null) wind = GetComponentInChildren<MinimapWind>(true);
    }

    private void OnDisable()
    {
        if (clouds != null && stepSubscribed) { clouds.WeatherStepped -= OnWeatherStep; stepSubscribed = false; }
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

        // 번개 판정은 날씨 스텝(OnWeatherStep, simStep 결정론)에 올라탄다 → clouds 준비되면 한 번 구독.
        if (!stepSubscribed) { clouds.WeatherStepped += OnWeatherStep; stepSubscribed = true; }

        float dt = Time.deltaTime;

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

    // ★번개 판정 — 날씨가 한 스텝 갈 때마다 호출(실시간 + 되감기/투영 공통). simStep이 결정론 축.
    //   attemptEverySteps 마다 시도 → 폭풍 셀 하나를 결정론 hash로 골라 번개.
    //   ★불(DropEventAt)은 바람 시뮬을 교란하므로 '결정론 재현'을 위해 되감기 중에도 항상 적용한다.
    //     시각 섬광(SpawnFx)만 '라이브 스텝'에서 낸다(되감기 중 옛 스텝은 화면에 안 뿌림).
    private void OnWeatherStep(long simStep, double wallSeconds)
    {
        if (grid == null || clouds == null || wind == null) return;
        if (simStep % Mathf.Max(1, attemptEverySteps) != 0) return;   // 이 스텝은 시도 주기 아님

        // ★번개 대상 = 먹구름(세기≥문턱)의 '중심'. (예전엔 구름 발자국에 걸친 모든 셀을 균등 추첨 →
        //   구름 반경이 넓어 '앞쪽 가장자리 셀'이 자주 뽑혔고, 거기에 섬광+불 저기압이 생겨 구름을 앞으로
        //   끌어당겨 "번개 먼저·먹구름 나중에 그쪽으로 이동"처럼 보였다. → 진한 중심에만 치도록 교정.)
        stormCenters.Clear();
        clouds.CollectStormCenters(stormCenters, minStrikeIntensity);
        if (stormCenters.Count == 0) return;

        var rng = new DetRng(DetRng.Seed(worldSeed, simStep, 0));   // ★결정론 축 = simStep
        if (rng.Value() >= strikeChance) return;                    // 이번엔 안 침

        Vector3 center = stormCenters[rng.Range(0, stormCenters.Count)];   // 먹구름 하나의 중심
        if (!grid.WorldToCell(center, out int tr, out int tc)) return;     // 그 중심이 있는 셀
        MinimapCell target = grid.GetCell(tr, tc);
        if (target == null) return;
        Vector3 pos = grid.CellToWorld(tr, tc);   // 셀 중심 = 진한 먹구름 바로 아래(가장자리 아님)
        bool flammable = IsFlammable(target.terrain);
        bool live = !MinimapClouds.IsProjecting && IsLiveStep(wallSeconds);   // 투영/되감기 중이면 연출 안 함(불은 아래서 적용)

        // 불 = 시뮬(바람) 교란 → 결정론 재현 위해 되감기 중에도 항상 적용.
        if (flammable)
            wind.DropEventAt(new Vector2(pos.x, pos.y), false, firePressureStrength, fireRadiusFactor, fireDuration);

        // 시각 연출 = 라이브 스텝만(되감기 중 옛 번개는 화면에 안 뿌림).
        if (live)
        {
            SpawnFx(pos, false, 0.22f, 1.3f);                        // ⚡ 섬광
            WeatherState.ReportLightning();                          // 트레드밀 등 연출에 통지
            ShowCaravanStrikeFx(target);                            // 마차 셀 강조 섬광(시각 전용)
            if (flammable) SpawnFx(pos, true, fireDuration, 1.1f);   // 🔥 불 연출
        }
    }

    // 이 스텝이 '지금(라이브)'인지 — 되감기 중 옛 스텝은 화면 연출을 생략하기 위함.
    private static bool IsLiveStep(double wallSeconds)
    {
        var fr = ND.Framework.FrameworkRoot.Instance;
        double now = (fr != null && fr.GameTime != null)
            ? fr.GameTime.CurrentUtc.Ticks / (double)System.TimeSpan.TicksPerSecond
            : System.DateTime.UtcNow.Ticks / (double)System.TimeSpan.TicksPerSecond;
        return (now - wallSeconds) < 1.0;   // 1초 이내면 라이브
    }

    /// <summary>가연 지형: 숲·풀.</summary>
    private static bool IsFlammable(TerrainType t) => t == TerrainType.Forest || t == TerrainType.Grass;

    // 번개가 떨어진 셀에 '이동 중 마차'가 있으면 강조 섬광을 띄운다(★시각 전용).
    //   ※행운 판정·카운트는 MinimapWeatherEventDetector가 결정론으로 담당한다. 여기서는 행운을
    //     굴리지도, 통지하지도 않는다(중복 제거). 순전히 "마차 근처에 번개가 번쩍" 하는 연출.
    //   마차 위치는 미니맵 마커와 동일 방식(진행도→route→WorldToCell)으로 계산.
    private void ShowCaravanStrikeFx(MinimapCell struckCell)
    {
        var fr = ND.Framework.FrameworkRoot.Instance;
        var save = fr != null ? fr.CurrentSaveData : null;
        if (save == null || save.caravans == null) return;
        var shared = fr.SharedGameData;
        if (routes == null) routes = renderRoot.GetComponentsInChildren<ND.UI.WorldMap.RouteVisual>(true);

        for (int i = 0; i < save.caravans.Count; i++)
        {
            var c = save.caravans[i];
            if (c == null || string.IsNullOrEmpty(c.caravanId)) continue;
            if (!ND.Framework.SaveDataLookup.TryGetTradeProgress(save, c.caravanId, out var entry) || entry == null) continue;
            if (entry.state != ND.Framework.TradeProgressState.Traveling) continue;

            float p = CalcProgress(entry.tradeStartUtcTick, entry.expectedTradeEndUtcTick);
            if (!ND.Framework.CaravanMapDisplayResolver.TryResolve(save, shared, c, entry, p, out var display)) continue;
            if (display.Mode != ND.Framework.CaravanMapDisplayMode.Route) continue;   // 이동 중(경로 위)만
            var route = FindRoute(display.RouteId);
            if (route == null) continue;
            Vector3 world = route.EvaluatePosition(display.Progress01);
            if (!grid.WorldToCell(world, out int crow, out int ccol)) continue;
            if (crow != struckCell.row || ccol != struckCell.col) continue;   // 이 마차 셀엔 안 떨어짐

            SpawnFx(grid.CellToWorld(struckCell.row, struckCell.col), false, 0.5f, 2.2f);   // 강조 섬광(연출만)
            return;   // 한 마차 강조면 충분
        }
    }

    // 진행률 = (now - start) / (end - start), 0~1. 미니맵 마커(CalcProgress)와 동일 공식.
    private static float CalcProgress(long startTick, long endTick)
    {
        if (startTick <= 0 || endTick <= startTick) return 1f;
        var fr = ND.Framework.FrameworkRoot.Instance;
        long now = (fr != null && fr.GameTime != null) ? fr.GameTime.CurrentUtc.Ticks : System.DateTime.UtcNow.Ticks;
        return Mathf.Clamp01((float)(now - startTick) / (endTick - startTick));
    }

    // routeId로 경로(RouteVisual)를 찾는다. 없으면 null.
    private ND.UI.WorldMap.RouteVisual FindRoute(string routeId)
    {
        if (string.IsNullOrEmpty(routeId) || routes == null) return null;
        for (int i = 0; i < routes.Length; i++)
            if (routes[i] != null && routes[i].RouteId == routeId) return routes[i];
        return null;
    }

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
