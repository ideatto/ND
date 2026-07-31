// =============================================================================
// EditorPreviewCamera — 프리뷰 편집용 화면 카메라(additive로 얹히면 자동 꺼짐)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템 · 편집 보조
//
// [역할] Treadmill_Preview를 '단독으로 열어 편집/플레이'할 때 화면(Game 뷰)에 트레드밀을
//        보여주기 위한 임시 카메라. 런타임 출력용 Main Camera는 RenderTexture로만 나가서
//        단독으로 열면 화면에 아무것도 안 보이기 때문.
//
// [핵심] 이 씬이 다른 씬(InGame/Test3)에 additive로 얹히면(=활성 씬이 이 씬이 아니면)
//        이 카메라와 AudioListener를 자동으로 꺼서 메인 카메라/리스너와 충돌하지 않게 한다.
//        → 프리뷰 단독 편집: 화면 보임 / 게임에 얹힘: 조용히 비활성.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>프리뷰 단독일 때만 화면에 렌더하는 편집용 카메라(additive 시 자동 비활성).</summary>
[RequireComponent(typeof(Camera))]
public class EditorPreviewCamera : MonoBehaviour
{
    private void Awake()
    {
        // additive로 얹힌 경우(활성 씬이 이 씬이 아님) → 화면 카메라/리스너 끔
        if (gameObject.scene != SceneManager.GetActiveScene())
        {
            var cam = GetComponent<Camera>();
            if (cam != null) cam.enabled = false;
            var al = GetComponent<AudioListener>();
            if (al != null) al.enabled = false;
        }
    }
}
