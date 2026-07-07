using UnityEngine;
using UnityEngine.SceneManagement;  // 씬을 불러오려면 필요한 도구 모음

/// <summary>
/// 씬(화면) 전환을 담당하는 매니저.
/// Boot → Title → Loading → InGame 처럼 화면을 바꿀 때 항상 이 매니저를 통한다.
/// GameManager와 마찬가지로 씬이 바뀌어도 사라지지 않는다.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    // 어디서든 SceneLoader.Instance 로 접근할 수 있게 하는 싱글턴 참조
    public static SceneLoader Instance { get; private set; }

    // 오브젝트가 처음 생성될 때 한 번 호출 (GameManager와 같은 중복 방지 패턴)
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("[SceneLoader] 로드 완료");
    }

    /// <summary>
    /// 이름으로 씬을 바로 불러온다. (예: SceneLoader.Instance.LoadScene("Title"))
    /// </summary>
    public void LoadScene(string sceneName)
    {
        // 어떤 씬으로 넘어가는지 로그로 남겨 나중에 확인하기 쉽게 한다
        Debug.Log($"[SceneLoader] '{sceneName}' 씬으로 이동합니다.");

        // 실제로 화면을 그 씬으로 전환한다
        SceneManager.LoadScene(sceneName);
    }
}