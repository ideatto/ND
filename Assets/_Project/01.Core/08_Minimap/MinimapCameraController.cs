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
using ND.UI.WorldMap;

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

    [Header("맵 경계")]
    // autoBounds=true면 렌더 루트의 실제 콘텐츠(배경 스프라이트/마을)에서 경계를 자동 계산한다.
    // → 렌더 루트를 어느 위치에 두든(프리팹 드롭) 맵이 사라지지 않는다. false면 아래 수동값 사용.
    [SerializeField] private bool autoBounds = true;
    [SerializeField] private float boundsPadding = 0.5f; // 자동 경계에 더할 여유(월드)
    [SerializeField] private Vector2 mapCenter;        // (수동) 맵 중심
    [SerializeField] private Vector2 mapHalfSize;      // (수동) 맵 반크기

    private Camera cachedCam;
    private bool boundsComputed;

    private void Awake()
    {
        if (view == null) view = GetComponent<RawImage>();
    }

    /// <summary>
    /// 렌더 루트(카메라의 최상위 부모)의 실제 콘텐츠에서 맵 중심·크기를 1회 계산한다.
    /// 우선순위: 가장 큰 SpriteRenderer(배경 맵 아트) → 없으면 마을(TownWorldView) 바운즈.
    /// 절대 좌표 하드코딩을 대체해, 렌더 루트 위치와 무관하게 클램프가 맞도록 한다.
    /// </summary>
    private void ComputeBoundsIfNeeded(Camera cam)
    {
        if (boundsComputed || !autoBounds || cam == null) return;
        Transform root = cam.transform.root;

        // 1) 가장 큰 SpriteRenderer = 배경 맵 아트
        SpriteRenderer bg = null; float bestArea = -1f;
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null || sr.sprite == null) continue;
            Vector3 s = sr.bounds.size;
            float area = s.x * s.y;
            if (area > bestArea) { bestArea = area; bg = sr; }
        }
        if (bg != null)
        {
            Bounds b = bg.bounds;
            mapCenter = b.center;
            mapHalfSize = new Vector2(b.extents.x + boundsPadding, b.extents.y + boundsPadding);
            boundsComputed = true;
            return;
        }

        // 2) 폴백: 마을 위치들의 바운즈
        var towns = root.GetComponentsInChildren<TownWorldView>(true);
        if (towns.Length == 0) return;   // 아직 준비 안 됨 — 다음 기회에 재시도
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        foreach (var t in towns)
        {
            Vector2 p = t.transform.position;
            min = Vector2.Min(min, p); max = Vector2.Max(max, p);
        }
        mapCenter = (min + max) * 0.5f;
        mapHalfSize = (max - min) * 0.5f + Vector2.one * (boundsPadding + 1f);
        boundsComputed = true;
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
        ComputeBoundsIfNeeded(cam);   // 최초 1회 실제 콘텐츠에서 경계 산출
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
