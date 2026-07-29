// =============================================================================
// MinimapWeatherEventDetector — 날씨 이벤트 시스템(루트 이벤트와 별개, 위치/날씨 기반)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 프로토타입 — 프레임워크 독립
//
// [역할] 이동중 캐러밴의 월드 위치(멀티캐러밴과 동일 방식)를 읽어,
//   - (표시) 먹구름 밑이면 캐러밴 위에 물방울 아이콘,
//   - (이벤트) 캐러밴이 '새 셀에 진입'할 때마다 그 셀 날씨로 '이산 체크' → 조건·확률 판정 →
//              결정론(hash(tradeId,checkIndex,cellId))으로 WeatherEventData 이벤트 발생 → WeatherEvents 알림.
//
// [설계] 프레임워크 루트 이벤트(거리+해시, 위치 개념 없음, Weather 제거됨)를 안 건드리고,
//        위치/날씨 기반 이벤트를 우리가 별도로 등록·발행. 효과 적용은 구독자/협의 몫(지금은 표시/기록).
//        결정론 판정이라 오프라인 리플레이에서도 같은 여행 → 같은 이벤트(Phase2 정합).
//
// [부착] V2 렌더 루트(WorldMapRenderRootV2). MinimapGrid·MinimapClouds 필요(자동 탐색).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using ND.Framework;
using ND.UI.WorldMap;
// 전역(Sandbox) 동명 타입과 충돌하므로 Framework 타입을 alias로 고정.
using FrameworkSaveData = ND.Framework.SaveData;
using FrameworkTradeProgress = ND.Framework.TradeProgressSaveData;
using FrameworkTradeProgressState = ND.Framework.TradeProgressState;

/// <summary>캐러밴이 먹구름 밑 셀에 진입하면 날씨 이벤트를 발생시키는 시스템(위치/날씨 기반, 결정론).</summary>
public class MinimapWeatherEventDetector : MonoBehaviour
{
    [Header("참조 (비면 자동 탐색)")]
    [SerializeField] private Transform renderRoot;
    [SerializeField] private MinimapGrid grid;
    [SerializeField] private MinimapClouds clouds;

    [Header("이벤트 정의(SO 풀)")]
    [Tooltip("발생 후보 날씨 이벤트들. 비면 기본 'rain' 이벤트로 동작")]
    [SerializeField] private WeatherEventData[] weatherEvents;

    [Header("표시")]
    [SerializeField] private Vector3 iconOffset = new Vector3(0f, 0.6f, 0f);
    [SerializeField] private float iconScale = 0.35f;
    [SerializeField] private Color rainColor = new Color(0.35f, 0.55f, 0.95f, 0.95f);
    [SerializeField] private bool showTestButton = true;   // 테스트: 루트 위 먹구름 강제

    private RouteVisual[] routes;
    private readonly Dictionary<string, SpriteRenderer> rainIcons = new Dictionary<string, SpriteRenderer>();
    private readonly Dictionary<string, Vector2Int> lastCell = new Dictionary<string, Vector2Int>();   // 캐러밴별 마지막 셀(진입 감지)
    private readonly Dictionary<string, int> checkCursor = new Dictionary<string, int>();               // 캐러밴별 셀 체크 횟수(결정론 축)
    private GUIStyle btnStyle;

    private void Awake()
    {
        if (renderRoot == null) renderRoot = transform;
        if (grid == null) grid = GetComponentInChildren<MinimapGrid>(true);
        if (clouds == null) clouds = GetComponentInChildren<MinimapClouds>(true);
    }

