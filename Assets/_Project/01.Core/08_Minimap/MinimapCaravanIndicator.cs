// =============================================================================
// MinimapCaravanIndicator — 미니맵에 "정박 중인 캐러밴"의 현재 마을 위치를 표시
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 캐러밴이 무역 중이 아닐 때(거점 등에 정박), 캐러밴이 있는 마을 위에
//        아이콘을 띄워 "여기 캐러밴이 있다"를 보여준다.
//        무역 중일 때는 기존 CaravanMapMarker(경로 위 진행 표시)가 담당하므로 숨긴다.
//
// [데이터] FrameworkRoot.CurrentSaveData.caravans[0].currentTownId 를 읽고,
//          같은 townId를 가진 미니맵 TownWorldView 위치에 아이콘을 올린다.
//          진행 여부는 TradeProgressCoordinator.TryGetMapProgress로 판단(읽기 전용).
//
// [부착] V2 렌더 루트(WorldMapRenderRootV2)에 붙인다. 아이콘은 런타임에 자동 생성.
// =============================================================================

using UnityEngine;
using ND.UI.WorldMap;

/// <summary>정박 중인 캐러밴을 현재 마을 위에 아이콘으로 표시한다.</summary>
public class MinimapCaravanIndicator : MonoBehaviour
{
    [SerializeField] private Transform renderRoot;                 // 마을 탐색 범위(비면 자기 자신)
    [SerializeField] private Color iconColor = new Color(0.95f, 0.4f, 0.15f, 1f); // 캐러밴 아이콘 색(주황)
    [SerializeField] private float iconScale = 0.35f;              // 아이콘 크기(월드)
    [SerializeField] private Vector3 offset = new Vector3(0f, 0.35f, 0f); // 마을 위로 살짝

    private SpriteRenderer icon;

    private void Awake()
    {
        if (renderRoot == null) renderRoot = transform;

        // 표시용 아이콘 스프라이트를 런타임에 만든다(별도 에셋 불필요).
        var go = new GameObject("CaravanLocationIndicator");
        go.transform.SetParent(renderRoot, false);
        icon = go.AddComponent<SpriteRenderer>();
        icon.sprite = MakeSquareSprite(iconColor);
        icon.sortingOrder = 30;                 // 마을(10)보다 위
        go.transform.localScale = Vector3.one * iconScale;
        icon.enabled = false;
    }

    private void LateUpdate()
    {
        if (icon == null) return;

        var fr = ND.Framework.FrameworkRoot.Instance;
        if (fr == null) { icon.enabled = false; return; }

        // 무역 진행 중이면 정박 아이콘 숨김(진행 마커가 담당)
        var coord = fr.TradeProgressCoordinator;
        if (coord != null && coord.TryGetMapProgress(out var snap) && snap.HasActiveTrade)
        {
            icon.enabled = false;
            return;
        }

        // 캐러밴 현재 마을 → 같은 townId 마을 위치에 아이콘
        string townId = GetCaravanTownId(fr.CurrentSaveData);
        TownWorldView town = FindTown(townId);
        if (town == null) { icon.enabled = false; return; }

        icon.transform.position = town.transform.position + offset;
        icon.enabled = true;
    }

    /// <summary>첫 캐러밴의 현재 마을 ID(없으면 null).</summary>
    private static string GetCaravanTownId(ND.Framework.SaveData save)
    {
        if (save == null || save.caravans == null || save.caravans.Count == 0) return null;
        var c = save.caravans[0];
        return c != null ? c.currentTownId : null;
    }

    /// <summary>renderRoot 아래에서 townId가 일치하는 마을을 찾는다.</summary>
    private TownWorldView FindTown(string townId)
    {
        if (string.IsNullOrEmpty(townId)) return null;
        foreach (var t in renderRoot.GetComponentsInChildren<TownWorldView>(true))
            if (t.TownId == townId) return t;
        return null;
    }

    /// <summary>단색 정사각형 스프라이트 생성(아이콘용).</summary>
    private static Sprite MakeSquareSprite(Color color)
    {
        var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        var px = new Color[64];
        for (int i = 0; i < px.Length; i++) px[i] = color;
        tex.SetPixels(px); tex.Apply();
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
    }
}
