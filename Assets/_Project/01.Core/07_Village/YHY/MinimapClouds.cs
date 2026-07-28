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
    }

    [Header("참조")]
    [SerializeField] private MinimapGrid grid;      // 비면 자동 탐색
    [SerializeField] private MinimapWind wind;      // 비면 자동 탐색
    [SerializeField] private Transform renderRoot;  // 구름을 담을 부모(비면 자기 자신)

    [Header("생성/수명")]
    [SerializeField] private int initialClouds = 8;      // 시작 시 맵 안에 뿌리는 수
    [SerializeField] private int maxClouds = 16;         // 동시 최대 수(유지 목표)
    [SerializeField] private float spawnInterval = 0.7f; // 이 주기마다 부족하면 4방향 가장자리에서 1개 생성
    [SerializeField] private float lifeMin = 30f;        // 수명 랜덤 범위
    [SerializeField] private float lifeMax = 60f;
    [SerializeField] private float fadeIn = 3f;          // 생성 직후 서서히 나타남
    [SerializeField] private float fadeOut = 4f;         // 수명 끝 서서히 사라짐

    [Header("이동")]
    [SerializeField] private float cloudSpeed = 0.2f;    // 바람 벡터 배율(기본 속도). 셀(0.64u) 통과에 평균 ~12초(느긋한 실제 구름 느낌)
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
    [SerializeField] private float dryRate = 0.1f;      // 육지: 초당 수분 감소(먹구름이 물 떠나면 다시 흰색으로)
    [SerializeField] private float crowdRadius = 1.3f;  // 이 거리 안 다른 구름 = 뭉침
    [SerializeField] private float crowdGain = 0.05f;   // 뭉친 이웃 1개당 초당 수분 증가(과포화 방지로 약하게)
    [SerializeField, Range(0f, 1f)] private float darkThreshold = 0.5f;  // 이 이상이면 먹구름 취급(정렬 낮춤)
    [SerializeField] private float rainDuration = 5f;   // 먹구름이 물을 벗어나면 이 시간 동안 크기가 줄며 소멸(비)
    [SerializeField, Range(0f, 1f)] private float waterSpawnChance = 0.55f;  // 생성 시 이 확률로 강/호수 위에 생성(수증기원 → 먹구름 형성)

    [Header("디버그 UI")]
    [SerializeField] private bool showPanel = true;
    [SerializeField] private bool startVisible = true;

    private static readonly Color WhiteCol = new Color(0.98f, 0.98f, 1f);
    private static readonly Color DarkCol = new Color(0.20f, 0.22f, 0.28f);   // 진한 청회색(먹구름)

    private bool on;
    private Transform cloudRoot;
    private float spawnTimer;
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
        if (!on || wind == null || grid == null || cloudRoot == null) return;
        Bounds a = grid.Area;
        if (a.size.x <= 0f) return;
        float dt = Time.deltaTime;

        // 지속 생성: 최대 수보다 적으면 주기마다 가장자리에서 1개
        spawnTimer += dt;
        if (spawnTimer >= spawnInterval && clouds.Count < maxClouds)
        {
            spawnTimer = 0f;
            SpawnCloud(a, true, 0f);
        }

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
            p.x += w.x * spd * dt;
            p.y += w.y * spd * dt;
            cl.t.position = p;

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
                Color rc = DarkCol; rc.a = darkAlpha * Mathf.Clamp01(k * 1.4f);   // 끝에 살짝 옅어지며 사라짐
                cl.sr.color = rc;
                cl.sr.sortingOrder = darkSortingOrder;
                continue;
            }

            // 수분: 물(강/호수/다리) 근처는 시간 기반 증가(느린 구름이 머물며 젖음), 아니면 증발.
            //       발자국(중심+상하좌우) 젖은 비율 반영.
            float wf = WetFraction(p);
            if (wf > 0f) cl.moisture += waterGain * wf * dt;
            else cl.moisture -= dryRate * dt;

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

            // 먹구름이 물을 벗어나거나(비) 완전 포화(저기압에 정체)면 → 비 시작(색 유지, 크기 줄며 소멸)
            if ((cl.moisture >= darkThreshold && wf <= 0f) || cl.moisture >= 0.97f) { cl.raining = true; cl.rainT = 0f; }

            // 페이드(생성/수명)
            float fade = 1f;
            if (cl.age < fadeIn) fade = cl.age / fadeIn;
            else if (cl.age > cl.life - fadeOut) fade = Mathf.Max(0f, (cl.life - cl.age) / fadeOut);

            // 시각: 수분↑ → 색 진하게·불투명↑, 정렬순서 낮게(아래). 속도는 위 이동에서 반영됨
            float dark01 = Mathf.Clamp01(cl.moisture * 1.5f);   // 조금만 젖어도 색은 빨리 진해지게
            Color col = Color.Lerp(WhiteCol, DarkCol, dark01);
            col.a = Mathf.Lerp(cloudAlpha, darkAlpha, dark01) * fade;
            cl.sr.color = col;
            cl.sr.sortingOrder = cl.moisture >= darkThreshold ? darkSortingOrder : normalSortingOrder;

            // 소멸: 수명 끝(흰구름 자연 소멸) or 맵 밖
            if (cl.age >= cl.life || outMap) RemoveCloud(i);
        }
    }

    private static bool IsWet(TerrainType t) => t == TerrainType.River || t == TerrainType.Water || t == TerrainType.Bridge;

    /// <summary>구름 발자국(중심+상하좌우 5점) 중 물(강/호수/다리)에 걸친 비율 0~1.</summary>
    private float WetFraction(Vector3 p)
    {
        const float o = 0.6f;   // 샘플 반경(구름 폭의 절반 정도)
        int c = 0;
        if (IsWetAt(p.x, p.y)) c++;
        if (IsWetAt(p.x + o, p.y)) c++;
        if (IsWetAt(p.x - o, p.y)) c++;
        if (IsWetAt(p.x, p.y + o)) c++;
        if (IsWetAt(p.x, p.y - o)) c++;
        return c / 5f;
    }

    private bool IsWetAt(float x, float y)
    {
        return grid.TryGetCellAtWorld(new Vector3(x, y, 0f), out MinimapCell cell) && cell != null && IsWet(cell.terrain);
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

        // 시작은 맵 안 랜덤 위치에 뿌리되, 나이를 랜덤으로 줘 동시에 사라지지 않게
        for (int i = 0; i < initialClouds; i++)
        {
            float startAge = Random.Range(0f, (lifeMin) * 0.5f);
            SpawnCloud(a, false, startAge);
        }
    }

    /// <summary>구름 1개 생성. atEdge=바람 불어드는 가장자리, 아니면 맵 안 랜덤. startAge=초기 나이(시작용).</summary>
    private void SpawnCloud(Bounds a, bool atEdge, float startAge)
    {
        Vector3 pos;
        if (atEdge)
            // 지속 생성: 일부는 물(강/호수) 위에서(수증기원 → 먹구름), 나머지는 4방향 가장자리에서
            pos = (waterCells.Count > 0 && Random.value < waterSpawnChance)
                ? waterCells[Random.Range(0, waterCells.Count)]
                : RandomEdgeSpawn(a);
        else
            pos = new Vector3(Random.Range(a.min.x, a.max.x), Random.Range(a.min.y, a.max.y), a.center.z);

        var go = new GameObject("Cloud");
        go.transform.SetParent(cloudRoot, false);
        go.transform.position = pos;
        float sc = Random.Range(cloudScaleMin, cloudScaleMax);
        go.transform.localScale = new Vector3(sc, sc, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CloudSprite();
        sr.color = new Color(WhiteCol.r, WhiteCol.g, WhiteCol.b, 0f);   // 처음엔 투명 → 페이드인
        sr.sortingOrder = normalSortingOrder;
        sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;   // 맵 영역 안에서만 보이게(프레임 밖 클리핑)

        clouds.Add(new Cloud { t = go.transform, sr = sr, moisture = 0f, age = startAge, life = Random.Range(lifeMin, lifeMax), baseScale = sc });
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

    /// <summary>4방향 가장자리 중 하나에서 랜덤 위치(구름이 사방에서 골고루 들어오게).</summary>
    private Vector3 RandomEdgeSpawn(Bounds a)
    {
        float inset = 1.0f;   // 가장자리에서 살짝 안쪽(바로 안 빠져나가고 들어오는 느낌)
        float t = Random.value;
        switch (Random.Range(0, 4))
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
