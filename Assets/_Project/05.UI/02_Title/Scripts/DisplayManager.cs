/*
 * Technical Ownership
 * - Responsible Area: UI & Title Settings
 *
 * Script Purpose
 * - 창 모드·해상도 전환과 ScriptableObject 기본값 Reset을 담당하는 DontDestroyOnLoad 싱글톤이다.
 * - FrameworkRoot와 분리되어 BeforeSceneLoad에서 자동 생성된다.
 *
 * Main Features
 * - Resources/DisplaySettingsConfig를 로드해 최초 적용 및 Reset 기준으로 사용한다.
 * - 창모드 / 전체 창모드 / 전체모드와 1280x720·1920x1080·2560x1440 프리셋을 적용한다.
 *
 * Usage for Team Members
 * - Title Settings UI는 SettingsUIManager 위임 메서드를 Prefab UnityEvent에 연결한다.
 * - Dropdown 옵션 순서는 WindowDisplayMode / ResolutionPreset enum 순서와 일치해야 한다.
 * - 기본값 변경: Project에서 DisplaySettingsConfig asset Inspector를 수정한다.
 *
 * Main Public APIs
 * - Instance: 현재 DisplayManager 싱글톤.
 * - SetWindowMode / SetResolution: 모드·해상도 변경 후 즉시 적용.
 * - ApplyCurrent: 현재 런타임 상태를 Screen API에 반영.
 * - ResetToDefaults: DisplaySettingsConfig 기본값으로 복원.
 */
using UnityEngine;

namespace ND.UI.Title
{
    /// <summary>
    /// 게임 윈도우 모드와 해상도를 관리하는 runtime singleton이다.
    /// </summary>
    public sealed class DisplayManager : MonoBehaviour
    {
        private const string RootObjectName = "DisplayManager";

        /// <summary>
        /// 현재 활성화된 DisplayManager 인스턴스이다.
        /// </summary>
        public static DisplayManager Instance { get; private set; }

        private DisplaySettingsConfig settingsConfig;
        private WindowDisplayMode currentWindowMode = WindowDisplayMode.Windowed;
        private ResolutionPreset currentResolution = ResolutionPreset.Res1920x1080;

        /// <summary>
        /// 현재 창 모드이다.
        /// </summary>
        public WindowDisplayMode CurrentWindowMode => currentWindowMode;

        /// <summary>
        /// 현재 해상도 프리셋이다.
        /// </summary>
        public ResolutionPreset CurrentResolution => currentResolution;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null)
            {
                return;
            }

            var rootObject = new GameObject(RootObjectName);
            rootObject.AddComponent<DisplayManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettingsConfig();
            ResetToDefaults();
        }

        /// <summary>
        /// 창 모드를 설정하고 즉시 적용한다.
        /// </summary>
        public void SetWindowMode(WindowDisplayMode mode)
        {
            currentWindowMode = mode;
            ApplyCurrent();
        }

        /// <summary>
        /// Dropdown용 창 모드 설정이다. 옵션 index는 WindowDisplayMode enum 순서를 따른다.
        /// </summary>
        /// <param name="modeIndex">0=창모드, 1=전체 창모드, 2=전체모드.</param>
        public void SetWindowMode(int modeIndex)
        {
            if (modeIndex < 0 || modeIndex > (int)WindowDisplayMode.ExclusiveFullscreen)
            {
                Debug.LogWarning($"[DisplayManager] Invalid window mode index: {modeIndex}");
                return;
            }

            SetWindowMode((WindowDisplayMode)modeIndex);
        }

        /// <summary>
        /// 해상도 프리셋을 설정하고 즉시 적용한다.
        /// </summary>
        public void SetResolution(ResolutionPreset preset)
        {
            currentResolution = preset;
            ApplyCurrent();
        }

        /// <summary>
        /// Dropdown용 해상도 설정이다. 옵션 index는 ResolutionPreset enum 순서를 따른다.
        /// </summary>
        /// <param name="presetIndex">0=1280x720, 1=1920x1080, 2=2560x1440.</param>
        public void SetResolution(int presetIndex)
        {
            if (presetIndex < 0 || presetIndex > (int)ResolutionPreset.Res2560x1440)
            {
                Debug.LogWarning($"[DisplayManager] Invalid resolution preset index: {presetIndex}");
                return;
            }

            SetResolution((ResolutionPreset)presetIndex);
        }

        /// <summary>
        /// 현재 창 모드와 해상도를 Screen API에 반영한다.
        /// </summary>
        public void ApplyCurrent()
        {
            GetResolutionSize(currentResolution, out var width, out var height);
            Screen.SetResolution(width, height, ToFullScreenMode(currentWindowMode));
        }

        /// <summary>
        /// DisplaySettingsConfig의 기본값으로 창 모드·해상도를 복원한 뒤 적용한다.
        /// </summary>
        public void ResetToDefaults()
        {
            if (settingsConfig == null)
            {
                LoadSettingsConfig();
            }

            if (settingsConfig == null)
            {
                currentWindowMode = WindowDisplayMode.Windowed;
                currentResolution = ResolutionPreset.Res1920x1080;
            }
            else
            {
                currentWindowMode = settingsConfig.DefaultWindowMode;
                currentResolution = settingsConfig.DefaultResolution;
            }

            ApplyCurrent();
        }

        private void LoadSettingsConfig()
        {
            settingsConfig = Resources.Load<DisplaySettingsConfig>(DisplaySettingsConfig.ResourceName);
            if (settingsConfig == null)
            {
                settingsConfig = ScriptableObject.CreateInstance<DisplaySettingsConfig>();
                Debug.LogWarning(
                    $"[DisplayManager] DisplaySettingsConfig was not found at Resources/{DisplaySettingsConfig.ResourceName}. Using runtime defaults.");
            }
        }

        private static FullScreenMode ToFullScreenMode(WindowDisplayMode mode)
        {
            switch (mode)
            {
                case WindowDisplayMode.BorderlessFullscreen:
                    return FullScreenMode.FullScreenWindow;
                case WindowDisplayMode.ExclusiveFullscreen:
                    return FullScreenMode.ExclusiveFullScreen;
                default:
                    return FullScreenMode.Windowed;
            }
        }

        private static void GetResolutionSize(ResolutionPreset preset, out int width, out int height)
        {
            switch (preset)
            {
                case ResolutionPreset.Res1280x720:
                    width = 1280;
                    height = 720;
                    break;
                case ResolutionPreset.Res2560x1440:
                    width = 2560;
                    height = 1440;
                    break;
                default:
                    width = 1920;
                    height = 1080;
                    break;
            }
        }
    }
}
