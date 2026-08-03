// =============================================================================
// TreadmillRain — 트레드밀 화면에 비 연출(절차 생성 빗줄기 파티클 + 흐린 하늘 어둡게)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템 2단계 (juice) + 날씨 연동
//
// [역할] 미니맵 날씨에서 캐러밴이 비를 맞으면(WeatherState.Max > 0) 트레드밀 뷰에도 비가
//        내리게 한다. 비 세기에 비례해 빗줄기 양·진하기·화면 어두움이 부드럽게 바뀐다.
//        파티클·어둠 오버레이·재질·텍스처를 모두 코드로 절차 생성 → 별도 에셋 불필요.
//
// [연결] 세기 신호원:
//          - 실제 날씨: WeatherState.Max (감지기가 매 프레임 게시. 없으면 0=비 없음, 안전)
//          - 디버그: debugIntensity ≥ 0 이면 그 값으로 강제(0.5=약한비, 1=폭우). -1=실제 사용.
//
// [부착] 트레드밀 RenderTexture 카메라(길·캐러밴을 찍는 카메라)에 붙인다. 없으면 자동 탐색.
//        파티클은 카메라에 매달아 '보이는 앞쪽'에만 내리고, 월드 시뮬이라 곧게 떨어진다.
// =============================================================================

using UnityEngine;

/// <summary>날씨(WeatherState) 또는 디버그값에 따라 트레드밀 뷰에 비를 내리는 연출 컴포넌트.</summary>
[ExecuteAlways]
public class TreadmillRain : MonoBehaviour
{
    [Header("세기 신호")]
    [Tooltip("특정 캐러밴 비 세기를 쓰려면 ID 지정(비우면 가장 센 비=Max 사용).")]
    [SerializeField] private string caravanId = "";
    [Range(-1f, 1f)]
    [Tooltip("-1=실제 날씨(WeatherState) 사용. 0~1=그 세기로 강제(디버그). 0.5=약한비, 1=폭우.")]
    [SerializeField] private float debugIntensity = -1f;

    [Header("연출 파라미터")]
    [Tooltip("폭우(세기 1.0)일 때 초당 빗방울 수.")]
    [SerializeField] private float maxRate = 1600f;
    [Tooltip("빗방울 낙하 속도(m/s).")]
    [SerializeField] private float fallSpeed = 32f;
    [Tooltip("바람에 의한 좌우 기울기(낙하 대비 비율).")]
    [SerializeField] private float slant = 0.18f;
    [Tooltip("폭우일 때 화면 어두움(0=그대로,1=검정). 흐린 하늘 느낌.")]
    [Range(0f, 0.6f)]
    [SerializeField] private float maxDarken = 0.3f;
    [Tooltip("세기 변화가 이 속도로 부드럽게 따라감(급변 방지).")]
    [SerializeField] private float responseSpeed = 2.5f;

    [Header("번개(섬광)")]
    [Tooltip("번개 섬광 최대 밝기(0~1).")]
    [Range(0f, 1f)]
    [SerializeField] private float flashStrength = 0.7f;
    [Tooltip("섬광이 사라지는 속도(클수록 짧게 번쩍).")]
    [SerializeField] private float flashDecay = 3.2f;
    [Tooltip("이 세기 이상 비 올 때만 번개 섬광(폭풍 중에만). 0이면 비와 무관하게 항상.")]
    [SerializeField] private float flashNeedsRain = 0.12f;

    /// <summary>디버그 세기 조절(-1=실제 날씨 사용, 0~1=강제). 디버그 UI에서 호출.</summary>
    public float DebugIntensity { get => debugIntensity; set => debugIntensity = Mathf.Clamp(value, -1f, 1f); }

    /// <summary>비를 특정 캐러밴 날씨에 동기화(표시 중 캐러밴 id). 빈 문자열이면 가장 센 비(Max) 사용.</summary>
    public void SetCaravan(string id) => caravanId = id ?? "";

    /// <summary>번개 섬광 즉시 발생(디버그 버튼/외부 호출용). 비 여부 무관.</summary>
    public void TriggerLightning() => flash = Mathf.Max(flash, flashStrength);

