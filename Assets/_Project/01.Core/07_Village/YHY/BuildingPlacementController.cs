// =============================================================================
// BuildingPlacementController — 마을 건물 배치(드래그 이동 + 회전)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 마을이 RenderTexture(RawImage)로 보여지므로, 유저가 화면에서 건물을
//        클릭/드래그해도 그건 UI(RawImage) 클릭이다. 이 컨트롤러가 그 클릭을
//        "마을 카메라 광선"으로 변환해서 3D 건물을 집고, 바닥으로 드래그해 옮기고,
//        스크롤로 회전시킨다.
//
// [부착] 마을을 표시하는 RawImage(VillageView) 오브젝트에 붙인다.
//        RawImage.raycastTarget = true 여야 포인터 이벤트를 받는다.
//
// [의존] 건물 프리팹 = PlaceableBuilding + BoxCollider(집기용), 바닥 = Collider(놓기용).
//        마을 카메라는 RawImage의 RenderTexture를 그리는 카메라를 런타임에 찾는다.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>RawImage 클릭 → 마을 카메라 광선 → 건물 집기/드래그 이동/스크롤 회전.</summary>
public class BuildingPlacementController : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IScrollHandler
{
    [SerializeField] private RawImage view;            // RT를 그리는 RawImage(비면 자기 자신)
    [SerializeField] private Camera villageCamera;     // 마을 카메라(비면 런타임 탐색)
    [SerializeField] private float rotateStep = 15f;   // 스크롤 1노치당 회전 각도
    [SerializeField] private float buttonRotateStep = 45f;                 // ↺↻ 버튼 1회 각도
    [SerializeField] private Color highlightTint = new Color(1f, 0.85f, 0.4f); // 선택 하이라이트 색

    private Transform selected;   // 현재 집은 건물 루트
    private Camera uiCamera;      // 캔버스 렌더 카메라(Overlay면 null)

    // 선택 하이라이트 복원용(집은 건물의 렌더러 원래 색 저장)
    private readonly List<Renderer> tintedRenderers = new List<Renderer>();
    private readonly List<Color> tintedOriginals = new List<Color>();

    private void Awake()
    {
        if (view == null) view = GetComponent<RawImage>();
        Canvas canvas = GetComponentInParent<Canvas>();
        uiCamera = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera : null;
    }

    // ── 포인터 이벤트 ──
    /// <summary>누르는 순간: 광선으로 건물을 집는다(없으면 선택 해제).</summary>
    public void OnPointerDown(PointerEventData e)
    {
        if (IsOverButton(e.position)) return;   // 회전 버튼 클릭이면 선택 해제 안 함
        if (!TryMakeRay(e.position, out Ray ray)) return;

        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
        Transform best = null;
        float bestDist = float.MaxValue;
        foreach (RaycastHit h in hits)
        {
            PlaceableBuilding pb = h.collider.GetComponentInParent<PlaceableBuilding>();
            if (pb != null && h.distance < bestDist) { bestDist = h.distance; best = pb.transform; }
        }
        SetSelected(best);   // 빈 곳 클릭이면 null → 선택 해제
    }

    /// <summary>드래그: 바닥에 광선을 쏴 그 지점으로 건물을 옮긴다(자신·다른 건물은 무시).</summary>
    public void OnDrag(PointerEventData e)
    {
        if (selected == null) return;
        if (IsOverButton(e.position)) return;   // 버튼 위에서의 드래그로 건물이 튀지 않게
        if (!TryMakeRay(e.position, out Ray ray)) return;

        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
        // 가까운 것부터 정렬해 바닥(건물 아닌 것) 첫 히트를 찾는다.
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
        {
            if (h.transform.IsChildOf(selected)) continue;                         // 자기 자신
            if (h.collider.GetComponentInParent<PlaceableBuilding>() != null) continue; // 다른 건물
            Vector3 target = new Vector3(h.point.x, 0f, h.point.z);
            if (!WouldOverlap(selected, target)) selected.position = target;       // 겹치면 이동 안 함
            return;
        }
    }

    /// <summary>건물을 targetPos로 옮기면 다른 건물과 겹치는가(BoxCollider 기준).</summary>
    private bool WouldOverlap(Transform building, Vector3 targetPos)
    {
        BoxCollider bc = building.GetComponentInChildren<BoxCollider>();
        if (bc == null) return false;

        // 현재 → target 이동을 가정한 박스의 월드 파라미터
        Vector3 offset = targetPos - building.position;
        Vector3 worldCenter = building.TransformPoint(bc.center) + offset;
        Vector3 halfExtents = Vector3.Scale(bc.size, building.lossyScale) * 0.5f * 0.9f; // 살짝 줄여 인접 허용

        Collider[] hits = Physics.OverlapBox(worldCenter, halfExtents, building.rotation);
        foreach (Collider c in hits)
        {
            PlaceableBuilding pb = c.GetComponentInParent<PlaceableBuilding>();
            if (pb != null && pb.transform != building) return true;   // 다른 건물과 겹침
        }
        return false;
    }

    /// <summary>스크롤: 선택한 건물을 Y축으로만 회전.</summary>
    public void OnScroll(PointerEventData e)
    {
        if (selected == null) return;
        float dir = Mathf.Sign(e.scrollDelta.y);
        if (dir != 0f) ApplyYaw(rotateStep * dir);
    }

    private const float BtnW = 38f, BtnH = 38f, BtnOff = 52f;   // 회전 버튼 크기 + 중심에서 좌우 간격

