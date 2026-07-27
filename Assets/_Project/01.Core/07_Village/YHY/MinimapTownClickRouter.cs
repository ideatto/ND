// =============================================================================
// MinimapTownClickRouter — 미니맵에서 "활성 무역마을" 클릭 → 무역마을 화면 전환
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 미니맵 RawImage 클릭 위치를 맵 카메라 월드 좌표로 변환해 가장 가까운 마을을 찾고,
//        그 마을이 "활성"이면 TradeTownCameraMover로 VillageCamera를 그 마을 좌표로 이동시킨다.
//        (거점·무역마을이 Village_Home 한 씬에 서로 다른 좌표로 있고, 카메라만 이동 → 하나의 큰 맵으로 확장 가능)
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
    [SerializeField] private float hitRadius = 1.2f;    // 마을 클릭 판정 반경(월드 단위)
    [SerializeField] private string homeTownId = "BaseCamp"; // 거점 townId — 언제나 활성(홈)

    /// <summary>현재 카메라가 비추는 마을 townId. 이름표 등에서 참조.</summary>
    public string CurrentTownId { get; private set; }

    private Camera cachedCam;

    public void OnPointerClick(PointerEventData e)
    {
        string townId = ResolveClickedTown(e);
        if (string.IsNullOrEmpty(townId)) return;
        HandleTownClicked(townId);
    }

    /// <summary>
    /// 클릭한 townId 처리 — 활성이면 VillageCamera를 그 마을 좌표로 이동.
    /// - 거점(homeTownId): 언제나 활성.
    /// - 그 외: 캐러밴이 있는 마을만 활성(도착 후).
    /// </summary>
    public void HandleTownClicked(string townId)
    {
        if (string.IsNullOrEmpty(townId)) return;

        // 거점은 무조건 활성. 무역마을은 캐러밴이 "정박(도착)"해 있어야 활성.
        if (townId != homeTownId && !IsCaravanDockedAt(townId))
        {
            Debug.Log($"[MinimapTownClickRouter] '{townId}'는 비활성(정박 캐러밴 없음/이동중) — 진입 안 함");
            return;
        }

        // 카메라를 그 마을 좌표로 이동(enable/disable 아님 — 한 씬 안에서 카메라만 이동).
        TradeTownCameraMover.RequestedTownId = townId;
        CloseMinimap();
        CurrentTownId = townId;   // 이름표가 표시할 현재 마을 기록
        Debug.Log($"[MinimapTownClickRouter] '{townId}' 활성 → 카메라 이동(지도 닫음)");
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

    /// <summary>
    /// 그 마을에 "정박(이동 중 아님)"한 캐러밴이 있으면 true.
    /// SaveData를 직접 읽으므로 미니맵 마커 표시 방식과 무관하다(다중 캐러밴 대응).
    /// </summary>
    private bool IsCaravanDockedAt(string townId)
    {
        var fr = ND.Framework.FrameworkRoot.Instance;
        var save = fr != null ? fr.CurrentSaveData : null;
        if (save == null || save.caravans == null) return false;

        for (int i = 0; i < save.caravans.Count; i++)
        {
            var c = save.caravans[i];
            if (c == null || c.currentTownId != townId) continue;
            if (IsTraveling(save, c.caravanId)) continue;   // 이동 중이면 currentTownId는 출발지일 뿐 — "정박" 아님
            return true;
        }
        return false;
    }

    /// <summary>해당 캐러밴이 지금 이동(Traveling) 중인가.</summary>
    private static bool IsTraveling(ND.Framework.SaveData save, string caravanId)
    {
        if (save.tradeProgressEntries == null) return false;
        for (int i = 0; i < save.tradeProgressEntries.Count; i++)
        {
            var e = save.tradeProgressEntries[i];
            if (e != null && e.caravanId == caravanId
                && e.state == ND.Framework.TradeProgressState.Traveling) return true;
        }
        return false;
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
