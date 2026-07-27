// =============================================================================
// MinimapTownClickRouter — 미니맵에서 "활성 무역마을" 클릭 → 무역마을 화면 전환
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 미니맵 RawImage 클릭 위치를 맵 카메라 월드 좌표로 변환해 가장 가까운 마을을 찾고,
//        그 마을이 "활성"(=캐러밴이 현재 있는 마을)이면 TradeTownView.Show()로 전환한다.
//        (미니맵은 RenderTexture라 마을 콜라이더가 화면 클릭을 직접 못 받으므로 좌표 변환이 필요)
//
// [활성 판정] FrameworkRoot.CurrentSaveData.caravans[0].currentTownId 와 클릭한 townId가 같으면 활성.
//        무역 중(TryGetMapProgress.HasActiveTrade)엔 진입 막음(도착 후에만).
//
// [부착] 미니맵 RawImage(V2 RT 표시하는 것)에 붙인다. drag/scroll(줌·패닝)과 공존(클릭만 처리).
// =============================================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ND.UI.WorldMap;

/// <summary>미니맵 클릭 → 활성 무역마을이면 무역마을 화면으로 전환.</summary>
public class MinimapTownClickRouter : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private RawImage view;             // 미니맵 RawImage(RT 표시)
    [SerializeField] private Transform renderRoot;      // 마을 탐색 범위(V2 렌더 루트)
    [SerializeField] private TradeTownView tradeTownView; // 활성 마을 클릭 시 열 화면
    [SerializeField] private float hitRadius = 1.2f;    // 마을 클릭 판정 반경(월드 단위)
    [SerializeField] private string homeTownId = "BaseCamp"; // 거점 townId — 언제나 활성(홈)

    /// <summary>현재 진입해 표시 중인 무역마을 townId (없으면 null). 이름표 등에서 참조.</summary>
    public string CurrentTownId { get; private set; }

    private Camera cachedCam;

    public void OnPointerClick(PointerEventData e)
    {
        string townId = ResolveClickedTown(e);
        if (string.IsNullOrEmpty(townId)) return;
        HandleTownClicked(townId);
    }

    /// <summary>
    /// 클릭한 townId 처리.
    /// - 거점(homeTownId): 언제나 활성 → 거점 화면(무역마을 숨김)으로.
    /// - 그 외: 캐러밴이 있는 마을만 활성 → 그 무역마을 화면으로.
    /// </summary>
    public void HandleTownClicked(string townId)
    {
        if (string.IsNullOrEmpty(townId)) return;

        // 거점은 무조건 활성 — 누르면 거점 화면으로 돌아간다.
        if (townId == homeTownId)
        {
            if (tradeTownView != null) tradeTownView.Hide();   // 무역마을 숨김 → 거점(VillageView) 노출
            CloseMinimap();
            CurrentTownId = null;
            Debug.Log($"[MinimapTownClickRouter] 거점({townId}) 클릭 → 거점 화면");
            return;
        }

        // 그 외 마을: 캐러밴이 지금 그 마을에 있어야(도착) 활성.
        if (townId != GetCaravanMapTownId())
        {
            Debug.Log($"[MinimapTownClickRouter] '{townId}'는 비활성(캐러밴 없음/이동중) — 진입 안 함");
            return;
        }
        if (tradeTownView != null)
        {
            tradeTownView.Show();
            CloseMinimap();
            CurrentTownId = townId;   // 이름표가 표시할 현재 무역마을 기록
            Debug.Log($"[MinimapTownClickRouter] '{townId}' 활성 → 무역마을 화면 열기(지도 닫음)");
        }
    }

    /// <summary>열려 있던 지도(SlidePanel)를 정상 방식으로 닫는다(지도버튼으로 재열기 가능).</summary>
    private void CloseMinimap()
    {
        var slide = GetComponentInParent<SlidePanel>();
        if (slide != null && slide.IsOpen) slide.SetOpen(false);
    }

    /// <summary>클릭 스크린 좌표 → 맵 카메라 월드 → 가장 가까운 마을의 townId.</summary>
    private string ResolveClickedTown(PointerEventData e)
    {
        Camera cam = ResolveCamera();
        if (cam == null || view == null) return null;

        RectTransform rt = view.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out Vector2 local))
            return null;

        // RawImage 로컬 → UV(0~1) → 카메라 뷰포트 → 월드(XY)
        float u = Mathf.InverseLerp(rt.rect.xMin, rt.rect.xMax, local.x);
        float v = Mathf.InverseLerp(rt.rect.yMin, rt.rect.yMax, local.y);
        Vector3 world = cam.ViewportToWorldPoint(new Vector3(u, v, Mathf.Abs(cam.transform.position.z)));

        return FindNearestTownId(world, hitRadius);
    }

    /// <summary>월드 좌표에서 radius 안의 가장 가까운 마을 townId(없으면 null).</summary>
    private string FindNearestTownId(Vector3 world, float radius)
    {
        Transform scope = (renderRoot != null) ? renderRoot : transform;
        string best = null; float bestSqr = radius * radius;
        foreach (var t in scope.GetComponentsInChildren<TownWorldView>(true))
        {
            Vector2 d = (Vector2)t.transform.position - (Vector2)world;
            if (d.sqrMagnitude <= bestSqr) { bestSqr = d.sqrMagnitude; best = t.TownId; }
        }
        return best;
    }

    /// <summary>캐러밴 마커의 현재 지도 위치에서 그 위에 있는 마을 townId(마을 위가 아니면 null).</summary>
    private string GetCaravanMapTownId()
    {
        Transform scope = (renderRoot != null) ? renderRoot : transform;

        // 캐러밴이 지도에서 실제 그려지는 위치: 진행 마커(이동/도착) 우선, 없으면 정박 인디케이터
        Vector3? caravanPos = null;
        var marker = scope.GetComponentInChildren<CaravanMapMarker>(true);
        if (marker != null && marker.gameObject.activeInHierarchy) caravanPos = marker.transform.position;
        if (caravanPos == null)
        {
            var indicator = scope.Find("CaravanLocationIndicator");   // MinimapCaravanIndicator가 만든 아이콘
            if (indicator != null)
            {
                var sr = indicator.GetComponent<SpriteRenderer>();
                if (sr != null && sr.enabled) caravanPos = indicator.position;
            }
        }
        if (caravanPos == null) return null;

        // 마커가 "마을 위"에 있어야 그 마을이 활성 (이동 중이면 마을 사이라 반경 밖 → null)
        const float onTownRadius = 0.4f;
        return FindNearestTownId(caravanPos.Value, onTownRadius);
    }

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
