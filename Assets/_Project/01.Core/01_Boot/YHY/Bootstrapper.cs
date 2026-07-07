using UnityEngine;

/// <summary>
/// 게임이 시작되는 Boot 씬에서 한 번 실행되어,
/// 매니저 준비가 끝난 뒤 타이틀 화면(Title)으로 넘겨준다.
/// </summary>
public class Bootstrapper : MonoBehaviour
{
    // Awake(매니저 등록) 다음에 실행되는 Start → SceneLoader가 준비된 뒤 안전하게 호출
    private void Start()
    {
        // 씬 로더를 통해 타이틀 화면으로 이동
        SceneLoader.Instance.LoadScene("Title");
    }
}