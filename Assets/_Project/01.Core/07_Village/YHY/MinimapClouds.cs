// =============================================================================
// MinimapClouds — 바람 따라 흐르는 구름 + 수분/먹구름 날씨 레이어(프로토)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 프로토타입 — 프레임워크 독립, 표시/연출용
//
// [역할]
//  - 임시 흰 구름(코드 베이킹)을 유입 가장자리에서 '지속 생성'하고, 수명·페이드로 자연스레 소멸.
//  - 매 프레임 구름 위치의 바람(MinimapWind.WindAt)을 cloudSwirlDeg만큼 돌려(등압선 감돌기) 이동
//    → 저기압 sink에 안 빨려들고 흐름.
//  - 수분(moisture 0~1): 강/호수/다리 위에서 증가, 육지에서 서서히 감소, 근처 구름과 뭉치면 증가.
//    수분이 높을수록 색이 흰색→진회색(먹구름), '아래에' 그려지고(정렬순서↓), '더 빠르게' 이동.
//
// [부착] 미니맵 렌더 루트(WorldMapRenderRootV2). MinimapGrid·MinimapWind 필요.
// [의도] 나중에 "먹구름 밑 셀 = 비/이벤트" 판정으로 확장(지금은 연출만).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>바람 따라 흐르며 물 위에서 먹구름이 되는 구름 레이어(프로토).</summary>
public class MinimapClouds : MonoBehaviour
{
    // 구름 하나의 런타임 상태
    private class Cloud
    {
        public Transform t;
        public SpriteRenderer sr;
        public float moisture;    // 0(흰구름)~1(먹구름)
        public float age;         // 생성 후 경과(초)
        public float life;        // 수명(초)
        public float baseScale;   // 원래 크기(비 내릴 때 여기서 줄어듦)
        public bool raining;      // 먹구름이 물을 벗어나 비를 뿌리는 중
        public float rainT;       // 비 경과(초)
        public float carryDist;   // 먹구름 상태로 육지를 '실제로 이동한 거리'. 물 만나면 0 리셋(제자리면 안 쌓임)
    }

    [Header("참조")]
    [SerializeField] private MinimapGrid grid;      // 비면 자동 탐색
    [SerializeField] private MinimapWind wind;      // 비면 자동 탐색
    [SerializeField] private Transform renderRoot;  // 구름을 담을 부모(비면 자기 자신)

    [Header("재현성(결정론)")]
    [Tooltip("월드 씨앗. 같은 씨앗 → 항상 같은 구름. 바람에도 이 씨앗을 밀어넣어 동기화한다.")]
    [SerializeField] private uint worldSeed = 12345u;
    [Tooltip("똑딱시계 한 칸(초). 이 단위로만 시뮬을 전진 → 컴퓨터 속도 무관하게 같은 결과")]
    [SerializeField] private float fixedDt = 0.1f;
    [Tooltip("시뮬 전체 속도 배율. 모든 비율(이동·먹구름화·비·수명·생성)을 유지한 채 느리게/빠르게. 1=기본, 작을수록 전체 느림")]
    [SerializeField] private float simSpeed = 0.2f;

    [Header("생성/수명")]
    [SerializeField] private int initialClouds = 12;     // 시작 시 맵 안에 뿌리는 수
    [SerializeField] private int maxClouds = 24;         // 동시 최대 수(유지 목표)
    [SerializeField] private float spawnInterval = 0.4f; // 이 주기마다 부족하면 유입 가장자리에서 1개 생성(작을수록 자주)
    [SerializeField] private float lifeMin = 50f;        // 수명 랜덤 범위(흰 구름이 오래 남게)
    [SerializeField] private float lifeMax = 100f;
    [SerializeField] private float fadeIn = 3f;          // 생성 직후 서서히 나타남
    [SerializeField] private float fadeOut = 4f;         // 수명 끝 서서히 사라짐