    private void LateUpdate()
    {
        if (grid == null) grid = GetComponentInChildren<MinimapGrid>(true);
        if (clouds == null) clouds = GetComponentInChildren<MinimapClouds>(true);
        if (renderRoot == null) renderRoot = transform;

        var fr = FrameworkRoot.Instance;
        var save = fr != null ? fr.CurrentSaveData : null;
        if (save == null || save.caravans == null || grid == null || clouds == null) { HideAllIcons(); return; }
        if (routes == null) routes = renderRoot.GetComponentsInChildren<RouteVisual>(true);

        var used = new HashSet<string>();
        for (int i = 0; i < save.caravans.Count; i++)
        {
            var c = save.caravans[i];
            if (c == null || string.IsNullOrEmpty(c.caravanId)) continue;

            FrameworkTradeProgress entry = FindTravelingEntry(save, c.caravanId);
            if (entry == null) continue;
            RouteVisual route = FindRoute(entry.activeRouteId);
            if (route == null) continue;

            Vector3 pos = route.EvaluatePosition(CalcProgress(entry));

            // (표시) 먹구름 밑이면 아이콘
            if (clouds.IsRainAt(pos))
            {
                SpriteRenderer icon = GetOrCreateIcon(c.caravanId);
                icon.transform.position = pos + iconOffset;
                icon.enabled = true;
                used.Add(c.caravanId);
            }

            // (이벤트) 새 셀 진입 = 1 이산 체크 → 그 셀 날씨로 판정
            if (grid.TryGetCellAtWorld(pos, out MinimapCell cell) && cell != null)
            {
                Vector2Int cur = new Vector2Int(cell.col, cell.row);
                if (!lastCell.TryGetValue(c.caravanId, out var prev) || prev != cur)
                {
                    int ci = checkCursor.TryGetValue(c.caravanId, out var v) ? v + 1 : 0;
                    checkCursor[c.caravanId] = ci;
                    lastCell[c.caravanId] = cur;
                    EvaluateCheck(c.caravanId, entry.activeTradeId, cell, pos, ci);
                }
            }
        }

        foreach (var kv in rainIcons)
            if (!used.Contains(kv.Key) && kv.Value != null) kv.Value.enabled = false;
    }

    /// <summary>한 셀 체크 판정: 그 셀에 비(먹구름)면, 조건 맞는 이벤트를 결정론 확률로 발생시킨다(한 체크당 1건).</summary>
    private void EvaluateCheck(string caravanId, string tradeId, MinimapCell cell, Vector3 pos, int checkIndex)
    {
        if (!clouds.IsRainAt(pos)) return;   // 지금은 DarkCloud(비) 조건만 지원

        uint tradeHash = FnvHash(tradeId);
        int cellId = cell.row * 100 + cell.col;

        // SO 풀이 비면 기본 rain(확률 1) 이벤트
        if (weatherEvents == null || weatherEvents.Length == 0)
        {
            WeatherEvents.Raise(new WeatherEventOccurrence {
                caravanId = caravanId, tradeId = tradeId, eventId = "rain",
                cellRow = cell.row, cellCol = cell.col, checkIndex = checkIndex, severity = 1f });
            return;
        }

        for (int i = 0; i < weatherEvents.Length; i++)
        {
            var ev = weatherEvents[i];
            if (ev == null || ev.condition != WeatherCondition.DarkCloud) continue;
            // 결정론: 같은 (trade, check, cell, 이벤트) → 항상 같은 판정. 오프라인 리플레이에서도 동일.
            var rng = new DetRng(DetRng.Seed(tradeHash, checkIndex, cellId + i * 7919));
            if (rng.Value() < ev.chance)
            {
                WeatherEvents.Raise(new WeatherEventOccurrence {
                    caravanId = caravanId, tradeId = tradeId, eventId = ev.id,
                    cellRow = cell.row, cellCol = cell.col, checkIndex = checkIndex,
                    severity = ev.severity, foodPenaltyRate = ev.foodPenaltyRate, delayRate = ev.delayRate });
                break;   // 한 체크당 이벤트 1건
            }
        }
    }

    // ── 캐러밴 위치 해석(멀티캐러밴과 동일 로직) ──

    private static FrameworkTradeProgress FindTravelingEntry(FrameworkSaveData save, string caravanId)
    {
        if (save.tradeProgressEntries == null) return null;
        for (int i = 0; i < save.tradeProgressEntries.Count; i++)
        {
            var e = save.tradeProgressEntries[i];
            if (e != null && e.caravanId == caravanId && e.state == FrameworkTradeProgressState.Traveling)
                return e;
        }
        return null;
    }

