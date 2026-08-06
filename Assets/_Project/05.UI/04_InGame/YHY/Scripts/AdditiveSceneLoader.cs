// =============================================================================
// AdditiveSceneLoader — 필수 보조 씬의 비동기 로드와 준비 상태 관리
// =============================================================================
// [담당] Core Gameplay
//
// [역할] 지정 씬을 additive로 한 번만 비동기 로드하고, 활성화 직후 초기화가 끝난 다음
//        프레임부터 외부 Loading flow가 조회할 수 있는 Ready 상태를 제공한다.
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>지정 Additive 씬의 비동기 로드와 씬별 Ready 상태를 관리한다.</summary>
public sealed class AdditiveSceneLoader : MonoBehaviour
{
    private sealed class SceneLoadState
    {
        public AsyncOperation Operation;
        public bool IsLoading;
        public bool IsReady;
        public string Error;
    }

    private static readonly Dictionary<string, SceneLoadState> States =
        new Dictionary<string, SceneLoadState>(StringComparer.Ordinal);

    [Tooltip("겹쳐 로드할 씬 이름(Build Profile 등록 필요)")]
    [SerializeField] private string sceneName = "Village_Home";

    /// <summary>이 로더가 지정한 씬의 비동기 작업이 진행 중인지 나타낸다.</summary>
    public bool IsLoading => GetIsLoading(sceneName);

    /// <summary>씬 활성화와 첫 프레임 초기화가 끝났는지 나타낸다.</summary>
    public bool IsReady => GetIsReady(sceneName);

    /// <summary>Unity의 0~0.9 로드 진행률을 0~1로 정규화해 반환한다.</summary>
    public float Progress01 => GetProgress01(sceneName);

    private void Start()
    {
        StartCoroutine(LoadRequiredSceneAsync());
    }

    /// <summary>
    /// 직렬화된 Additive 씬의 기존 작업에 합류하거나 새 비동기 로드를 시작한다.
    /// </summary>
    /// <remarks>중복 호출은 동일 씬의 진행 상태를 기다리며 별도 로드를 만들지 않는다.</remarks>
    public IEnumerator LoadRequiredSceneAsync()
    {
        yield return LoadRequiredSceneAsync(sceneName);
    }

    /// <summary>
    /// 지정 Additive 씬의 기존 작업에 합류하거나 새 비동기 로드를 시작한다.
    /// </summary>
    /// <remarks>Ready는 씬 활성화 콜백과 동기 OnEnable/Start 초기화 다음 프레임에 설정된다.</remarks>
    public static IEnumerator LoadRequiredSceneAsync(string requiredSceneName)
    {
        if (string.IsNullOrWhiteSpace(requiredSceneName))
        {
            Debug.LogError("Required additive scene load was rejected because the scene name is empty.");
            yield break;
        }

        var loadedScene = SceneManager.GetSceneByName(requiredSceneName);
        var state = GetOrCreateState(requiredSceneName);
        if (loadedScene.isLoaded)
        {
            state.Operation = null;
            state.IsLoading = false;
            state.IsReady = true;
            state.Error = null;
            yield break;
        }

        if (state.IsLoading)
        {
            while (state.IsLoading)
            {
                yield return null;
            }

            yield break;
        }

        state.Operation = null;
        state.IsLoading = true;
        state.IsReady = false;
        state.Error = null;

        try
        {
            state.Operation = SceneManager.LoadSceneAsync(requiredSceneName, LoadSceneMode.Additive);
        }
        catch (Exception exception)
        {
            Fail(requiredSceneName, state, exception.Message);
            yield break;
        }

        if (state.Operation == null)
        {
            Fail(requiredSceneName, state, "Unity returned a null AsyncOperation.");
            yield break;
        }

        yield return state.Operation;
        state.Operation = null;

        if (!SceneManager.GetSceneByName(requiredSceneName).isLoaded)
        {
            Fail(requiredSceneName, state, "Unity completed the operation but the scene is not loaded.");
            yield break;
        }

        // Awake, OnEnable, sceneLoaded callbacks, and synchronous Rebuild calls have returned here.
        yield return null;
        state.IsLoading = false;
        state.IsReady = true;
    }

    /// <summary>지정 씬의 로드 작업 진행 여부를 조회한다.</summary>
    public static bool GetIsLoading(string requiredSceneName)
    {
        return TryGetState(requiredSceneName, out var state) && state.IsLoading;
    }

    /// <summary>지정 씬이 현재 로드되어 있고 초기화 Ready 상태인지 조회한다.</summary>
    public static bool GetIsReady(string requiredSceneName)
    {
        if (!TryGetState(requiredSceneName, out var state) || !state.IsReady)
        {
            return false;
        }

        if (SceneManager.GetSceneByName(requiredSceneName).isLoaded)
        {
            return true;
        }

        state.IsReady = false;
        return false;
    }

    /// <summary>지정 씬의 현재 정규화된 로드 진행률을 조회한다.</summary>
    public static float GetProgress01(string requiredSceneName)
    {
        if (GetIsReady(requiredSceneName))
        {
            return 1f;
        }

        if (!TryGetState(requiredSceneName, out var state) || state.Operation == null)
        {
            return 0f;
        }

        return Mathf.Clamp01(state.Operation.progress / 0.9f);
    }

    /// <summary>지정 씬의 마지막 로드 실패 상세를 반환하며 실패가 없으면 빈 문자열이다.</summary>
    public static string GetError(string requiredSceneName)
    {
        return TryGetState(requiredSceneName, out var state) ? state.Error ?? string.Empty : string.Empty;
    }

    private static SceneLoadState GetOrCreateState(string requiredSceneName)
    {
        if (!States.TryGetValue(requiredSceneName, out var state))
        {
            state = new SceneLoadState();
            States.Add(requiredSceneName, state);
        }

        return state;
    }

    private static bool TryGetState(string requiredSceneName, out SceneLoadState state)
    {
        if (string.IsNullOrWhiteSpace(requiredSceneName))
        {
            state = null;
            return false;
        }

        return States.TryGetValue(requiredSceneName, out state);
    }

    private static void Fail(string requiredSceneName, SceneLoadState state, string detail)
    {
        state.Operation = null;
        state.IsLoading = false;
        state.IsReady = false;
        state.Error = detail;
        Debug.LogError($"Required additive scene load failed: {requiredSceneName}. {detail}");
    }
}
