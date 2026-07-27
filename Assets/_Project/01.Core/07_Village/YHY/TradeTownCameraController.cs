// =============================================================================
// TradeTownCameraController — 무역마을 화면의 카메라 조작(드래그 패닝 + 스크롤 줌)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 거점마을(BuildingPlacementController)의 카메라 조작 부분만 떼와 만든 것.
//        무역마을 RawImage 위에서 드래그하면 카메라 이동(패닝), 스크롤하면 줌.
//        ※ 건물 선택·이동·회전은 넣지 않는다 (무역마을은 "구경"만 — 편집 없음).
//
// [카메라 탐색] RawImage가 그리는 RenderTexture(RT_TradeTown)와 같은 targetTexture를
//        가진 카메라를 찾는다. (거점마을과 동일한 방식)
//
// [부착] TradeTownView 프리팹(무역마을 RawImage)에 같이 붙는다.
// =============================================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>무역마을 카메라 드래그 패닝 + 스크롤 줌 (건물 편집 없음).</summary>
public class TradeTownCameraController : MonoBehaviour, IDragHandler, IScrollHandler
{
    [SerializeField] private RawImage view;            // RT를 그리는 RawImage (비면 자기 자신)

    [Header("줌")]
    [SerializeField] private float zoomStep = 1f;      // 스크롤 1노치당 ortho size 변화
    [SerializeField] private float zoomMin = 4f;       // 최대 확대(가까이)
    [SerializeField] private float zoomMax = 15f;      // 최대 축소(멀리)

    [Header("패닝")]
    [SerializeField] private float panSpeed = 0.01f;   // 드래그 픽셀당 이동량(월드 m)
    [SerializeField] private float panRange = 12f;     // 마을 중심에서 벗어날 수 있는 최대 거리(m)

    private Camera cachedCam;    // 찾은 무역마을 카메라(캐시)
    private Vector3 camHome;     // 카메라 기본 위치(범위 제한 기준)
    private bool camHomeSet;

    private void Awake()
    {
        if (view == null) view = GetComponent<RawImage>();
    }

    /// <summary>드래그: 카메라 평행 이동(패닝). 건물 이동 없음.</summary>
    public void OnDrag(PointerEventData e)
    {
        PanCamera(e.delta);
    }

    /// <summary>스크롤: 카메라 줌만(회전 없음).</summary>
    public void OnScroll(PointerEventData e)
    {
        float dir = Mathf.Sign(e.scrollDelta.y);
        if (dir == 0f) return;
        ApplyZoom(dir);
    }

    /// <summary>직교 size 조절. dir&gt;0=확대(size↓), dir&lt;0=축소(size↑).</summary>
    private void ApplyZoom(float dir)
    {
        Camera cam = ResolveCamera();
        if (cam == null || !cam.orthographic) return;
        float next = cam.orthographicSize - dir * zoomStep;
        cam.orthographicSize = Mathf.Clamp(next, zoomMin, zoomMax);
    }

    /// <summary>
    /// 드래그 방향과 반대로 카메라를 밀어 "바닥을 잡고 끄는" 느낌. 카메라 로컬축 기준(비스듬한 뷰 대응).
    /// 기본 위치에서 panRange 안으로 제한해 마을을 너무 벗어나지 않게 한다.
    /// </summary>
    private void PanCamera(Vector2 dragDelta)
    {
        Camera cam = ResolveCamera();
        if (cam == null) return;

        if (!camHomeSet) { camHome = cam.transform.position; camHomeSet = true; }

        float scale = panSpeed * (cam.orthographicSize / 6f);   // 줌 상태에 비례해 이동량 보정

        Vector3 right = cam.transform.right;
        Vector3 up = cam.transform.up;
        right.y = 0f; up.y = 0f;
        right.Normalize(); up.Normalize();

        Vector3 move = (-right * dragDelta.x - up * dragDelta.y) * scale;
        Vector3 next = cam.transform.position + move;

        Vector3 offset = next - camHome;
        offset.y = 0f;
        if (offset.magnitude > panRange) offset = offset.normalized * panRange;
        cam.transform.position = new Vector3(camHome.x + offset.x, camHome.y, camHome.z + offset.z);
    }

    /// <summary>RawImage가 그리는 RT와 같은 targetTexture를 쓰는 카메라를 찾는다(한 번 찾으면 캐시).</summary>
    private Camera ResolveCamera()
    {
        if (cachedCam != null) return cachedCam;
        RenderTexture rtTex = (view != null) ? view.texture as RenderTexture : null;
        foreach (Camera c in Camera.allCameras)
        {
            if (c.targetTexture == null) continue;
            if (rtTex == null || c.targetTexture == rtTex) { cachedCam = c; return c; }
        }
        return null;
    }
}
