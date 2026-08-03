// =============================================================================
// TerrainRoadStrip — 한 지형의 '독립 길'(루프/슬라이드 가능한 길 조각 묶음)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템 2단계
//
// [역할] 지형 하나(예: 숲)만의 길을 자기 자식 조각(Piece)들로 만든다. 이 길은 스스로
//        모양(숲=나무, 산=바위 …)을 가지며, 컨트롤러(TreadmillRoad)의 지시로:
//          - Loop  : 조각들을 -Z로 흘리고 뒤로 지나가면 앞으로 되돌려 '제자리 무한 루프'
//          - Slide : 되돌리지 않고 그냥 흘려 화면 밖으로 나가거나(퇴장) 앞에서 들어옴(등장)
//        길끼리 섞이지 않도록 각 지형 길은 '따로' 만들어지고, 평소엔 멀찌감치 떨어져 대기한다.
//
// [부착] 컨트롤러가 런타임/에디트에서 자식 오브젝트로 생성하며 BuildFor로 초기화한다.
//        (컨트롤러가 매 프레임 Advance를 호출해 두 길의 흐름을 정확히 동기화한다.)
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>한 지형만의 길. 컨트롤러 지시로 루프/슬라이드한다.</summary>
[ExecuteAlways]
public class TerrainRoadStrip : MonoBehaviour
{
    [SerializeField] private TerrainType terrain;
    [SerializeField] private float segLength = 10f;   // 조각(루프 단위) 길이
    [SerializeField] private float roadWidth = 12f;   // 길 너비
    [SerializeField] private int pieces = 8;          // 조각 수(총 길이 = pieces×segLength)
    [SerializeField] private float curvature = 0.0025f;
    [SerializeField] private float loopBackZ = -10f;  // 루프 창의 뒤끝(이보다 뒤로 가면 앞으로 되돌림)

    // 바닥 텍스처(컨트롤러가 지형별로 지정). null이면 지형색 단색으로 폴백.
    private Texture groundTex;
    private Color groundTint = Color.white;
    private Texture detailTex;                // 섞을 텍스처(예: 흙). null이면 단일
    private Color detailTint = Color.white;
    private float detailAmount = 0f;          // 얼룩 비율
    private float groundTileMeters = 4f;      // 텍스처 1장이 덮는 실제 크기(m)
    private float noiseScale = 3.5f;          // 얼룩 크기
    private ScatterLayer[] scatterLayers;     // 바닥에 뿌릴 툰 식생/바위(종류별 개수·크기)
    private static Vector3[] planeBase;        // Unity Plane 기본 정점(커브 계산 기준)
    private float traveled;                    // 누적 스크롤 거리(노이즈를 길과 함께 연속으로 흐르게)
    // 지형 전환 디졸브 상태(전환 중 '들어오는 길'에만 켬)
    private bool dissolveOn;
    private float dissolveEdgeZ;               // 디졸브 경계(월드 z)
    private float dissolveBand = 14f;          // 디졸브 폭(m)

    private readonly List<Transform> segs = new List<Transform>();
    // 스캐터 오브젝트별 '피벗→바닥' 들어올림 값(피벗이 바닥이 아닌 프리팹(돌 등)이 파묻히지 않도록)
    private readonly Dictionary<Transform, float> scatterLift = new Dictionary<Transform, float>();
    // 회피 대상(나무·바위 등) 트랜스폼 목록 — 캐러밴 회피기동이 읽는다.
    private readonly List<Transform> obstacles = new List<Transform>();
    /// <summary>이 길의 회피 대상(avoid 레이어) 오브젝트들.</summary>
    public List<Transform> Obstacles => obstacles;
    private Material sharedMat;
    private MaterialPropertyBlock mpb;
    private Shader curvedShader;

    public TerrainType Terrain => terrain;
    public float SegLength => segLength;
    public float TotalLength => pieces * segLength;

    /// <summary>가장 앞 조각의 중심 z(가장 큰 값).</summary>
    public float MaxCenterZ
    {
        get { float m = float.MinValue; foreach (var s in segs) if (s != null && s.localPosition.z > m) m = s.localPosition.z; return m; }
    }
    /// <summary>가장 앞 조각의 물리적 앞 끝 z.</summary>
    public float FrontEdgeZ => MaxCenterZ + segLength * 0.5f;

