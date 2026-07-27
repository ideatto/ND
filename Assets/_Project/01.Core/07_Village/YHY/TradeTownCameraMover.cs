// =============================================================================
// TradeTownCameraMover — VillageCamera를 "선택한 마을 좌표"로 이동
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] Village_Home 한 씬에 거점·무역마을이 "서로 다른 좌표"로 배치되어 있고(각자 바닥·건물 독립),
//        RequestedTownId에 해당하는 마을 위치로 카메라를 옮겨 그 마을을 비춘다.
//        (enable/disable가 아니라 카메라 이동 → 마을마다 독립 공간, 나중에 하나의 큰 맵으로 확장 가능)
//        거점(BaseCamp)도 하나의 마커로 취급 → 거점/무역마을을 동일하게 카메라 이동으로 오간다.
//
// [연동] MinimapTownClickRouter / HomeViewButton이 static RequestedTownId를 설정한다.
//
// [부착] Village_Home 씬의 마을 마커들 부모(WorldTowns)에 붙인다.
// =============================================================================

using UnityEngine;

/// <summary>선택한 무역마을 좌표로 카메라를 이동시킨다.</summary>
public class TradeTownCameraMover : MonoBehaviour
{
    /// <summary>비출 무역마을 townId (라우터가 설정). 씬 경계를 넘기려 static.</summary>
    public static string RequestedTownId;

    [SerializeField] private Transform groupsParent;                  // 마을들의 부모(비면 자기 자신)
    [SerializeField] private Camera targetCamera;                     // 옮길 무역마을 카메라
    [SerializeField] private Vector3 cameraOffset = new Vector3(12f, 10f, -12f); // 마을 기준 카메라 오프셋(각도 유지)
    private const string Prefix = "TradeTown_";

    private string appliedTownId;

    private void Awake()
    {
        if (groupsParent == null) groupsParent = transform;
    }

    private void Update()
    {
        if (string.IsNullOrEmpty(RequestedTownId)) return;
        if (RequestedTownId == appliedTownId) return;
        MoveTo(RequestedTownId);
    }

    /// <summary>townId 마을 좌표 + 오프셋으로 카메라를 이동한다.</summary>
    public void MoveTo(string townId)
    {
        if (targetCamera == null) return;
        Transform town = FindTown(townId);
        if (town == null) return;
        targetCamera.transform.position = town.position + cameraOffset;
        appliedTownId = townId;

        // 거점 팬/줌 컨트롤러의 "팬 중심"을 이 마을로 재설정 → 드래그해도 옛 거점으로 안 끌려온다.
        var bpc = FindObjectOfType<BuildingPlacementController>();
        if (bpc != null) bpc.RecenterPanHome();
    }

    private Transform FindTown(string townId)
    {
        foreach (Transform g in groupsParent)
            if (g.name == Prefix + townId) return g;
        return null;
    }
}
