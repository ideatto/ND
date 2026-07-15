// =============================================================================
// AdditiveSceneLoader — 시작 시 지정 씬을 additive(겹쳐)로 로드
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 메인 UI 씬이 시작될 때 거점 마을 씬(Village_Home)을 additive로 겹쳐 로드한다.
//        → 마을은 별도 씬에서 3D로 렌더되고, 그 카메라 출력이 RenderTexture를 통해
//          메인 UI의 마을 창(RawImage)에 표시된다. (마을/UI 협업 분리)
//
// [주의] 로드할 씬은 Build Settings(Scenes In Build)에 등록돼 있어야 한다.
//        에디터에서 이미 열려 있으면(수동 additive) 중복 로드하지 않는다.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>메인 씬 시작 시 지정 씬을 additive로 겹쳐 로드한다.</summary>
public class AdditiveSceneLoader : MonoBehaviour
{
    [Tooltip("겹쳐 로드할 씬 이름(Build Settings에 등록 필요)")]
    [SerializeField] private string sceneName = "Village_Home";

    private void Start()
    {
        // 에디터에서 이미 열려 있으면(수동 additive) 중복 로드 방지
        if (SceneManager.GetSceneByName(sceneName).isLoaded)
            return;

        SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
    }
}
