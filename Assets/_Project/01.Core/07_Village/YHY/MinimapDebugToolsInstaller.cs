// =============================================================================
// MinimapDebugToolsInstaller — 에디터/디버그용 미니맵 툴 설치기(프리팹)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 개발 디버그 도구
//
// [역할] 이 프리팹을 씬에 두면, 런타임(에디터)에서 미니맵 RawImage에
//        MinimapGridPainter(지형 페인트) · MinimapEventPlacer(이벤트 배치)를
//        얹어준다. 프리팹을 빼면 안 붙는다.
//
// [의도] 페인트·이벤트배치 툴은 클릭을 받으려면 미니맵 RawImage(공유 MainUICanvas 안)에
//        붙어야 하는데, 공유 프리팹을 더럽히지 않으려고 '설치기' 방식으로 분리했다.
//        editorOnly=true면 실제 빌드에는 디버그 툴이 뜨지 않는다.
//
// [사용] MinimapEditorTools 프리팹을 InGame_Test 등 개발 씬에 하나 놓기만 하면 됨.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;

/// <summary>미니맵 RawImage에 디버그 툴(페인트·이벤트배치)을 런타임에 설치(에디터용).</summary>
public class MinimapDebugToolsInstaller : MonoBehaviour
{
    [SerializeField] private bool editorOnly = true;   // true면 에디터(플레이 포함)에서만 설치, 빌드엔 미설치
    [SerializeField] private RawImage view;            // 비면 자동 탐색(V2 RT 표시하는 미니맵 RawImage)

    private void Start()
    {
        if (editorOnly && !Application.isEditor) return;

        RawImage v = view != null ? view : FindMinimapRawImage();
        if (v == null) { Debug.LogWarning("[디버그툴] 미니맵 RawImage를 못 찾음 — 설치 건너뜀"); return; }

        // 이미 있으면 중복 추가 안 함
        if (v.GetComponent<MinimapGridPainter>() == null) v.gameObject.AddComponent<MinimapGridPainter>();
        if (v.GetComponent<MinimapEventPlacer>() == null) v.gameObject.AddComponent<MinimapEventPlacer>();
    }

    /// <summary>V2 렌더텍스처를 표시하는 미니맵 RawImage 탐색(라우터 있는 진짜 미니맵 우선).</summary>
    private static RawImage FindMinimapRawImage()
    {
        RawImage byRt = null;
        foreach (var img in FindObjectsOfType<RawImage>(true))
        {
            if (!(img.texture is RenderTexture rt) || rt.name == null || !rt.name.Contains("WorldMapRenderTextureV2")) continue;
            if (img.GetComponent<MinimapTownClickRouter>() != null) return img;   // 마을 라우터 있는 미니맵 우선
            if (byRt == null) byRt = img;
        }
        return byRt;
    }
}