    private static float CalcProgress(FrameworkTradeProgress e)
    {
        if (e.tradeStartUtcTick <= 0 || e.expectedTradeEndUtcTick <= e.tradeStartUtcTick) return 1f;
        var fr = FrameworkRoot.Instance;
        long now = (fr != null && fr.GameTime != null) ? fr.GameTime.CurrentUtc.Ticks : System.DateTime.UtcNow.Ticks;
        float p = (float)(now - e.tradeStartUtcTick) / (e.expectedTradeEndUtcTick - e.tradeStartUtcTick);
        return Mathf.Clamp01(p);
    }

    private RouteVisual FindRoute(string routeId)
    {
        if (string.IsNullOrEmpty(routeId) || routes == null) return null;
        for (int i = 0; i < routes.Length; i++)
            if (routes[i] != null && routes[i].RouteId == routeId) return routes[i];
        return null;
    }

    /// <summary>문자열 → uint FNV-1a 해시(결정론 씨앗용). 루트 이벤트 StableHash와 동일 계열.</summary>
    private static uint FnvHash(string s)
    {
        uint h = 2166136261u;
        if (s != null) for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619u; }
        return h;
    }

    // ── 테스트: 루트 위에 먹구름 강제 ──

    private void OnGUI()
    {
        if (!showTestButton) return;
        if (btnStyle == null) btnStyle = new GUIStyle(GUI.skin.button);
        float s = Mathf.Max(1f, Screen.height / 1080f);
        btnStyle.fontSize = Mathf.RoundToInt(20f * s);
        if (GUI.Button(new Rect(545f * s, 655f * s, 190f * s, 58f * s), "☔ 먹구름 테스트", btnStyle))
            SpawnTestClouds();
    }

    private void SpawnTestClouds()
    {
        if (clouds == null) return;
        var fr = FrameworkRoot.Instance;
        var save = fr != null ? fr.CurrentSaveData : null;
        int placed = 0;
        if (save != null && save.caravans != null)
        {
            for (int i = 0; i < save.caravans.Count; i++)
            {
                var c = save.caravans[i];
                if (c == null || string.IsNullOrEmpty(c.caravanId)) continue;
                var entry = FindTravelingEntry(save, c.caravanId);
                if (entry == null) continue;
                var route = FindRoute(entry.activeRouteId);
                if (route == null) continue;
                clouds.SpawnDarkCloudAt(route.EvaluatePosition(CalcProgress(entry)));
                placed++;
            }
        }
        if (placed == 0)   // 이동중 캐러밴 없으면 각 루트 중간에(거점·리버·윈디 경로 위)
        {
            if (routes == null) routes = renderRoot.GetComponentsInChildren<RouteVisual>(true);
            if (routes != null)
                for (int r = 0; r < routes.Length; r++)
                    if (routes[r] != null) { clouds.SpawnDarkCloudAt(routes[r].EvaluatePosition(0.5f)); placed++; }
        }
        Debug.Log("[날씨이벤트] 테스트 먹구름 " + placed + "개 생성 (루트 위)");
    }

    // ── 비 아이콘 ──

    private SpriteRenderer GetOrCreateIcon(string caravanId)
    {
        if (rainIcons.TryGetValue(caravanId, out var sr) && sr != null) return sr;
        var go = new GameObject("RainIcon_" + caravanId);
        go.transform.SetParent(renderRoot, false);
        go.transform.localScale = Vector3.one * iconScale;
        sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeDropSprite();
        sr.color = rainColor;
        sr.sortingOrder = 40;
        rainIcons[caravanId] = sr;
        return sr;
    }

    private void HideAllIcons()
    {
        foreach (var kv in rainIcons) if (kv.Value != null) kv.Value.enabled = false;
    }

    private static Sprite cachedDrop;
    private static Sprite MakeDropSprite()
    {
        if (cachedDrop != null) return cachedDrop;
        int S = 32;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float nx = (x + 0.5f) / S * 2f - 1f;
                float ny = (y + 0.5f) / S;
                float rad = Mathf.Lerp(0.75f, 0.02f, ny);
                float d = Mathf.Sqrt(nx * nx + (ny - 0.35f) * (ny - 0.35f) * 2.2f);
                float aa = d <= rad ? 1f : Mathf.Clamp01(1f - (d - rad) * 8f);
                px[y * S + x] = new Color(1f, 1f, 1f, aa);
            }
        tex.SetPixels(px); tex.Apply();
        cachedDrop = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
        return cachedDrop;
    }
}
