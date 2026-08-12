// =============================================================================
// BuildingTerrainSnap — 건물을 항상 지형(터레인) 높이에 붙여두는 보정 컴포넌트
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 마을 건물 배치 시스템은 건물을 "평지(y=0)" 전제로 놓는다. 하지만 강 마을처럼
//        지형을 높인 곳에선 그 건물이 땅에 파묻힌다. 이 컴포넌트를 건물에 붙이면
//        매 프레임(LateUpdate) 그 위치의 지형 표면 높이로 y를 보정한다.
//        → 다른 시스템이 y=0으로 되돌려도 다시 지형 위로 올려 파묻힘을 막는다.
//
// [경계] 순수 위치 보정. 평지 마을에선 지형=0이라 사실상 아무 변화 없음(안전).
// =============================================================================

using UnityEngine;

/// <summary>건물 y를 항상 그 지점의 터레인 표면 높이에 맞춰 파묻힘/뜸을 막는다.</summary>
public class BuildingTerrainSnap : MonoBehaviour
{
    private Terrain terr;

    private void Start()
    {
        FindTerrain();
        Apply();
    }

    // 다른 배치 로직(y=0 스냅 등)보다 나중에 돌려 확실히 지형 높이로 보정한다.
    private void LateUpdate()
    {
        if (terr == null) FindTerrain();
        Apply();
    }

    /// <summary>이 건물 xz를 품는 터레인을 찾는다.</summary>
    private void FindTerrain()
    {
        Vector3 p = transform.position;
        foreach (Terrain t in Terrain.activeTerrains)
        {
            Vector3 tp = t.transform.position; Vector3 sz = t.terrainData.size;
            if (p.x >= tp.x && p.x <= tp.x + sz.x && p.z >= tp.z && p.z <= tp.z + sz.z)
            { terr = t; return; }
        }
    }

    /// <summary>y를 지형 표면 높이로 보정(차이가 있을 때만).</summary>
    private void Apply()
    {
        if (terr == null) return;
        Vector3 p = transform.position;
        float ty = terr.transform.position.y + terr.SampleHeight(p);
        if (Mathf.Abs(p.y - ty) > 0.02f)
            transform.position = new Vector3(p.x, ty, p.z);
    }
}