    private Camera cam;
    private ParticleSystem ps;
    private ParticleSystem.EmissionModule emission;
    private Renderer darkQuad;               // 화면 어둡게(흐린 하늘) 오버레이
    private Renderer flashQuad;              // 번개 섬광(흰색) 오버레이
    private float current;                    // 부드럽게 따라가는 현재 세기
    private float flash;                      // 현재 섬광 밝기(0=없음, 감쇠)
    private float lastSeenLightning = -999f;  // 마지막으로 처리한 번개 시각

    private void OnEnable() => EnsureRig();

    // 목표 세기(실제 날씨 or 디버그)
    private float TargetIntensity()
    {
        if (debugIntensity >= 0f) return debugIntensity;
        float v = string.IsNullOrEmpty(caravanId) ? WeatherState.Max : WeatherState.Get(caravanId);
        return Mathf.Clamp01(v);
    }

    private void Update()
    {
        if (cam == null || ps == null) EnsureRig();
        if (ps == null) return;

        float target = TargetIntensity();
        float dt = Application.isPlaying ? Time.unscaledDeltaTime : 1f;
        current = Mathf.MoveTowards(current, target, responseSpeed * dt);
        if (!Application.isPlaying) current = target;

        // 빗줄기 양(세기 비례)
        emission.rateOverTime = maxRate * current;
        var main = ps.main;
        var col = main.startColor.color;
        col.a = Mathf.Lerp(0.35f, 0.75f, current);      // 셀수록 진하게
        main.startColor = col;
        if (current > 0.001f && !ps.isEmitting) ps.Play();
        if (current <= 0.001f && ps.isEmitting) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);

        // 화면 어둡게(흐린 하늘) — 쿼드 전용 머티리얼 색 직접 조절
        if (darkQuad != null)
        {
            float a = maxDarken * current;
            if (a <= 0.002f) darkQuad.enabled = false;
            else
            {
                darkQuad.enabled = true;
                var dm = darkQuad.sharedMaterial;
                var dcol = new Color(0.05f, 0.06f, 0.09f, a);
                if (dm.HasProperty("_BaseColor")) dm.SetColor("_BaseColor", dcol);
                if (dm.HasProperty("_Color")) dm.SetColor("_Color", dcol);
            }
        }

