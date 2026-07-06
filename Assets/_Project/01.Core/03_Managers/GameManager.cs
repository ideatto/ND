using UnityEngine;

/// <summary>
/// 게임 전역 매니저.
/// 씬이 바뀌어도 사라지지 않고(DontDestroyOnLoad) 게임 전체 상태를 관리한다.
/// BootScene에서 가장 먼저 생성되어 게임이 끝날 때까지 유지된다.
/// </summary>
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
    }
}