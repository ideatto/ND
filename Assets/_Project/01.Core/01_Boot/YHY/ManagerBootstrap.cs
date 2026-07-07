// =============================================================================
// ManagerBootstrap — 공용 매니저 자동 생성
// =============================================================================
// [작성] 윤호영
// [영역] Framework & Integration (매니저·초기화) — 통합 시 담당 조율 필요
//
// [핵심 포인트]
//  · 어느 씬에서 게임을 시작하든(예: 개발 중 InGame에서 바로 Play) 매니저를 자동 생성한다.
//  · [RuntimeInitializeOnLoadMethod]로 "씬 로드 전"에 Unity가 자동 호출한다.
//  · Resources 폴더의 Managers 프리팹을 불러와 생성한다. (프리팹 이름 "Managers" 고정)
//  · 이미 매니저가 있으면(예: Boot에서 정상 시작) 중복 생성하지 않는다.
//  · 덕분에 팀원은 Boot를 거치지 않고 InGame에서 바로 작업할 수 있다.
// =============================================================================

using UnityEngine;

/// <summary>어느 씬에서 시작하든 Managers 프리팹을 자동 생성. 자세한 설명은 상단 주석 참고.</summary>
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
