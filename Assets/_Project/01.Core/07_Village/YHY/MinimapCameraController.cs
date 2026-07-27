// =============================================================================
// MinimapCameraController — 미니맵(2D XY 평면) 전용 드래그 패닝 + 스크롤 줌
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 미니맵 RawImage 위에서 드래그로 카메라를 X·Y로 이동, 스크롤로 줌.
//        ※ 거점/무역마을용 카메라는 바닥이 XZ 평면(카메라 비스듬)이지만,
//          미니맵은 XY 정면 뷰라 별도 컨트롤러가 필요하다.
//          이 컨트롤러는 XY 평면 기준이라 상하좌우 모두 이동된다.
//
// [경계 클램프] 카메라 뷰가 맵(mapCenter±mapHalfSize) 밖으로 안 나가게 제한.
//        줌 인 하면 이동 가능 범위가 넓어져 가장자리(밑 포함)까지 볼 수 있고,
//        줌 아웃해 맵보다 커지면 중앙 고정(맵 전체 표시).
//
// [카메라 탐색] RawImage가 그리는 RenderTexture와 같은 targetTexture 카메라를 찾는다.
// =============================================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>미니맵(XY 평면) 카메라 드래그 패닝 + 줌 (맵 경계 클램프).</summary>
public class MinimapCameraController : MonoBehaviour, IDragHandler, IScrollHandler
{
    [SerializeField] private RawImage view;            // RT를 그리는 RawImage (비면 자기 자신)

    [Header("줌")]
    [SerializeField] private float zoomStep = 0.5f;
    [SerializeField] private float zoomMin = 2.5f;     // 최대 확대
    [SerializeField] private float zoomMax = 6.5f;     // 전체 맵 보기

    [Header("패닝")]
    [SerializeField] private float panSpeed = 0.01f;

    [Header("맵 경계 (월드 XY)")]
    [SerializeField] private Vector2 mapCenter;        // 맵 중심
    [SerializeField] private Vector2 mapHalfSize;      // 맵 반크기(가로/세로 절반)

    private Camera cachedCam;

    private void Awake()
    {
        if (view == null) view = GetComponent<RawImage>();
    }

    public void OnScroll(PointerEventData e)
    {
        Camera cam = ResolveCamera();
        if (cam == null || !cam.orthographic) return;
        float dir = Mathf.Sign(e.scrollDelta.y);
        if (dir == 0f) return;
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - dir * zoomStep, zoomMin, zoomMax);
        ClampToBounds(cam);   // 줌 바뀌면 경계도 다시 맞춤
    }

    public void OnDrag(PointerEventData e)
    {
        Camera cam = ResolveCamera();
        if (cam == null) return;

        // 줌 상태에 비례한 이동량 (확대 상태에선 조금, 축소 상태에선 많이)
        float scale = panSpeed * (cam.orthographicSize / 6f);

        // 화면 드래그 반대로 카메라를 밀어 "지도를 잡고 끄는" 느낌 (XY 평면)
        Vector3 p = cam.transform.position;
        p.x -= e.delta.x * scale;
        p.y -= e.delta.y * scale;
        cam.transform.position = p;

        ClampToBounds(cam);
    }

    /// <summary>카메라 뷰가 맵 경계를 벗어나지 않게 X·Y를 제한한다(줌 크기 반영).</summary>
    private void ClampToBounds(Camera cam)
    {
        float viewHalfH = cam.orthographicSize;
        float viewHalfW = cam.orthographicSize * cam.aspect;

        // 뷰 절반이 맵 절반보다 작을 때만 이동 여유가 생긴다(음수면 0=중앙 고정).
        float maxX = Mathf.Max(0f, mapHalfSize.x - viewHalfW);
        float maxY = Mathf.Max(0f, mapHalfSize.y - viewHalfH);

        Vector3 p = cam.transform.position;
        p.x = Mathf.Clamp(p.x, mapCenter.x - maxX, mapCenter.x + maxX);
        p.y = Mathf.Clamp(p.y, mapCenter.y - maxY, mapCenter.y + maxY);
        cam.transform.position = p;
    }

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
