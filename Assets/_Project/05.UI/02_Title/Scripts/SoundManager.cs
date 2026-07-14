/*
 * Technical Ownership
 * - Responsible Area: UI & Title Settings
 *
 * Script Purpose
 * - BGM/SFX 재생, 볼륨·토글, ScriptableObject 기본값 Reset을 담당하는 DontDestroyOnLoad 싱글톤이다.
 * - FrameworkRoot와 분리되어 BeforeSceneLoad에서 자동 생성된다.
 *
 * Main Features
 * - Resources/SoundSettingsConfig를 로드해 최초 적용 및 Reset 기준으로 사용한다.
 * - BGM/SFX AudioSource를 런타임에 생성해 볼륨과 mute를 제어한다.
 * - PlayBgm / PlaySfx로 재생 클립을 교체한다.
 *
 * Usage for Team Members
 * - Title Settings UI는 SettingsUIManager 위임 메서드를 Prefab UnityEvent에 연결한다.
 * - 기본값 변경: Project에서 SoundSettingsConfig asset Inspector를 수정한다.
 * - ResetAllSettings → ResetToDefaults()가 SO 기본값으로 복원한다.
 *
 * Main Public APIs
 * - Instance: 현재 SoundManager 싱글톤.
 * - SetBgmVolume / SetSfxVolume: 볼륨(0~1).
 * - SetBgmEnabled / SetSfxEnabled: 채널 활성 토글.
 * - PlayBgm / StopBgm / PlaySfx: 클립 재생·정지.
 * - ResetToDefaults: SoundSettingsConfig 기본값으로 복원.
 */
using UnityEngine;

namespace ND.UI.Title
{
    /// <summary>
    /// BGM과 SFX를 관리하는 runtime singleton이다.
    /// </summary>
    public sealed class SoundManager : MonoBehaviour
    {
        private const string RootObjectName = "SoundManager";

        /// <summary>
        /// 현재 활성화된 SoundManager 인스턴스이다.
        /// </summary>
        public static SoundManager Instance { get; private set; }

        private SoundSettingsConfig settingsConfig;
        private AudioSource bgmSource;
        private AudioSource sfxSource;
        private float bgmVolume = 1f;
        private float sfxVolume = 1f;
        private bool bgmEnabled = true;
        private bool sfxEnabled = true;

        /// <summary>
        /// 현재 BGM 볼륨이다. 범위: 0~1.
        /// </summary>
        public float BgmVolume => bgmVolume;

        /// <summary>
        /// 현재 SFX 볼륨이다. 범위: 0~1.
        /// </summary>
        public float SfxVolume => sfxVolume;

        /// <summary>
        /// BGM 채널 활성 여부이다.
        /// </summary>
        public bool BgmEnabled => bgmEnabled;

        /// <summary>
        /// SFX 채널 활성 여부이다.
        /// </summary>
        public bool SfxEnabled => sfxEnabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null)
            {
                return;
            }

            var rootObject = new GameObject(RootObjectName);
            rootObject.AddComponent<SoundManager>();
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
            EnsureAudioSources();
            LoadSettingsConfig();
            ResetToDefaults();
        }

        /// <summary>
        /// BGM 볼륨을 설정한다.
        /// </summary>
        /// <param name="volume">0~1 범위의 볼륨.</param>
        public void SetBgmVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            ApplyBgmAudioState();
        }

        /// <summary>
        /// SFX 볼륨을 설정한다.
        /// </summary>
        /// <param name="volume">0~1 범위의 볼륨.</param>
        public void SetSfxVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            ApplySfxAudioState();
        }

        /// <summary>
        /// BGM 채널 활성 여부를 설정한다.
        /// </summary>
        public void SetBgmEnabled(bool enabled)
        {
            bgmEnabled = enabled;
            ApplyBgmAudioState();
        }

        /// <summary>
        /// SFX 채널 활성 여부를 설정한다.
        /// </summary>
        public void SetSfxEnabled(bool enabled)
        {
            sfxEnabled = enabled;
            ApplySfxAudioState();
        }

        /// <summary>
        /// BGM 클립을 교체해 재생한다.
        /// </summary>
        /// <param name="clip">재생할 BGM. null이면 무시한다.</param>
        /// <param name="loop">루프 재생 여부. 기본값 true.</param>
        public void PlayBgm(AudioClip clip, bool loop = true)
        {
            if (clip == null || bgmSource == null)
            {
                return;
            }

            bgmSource.clip = clip;
            bgmSource.loop = loop;
            ApplyBgmAudioState();
            if (bgmEnabled)
            {
                bgmSource.Play();
            }
        }

        /// <summary>
        /// 현재 BGM 재생을 중지한다.
        /// </summary>
        public void StopBgm()
        {
            if (bgmSource == null)
            {
                return;
            }

            bgmSource.Stop();
        }

        /// <summary>
        /// SFX 클립을 한 번 재생한다.
        /// </summary>
        /// <param name="clip">재생할 SFX. null이거나 SFX가 비활성이면 무시한다.</param>
        public void PlaySfx(AudioClip clip)
        {
            if (clip == null || sfxSource == null || !sfxEnabled)
            {
                return;
            }

            sfxSource.PlayOneShot(clip, sfxVolume);
        }

        /// <summary>
        /// SoundSettingsConfig의 기본값으로 볼륨·토글을 복원한다.
        /// </summary>
        public void ResetToDefaults()
        {
            if (settingsConfig == null)
            {
                LoadSettingsConfig();
            }

            if (settingsConfig == null)
            {
                bgmVolume = 1f;
                sfxVolume = 1f;
                bgmEnabled = true;
                sfxEnabled = true;
            }
            else
            {
                bgmVolume = settingsConfig.DefaultBgmVolume;
                sfxVolume = settingsConfig.DefaultSfxVolume;
                bgmEnabled = settingsConfig.DefaultBgmEnabled;
                sfxEnabled = settingsConfig.DefaultSfxEnabled;
            }

            ApplyBgmAudioState();
            ApplySfxAudioState();
        }

        private void LoadSettingsConfig()
        {
            settingsConfig = Resources.Load<SoundSettingsConfig>(SoundSettingsConfig.ResourceName);
            if (settingsConfig == null)
            {
                settingsConfig = ScriptableObject.CreateInstance<SoundSettingsConfig>();
                Debug.LogWarning(
                    $"[SoundManager] SoundSettingsConfig was not found at Resources/{SoundSettingsConfig.ResourceName}. Using runtime defaults.");
            }
        }

        private void EnsureAudioSources()
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }

        private void ApplyBgmAudioState()
        {
            if (bgmSource == null)
            {
                return;
            }

            bgmSource.volume = bgmEnabled ? bgmVolume : 0f;
            bgmSource.mute = !bgmEnabled;
            if (!bgmEnabled && bgmSource.isPlaying)
            {
                bgmSource.Pause();
            }
            else if (bgmEnabled && bgmSource.clip != null && !bgmSource.isPlaying)
            {
                bgmSource.UnPause();
                if (!bgmSource.isPlaying)
                {
                    bgmSource.Play();
                }
            }
        }

        private void ApplySfxAudioState()
        {
            if (sfxSource == null)
            {
                return;
            }

            sfxSource.volume = sfxEnabled ? sfxVolume : 0f;
            sfxSource.mute = !sfxEnabled;
        }
    }
}
