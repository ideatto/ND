using UnityEngine;

/// <summary>
/// 어느 씬에서 게임을 시작하든(예: 개발 중 InGame에서 바로 Play),
/// 공용 매니저(Managers 프리팹)를 자동으로 생성해준다.
/// 덕분에 팀원은 Boot를 거치지 않고 InGame에서 바로 작업할 수 있다.
/// </summary>
public static class ManagerBootstrap
{
    // 씬이 로드되기 "전에" Unity가 자동으로 호출해주는 함수
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateManagers()
    {
        // 이미 매니저가 있으면(예: Boot에서 정상 시작) 중복 생성하지 않는다
        if (GameManager.Instance != null) return;

        // Resources 폴더에서 Managers 프리팹을 불러온다
        GameObject prefab = Resources.Load<GameObject>("Managers");

        // 씬에 실제로 생성 → 안의 Awake가 돌며 싱글턴 등록 + DontDestroyOnLoad 실행
        Object.Instantiate(prefab);
    }
}