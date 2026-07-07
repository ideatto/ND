// =============================================================================
// GameManager — 게임 전역 매니저
// =============================================================================
// [작성] 윤호영
// [영역] Framework & Integration (공용 매니저) — 통합 시 담당 조율 필요
//
// [핵심 포인트]
//  · 씬이 바뀌어도 사라지지 않고(DontDestroyOnLoad) 게임 전체 상태를 관리한다.
//  · 싱글턴(GameManager.Instance)이라 어디서든 접근 가능.
//  · 게임에 딱 하나만 존재하도록 중복을 스스로 제거한다.
//  · ManagerBootstrap이 게임 시작 시 자동으로 생성한다. (Managers 프리팹에 포함)
// =============================================================================

using UnityEngine;

/// <summary>게임 전역 매니저. 자세한 설명은 상단 주석 참고.</summary>
public class GameManager : MonoBehaviour
{
    // 어디서든 GameManager.Instance 로 접근할 수 있게 하는 싱글턴(유일한 인스턴스) 참조
    public static GameManager Instance { get; private set; }

    // 오브젝트가 처음 생성될 때 딱 한 번 호출되는 함수
    private void Awake()
    {
        // 이미 다른 GameManager가 존재하면, 나는 중복이므로 스스로 삭제한다
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 내가 최초의 GameManager라면 싱글턴으로 등록한다
        Instance = this;

        // 씬이 바뀌어도 이 오브젝트를 파괴하지 않도록 설정한다
        DontDestroyOnLoad(gameObject);

        Debug.Log("[GameManager] 로드 완료");
    }
}
