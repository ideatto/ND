// =============================================================================
// MinimapCaravanClickRouter — 미니맵에서 "이동 중인 마차" 클릭 → 트레드밀 패널 열기
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템 1단계
//
// [역할] 미니맵 RawImage 클릭 위치를 맵 카메라 월드 좌표로 변환해, 가장 가까운 '캐러밴 마커'를
//        찾는다(MinimapMultiCaravanMarkers가 만든 "CaravanMarker_<id>" 오브젝트). 클릭 반경 안에
//        마차가 있으면 그 caravanId로 왼쪽 트레드밀 패널(TreadmillPanel)을 연다.
//
// [공존] 같은 RawImage의 MinimapCameraController(줌·패닝)·MinimapTownClickRouter(마을 클릭)와
//        공존한다. 각자 자기 대상만 처리(마을 라우터는 마을, 이 라우터는 마차). 이동 중 마차는
//        마을 위가 아니라 루트 중간에 있으므로 충돌이 드물다.
//
// [부착] 미니맵 RawImage(V2 RT 표시)에 붙인다. MinimapTownClickRouter와 같은 좌표 변환을 쓴다.
// =============================================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>미니맵 마차 클릭 → 왼쪽 트레드밀 패널 열기(그 마차 기준).</summary>
public class MinimapCaravanClickRouter : MonoBehaviour, IPointerClickHandler
{
    private const string MarkerPrefix = "CaravanMarker_";   // MinimapMultiCaravanMarkers가 붙이는 이름 규칙

    [SerializeField] private RawImage view;              // 미니맵 RawImage(비면 자기 자신)
    [SerializeField] private Transform renderRoot;       // 마커 탐색 범위(미니맵 렌더 루트, 비면 카메라 root)
    [SerializeField] private float hitRadius = 0.8f;     // 마차 클릭 판정 반경(월드 단위)
    [SerializeField] private TreadmillPanel treadmillPanel;   // 열 패널(비면 런타임 탐색)

    private Camera cachedCam;

    private void Awake() => EnsureWiring();

    /// <summary>인스펙터 배선 없이도 동작하도록 자동 연결(프리팹 교체만으로 얹기 위함).</summary>
    private void EnsureWiring()
    {
        if (view == null) view = GetComponent<RawImage>();
        if (renderRoot == null)
        {
            Camera cam = ResolveCamera();
            if (cam != null) renderRoot = cam.transform.root;
        }
        if (treadmillPanel == null)
            treadmillPanel = Object.FindAnyObjectByType<TreadmillPanel>(FindObjectsInactive.Include);
    }

    public void OnPointerClick(PointerEventData e)
    {
        EnsureWiring();   // Awake 때 아직 준비 안 됐을 수 있어 클릭 시 재확인
        string caravanId = ResolveClickedCaravan(e);
        if (string.IsNullOrEmpty(caravanId)) return;   // 반경 안에 마차 없음 → 무시(마을 라우터가 처리할 수도)
        if (treadmillPanel != null) treadmillPanel.Open(caravanId);
        else Debug.LogWarning("[Treadmill] 마차 클릭됨(" + caravanId + ")인데 TreadmillPanel을 못 찾음");
    }

    /// <summary>클릭 스크린 좌표 → 맵 카메라 월드 → 가장 가까운 마차의 caravanId(없으면 null).</summary>
    private string ResolveClickedCaravan(PointerEventData e)
    {
        Camera cam = ResolveCamera();
        if (cam == null || view == null) return null;

        RectTransform rt = view.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out Vector2 local))
            return null;

        // RawImage 로컬 → UV(0~1) → 카메라 뷰포트 → 월드(XY) : TownClickRouter와 동일 변환
        float u = Mathf.InverseLerp(rt.rect.xMin, rt.rect.xMax, local.x);
        float v = Mathf.InverseLerp(rt.rect.yMin, rt.rect.yMax, local.y);
        Vector3 world = cam.ViewportToWorldPoint(new Vector3(u, v, Mathf.Abs(cam.transform.position.z)));

        return FindNearestCaravanId(world, hitRadius);
    }

    /// <summary>월드 좌표 반경 안에서 가장 가까운 "CaravanMarker_*" 오브젝트의 caravanId.</summary>
    private string FindNearestCaravanId(Vector3 world, float radius)
    {
        Transform scope = (renderRoot != null) ? renderRoot : transform;
        string best = null; float bestSqr = radius * radius;
        foreach (Transform t in scope.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || !t.name.StartsWith(MarkerPrefix)) continue;
            if (!t.gameObject.activeInHierarchy) continue;   // 표시 중인 마커만(이동 중 마차)
            Vector2 d = (Vector2)t.position - (Vector2)world;
            if (d.sqrMagnitude <= bestSqr) { bestSqr = d.sqrMagnitude; best = t.name.Substring(MarkerPrefix.Length); }
        }
        return best;
    }

    /// <summary>이 RawImage의 RenderTexture를 그리는 카메라를 찾는다(미니맵 카메라).</summary>
    private Camera ResolveCamera()
    {
        if (cachedCam != null) return cachedCam;
        RenderTexture rtTex = (view != null) ? view.texture as RenderTexture : null;
        foreach (Camera cam in Camera.allCameras)
        {
            if (cam.targetTexture == null) continue;
            if (rtTex == null || cam.targetTexture == rtTex) { cachedCam = cam; return cam; }
        }
        return null;
    }
}
