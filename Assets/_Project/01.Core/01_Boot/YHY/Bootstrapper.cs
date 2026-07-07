// =============================================================================
// Bootstrapper — Boot 씬 시작점 (Title로 넘기기)
// =============================================================================
// [작성] 윤호영
// [영역] Framework & Integration (씬 흐름) — 통합 시 담당 조율 필요
//
// [핵심 포인트]
//  · Boot 씬에 하나 두면, 게임 시작 시 딱 한 번 실행된다.
//  · Start()에서 SceneLoader를 통해 타이틀(Title) 화면으로 넘긴다.
//  · Awake(매니저 등록) 다음에 Start가 실행되므로, SceneLoader가 준비된 뒤 안전하게 호출된다.
//  · 매니저 자체는 ManagerBootstrap이 자동 생성하므로, 여기서는 화면 전환만 담당한다.
// =============================================================================

using UnityEngine;

/// <summary>Boot 씬 시작점. 자세한 설명은 상단 주석 참고.</summary>
public class Bootstrapper : MonoBehaviour
{
    // Awake(매니저 등록) 다음에 실행되는 Start → SceneLoader가 준비된 뒤 안전하게 호출
    private void Start()
    {
        // 씬 로더를 통해 타이틀 화면으로 이동
        SceneLoader.Instance.LoadScene("Title");
    }
}