    [Header("이동")]
    [SerializeField] private float cloudSpeed = 1.2f;    // 바람 벡터 배율(기본 속도). 맵(~15u) 횡단 ~53초로 수명과 맞춤(0.2는 ~5분이라 얼어붙어 보였음)
    [SerializeField] private float cloudSwirlDeg = 0f;   // 바람에서 이만큼 돌려 이동(0=바람 그대로 따라감). 뭉침은 비내림으로 해소
    [SerializeField] private float darkSpeedMul = 1.9f;  // 먹구름(수분1)일 때 속도 배수

    [Header("크기/표시")]
    [SerializeField] private float cloudScaleMin = 0.7f;
    [SerializeField] private float cloudScaleMax = 1.4f;
    [SerializeField, Range(0f, 1f)] private float cloudAlpha = 0.75f;   // 흰구름 불투명도
    [SerializeField, Range(0f, 1f)] private float darkAlpha = 0.95f;    // 먹구름 불투명도(더 빽빽하게)
    [SerializeField] private int normalSortingOrder = 15;  // 일반 구름(마을10 위·화살표22 아래)
    [SerializeField] private int darkSortingOrder = 13;    // 먹구름은 일반보다 '아래에'

    [Header("수분/먹구름")]
    [SerializeField] private float waterGain = 0.15f;   // 강/호수/다리 근처(발자국 젖은 비율): 초당 수분 증가
    [SerializeField] private float dryRate = 0.1f;      // 흰 구름: 육지에서 초당 수분 감소(증발)
    [SerializeField] private float darkDryRate = 0.02f; // 먹구름: 육지에서 아주 천천히 마름(비 못 뿌리고 오래 떠돌면 흰구름으로 소산 → "다 비로 끝" 방지)
    [SerializeField] private float crowdRadius = 1.3f;  // 이 거리 안 다른 구름 = 뭉침
    [SerializeField] private float crowdGain = 0.06f;   // 뭉친 이웃 1개당 초당 수분 증가(뭉치면 먹구름)
    [SerializeField] private int crowdRainCount = 3;    // 이웃이 이 수 이상 모이면(수렴) 먹구름이 그 자리서 비 → 정체 더미 스스로 해소
    [SerializeField, Range(0f, 1f)] private float darkThreshold = 0.5f;  // 이 이상이면 먹구름 취급(정렬 낮춤)
    [SerializeField] private float rainDuration = 5f;   // 먹구름이 비를 뿌릴 때 이 시간 동안 크기가 줄며 소멸
    [SerializeField] private float carryDistance = 0.8f;  // 먹구름이 물을 떠난 뒤 육지를 이 '거리'(월드유닛, 1셀≈0.64)만큼 실제로 이동하면 비. 느린 구름 수명(30~60s) 안에 도달 가능하도록 ~1셀. 제자리 구름은 안 뿌림
    [SerializeField, Range(0f, 1f)] private float waterSpawnChance = 0.2f;  // 생성 시 이 확률로만 물 위에 생성(대부분은 가장자리 흰구름 → 태생부터 먹구름 방지)
    [SerializeField, Range(0f, 1f)] private float edgeDarkChance = 0.3f;    // 4방향 가장자리 생성 구름 중 이 확률로 '이미 먹구름'으로 유입(외부에서 비구름이 불어온 느낌)

    [Header("디버그 UI")]
    [SerializeField] private bool showPanel = true;
    [SerializeField] private bool startVisible = true;

    private static readonly Color WhiteCol = new Color(0.98f, 0.98f, 1f);
    private static readonly Color DarkCol = new Color(0.20f, 0.22f, 0.28f);   // 진한 청회색(비구름)
    private static readonly Color SnowCol = new Color(0.62f, 0.64f, 0.70f);   // 겨울 옅은 회색(눈구름)