    // 선택된 건물의 양 옆에 작은 좌/우 회전 버튼 표시(건물 따라다님. 임시 IMGUI, 추후 uGUI 교체 가능).
    private void OnGUI()
    {
        if (!TryButtonRects(out Rect left, out Rect right)) return;
        GUIStyle s = new GUIStyle(GUI.skin.button) { fontSize = 20 };
        // 화면좌표(좌하단 원점) → GUI좌표(좌상단 원점): y 뒤집기
        Rect lg = new Rect(left.x, Screen.height - left.y - left.height, left.width, left.height);
        Rect rg = new Rect(right.x, Screen.height - right.y - right.height, right.width, right.height);
        if (GUI.Button(lg, "◀", s)) RotateLeft();
        if (GUI.Button(rg, "▶", s)) RotateRight();
    }

    /// <summary>선택 건물 기준 좌/우 버튼의 화면좌표(좌하단 원점) 사각형. 선택 없거나 화면 밖이면 false.</summary>
    private bool TryButtonRects(out Rect left, out Rect right)
    {
        left = default(Rect); right = default(Rect);
        if (selected == null) return false;
        Collider col = selected.GetComponentInChildren<Collider>();
        Vector3 center = col != null ? col.bounds.center : selected.position;
        if (!TryWorldToScreen(center, out Vector2 sp)) return false;
        left = new Rect(sp.x - BtnOff - BtnW * 0.5f, sp.y - BtnH * 0.5f, BtnW, BtnH);
        right = new Rect(sp.x + BtnOff - BtnW * 0.5f, sp.y - BtnH * 0.5f, BtnW, BtnH);
        return true;
    }

    /// <summary>포인터가 회전 버튼 위에 있나(있으면 집기·드래그를 무시해 버튼 클릭만 처리).</summary>
    private bool IsOverButton(Vector2 screenPos)
    {
        if (!TryButtonRects(out Rect left, out Rect right)) return false;
        return left.Contains(screenPos) || right.Contains(screenPos);
    }

    /// <summary>마을 안 월드좌표 → 화면 좌표(RawImage/RT 경유). 카메라 뒤면 false.</summary>
    private bool TryWorldToScreen(Vector3 world, out Vector2 screen)
    {
        screen = default(Vector2);
        Camera cam = ResolveCamera();
        if (cam == null || view == null) return false;

        Vector3 vp = cam.WorldToViewportPoint(world);
        if (vp.z <= 0f) return false;

        RectTransform rt = view.rectTransform;
        Rect r = rt.rect;
        Vector2 local = new Vector2(Mathf.Lerp(r.xMin, r.xMax, vp.x), Mathf.Lerp(r.yMin, r.yMax, vp.y));
        screen = RectTransformUtility.WorldToScreenPoint(uiCamera, rt.TransformPoint(local));
        return true;
    }

    /// <summary>◀ 버튼(화면상 왼쪽으로 도는 느낌).</summary>
    public void RotateLeft() { ApplyYaw(buttonRotateStep); }

    /// <summary>▶ 버튼(화면상 오른쪽으로 도는 느낌).</summary>
    public void RotateRight() { ApplyYaw(-buttonRotateStep); }

    /// <summary>선택 건물을 Y축으로만 회전(X·Z는 항상 0으로 강제 → 다른 축 안 섞임).</summary>
    private void ApplyYaw(float deg)
    {
        if (selected == null) return;
        float y = selected.eulerAngles.y + deg;
        selected.rotation = Quaternion.Euler(0f, y, 0f);
    }

    // ── 선택 + 하이라이트 ──
    /// <summary>선택을 바꾸며, 이전 건물은 원래 색으로, 새 건물은 하이라이트 색으로.</summary>
    private void SetSelected(Transform t)
    {
        if (selected == t) return;
        ClearHighlight();
        selected = t;
        if (selected != null) ApplyHighlight(selected);
    }

    private void ApplyHighlight(Transform root)
    {
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            tintedRenderers.Add(r);
            tintedOriginals.Add(r.material.color);
            r.material.color = highlightTint;   // 런타임이라 material 인스턴스화됨(원본 에셋 영향 없음)
        }
    }

    private void ClearHighlight()
    {
        for (int i = 0; i < tintedRenderers.Count; i++)
            if (tintedRenderers[i] != null) tintedRenderers[i].material.color = tintedOriginals[i];
        tintedRenderers.Clear();
        tintedOriginals.Clear();
    }

    // ── RawImage 스크린좌표 → 마을 카메라 광선 ──
    private bool TryMakeRay(Vector2 screenPos, out Ray ray)
    {
        ray = default(Ray);
        Camera cam = ResolveCamera();
        if (cam == null || view == null) return false;

        RectTransform rt = view.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, uiCamera, out Vector2 local))
            return false;

        // RawImage 로컬좌표 → 0..1 뷰포트 UV
        Rect r = rt.rect;
        float u = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
        float v = Mathf.InverseLerp(r.yMin, r.yMax, local.y);
        if (u < 0f || u > 1f || v < 0f || v > 1f) return false;

        ray = cam.ViewportPointToRay(new Vector3(u, v, 0f));
        return true;
    }

    /// <summary>RawImage의 RenderTexture를 그리는 카메라를 찾는다.</summary>
    private Camera ResolveCamera()
    {
        if (villageCamera != null) return villageCamera;
        RenderTexture rtTex = view != null ? view.texture as RenderTexture : null;
        foreach (Camera c in Camera.allCameras)
        {
            if (c.targetTexture == null) continue;
            if (rtTex == null || c.targetTexture == rtTex) { villageCamera = c; return c; }
        }
        return null;
    }
}