    /// <summary>컨트롤러가 이 길을 특정 지형으로 만든다.</summary>
    public void BuildFor(TerrainType t, float seg, float width, int count, float curv, float backZ,
                         Texture tex, Color tint, Texture dtex, Color dtint, float amount, float tileMeters, float noise,
                         ScatterLayer[] layers, float centerPathHalf = 0f)
    {
        terrain = t; segLength = seg; roadWidth = width; pieces = Mathf.Max(1, count); curvature = curv; loopBackZ = backZ;
        groundTex = tex; groundTint = tint; detailTex = dtex; detailTint = dtint; detailAmount = amount;
        groundTileMeters = Mathf.Max(0.5f, tileMeters); noiseScale = noise;
        scatterLayers = layers;
        roadCenterPathHalf = Mathf.Max(0f, centerPathHalf);   // 전 지형 공통 마차길 반폭
        Rebuild();
    }

    private float roadCenterPathHalf = 0f;   // 도로가 지정한 '전 지형 공통' 마차길 반폭(레이어 값과 큰 쪽 적용)

    private void OnEnable()
    {
        // 이미 만들어진 조각이 있으면(씬 로드/도메인 리로드) 수집만 하고 다시 만들지 않는다.
        if (segs.Count == 0) CollectExisting();
    }

    private void CollectExisting()
    {
        segs.Clear();
        foreach (Transform c in transform) if (c.name == "Piece") segs.Add(c);
    }

    /// <summary>조각들을 이 지형 모양으로 새로 만든다(루프 창 [loopBackZ, loopBackZ+총길이]에 배치).</summary>
    public void Rebuild()
    {
        for (int i = transform.childCount - 1; i >= 0; i--) DestroyObj(transform.GetChild(i).gameObject);
        segs.Clear();
        scatterLift.Clear();
        obstacles.Clear();
        traveled = 0f;
        EnsureMaterial();
        for (int i = 0; i < pieces; i++)
            segs.Add(MakePiece(loopBackZ + i * segLength));
        ApplyCurveToProps();   // 에디트/초기: 잔디를 커브에 맞춰 내림
    }

    public void SetLoopWindow(float backZ) => loopBackZ = backZ;

    /// <summary>첫 조각 중심을 centerZ0에 두고 이어서 배치(등장 시작 위치 지정 등).</summary>
    public void PlaceStartingAt(float centerZ0)
    {
        for (int i = 0; i < segs.Count; i++)
        {
            var p = segs[i].localPosition; p.z = centerZ0 + i * segLength; p.y = CurveY(p.z); segs[i].localPosition = p;
        }
    }

    // 커브는 셰이더(부드러움)로 처리 → 조각 Y는 0(계단 방지). 프리팹은 그 위에 얹는다.
    private float CurveY(float z) => 0f;

