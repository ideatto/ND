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

    private readonly List<Transform> segs = new List<Transform>();
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
    public void BuildFor(TerrainType t, float seg, float width, int count, float curv, float backZ)
    {
        terrain = t; segLength = seg; roadWidth = width; pieces = Mathf.Max(1, count); curvature = curv; loopBackZ = backZ;
        Rebuild();
    }

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
        EnsureMaterial();
        for (int i = 0; i < pieces; i++)
            segs.Add(MakePiece(loopBackZ + i * segLength));
    }

    public void SetLoopWindow(float backZ) => loopBackZ = backZ;

    /// <summary>첫 조각 중심을 centerZ0에 두고 이어서 배치(등장 시작 위치 지정 등).</summary>
    public void PlaceStartingAt(float centerZ0)
    {
        for (int i = 0; i < segs.Count; i++)
        {
            var p = segs[i].localPosition; p.z = centerZ0 + i * segLength; segs[i].localPosition = p;
        }
    }

    /// <summary>조각들을 루프 창 시작으로 정렬(교체 완료 시 딱 맞추기).</summary>
    public void SnapToLoopWindow() => PlaceStartingAt(loopBackZ);

    /// <summary>조각들을 dz만큼 -Z로 흘린다. wrap이면 뒤로 지난 조각을 앞으로 되돌린다(무한 루프).</summary>
    public void Advance(float dz, bool wrap)
    {
        float span = pieces * segLength;
        for (int i = 0; i < segs.Count; i++)
        {
            var p = segs[i].localPosition; p.z -= dz;
            if (wrap && p.z < loopBackZ) p.z += span;
            segs[i].localPosition = p;
        }
    }

    // ── 조각(지형 모양) 생성 ──

    private Transform MakePiece(float centerZ)
    {
        var piece = new GameObject("Piece");
        piece.transform.SetParent(transform, false);
        piece.transform.localPosition = new Vector3(0f, 0f, centerZ);

        Color baseCol = MinimapGridDebug.TerrainColor(terrain);
        AddPlane(piece.transform, Vector3.zero, roadWidth, segLength, baseCol);   // 지형색 바닥
        Decorate(piece.transform, baseCol);                                        // 지형별 임시 장식
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

    private void AddPlane(Transform parent, Vector3 localPos, float w, float l, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);   // XZ 평면, 세분화 → 커브 부드러움
        go.name = "Ground"; StripCollider(go);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(w / 10f, 1f, l / 10f);
        Paint(go, color);
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
        if (sharedMat.HasProperty("_Curvature")) sharedMat.SetFloat("_Curvature", curvature);
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