        // 번개: 새 번개가 통지되면(값 변화) 폭풍 중(비>문턱)일 때 섬광 발동
        float lt = WeatherState.LastLightningTime;
        if (lt != lastSeenLightning)
        {
            lastSeenLightning = lt;
            if (lt > 0f && current >= flashNeedsRain) flash = flashStrength;
        }
        flash = Mathf.MoveTowards(flash, 0f, flashDecay * dt);   // 빠르게 사그라듦
        if (flashQuad != null)
        {
            if (flash <= 0.002f) flashQuad.enabled = false;
            else
            {
                flashQuad.enabled = true;
                var fm = flashQuad.sharedMaterial;
                var fcol = new Color(0.95f, 0.96f, 1f, flash);   // 밝은 흰빛 섬광
                if (fm.HasProperty("_BaseColor")) fm.SetColor("_BaseColor", fcol);
                if (fm.HasProperty("_Color")) fm.SetColor("_Color", fcol);
            }
        }
    }

    // ── 리그(파티클 + 어둠 오버레이) 절차 생성 ──

    private void EnsureRig()
    {
        // 1) 카메라 찾기: 이 오브젝트 → RT 카메라 → 아무 카메라
        cam = GetComponent<Camera>();
        if (cam == null) foreach (var c in GetComponentsInChildren<Camera>(true)) { cam = c; break; }
        if (cam == null)
        {
            var all = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var c in all) if (c.targetTexture != null) { cam = c; break; }
            if (cam == null && all.Length > 0) cam = all[0];
        }
        if (cam == null) return;

        // 2) 파티클 시스템(카메라 자식, 앞·위쪽 상자에서 방출 → 곧게 낙하)
        if (ps == null)
        {
            var found = cam.transform.Find("TreadmillRainPS");
            ps = found != null ? found.GetComponent<ParticleSystem>() : null;
        }
        if (ps == null)
        {
            var go = new GameObject("TreadmillRainPS");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 9f, 17f);   // 카메라 앞·위쪽
            ps = go.AddComponent<ParticleSystem>();
            ConfigureParticles();
        }
        emission = ps.emission;

        // 3) 어둠 오버레이(카메라 아주 앞쪽 쿼드, 항상 화면 덮음)
        if (darkQuad == null)
        {
            var found = cam.transform.Find("TreadmillRainDark");
            darkQuad = found != null ? found.GetComponent<Renderer>() : null;
        }
        if (darkQuad == null) CreateDarkOverlay();

        // 4) 번개 섬광 오버레이(흰색, 비/어둠 위 최상단)
        if (flashQuad == null)
        {
            var found = cam.transform.Find("TreadmillRainFlash");
            flashQuad = found != null ? found.GetComponent<Renderer>() : null;
        }
        if (flashQuad == null) flashQuad = CreateOverlay("TreadmillRainFlash", 3060);
    }

    private void ConfigureParticles()
    {
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;   // 곧게 낙하(카메라 회전 무관)
        main.startLifetime = 0.7f;
        main.startSpeed = fallSpeed;
        main.startSize = 0.09f;                                       // 얇은 빗줄기(스트레치로 길어짐)
        main.startColor = new Color(0.78f, 0.85f, 0.98f, 0.75f);      // 푸른 회색
        main.maxParticles = 4000;
        main.gravityModifier = 0f;
        main.playOnAwake = false;

        ps.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // +Z(전방) → 아래로
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(46f, 46f, 0.1f);                    // 넓은 판에서 뿌림
        shape.rotation = Vector3.zero;

        var vel = ps.velocityOverLifetime;                            // 바람 기울기(월드 X)
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(fallSpeed * slant);
        vel.y = new ParticleSystem.MinMaxCurve(0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f);

        var emis = ps.emission;
        emis.enabled = true;
        emis.rateOverTime = 0f;                                       // Update에서 세기로 조절

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.08f;
        r.lengthScale = 5.5f;
        r.material = MakeRainMaterial();
        r.sortingOrder = 50;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    // 빗줄기용 반투명 언릿 재질(세로 그라디언트 텍스처)
    private static Material cachedRainMat;
    private Material MakeRainMaterial()
    {
        if (cachedRainMat != null) return cachedRainMat;
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh);
        m.mainTexture = MakeStreakTexture();
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", MakeStreakTexture());
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        MakeTransparent(m);
        m.renderQueue = 3010;                                          // 어둠 오버레이(2980)보다 위
        cachedRainMat = m;
        return m;
    }

    // URP Unlit/파티클 재질을 실제 알파블렌드로 전환(_Surface float만으론 블렌드 상태·키워드가 안 바뀜).
    private static void MakeTransparent(Material m)
    {
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    private static Texture2D cachedStreak;
    private static Texture2D MakeStreakTexture()
    {
        if (cachedStreak != null) return cachedStreak;
        int w = 8, h = 32;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
        {
            float ny = (y + 0.5f) / h;
            float along = Mathf.Sin(ny * Mathf.PI);            // 양끝 페이드
            for (int x = 0; x < w; x++)
            {
                float nx = (x + 0.5f) / w * 2f - 1f;
                float across = Mathf.Clamp01(1f - Mathf.Abs(nx) * 1.2f);   // 가운데 밝게
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, along * across));
            }
        }
        tex.Apply();
        cachedStreak = tex;
        return tex;
    }

    private void CreateDarkOverlay() => darkQuad = CreateOverlay("TreadmillRainDark", 2980);   // 씬 위, 빗줄기 아래

    // 카메라 바로 앞에 화면을 덮는 반투명 쿼드 생성(어둠·섬광 공용). queue로 그리는 순서 결정.
    private Renderer CreateOverlay(string name, int queue)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        var coll = go.GetComponent<Collider>(); if (coll != null) DestroyImmediate(coll);
        go.transform.SetParent(cam.transform, false);
        float d = Mathf.Max(0.5f, cam.nearClipPlane + 0.2f);
        go.transform.localPosition = new Vector3(0f, 0f, d);
        go.transform.localRotation = Quaternion.identity;
        float hh = 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float ww = hh * Mathf.Max(1f, cam.aspect);
        go.transform.localScale = new Vector3(ww * 1.5f, hh * 1.5f, 1f);
        var rend = go.GetComponent<Renderer>();
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh);
        MakeTransparent(m);
        m.renderQueue = queue;
        rend.sharedMaterial = m;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.enabled = false;
        return rend;
    }
}