    // 계절 온도 프로파일(런타임 계산). 여름=고온(증발↑·습함·비 잘옴), 겨울=저온(건조·눈·비 드묾)
    private float seEvap = 1f;       // 증발(젖음) 배율
    private float seDry = 1f;        // 건조(마름) 배율
    private float seThreshAdd = 0f;  // 먹구름 문턱 가감(+면 비 되기 어려움)
    private float seSpawnMul = 1f;   // 구름 개수 배율
    private Color seDarkCol = DarkCol;   // 이 계절의 '비구름 색'(겨울=눈구름 옅은 회색)

    private bool on;
    private Transform cloudRoot;
    private float spawnTimer;
    private long simStep;        // 몇 번째 '똑딱'(순간). 모든 번호표의 시간 축
    private int spawnCounter;    // 몇 번째로 태어난 구름인지(구름마다 고유 씨앗)
    private float acc;           // 실시간 누적(고정스텝으로 잘라 쓰기 위한 통)
    private readonly List<Cloud> clouds = new List<Cloud>();
    private readonly List<Vector3> waterCells = new List<Vector3>();   // 강/호수/다리 셀 월드좌표(물 위 생성용)
    private static Sprite cloudSprite;
    private static Sprite maskSprite;
    private GUIStyle btnStyle;

    private void Awake()
    {
        if (renderRoot == null) renderRoot = transform;
        if (grid == null) grid = GetComponent<MinimapGrid>() ?? GetComponentInChildren<MinimapGrid>(true);
        if (wind == null) wind = GetComponent<MinimapWind>() ?? GetComponentInChildren<MinimapWind>(true);
    }

    private void Start()
    {
        if (startVisible) SetClouds(true);
    }

    // ------------------------------------------------------------------ 매 프레임: 생성·이동·수분·소멸

    private void Update()
    {
        // 핫 리로드 복구: [SerializeField] 참조(프리팹엔 null, Awake에서 자동탐색)가 도메인 리로드로 날아가면 다시 찾는다.
        if (renderRoot == null) renderRoot = transform;
        if (grid == null) grid = GetComponent<MinimapGrid>() ?? GetComponentInChildren<MinimapGrid>(true);
        if (wind == null) wind = GetComponent<MinimapWind>() ?? GetComponentInChildren<MinimapWind>(true);

        if (!on || wind == null || grid == null) return;
        if (cloudRoot == null) BuildClouds();   // 구름 루트가 핫 리로드로 날아가면 재생성
        Bounds a = grid.Area;
        if (cloudRoot == null || a.size.x <= 0f) return;

        ApplySeasonProfile();   // 계절(온도)에 따라 증발·건조·비확률·개수 배율 갱신

        // 똑딱시계: 실시간을 fixedDt 단위로 잘라, 그 단위만큼만 시뮬을 전진시킨다.
        // (프레임 속도가 달라도 '몇 번 전진했나'가 같으면 결과가 같다 → 재현 가능)
        acc += Time.deltaTime;
        int guard = 0;                        // 폭주 방지(멈췄다 돌아왔을 때 무한루프 차단)
        while (acc >= fixedDt && guard < 500)
        {
            float sdt = fixedDt * simSpeed;   // 시뮬 시간 배율: 모든 것(이동·수분·수명·비·생성)이 같은 비율로 느려짐
            wind.StepSim(sdt);                // ① 바람 먼저 전진(구름이 읽을 바람)
            StepClouds(a, sdt);               // ② 그 바람으로 구름 전진
            simStep++;                        // ③ 순간 카운터 +1(번호표 시간 축)
            acc -= fixedDt;
            guard++;
        }
        if (guard >= 500) acc = 0f;           // 너무 많이 밀렸으면 남은 시간은 버림
    }

