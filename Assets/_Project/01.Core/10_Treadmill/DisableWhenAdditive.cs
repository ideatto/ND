// =============================================================================
// DisableWhenAdditive — 이 씬이 additive로 얹히면 해당 GameObject 자동 비활성
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템 · 편집 보조
//
// [역할] Treadmill_Preview를 단독으로 열 땐 필요하지만, 게임 씬(InGame 등)에 additive로
//        얹힐 땐 충돌/중복되는 오브젝트(예: 프리뷰 전용 방향광)를 자동으로 끈다.
//        (EditorPreviewCamera가 카메라·오디오에 하는 처리의 범용판.)
//
// [예] Preview의 Directional Light에 붙이면 — 단독 편집 시 켜지고, 게임에 얹히면 꺼져서
//      InGame 월드가 두 개의 태양광으로 이중 조명되는 걸 막는다(트레드밀은 InGame 광으로 켜짐).
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>이 씬이 활성 씬이 아니면(additive로 얹힘) 이 GameObject를 비활성화한다.</summary>
public class DisableWhenAdditive : MonoBehaviour
{
    private void Awake()
    {
        // additive로 얹힌 경우(활성 씬이 이 씬이 아님) → 조용히 끔
        if (gameObject.scene != SceneManager.GetActiveScene())
            gameObject.SetActive(false);
    }
}