    // 바닥 메시 정점과 얹은 프리팹을 '같은 world-z 커브'로 실제 구부린다 → 잔디가 물리적으로 바닥에 붙음.
    private void ApplyCurveToProps()
    {
        float sz = segLength / 10f;   // Plane(10유닛) 정점 z → 실제 길이
        foreach (var seg in segs)
        {
            if (seg == null) continue;
            float pz = seg.localPosition.z;
            for (int i = 0; i < seg.childCount; i++)
            {
                var ch = seg.GetChild(i);
                if (ch.name == "Ground")
                {
                    var mf = ch.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null && planeBase != null)
                    {
                        var vs = mf.sharedMesh.vertices;
                        int n = Mathf.Min(vs.Length, planeBase.Length);
                        for (int k = 0; k < n; k++)
                        {
                            float wz = pz + planeBase[k].z * sz;            // 이 정점의 실제 world z
                            vs[k] = new Vector3(planeBase[k].x, -curvature * wz * wz, planeBase[k].z);
                        }
                        mf.sharedMesh.vertices = vs;
                        mf.sharedMesh.RecalculateBounds();
                    }
                    // 텍스처·흙 노이즈는 셰이더가 '월드 좌표 + _ScrollZ'로 직접 계산(타일 UV 안 씀).
                    //  → 조각(타일)이 몇 개든 하나의 연속 지면. 스크롤 진행거리 + 전환 디졸브를 넘긴다.
                    var gr = ch.GetComponent<MeshRenderer>();
                    if (gr != null)
                    {
                        gr.GetPropertyBlock(mpb);
                        mpb.SetFloat("_ScrollZ", traveled);
                        mpb.SetFloat("_DissolveOn", dissolveOn ? 1f : 0f);
                        if (dissolveOn)
                        {
                            mpb.SetFloat("_DissolveEdgeZ", dissolveEdgeZ);
                            mpb.SetFloat("_DissolveBand", dissolveBand);
                        }
                        gr.SetPropertyBlock(mpb);
                    }
                }
                else
                {
                    var lp = ch.localPosition;                          // 스캐터: 커브 지면에 바닥을 맞춤
                    float wz = pz + lp.z;
                    float lift = scatterLift.TryGetValue(ch, out var lv) ? lv : 0f;
                    lp.y = -curvature * wz * wz + lift;                 // 커브 높이 + 피벗→바닥 보정(안 묻히게)
                    ch.localPosition = lp;
                }
            }
        }
    }

    /// <summary>조각들을 루프 창 시작으로 정렬(교체 완료 시 딱 맞추기).</summary>
    public void SnapToLoopWindow() => PlaceStartingAt(loopBackZ);

    /// <summary>지형 전환 디졸브 설정. on이면 경계(edgeWorldZ)~+band 구간을 부드러운 알파로 옛 지형과 섞음.</summary>
    public void SetDissolve(bool on, float edgeWorldZ, float band)
    {
        dissolveOn = on; dissolveEdgeZ = edgeWorldZ; dissolveBand = Mathf.Max(0.01f, band);

        // 디졸브 중엔 재질을 알파 블렌드(투명 큐)로 → 경계가 하드컷이 아니라 흐릿하게 섞임.
        if (sharedMat != null)
        {
            if (on)
            {
                sharedMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                sharedMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                sharedMat.SetFloat("_ZWrite", 0f);
                sharedMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;  // 옛 지형(불투명) 위에 덮어 블렌드
            }
            else
            {
                sharedMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                sharedMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                sharedMat.SetFloat("_ZWrite", 1f);
                sharedMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;      // 불투명 복원
            }
        }

        if (mpb == null) mpb = new MaterialPropertyBlock();
        foreach (var seg in segs)
        {
            if (seg == null) continue;
            for (int i = 0; i < seg.childCount; i++)
            {
                var ch = seg.GetChild(i);
                if (ch.name != "Ground") continue;
                var gr = ch.GetComponent<MeshRenderer>();
                if (gr == null) continue;
                gr.GetPropertyBlock(mpb);
                mpb.SetFloat("_DissolveOn", on ? 1f : 0f);
                if (on) { mpb.SetFloat("_DissolveEdgeZ", edgeWorldZ); mpb.SetFloat("_DissolveBand", dissolveBand); }
                gr.SetPropertyBlock(mpb);
            }
        }
    }

    /// <summary>조각들을 dz만큼 -Z로 흘린다. wrap이면 뒤로 지난 조각을 앞으로 되돌린다(무한 루프).</summary>
    public void Advance(float dz, bool wrap)
    {
        traveled += dz;   // 노이즈 연속 스크롤용
        float span = pieces * segLength;
        for (int i = 0; i < segs.Count; i++)
        {
            var p = segs[i].localPosition; p.z -= dz;
            if (wrap && p.z < loopBackZ)
            {
                p.z += span;
                RescatterPiece(segs[i]);   // 되돌아오는 조각은 스캐터를 새로 랜덤 → 반복 안 보임
            }
            p.y = CurveY(p.z);
            segs[i].localPosition = p;
        }
        ApplyCurveToProps();   // 스크롤 중에도 잔디를 커브에 맞춤
    }

    // ── 조각(지형 모양) 생성 ──

    private Transform MakePiece(float centerZ)
    {
        var piece = new GameObject("Piece");
        piece.transform.SetParent(transform, false);
        piece.transform.localPosition = new Vector3(0f, CurveY(centerZ), centerZ);

        Color baseCol = MinimapGridDebug.TerrainColor(terrain);
        // 바닥: 텍스처 있으면 그걸로(틴트 적용), 없으면 지형색 단색
        AddPlane(piece.transform, Vector3.zero, roadWidth, segLength,
                 groundTex != null ? groundTint : baseCol, groundTex, groundTileMeters);
        Scatter(piece.transform);   // 툰 식생/바위 프리팹 뿌리기
        return piece.transform;
    }

    /// <summary>지형 종류별로 '다른 모양'이 되도록 임시 장식(프리미티브)을 얹는다.</summary>
    private void Decorate(Transform parent, Color baseCol)
    {
        float side = roadWidth * 0.5f - 1f;   // 좌우 장식 위치(가운데 마차 길 확보)
        switch (terrain)
        {
            case TerrainType.Forest:   // 숲: 양옆 나무
                foreach (float z in Zs(3))
                {
                    AddBox(parent, new Vector3(-side, 1.4f, z), new Vector3(0.8f, 2.8f, 0.8f), new Color(0.10f, 0.32f, 0.10f));
                    AddBox(parent, new Vector3( side, 1.4f, z), new Vector3(0.8f, 2.8f, 0.8f), new Color(0.10f, 0.32f, 0.10f));
                }
                break;
            case TerrainType.Mountain: // 산: 양옆 바위
                foreach (float z in Zs(2))
                {
                    AddBox(parent, new Vector3(-side, 0.8f, z), new Vector3(1.6f, 1.6f, 1.6f), new Color(0.45f, 0.45f, 0.48f));
                    AddBox(parent, new Vector3( side, 0.9f, z), new Vector3(1.8f, 1.8f, 1.8f), new Color(0.40f, 0.40f, 0.43f));
                }
                break;
            case TerrainType.Grass:    // 풀: 낮은 풀포기
                foreach (float z in Zs(4))
                {
                    AddBox(parent, new Vector3(-side * 0.7f, 0.2f, z), new Vector3(0.5f, 0.4f, 0.5f), new Color(0.42f, 0.68f, 0.28f));
                    AddBox(parent, new Vector3( side * 0.8f, 0.2f, z + 1f), new Vector3(0.5f, 0.4f, 0.5f), new Color(0.42f, 0.68f, 0.28f));
                }
                break;
            case TerrainType.Plain:    // 평지: 가운데 흙길
                AddPlane(parent, new Vector3(0f, 0.02f, 0f), roadWidth * 0.4f, segLength, new Color(0.72f, 0.58f, 0.34f));
                break;
            case TerrainType.Farmland: // 논밭: 가로 이랑
                foreach (float z in Zs(4))
                    AddBox(parent, new Vector3(0f, 0.08f, z), new Vector3(roadWidth * 0.9f, 0.16f, 0.5f), new Color(0.52f, 0.36f, 0.16f));
                break;
            case TerrainType.Bridge:   // 다리: 가로 널판
                foreach (float z in Zs(5))
                    AddBox(parent, new Vector3(0f, 0.1f, z), new Vector3(roadWidth * 0.95f, 0.2f, 0.7f), new Color(0.58f, 0.38f, 0.18f));
                break;
            default:                   // 강/강변/호수: 물색 바닥만
                break;
        }
    }

    private IEnumerable<float> Zs(int count)
    {
        if (count <= 1) { yield return 0f; yield break; }
        float start = -segLength * 0.5f + segLength / (count * 2f);
        float stepZ = segLength / count;
        for (int i = 0; i < count; i++) yield return start + i * stepZ;
    }

    // 조각 바닥에 스캐터 레이어들(풀/돌/나무/작물…)을 배치. 레이어별로 랜덤 또는 격자(줄맞춤).
    // (피스의 자식 → 스크롤과 함께 이동. 피벗과 무관하게 바닥에 앉도록 보정값도 저장.)
    private void Scatter(Transform parent)
    {
        if (scatterLayers == null) return;
        float halfW = roadWidth * 0.5f - 0.4f;
        float halfL = segLength * 0.5f;
        foreach (var layer in scatterLayers)
        {
            if (layer == null || layer.prefabs == null || layer.prefabs.Length == 0) continue;
            float path = Mathf.Max(layer.centerPathHalf, roadCenterPathHalf);   // 중앙 마차길 반폭(레이어·도로공통 중 큰 값)
            if (layer.gridSpacing > 0.01f)
            {
                // 격자(줄맞춤) — 밭·옥수수농장. 각 칸에 약간의 흔들림만.
                float s = layer.gridSpacing, j = s * 0.28f;
                for (float gx = -halfW; gx <= halfW; gx += s)
                {
                    if (path > 0f && Mathf.Abs(gx) < path) continue;   // 가운데 마차길은 비운다
                    for (float gz = -halfL; gz <= halfL; gz += s)
                        SpawnOneScatter(layer, parent, gx + Random.Range(-j, j), gz + Random.Range(-j, j));
                }
            }
            else
            {
                if (layer.count <= 0) continue;
                int clump = Mathf.Max(1, layer.clusterSize);   // 무리당 개체 수(1=낱개)
                float cr = layer.clusterRadius;
                for (int i = 0; i < layer.count; i++)
                {
                    // 무리 중심 하나를 정하고
                    float cx = Random.Range(-halfW, halfW);
                    if (path > 0f && Mathf.Abs(cx) < path)           // 가운데 마차길 회피(양옆으로 밀기)
                        cx = (cx >= 0f ? 1f : -1f) * Random.Range(path, halfW);
                    float cz = Random.Range(-halfL, halfL);
                    // 그 주위에 clump송이를 반경 cr 안에 뭉쳐 심는다(꽃무리)
                    for (int c = 0; c < clump; c++)
                    {
                        float ox = clump > 1 ? cx + Random.Range(-cr, cr) : cx;
                        float oz = clump > 1 ? cz + Random.Range(-cr, cr) : cz;
                        ox = Mathf.Clamp(ox, -halfW, halfW);
                        if (path > 0f && Mathf.Abs(ox) < path) continue;   // 길 위엔 안 심음
                        SpawnOneScatter(layer, parent, ox, oz);
                    }
                }
            }
        }
    }

    // 스캐터 1개 생성(위치는 호출측이 지정) + 바닥스냅·회피등록·틴트 처리.
    private void SpawnOneScatter(ScatterLayer layer, Transform parent, float x, float z)
    {
        if (layer.spawnChance < 1f && Random.value > layer.spawnChance) return;   // 확률적으로 건너뜀(듬성듬성)
        var pf = layer.prefabs[Random.Range(0, layer.prefabs.Length)];
        if (pf == null) return;
        var go = Instantiate(pf, parent);
        go.transform.localPosition = new Vector3(x, 0f, z);
        go.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        float jit = 1f + Random.Range(-layer.scaleJitter, layer.scaleJitter);
        go.transform.localScale *= Mathf.Max(0.01f, layer.scale) * jit;

        // 피벗→바닥 보정: LODGroup이면 LOD0 렌더러만 써서 바운즈 정확(나무 뜸 방지).
        var rends = go.GetComponentsInChildren<Renderer>();
        var lodGroup = go.GetComponentInChildren<LODGroup>();
        if (lodGroup != null)
        {
            var lods = lodGroup.GetLODs();
            if (lods.Length > 0 && lods[0].renderers != null)
            {
                var lod0 = new List<Renderer>();
                foreach (var r in lods[0].renderers) if (r != null) lod0.Add(r);
                if (lod0.Count > 0) rends = lod0.ToArray();
            }
        }
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int r = 1; r < rends.Length; r++) b.Encapsulate(rends[r].bounds);
            float sink = Mathf.Min(0.4f, 0.04f * b.size.y);
            scatterLift[go.transform] = go.transform.position.y - b.min.y - sink;
        }
        if (layer.avoid) obstacles.Add(go.transform);
        if (layer.tint != Color.white)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            foreach (var tr in go.GetComponentsInChildren<Renderer>())
            {
                tr.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", layer.tint);
                mpb.SetColor("_Color", layer.tint);
                tr.SetPropertyBlock(mpb);
            }
        }
    }

    // 되돌아오는 조각의 스캐터(나무·바위·풀 등)를 지우고 새로 랜덤 배치 → 무한 루프해도 같은 배치가 반복되지 않는다.
    private void RescatterPiece(Transform seg)
    {
        for (int i = seg.childCount - 1; i >= 0; i--)
        {
            var ch = seg.GetChild(i);
            if (ch.name == "Ground") continue;   // 바닥은 유지, 스캐터만 교체
            scatterLift.Remove(ch);
            obstacles.Remove(ch);
            DestroyObj(ch.gameObject);
        }
        Scatter(seg);   // 새 랜덤 스캐터
    }

    private void AddPlane(Transform parent, Vector3 localPos, float w, float l, Color color, Texture tex = null, float tileMeters = 0f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);   // XZ 평면, 세분화 → 커브 부드러움
        go.name = "Ground"; StripCollider(go);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(w / 10f, 1f, l / 10f);
        // 바닥마다 자기 메시(정점 구부리기용). 기본 정점은 한 번만 저장.
        var mf = go.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            if (planeBase == null) planeBase = mf.sharedMesh.vertices;
            mf.sharedMesh = Object.Instantiate(mf.sharedMesh);
        }
        PaintGround(go, color, tex, w, l, tileMeters);
    }

    // 바닥: 텍스처 있으면 _BaseMap + 정수 타일링 + 틴트, 없으면 단색.
    private void PaintGround(GameObject go, Color tint, Texture tex, float w, float l, float tileMeters)
    {
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = sharedMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        mr.GetPropertyBlock(mpb);
        if (tex != null)
        {
            mpb.SetTexture("_BaseMap", tex);
            // 텍스처·노이즈는 월드 좌표 기반(타일 UV 미사용) → 조각 경계 이음매 없음.
            float m = Mathf.Max(0.5f, tileMeters);
            mpb.SetFloat("_WorldTexScale", 1f / m);                    // 1m당 텍스처 반복수
            mpb.SetFloat("_NoiseWorldScale", noiseScale / Mathf.Max(0.5f, segLength)); // 얼룩 빈도(1m당)
            mpb.SetFloat("_ScrollZ", traveled);
            // 섞을(흙) 텍스처: 있으면 노이즈로 얼룩, 없으면 얼룩 0(단일 텍스처)
            mpb.SetTexture("_DetailMap", detailTex != null ? detailTex : tex);
            mpb.SetColor("_DetailColor", detailTint);
            mpb.SetFloat("_NoiseAmount", detailTex != null ? detailAmount : 0f);
        }
        mpb.SetColor("_BaseColor", tint);
        mr.SetPropertyBlock(mpb);
    }

    private void AddBox(Transform parent, Vector3 localPos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Prop"; StripCollider(go);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        Paint(go, color);
    }

    private void Paint(GameObject go, Color c)
    {
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = sharedMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        mr.GetPropertyBlock(mpb);
        mpb.SetColor("_BaseColor", c);
        mr.SetPropertyBlock(mpb);
    }

    private void EnsureMaterial()
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();
        if (curvedShader == null) curvedShader = Shader.Find("ND/CurvedWorld");
        if (sharedMat == null)
        {
            Shader sh = curvedShader != null ? curvedShader : Shader.Find("Universal Render Pipeline/Unlit");
            sharedMat = new Material(sh) { name = "TerrainRoadMat_" + terrain };
        }
        if (sharedMat.HasProperty("_Curvature")) sharedMat.SetFloat("_Curvature", 0f);   // 셰이더 커브 끔 — 실제 지오메트리로 휨(잔디가 물리적으로 붙게)
    }

    private static void StripCollider(GameObject go)
    {
        if (go.TryGetComponent<Collider>(out var col)) DestroyObj(col);
    }

    private static void DestroyObj(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
    }
}