    /// <summary>계절(온도)로 증발·건조·비확률·개수 배율을 정한다. 여름=고온다습, 겨울=저온건조(눈).</summary>
    private void ApplySeasonProfile()
    {
        string s = wind != null ? wind.Season : "summer";
        switch (s)
        {
            case "winter":   // 저온·건조: 증발 약함, 잘 마름, 비 되기 어려움, 구름 적음, 눈구름 색
                seEvap = 0.5f; seDry = 1.6f; seThreshAdd = 0.20f; seSpawnMul = 0.6f; seDarkCol = SnowCol; break;
            case "spring": case "autumn": case "fall":   // 중간
                seEvap = 1.0f; seDry = 1.0f; seThreshAdd = 0.0f; seSpawnMul = 1.0f; seDarkCol = DarkCol; break;
            default:         // summer: 고온다습 — 증발 왕성, 잘 안 마름, 비 잘옴, 구름 많음
                seEvap = 1.5f; seDry = 0.6f; seThreshAdd = -0.10f; seSpawnMul = 1.3f; seDarkCol = DarkCol; break;
        }
    }

    /// <summary>구름을 dt만큼 한 스텝 전진(생성·이동·수분·비·소멸). 고정스텝 루프에서 호출.</summary>
    private void StepClouds(Bounds a, float dt)
    {
        // 지속 생성: 계절별 목표 수보다 적으면 주기마다 1개(겨울엔 적게, 여름엔 많이)
        int targetMax = Mathf.Max(1, Mathf.RoundToInt(maxClouds * seSpawnMul));
        spawnTimer += dt;
        if (spawnTimer >= spawnInterval && clouds.Count < targetMax)
        {
            spawnTimer = 0f;
            SpawnCloud(a, true, 0f);
        }

        float darkT = Mathf.Clamp01(darkThreshold + seThreshAdd);   // 계절 반영 먹구름 문턱(겨울↑=비 어려움)

        // 등압선 감돌기 회전 상수
        float ang = cloudSwirlDeg * Mathf.Deg2Rad;
        float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);

