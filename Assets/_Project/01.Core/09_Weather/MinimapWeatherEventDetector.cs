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

    // 화면 알림 — detector가 '직접' 그린다(효과 컴포넌트가 핫리로드로 안 도는 경우 대비, 신뢰성↑).
    [SerializeField] private bool showNotices = true;
    private struct Notice { public string text; public float until; }
    private readonly List<Notice> notices = new List<Notice>();
    private GUIStyle noticeStyle;

    [Header("속도 감소(현상)")]
    [Tooltip("먹구름 세기당 속도 감소량. 세기1.0 × 이 값 만큼 배율↓ (0.3 → 세기1.0에서 x0.7)")]
    [SerializeField] private float rainSlowdown = 0.3f;
    [SerializeField, Range(0.1f, 1f)]
    [Tooltip("속도 배율 하한(아무리 세도 이 밑으론 안 느려짐)")]
    private float minSpeedMul = 0.5f;
    [Tooltip("이산 날씨 이벤트 발생(보류 중 — 지금은 속도 감소만). 켜면 셀 진입 이벤트 다시 동작")]
    [SerializeField] private bool fireEvents = false;

    [Header("번개 낙뢰(행운)")]
    [Tooltip("켜면 폭풍급 비 셀에 진입할 때 결정론으로 낙뢰 행운을 판정한다(속도감소와 동일한 셀 체크 훅).")]
    [SerializeField] private bool enableLightningLucky = true;
    [Tooltip("이 비 세기 이상(폭풍급)에서만 낙뢰 행운 판정. 번개 시스템 minStrikeIntensity(0.4)와 맞춤.")]
    [SerializeField] private float luckyStormIntensity = 0.4f;
    [Range(0f, 1f)]
    [Tooltip("폭풍급 셀에 진입했을 때 낙뢰 행운(정산 배율)이 뜰 확률. 0.5 = 50%. 인스펙터에서 조절.")]
    [SerializeField] private float luckyChance = 0.5f;

    private readonly Dictionary<string, float> speedMul = new Dictionary<string, float>();   // 캐러밴별 현재 날씨 속도배율(1=정상)
    private readonly HashSet<string> underRain = new HashSet<string>();                        // 지금 비 맞는 캐러밴(전이 알림용)

    // ── 낙뢰 행운(날씨 스텝 OnWeatherStep에서 판정, 무역별) ──
    private bool luckySubscribed;                                                                   // MinimapClouds.WeatherStepped 구독 여부
    private readonly Dictionary<string, Vector2Int> luckyLastCell = new Dictionary<string, Vector2Int>(); // 무역별 마지막 셀(진입 감지)
    private readonly Dictionary<string, int> luckyCheckCursor = new Dictionary<string, int>();            // 무역별 셀 체크 횟수(결정론 hash 축)
    private readonly Dictionary<string, double> luckyProcessedUntil = new Dictionary<string, double>();   // 무역별 '여기까지 처리한 스텝 시각'(되감기 중복 카운트 방지)

    private void Awake()
    {
        if (renderRoot == null) renderRoot = transform;
        if (grid == null) grid = GetComponentInChildren<MinimapGrid>(true);
        if (clouds == null) clouds = GetComponentInChildren<MinimapClouds>(true);
    }

    private void OnDisable()
    {
        if (clouds != null && luckySubscribed) { clouds.WeatherStepped -= OnWeatherStep; luckySubscribed = false; }
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

        // 낙뢰 행운은 날씨 스텝(OnWeatherStep)에 붙어 판정한다(되감기 재생 포함) → clouds 준비되면 한 번 구독.
        if (enableLightningLucky && clouds != null && !luckySubscribed) { clouds.WeatherStepped += OnWeatherStep; luckySubscribed = true; }

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
            float intensity = clouds.RainIntensityAt(pos);   // 먹구름 세기(0=비 없음)

            // 연속 비 세기 게시 → 트레드밀 비 연출(TreadmillRain) 등이 폴링해서 사용(느슨한 정적 채널).
            WeatherState.Report(c.caravanId, intensity);

            // 속도 감소(현상): 세기가 클수록 느려짐. 프레임워크가 이 배율을 여행 속도에 곱해 쓰면 됨(협의).
            float mul = intensity > 0f ? Mathf.Clamp(1f - intensity * rainSlowdown, minSpeedMul, 1f) : 1f;
            speedMul[c.caravanId] = mul;

            if (intensity > 0f)
            {
                SpriteRenderer icon = GetOrCreateIcon(c.caravanId);   // 비 아이콘(=지금 느려짐 표시)
                icon.transform.position = pos + iconOffset;
                icon.enabled = true;
                used.Add(c.caravanId);
                if (!underRain.Contains(c.caravanId))   // 비 진입(전이) 알림
                {
                    underRain.Add(c.caravanId);
                    ShowNotice("☔ 캐러밴 " + Short(c.caravanId) + " 비 진입 — 속도 x" + mul.ToString("F2") + " (세기 " + intensity.ToString("F2") + ")");
                }
            }
            else if (underRain.Remove(c.caravanId))   // 비 벗어남(전이) 알림
            {
                ShowNotice("☀ 캐러밴 " + Short(c.caravanId) + " 비 벗어남 — 속도 정상");
            }

            // (보류) 이산 날씨 이벤트 — fireEvents 켜야 동작(지금은 속도 감소만).
            //  ※낙뢰 행운은 여기(LateUpdate 실시간)가 아니라 OnWeatherStep(날씨 스텝)에서 판정한다 → 되감기서도 재생.
            if (fireEvents && grid.TryGetCellAtWorld(pos, out MinimapCell cell) && cell != null)
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

    /// <summary>이벤트 1건 발행: 알림 채널(WeatherEvents, 외부 구독자용) + detector 직접 화면 알림(신뢰성).</summary>
    private void Fire(WeatherEventOccurrence o)
    {
        WeatherEvents.Raise(o);   // 외부 구독자(dispatcher/효과)용 알림 채널
        ShowNotice("☔ '" + o.eventId + "' 캐러밴 " + Short(o.caravanId) + " 셀(" + o.cellRow + "," + o.cellCol + ")"
                 + " 세기 " + o.intensity.ToString("F2") + " [지연 +" + (o.delayRate * 100f).ToString("F0") + "%]");
    }

    /// <summary>화면 알림 큐에 추가(4초 표시).</summary>
    private void ShowNotice(string text)
    {
        if (!showNotices) return;
        notices.Add(new Notice { text = text, until = Time.time + 4f });
        if (notices.Count > 6) notices.RemoveAt(0);
    }

    private static string Short(string id)
        => string.IsNullOrEmpty(id) ? "?" : (id.Length > 6 ? id.Substring(0, 6) : id);

    /// <summary>캐러밴의 현재 날씨 속도 배율(1=정상, 비 아래면 1 미만). 프레임워크가 여행 속도에 곱해 쓰기용(협의).</summary>
    public float GetWeatherSpeedMultiplier(string caravanId)
        => speedMul.TryGetValue(caravanId, out var m) ? m : 1f;

    /// <summary>한 셀 체크 판정: 그 셀에 비(먹구름)면, 조건 맞는 이벤트를 결정론 확률로 발생시킨다(한 체크당 1건).</summary>
    private void EvaluateCheck(string caravanId, string tradeId, MinimapCell cell, Vector3 pos, int checkIndex)
    {
        float intensity = clouds.RainIntensityAt(pos);   // 먹구름 세기 = 강수량(젖음) × 크기
        if (intensity <= 0f) return;                      // 먹구름 없음

        uint tradeHash = FnvHash(tradeId);
        int cellId = cell.row * 100 + cell.col;

        // SO 풀이 비면 기본 rain(세기 그대로)
        if (weatherEvents == null || weatherEvents.Length == 0)
        {
            Fire(new WeatherEventOccurrence {
                caravanId = caravanId, tradeId = tradeId, eventId = "rain",
                cellRow = cell.row, cellCol = cell.col, checkIndex = checkIndex,
                intensity = intensity, severity = intensity });
            return;
        }

        // 세기가 minIntensity 이상인 이벤트 중 '가장 센 것'(minIntensity 최대)을 고른다 → 폭우가 약한비를 이김
        WeatherEventData best = null;
        for (int i = 0; i < weatherEvents.Length; i++)
        {
            var ev = weatherEvents[i];
            if (ev == null || ev.condition != WeatherCondition.DarkCloud) continue;
            if (intensity < ev.minIntensity) continue;                 // 세기 부족 → 이 이벤트 아님
            if (best == null || ev.minIntensity > best.minIntensity) best = ev;
        }
        if (best == null) return;   // 세기가 어떤 이벤트 문턱에도 못 미침

        // 결정론 확률 판정(같은 여행이면 같은 결과)
        var rng = new DetRng(DetRng.Seed(tradeHash, checkIndex, cellId));
        if (rng.Value() < best.chance)
        {
            Fire(new WeatherEventOccurrence {
                caravanId = caravanId, tradeId = tradeId, eventId = best.id,
                cellRow = cell.row, cellCol = cell.col, checkIndex = checkIndex,
                intensity = intensity, severity = best.severity, delayRate = best.delayRate });
        }
    }

    // ★낙뢰 행운(결정론) — 날씨가 한 스텝 갈 때마다 호출(실시간 + 되감기 공통).
    //   그 '스텝 시각' 기준으로 각 이동중 캐러밴의 셀을 구해, 새 셀에 들어왔고 그 셀이 폭풍급이면
    //   결정론 확률로 럭키 판정 → 우리 자체 저장소(WeatherLuckyStore)에 무역별 누적.
    //   ※날씨와 같은 시간축(스텝)을 타므로, 미니맵 닫았다 여는 되감기에서도 같은 판정이 재생된다.
    //     luckyProcessedUntil로 '이미 처리한 시각'은 건너뛰어 되감기 중복 카운트를 막는다.
    //   ※정산은 GetCount(tradeId) > 0 을 불리언으로 읽어 +10% 한 번 적용(팀 확정 — 스택 없음).
    private void OnWeatherStep(long simStep, double stepWallSeconds)
    {
        if (!enableLightningLucky || clouds == null || grid == null) return;
        var fr = FrameworkRoot.Instance;
        var save = fr != null ? fr.CurrentSaveData : null;
        if (save == null || save.caravans == null) return;
        if (routes == null) routes = renderRoot.GetComponentsInChildren<RouteVisual>(true);
        long stepTicks = (long)(stepWallSeconds * System.TimeSpan.TicksPerSecond);

        for (int i = 0; i < save.caravans.Count; i++)
        {
            var c = save.caravans[i];
            if (c == null || string.IsNullOrEmpty(c.caravanId)) continue;
            var entry = FindTravelingEntry(save, c.caravanId);
            if (entry == null || string.IsNullOrEmpty(entry.activeTradeId)) continue;
            string tid = entry.activeTradeId;

            // 이미 처리한 시각이면 건너뜀(되감기 재생 시 중복 카운트 방지). 아니면 여기까지 처리로 표시.
            if (luckyProcessedUntil.TryGetValue(tid, out var pu) && stepWallSeconds <= pu) continue;
            luckyProcessedUntil[tid] = stepWallSeconds;

            var route = FindRoute(entry.activeRouteId);
            if (route == null) continue;
            float p = ProgressAtTicks(entry, stepTicks);   // '그 스텝 시각' 기준 진행도
            if (p <= 0f || p >= 1f) continue;               // 무역 구간 밖(출발 전/이미 도착)
            Vector3 pos = route.EvaluatePosition(p);
            if (!grid.TryGetCellAtWorld(pos, out MinimapCell cell) || cell == null) continue;

            // 새 셀 진입만 1회 판정(같은 셀에 여러 스텝 머물러도 재판정 안 함)
            Vector2Int cur = new Vector2Int(cell.col, cell.row);
            if (luckyLastCell.TryGetValue(tid, out var prev) && prev == cur) continue;
            int ci = luckyCheckCursor.TryGetValue(tid, out var v) ? v + 1 : 0;
            luckyCheckCursor[tid] = ci;
            luckyLastCell[tid] = cur;

            float intensity = clouds.RainIntensityAt(pos);   // 그 스텝의 비 세기(되감기 중이면 그 시각 상태)
            if (intensity < luckyStormIntensity) continue;    // 폭풍급 아니면 낙뢰 없음
            int cellId = cell.row * 100 + cell.col;
            // 날씨 이벤트 roll과 겹치지 않게 "|lucky" salt로 독립 스트림(결정론 유지).
            var rng = new DetRng(DetRng.Seed(FnvHash(tid + "|lucky"), ci, cellId));
            if (rng.Value() >= luckyChance) continue;         // 확률 통과 못함

            int count = WeatherLuckyStore.Add(tid);           // 우리 자체 저장소에 누적(저장)
            WeatherState.ReportCaravanLightning(c.caravanId, tid);   // 연출/트레드밀 트리거(시각용)
            ShowNotice("⚡ 캐러밴 " + Short(c.caravanId) + " 낙뢰 행운! (누적 " + count + ") 셀("
                     + cell.row + "," + cell.col + ") 세기 " + intensity.ToString("F2"));
        }
    }

    // 진행률 = (지정 시각 - 출발) / (도착예정 - 출발), 0~1. now가 아니라 '특정 스텝 시각'으로 계산.
    private static float ProgressAtTicks(FrameworkTradeProgress e, long nowTicks)
    {
        if (e.tradeStartUtcTick <= 0 || e.expectedTradeEndUtcTick <= e.tradeStartUtcTick) return 1f;
        return Mathf.Clamp01((float)(nowTicks - e.tradeStartUtcTick) / (e.expectedTradeEndUtcTick - e.tradeStartUtcTick));
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
        float s = Mathf.Max(1f, Screen.height / 1080f);

        if (showTestButton)
        {
            if (btnStyle == null) btnStyle = new GUIStyle(GUI.skin.button);
            btnStyle.fontSize = Mathf.RoundToInt(20f * s);
            if (GUI.Button(new Rect(545f * s, 655f * s, 190f * s, 58f * s), "☔ 먹구름 테스트", btnStyle))
                SpawnTestClouds();
        }

        // 화면 알림(비 이벤트) — 상단 중앙 + 어두운 배경(패널에 안 가리게)
        if (showNotices && notices.Count > 0)
        {
            for (int i = notices.Count - 1; i >= 0; i--) if (Time.time > notices[i].until) notices.RemoveAt(i);
            if (notices.Count == 0) return;
            if (noticeStyle == null) { noticeStyle = new GUIStyle(GUI.skin.label); noticeStyle.fontStyle = FontStyle.Bold; noticeStyle.normal.textColor = new Color(0.78f, 0.9f, 1f); }
            noticeStyle.fontSize = Mathf.RoundToInt(22f * s);
            float w = 840f * s, x = (Screen.width - w) * 0.5f, y = 90f * s, lh = 30f * s;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);   // 어두운 배경 박스
            GUI.DrawTexture(new Rect(x - 12f * s, y - 8f * s, w + 24f * s, notices.Count * lh + 16f * s), Texture2D.whiteTexture);
            GUI.color = Color.white;
            for (int i = 0; i < notices.Count; i++) { GUI.Label(new Rect(x, y, w, lh), notices[i].text, noticeStyle); y += lh; }
        }
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