        for (int i = clouds.Count - 1; i >= 0; i--)
        {
            Cloud cl = clouds[i];
            Vector3 p = cl.t.position;

            // 이동: 바람을 cloudSwirlDeg만큼 돌리고, 수분이 많을수록 빠르게
            Vector2 w = wind.WindAt(new Vector2(p.x, p.y));
            w = new Vector2(w.x * ca - w.y * sa, w.x * sa + w.y * ca);
            float spd = cloudSpeed * Mathf.Lerp(1f, darkSpeedMul, cl.moisture);
            float mvx = w.x * spd * dt, mvy = w.y * spd * dt;
            p.x += mvx;
            p.y += mvy;
            cl.t.position = p;
            float moved = Mathf.Sqrt(mvx * mvx + mvy * mvy);   // 이번 스텝 실제 이동 거리(운반거리 누적용)

            cl.age += dt;
            bool outMap = p.x < a.min.x - 1f || p.x > a.max.x + 1f || p.y < a.min.y - 1f || p.y > a.max.y + 1f;

            // 비 오는 중: 먹구름 색 그대로 두고(하얘지지 않음) 크기를 줄이며 소멸
            if (cl.raining)
            {
                cl.rainT += dt;
                float k = 1f - cl.rainT / Mathf.Max(0.01f, rainDuration);   // 1→0
                if (k <= 0f || outMap) { RemoveCloud(i); continue; }
                float scl = cl.baseScale * k;
                cl.t.localScale = new Vector3(scl, scl, 1f);
                Color rc = seDarkCol; rc.a = darkAlpha * Mathf.Clamp01(k * 1.4f);   // 계절색(겨울=눈구름). 끝에 옅어지며 사라짐
                cl.sr.color = rc;
                cl.sr.sortingOrder = darkSortingOrder;
                continue;
            }

            // 수분: 발자국(중심+상하좌우)이 물(강/호수/다리)에 걸친 비율 반영.
            float wf = WetFraction(p);
            if (wf > 0f)
            {
                // 물 위: 수분 충전(먹구름 형성). 여름=증발 왕성(seEvap↑). 운반거리는 리셋.
                cl.moisture += waterGain * seEvap * wf * dt;
                cl.carryDist = 0f;
            }
            else if (cl.moisture >= darkT)
            {
                // 먹구름이 육지로: 비를 '싣고' 이동하며 아주 천천히 마름(오래 못 뿌리면 소산 → 다 비로 안 끝남). 겨울엔 더 빨리 마름.
                cl.carryDist += moved;
                cl.moisture -= darkDryRate * seDry * dt;
            }
            else
            {
                // 흰 구름은 육지에서 증발. 여름엔 습해서 덜 마르고(seDry↓), 겨울엔 건조해 잘 마름(seDry↑).
                cl.moisture -= dryRate * seDry * dt;
            }

            // 뭉침: 근처 구름 수만큼 수분 증가(밀집=먹구름)
            int near = 0;
            float r2 = crowdRadius * crowdRadius;
            for (int j = 0; j < clouds.Count; j++)
            {
                if (j == i) continue;
                Vector3 q = clouds[j].t.position;
                float dx = q.x - p.x, dy = q.y - p.y;
                if (dx * dx + dy * dy < r2) near++;
            }
            if (near > 0) cl.moisture += crowdGain * near * dt;
            cl.moisture = Mathf.Clamp01(cl.moisture);

            // 비 발동 2경로:
            //  ① 뭉침(수렴) 비: 이웃이 crowdRainCount 이상 모인 먹구름은 그 자리서 비 → 정체 더미 스스로 해소(정적 바람 타개).
            //  ② 운반 비: 물에서 젖은 먹구름이 육지(wf<=0)를 carryDistance 이동했거나 수명이 다하면 비.
            bool crowdRain = near >= crowdRainCount && cl.moisture >= darkT;
            bool carryRain = cl.moisture >= darkT && wf <= 0f && (cl.carryDist >= carryDistance || cl.age >= cl.life);
            if (crowdRain || carryRain) { cl.raining = true; cl.rainT = 0f; }

            // 페이드(생성/수명)
            float fade = 1f;
            if (cl.age < fadeIn) fade = cl.age / fadeIn;
            else if (cl.age > cl.life - fadeOut) fade = Mathf.Max(0f, (cl.life - cl.age) / fadeOut);

            // 시각: 수분↑ → 색 진하게(계절색: 여름 비구름/겨울 눈구름)·불투명↑, 정렬순서 낮게(아래).
            float dark01 = Mathf.Clamp01(cl.moisture * 1.5f);   // 조금만 젖어도 색은 빨리 진해지게
            Color col = Color.Lerp(WhiteCol, seDarkCol, dark01);
            col.a = Mathf.Lerp(cloudAlpha, darkAlpha, dark01) * fade;
            cl.sr.color = col;
            cl.sr.sortingOrder = cl.moisture >= darkT ? darkSortingOrder : normalSortingOrder;

            // 소멸: 흰 구름만 수명으로 자연 소멸(먹구름은 비를 뿌리기 전엔 안 사라짐 = 운반 보장) / 맵 밖은 공통
            if ((cl.age >= cl.life && cl.moisture < darkThreshold) || outMap) RemoveCloud(i);
        }
    }

    private static bool IsWet(TerrainType t) => t == TerrainType.River || t == TerrainType.Water || t == TerrainType.Bridge;

    /// <summary>지형별 '적시는 정도'. 호수(큰 물)=많이, 얇은 강/다리=조금 → 강 스친다고 다 먹구름 안 됨.</summary>
    private static float WetWeight(TerrainType t)
    {
        if (t == TerrainType.Water) return 1.0f;                          // 호수 = 큰 물, 강하게 적심
        if (t == TerrainType.River || t == TerrainType.Bridge) return 0.4f; // 얇은 강 = 약하게
        return 0f;
    }

    /// <summary>구름 발자국(중심+상하좌우 5점)의 '젖음 가중 평균' 0~1(호수>강).</summary>
    private float WetFraction(Vector3 p)
    {
        const float o = 0.6f;   // 샘플 반경(구름 폭의 절반 정도)
        float sum = WetWeightAt(p.x, p.y)
                  + WetWeightAt(p.x + o, p.y)
                  + WetWeightAt(p.x - o, p.y)
                  + WetWeightAt(p.x, p.y + o)
                  + WetWeightAt(p.x, p.y - o);
        return sum / 5f;
    }

    private float WetWeightAt(float x, float y)
    {
        return grid.TryGetCellAtWorld(new Vector3(x, y, 0f), out MinimapCell cell) && cell != null ? WetWeight(cell.terrain) : 0f;
    }

    // ------------------------------------------------------------------ 표시 토글/생성/소멸

    /// <summary>구름 레이어 켜기/끄기.</summary>
    public void SetClouds(bool show)
    {
        on = show;
        if (show) BuildClouds();
        else PurgeClouds();
    }

    private void BuildClouds()
    {
        PurgeClouds();
        if (grid == null) return;
        Bounds a = grid.Area;
        if (a.size.x <= 0f) return;

        cloudRoot = new GameObject("Clouds").transform;
        cloudRoot.SetParent(renderRoot, false);
        spawnTimer = 0f;

        // 재현성 리셋: 순간·구름번호를 처음으로, 바람에 같은 씨앗 주입 + '구름이 몰기' 모드 켜기
        simStep = 0;
        spawnCounter = 0;
        acc = 0f;
        if (wind != null) { wind.SetSeed(worldSeed); wind.SetExternallyDriven(true); }

        // 맵 영역 크기의 마스크 → 구름이 격자 밖(미니맵 프레임)으로 안 삐져나오고 가장자리에서 잘림
        var maskGo = new GameObject("CloudMask");
        maskGo.transform.SetParent(cloudRoot, false);
        maskGo.transform.position = new Vector3(a.center.x, a.center.y, a.center.z);
        maskGo.transform.localScale = new Vector3(a.size.x, a.size.y, 1f);
        maskGo.AddComponent<SpriteMask>().sprite = MaskSprite();

        // 강/호수/다리 셀 좌표 수집(물 위 생성용 — 수증기원)
        waterCells.Clear();
        grid.BuildCells();
        for (int r = 0; r < grid.Rows; r++)
            for (int c = 0; c < grid.Cols; c++)
            {
                var mc = grid.GetCell(r, c);
                if (mc != null && IsWet(mc.terrain)) waterCells.Add(grid.CellToWorld(r, c));
            }

        // 시작은 맵 안 랜덤 위치에 뿌리되, 나이를 랜덤으로 줘 동시에 사라지지 않게(번호표로)
        var ageRng = new DetRng(DetRng.Seed(worldSeed, 0, 300));   // salt 300 = 시작나이 용도
        for (int i = 0; i < initialClouds; i++)
        {
            float startAge = ageRng.Range(0f, lifeMin * 0.5f);
            SpawnCloud(a, false, startAge);
        }
    }

    /// <summary>구름 1개 생성. atEdge=바람 불어드는 가장자리, 아니면 맵 안 랜덤. startAge=초기 나이(시작용).</summary>
    private void SpawnCloud(Bounds a, bool atEdge, float startAge)
    {
        // 이 구름만의 번호표: (월드씨앗, 지금 순간, 구름번호) → 항상 같은 위치·크기·수명
        var rng = new DetRng(DetRng.Seed(worldSeed, simStep, spawnCounter));
        spawnCounter++;   // 다음 구름은 다른 번호표

        Vector3 pos;
        float initMoist = 0f;   // 시작 수분(보통 0 = 흰구름)
        if (atEdge)
        {
            if (waterCells.Count > 0 && rng.Value() < waterSpawnChance)
            {
                pos = waterCells[rng.Range(0, waterCells.Count)];   // 물 위 생성(수분 0에서 충전 → 먹구름 형성)
            }
            else
            {
                pos = RandomEdgeSpawn(a, ref rng);                  // 4방향 가장자리에서 유입
                // 그 중 일부는 '이미 먹구름'으로: 외부(맵 밖)에서 비구름이 불어온 것처럼 시작부터 수분 높게
                if (rng.Value() < edgeDarkChance)
                    initMoist = rng.Range(darkThreshold + 0.15f, 0.95f);
            }
        }
        else
            pos = new Vector3(rng.Range(a.min.x, a.max.x), rng.Range(a.min.y, a.max.y), a.center.z);

        var go = new GameObject("Cloud");
        go.transform.SetParent(cloudRoot, false);
        go.transform.position = pos;
        float sc = rng.Range(cloudScaleMin, cloudScaleMax);
        go.transform.localScale = new Vector3(sc, sc, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CloudSprite();
        sr.color = new Color(WhiteCol.r, WhiteCol.g, WhiteCol.b, 0f);   // 처음엔 투명 → 페이드인
        sr.sortingOrder = normalSortingOrder;
        sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;   // 맵 영역 안에서만 보이게(프레임 밖 클리핑)

        clouds.Add(new Cloud { t = go.transform, sr = sr, moisture = initMoist, age = startAge, life = rng.Range(lifeMin, lifeMax), baseScale = sc });
    }

    private void RemoveCloud(int i)
    {
        var cl = clouds[i];
        if (cl.t != null)
        {
            if (Application.isPlaying) Destroy(cl.t.gameObject);
            else DestroyImmediate(cl.t.gameObject);
        }
        clouds.RemoveAt(i);
    }

    /// <summary>
    /// '바람이 안으로 불어넣는' 가장자리에서 생성 위치를 고른다(번호표).
    /// 각 가장자리의 안쪽 방향 바람세기로 가중 → 바람 불어오는 쪽에서 생겨 맵을 가로질러 반대편으로 빠져나감
    /// (옆벽에 붙어 정체하는 것 방지). 바람 방향이 바뀌면 자동으로 따라감. rng는 SpawnCloud 번호표를 이어 씀.
    /// </summary>
    private Vector3 RandomEdgeSpawn(Bounds a, ref DetRng rng)
    {
        float inset = 1.0f;   // 가장자리에서 살짝 안쪽

        // 4 가장자리 대표점 + 그 지점에서 '안쪽으로 향하는' 단위벡터
        Vector2 mid = (Vector2)a.center;
        Vector2[] pts = {
            new Vector2(a.min.x + inset, mid.y),   // 왼쪽 벽 → 안쪽 = +x
            new Vector2(a.max.x - inset, mid.y),   // 오른쪽 벽 → 안쪽 = -x
            new Vector2(mid.x, a.min.y + inset),   // 아래 벽 → 안쪽 = +y
            new Vector2(mid.x, a.max.y - inset),   // 위 벽 → 안쪽 = -y
        };
        Vector2[] inward = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };

        // 각 가장자리의 '안쪽으로 부는 정도'만 가중치로(음수=밖/평행이면 후보 제외)
        float[] wgt = new float[4]; float tot = 0f;
        for (int e = 0; e < 4; e++)
        {
            float inw = wind != null ? Vector2.Dot(wind.WindAt(pts[e]), inward[e]) : 0f;
            wgt[e] = Mathf.Max(0f, inw);
            tot += wgt[e];
        }

        // 가중 추첨(다 애매하면 균등). 번호표(rng)로 결정론 유지
        int pick;
        if (tot <= 1e-5f) pick = rng.Range(0, 4);
        else
        {
            float r = rng.Value() * tot; pick = 3;
            for (int e = 0; e < 4; e++) { if (r < wgt[e]) { pick = e; break; } r -= wgt[e]; }
        }

        float t = rng.Value();
        switch (pick)
        {
            case 0:  return new Vector3(a.min.x + inset, Mathf.Lerp(a.min.y, a.max.y, t), a.center.z);   // 왼쪽
            case 1:  return new Vector3(a.max.x - inset, Mathf.Lerp(a.min.y, a.max.y, t), a.center.z);   // 오른쪽
            case 2:  return new Vector3(Mathf.Lerp(a.min.x, a.max.x, t), a.min.y + inset, a.center.z);   // 아래
            default: return new Vector3(Mathf.Lerp(a.min.x, a.max.x, t), a.max.y - inset, a.center.z);   // 위
        }
    }

    /// <summary>renderRoot 밑 Clouds를 전부 제거(중복/잔존 방지).</summary>
    private void PurgeClouds()
    {
        clouds.Clear();
        cloudRoot = null;
        if (wind != null) wind.SetExternallyDriven(false);   // 구름 끄면 바람은 다시 스스로 흐르게(디버그 화살표)
        if (renderRoot == null) return;
        var stray = new List<Transform>();
        foreach (Transform ch in renderRoot) if (ch.name == "Clouds") stray.Add(ch);
        foreach (var ch in stray)
        {
            if (Application.isPlaying) Destroy(ch.gameObject);
            else DestroyImmediate(ch.gameObject);
        }
    }

    // ------------------------------------------------------------------ 임시 구름 스프라이트(코드 베이킹)

    /// <summary>흰 뭉게구름 텍스처를 여러 원(lobe)의 부드러운 합으로 만든다(에셋 없이).</summary>
    private static Sprite CloudSprite()
    {
        if (cloudSprite != null) return cloudSprite;
        int W = 128, H = 64;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        float[,] lobes = {
            { 0.30f, 0.42f, 0.20f },
            { 0.46f, 0.55f, 0.26f },
            { 0.62f, 0.46f, 0.23f },
            { 0.50f, 0.38f, 0.30f },
            { 0.76f, 0.42f, 0.17f },
        };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float nx = (x + 0.5f) / W, ny = (y + 0.5f) / H;
                float aMax = 0f;
                for (int l = 0; l < lobes.GetLength(0); l++)
                {
                    float dx = nx - lobes[l, 0], dy = ny - lobes[l, 1], r = lobes[l, 2];
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    float aa = Mathf.Clamp01(1f - d);
                    aa = aa * aa * (3f - 2f * aa);   // smoothstep
                    if (aa > aMax) aMax = aa;
                }
                px[y * W + x] = new Color(1f, 1f, 1f, aMax);
            }
        tex.SetPixels(px);
        tex.Apply();
        cloudSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 64f);   // 기본 2×1 유닛
        return cloudSprite;
    }

    /// <summary>마스크용 꽉 찬 흰 사각 스프라이트(1유닛). 스케일로 맵 영역에 맞춘다.</summary>
    private static Sprite MaskSprite()
    {
        if (maskSprite != null) return maskSprite;
        var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white;
        t.SetPixels(px); t.Apply();
        maskSprite = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);   // 4px/4ppu = 1유닛
        return maskSprite;
    }

    // ------------------------------------------------------------------ 디버그 UI(좌열, 메테오 아래)

    private void OnGUI()
    {
        if (!showPanel) return;
        if (btnStyle == null) btnStyle = new GUIStyle(GUI.skin.button);
        float s = Mathf.Max(1f, Screen.height / 1080f);
        btnStyle.fontSize = Mathf.RoundToInt(20f * s);
        float w = 190f * s, h = 58f * s;
        if (GUI.Button(new Rect(545f * s, 585f * s, w, h), on ? "구름 끄기" : "구름 보기", btnStyle))
            SetClouds(!on);
    }
}
