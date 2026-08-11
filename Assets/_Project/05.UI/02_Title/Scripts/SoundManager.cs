/*
 * Technical Ownership
 * - Responsible Area: UI & Title Settings / Central Audio
 *
 * Script Purpose
 * - SoundCatalog ID 기반 BGM, SFX, UI SFX와 loop SFX를 재생하는 DontDestroyOnLoad 싱글톤이다.
 * - 기존 AudioClip 직접 재생 API와 설정 Reset 호환성을 유지한다.
 *
 * Main Features
 * - SceneChanged를 구독해 Scene BGM을 전환하고 활성 loop SFX를 정리한다.
 * - BGM fade factor와 사용자 볼륨을 분리해 전환 중 설정 변경을 보존한다.
 * - ID 기반 one-shot은 독립 AudioSource를 사용해 동시 random pitch를 보존한다.
 * - Config 기본값에 PlayerPrefs 사용자 설정을 덮어써 초기화하고 변경값을 즉시 기록한다.
 */
using System.Collections;
using System.Collections.Generic;
using ND.Audio;
using ND.Framework;
using UnityEngine;

namespace ND.UI.Title
{
    public sealed class SoundManager : MonoBehaviour
    {
        private const string RootObjectName = "SoundManager";
        private const float DefaultSceneFadeDuration = 0.75f;
        private const int InvalidLoopHandle = 0;

        public static SoundManager Instance { get; private set; }

        private sealed class LoopPlayback
        {
            public AudioSource Source;
            public float DefinitionVolume;
        }

        private readonly Dictionary<int, LoopPlayback> loopSources = new Dictionary<int, LoopPlayback>();
        private SoundSettingsConfig settingsConfig;
        private SoundCatalog catalog;
        private AudioSource bgmSource;
        private AudioSource sfxSource;
        private AudioSource uiSfxSource;
        private Coroutine bgmFadeCoroutine;
        private float bgmVolume = 1f;
        private float sfxVolume = 1f;
        private float uiSfxVolume = 1f;
        private float bgmFadeFactor = 1f;
        private bool bgmEnabled = true;
        private bool sfxEnabled = true;
        private bool uiSfxEnabled = true;
        private string currentBgmId;
        private int nextLoopHandle = 1;

        public float BgmVolume => bgmVolume;
        public float SfxVolume => sfxVolume;
        public float UiSfxVolume => uiSfxVolume;
        public bool BgmEnabled => bgmEnabled;
        public bool SfxEnabled => sfxEnabled;
        public bool UiSfxEnabled => uiSfxEnabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;
            new GameObject(RootObjectName).AddComponent<SoundManager>();
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
            LoadResources();
            LoadInitialSettings();
            FrameworkEvents.SceneChanged += HandleSceneChanged;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            FrameworkEvents.SceneChanged -= HandleSceneChanged;
            StopAllLoopSfx();
            Instance = null;
        }

        public void SetBgmVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            ApplyBgmAudioState();
            PlayerPrefs.SetFloat(SettingsPlayerPrefsKeys.BgmVolume, bgmVolume);
        }

        public void SetSfxVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            ApplySfxAudioState();
            foreach (LoopPlayback playback in loopSources.Values)
                playback.Source.volume = sfxVolume * playback.DefinitionVolume;
            PlayerPrefs.SetFloat(SettingsPlayerPrefsKeys.SfxVolume, sfxVolume);
        }

        public void SetUiSfxVolume(float volume)
        {
            uiSfxVolume = Mathf.Clamp01(volume);
            ApplyUiSfxAudioState();
            PlayerPrefs.SetFloat(SettingsPlayerPrefsKeys.UiSfxVolume, uiSfxVolume);
        }

        public void SetBgmEnabled(bool enabled)
        {
            bgmEnabled = enabled;
            ApplyBgmAudioState();
            PlayerPrefs.SetInt(SettingsPlayerPrefsKeys.BgmEnabled, enabled ? 1 : 0);
        }

        public void SetSfxEnabled(bool enabled)
        {
            sfxEnabled = enabled;
            ApplySfxAudioState();
            foreach (LoopPlayback playback in loopSources.Values) playback.Source.mute = !sfxEnabled;
            PlayerPrefs.SetInt(SettingsPlayerPrefsKeys.SfxEnabled, enabled ? 1 : 0);
        }

        public void SetUiSfxEnabled(bool enabled)
        {
            uiSfxEnabled = enabled;
            ApplyUiSfxAudioState();
            PlayerPrefs.SetInt(SettingsPlayerPrefsKeys.UiSfxEnabled, enabled ? 1 : 0);
        }

        public void PlayBgm(AudioClip clip, bool loop = true)
        {
            if (clip == null || bgmSource == null) return;
            CancelBgmFade();
            currentBgmId = null;
            bgmFadeFactor = 1f;
            SetAndPlayBgm(clip, loop);
        }

        public void PlayBgm(string soundId)
        {
            PlayBgm(soundId, 0f);
        }

        public void PlayBgm(string soundId, float fadeDuration)
        {
            if (!TryGetDefinition(soundId, SoundCategory.Bgm, out SoundDefinition definition)) return;
            AudioClip clip = GetFirstValidClip(definition);
            if (clip == null)
            {
                WarnNoUsableClip(soundId);
                return;
            }

            if (string.Equals(currentBgmId, soundId, System.StringComparison.Ordinal) &&
                bgmSource.clip == clip && bgmSource.isPlaying) return;

            currentBgmId = soundId;
            StartBgmTransition(clip, fadeDuration);
        }

        public void StopBgm()
        {
            StopBgm(0f);
        }

        public void StopBgm(float fadeDuration)
        {
            currentBgmId = null;
            CancelBgmFade();
            if (fadeDuration <= 0f)
            {
                StopAndClearBgm();
                return;
            }

            bgmFadeCoroutine = StartCoroutine(FadeOutAndStop(fadeDuration));
        }

        public void PlaySfx(AudioClip clip)
        {
            if (clip == null || sfxSource == null || !sfxEnabled) return;
            sfxSource.PlayOneShot(clip);
        }

        public void PlaySfx(string soundId)
        {
            PlayDefinitionOneShot(soundId, SoundCategory.Sfx, sfxEnabled, sfxVolume);
        }

        public void PlayUiSfx(string soundId)
        {
            PlayDefinitionOneShot(soundId, SoundCategory.UiSfx, uiSfxEnabled, uiSfxVolume);
        }

        public int PlayLoopSfx(string soundId)
        {
            if (!sfxEnabled || !TryGetDefinition(soundId, SoundCategory.Sfx, out SoundDefinition definition))
                return InvalidLoopHandle;

            AudioClip clip = GetRandomValidClip(definition);
            if (clip == null)
            {
                WarnNoUsableClip(soundId);
                return InvalidLoopHandle;
            }

            AudioSource source = CreateAudioSource();
            source.clip = clip;
            source.loop = true;
            source.pitch = GetRandomPitch(definition);
            source.volume = sfxVolume * definition.Volume;
            source.mute = !sfxEnabled;
            source.Play();

            int handle = AllocateLoopHandle();
            loopSources.Add(handle, new LoopPlayback { Source = source, DefinitionVolume = definition.Volume });
            return handle;
        }

        public bool StopLoopSfx(int handle)
        {
            if (!loopSources.TryGetValue(handle, out LoopPlayback playback)) return false;
            loopSources.Remove(handle);
            AudioSource source = playback.Source;
            if (source != null)
            {
                source.Stop();
                Destroy(source);
            }
            return true;
        }

        public void ResetToDefaults()
        {
            if (settingsConfig == null) LoadResources();
            ApplyDefaultSettings();
            DeleteSavedSettings();
            ApplyBgmAudioState();
            ApplySfxAudioState();
            ApplyUiSfxAudioState();
            foreach (LoopPlayback playback in loopSources.Values)
            {
                playback.Source.volume = sfxVolume * playback.DefinitionVolume;
                playback.Source.mute = !sfxEnabled;
            }
            PlayerPrefs.Save();
        }

        private void LoadInitialSettings()
        {
            ApplyDefaultSettings();
            bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SettingsPlayerPrefsKeys.BgmVolume, bgmVolume));
            sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SettingsPlayerPrefsKeys.SfxVolume, sfxVolume));
            uiSfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SettingsPlayerPrefsKeys.UiSfxVolume, uiSfxVolume));
            bgmEnabled = PlayerPrefs.GetInt(SettingsPlayerPrefsKeys.BgmEnabled, bgmEnabled ? 1 : 0) != 0;
            sfxEnabled = PlayerPrefs.GetInt(SettingsPlayerPrefsKeys.SfxEnabled, sfxEnabled ? 1 : 0) != 0;
            uiSfxEnabled = PlayerPrefs.GetInt(SettingsPlayerPrefsKeys.UiSfxEnabled, uiSfxEnabled ? 1 : 0) != 0;
            ApplyBgmAudioState();
            ApplySfxAudioState();
            ApplyUiSfxAudioState();
        }

        private void ApplyDefaultSettings()
        {
            bgmVolume = settingsConfig != null ? settingsConfig.DefaultBgmVolume : 1f;
            sfxVolume = settingsConfig != null ? settingsConfig.DefaultSfxVolume : 1f;
            uiSfxVolume = settingsConfig != null ? settingsConfig.DefaultUiSfxVolume : 1f;
            bgmEnabled = settingsConfig == null || settingsConfig.DefaultBgmEnabled;
            sfxEnabled = settingsConfig == null || settingsConfig.DefaultSfxEnabled;
            uiSfxEnabled = settingsConfig == null || settingsConfig.DefaultUiSfxEnabled;
        }

        private static void DeleteSavedSettings()
        {
            PlayerPrefs.DeleteKey(SettingsPlayerPrefsKeys.BgmVolume);
            PlayerPrefs.DeleteKey(SettingsPlayerPrefsKeys.BgmEnabled);
            PlayerPrefs.DeleteKey(SettingsPlayerPrefsKeys.SfxVolume);
            PlayerPrefs.DeleteKey(SettingsPlayerPrefsKeys.SfxEnabled);
            PlayerPrefs.DeleteKey(SettingsPlayerPrefsKeys.UiSfxVolume);
            PlayerPrefs.DeleteKey(SettingsPlayerPrefsKeys.UiSfxEnabled);
        }

        private void LoadResources()
        {
            settingsConfig = Resources.Load<SoundSettingsConfig>(SoundSettingsConfig.ResourceName);
            if (settingsConfig == null)
                Debug.LogWarning($"[SoundManager] Resources/{SoundSettingsConfig.ResourceName} is missing. Using runtime defaults.");

            catalog = Resources.Load<SoundCatalog>(SoundCatalog.ResourceName);
            if (catalog == null)
                Debug.LogWarning($"[SoundManager] Resources/{SoundCatalog.ResourceName} is missing. ID-based playback is disabled.");
        }

        private void EnsureAudioSources()
        {
            bgmSource = CreateAudioSource();
            bgmSource.loop = true;
            sfxSource = CreateAudioSource();
            uiSfxSource = CreateAudioSource();
        }

        private AudioSource CreateAudioSource()
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            return source;
        }

        private void HandleSceneChanged(string sceneName)
        {
            StopAllLoopSfx();
            if (catalog == null || !catalog.TryGetSceneBgm(sceneName, out string bgmId)) return;
            if (string.IsNullOrEmpty(bgmId)) StopBgm(DefaultSceneFadeDuration);
            else PlayBgm(bgmId, DefaultSceneFadeDuration);
        }

        private void StopAllLoopSfx()
        {
            foreach (LoopPlayback playback in loopSources.Values)
            {
                AudioSource source = playback.Source;
                if (source == null) continue;
                source.Stop();
                Destroy(source);
            }
            loopSources.Clear();
        }

        private void PlayDefinitionOneShot(string soundId, SoundCategory category, bool enabled, float channelVolume)
        {
            if (!enabled || !TryGetDefinition(soundId, category, out SoundDefinition definition)) return;
            AudioClip clip = GetRandomValidClip(definition);
            if (clip == null)
            {
                WarnNoUsableClip(soundId);
                return;
            }

            AudioSource source = CreateAudioSource();
            source.pitch = GetRandomPitch(definition);
            source.volume = channelVolume * definition.Volume;
            source.PlayOneShot(clip);
            StartCoroutine(DestroyOneShotSource(source, clip.length / Mathf.Abs(source.pitch) + 0.1f));
        }

        private IEnumerator DestroyOneShotSource(AudioSource source, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (source != null) Destroy(source);
        }

        private bool TryGetDefinition(string soundId, SoundCategory category, out SoundDefinition definition)
        {
            definition = null;
            if (catalog == null)
            {
                Debug.LogWarning("[SoundManager] SoundCatalog is unavailable. ID-based playback ignored.");
                return false;
            }
            if (!catalog.TryGet(soundId, out definition))
            {
                Debug.LogWarning($"[SoundManager] Unknown sound ID '{soundId}'.");
                return false;
            }
            if (definition.Category == category) return true;
            Debug.LogWarning($"[SoundManager] Sound '{soundId}' is {definition.Category}, not {category}.");
            definition = null;
            return false;
        }

        private static AudioClip GetFirstValidClip(SoundDefinition definition)
        {
            if (definition?.Clips == null) return null;
            foreach (AudioClip clip in definition.Clips) if (clip != null) return clip;
            return null;
        }

        private static AudioClip GetRandomValidClip(SoundDefinition definition)
        {
            if (definition?.Clips == null) return null;
            int validCount = 0;
            foreach (AudioClip clip in definition.Clips) if (clip != null) validCount++;
            if (validCount == 0) return null;
            int selected = Random.Range(0, validCount);
            foreach (AudioClip clip in definition.Clips)
            {
                if (clip == null) continue;
                if (selected-- == 0) return clip;
            }
            return null;
        }

        private static float GetRandomPitch(SoundDefinition definition)
        {
            return Mathf.Approximately(definition.MinPitch, definition.MaxPitch)
                ? definition.MinPitch
                : Random.Range(definition.MinPitch, definition.MaxPitch);
        }

        private static void WarnNoUsableClip(string soundId)
        {
            Debug.LogWarning($"[SoundManager] Sound '{soundId}' has no usable clip.");
        }

        private int AllocateLoopHandle()
        {
            while (nextLoopHandle <= 0 || loopSources.ContainsKey(nextLoopHandle)) nextLoopHandle++;
            return nextLoopHandle++;
        }

        private void StartBgmTransition(AudioClip clip, float duration)
        {
            CancelBgmFade();
            if (duration <= 0f)
            {
                bgmFadeFactor = 1f;
                SetAndPlayBgm(clip, true);
                return;
            }
            bgmFadeCoroutine = StartCoroutine(TransitionBgm(clip, duration));
        }

        private IEnumerator TransitionBgm(AudioClip clip, float duration)
        {
            if (bgmSource.isPlaying)
            {
                yield return Fade(0f, duration * 0.5f);
                bgmSource.Stop();
            }
            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmFadeFactor = 0f;
            ApplyBgmAudioState();
            if (bgmEnabled) bgmSource.Play();
            yield return Fade(1f, duration * 0.5f);
            bgmFadeCoroutine = null;
        }

        private IEnumerator FadeOutAndStop(float duration)
        {
            yield return Fade(0f, duration);
            StopAndClearBgm();
            bgmFadeCoroutine = null;
        }

        private IEnumerator Fade(float target, float duration)
        {
            float start = bgmFadeFactor;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmFadeFactor = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                ApplyBgmAudioState();
                yield return null;
            }
            bgmFadeFactor = target;
            ApplyBgmAudioState();
        }

        private void CancelBgmFade()
        {
            if (bgmFadeCoroutine == null) return;
            StopCoroutine(bgmFadeCoroutine);
            bgmFadeCoroutine = null;
        }

        private void SetAndPlayBgm(AudioClip clip, bool loop)
        {
            bgmSource.clip = clip;
            bgmSource.loop = loop;
            ApplyBgmAudioState();
            if (bgmEnabled) bgmSource.Play();
        }

        private void StopAndClearBgm()
        {
            bgmSource.Stop();
            bgmSource.clip = null;
            bgmFadeFactor = 1f;
            ApplyBgmAudioState();
        }

        private void ApplyBgmAudioState()
        {
            if (bgmSource == null) return;
            bgmSource.volume = bgmVolume * bgmFadeFactor;
            bgmSource.mute = !bgmEnabled;
            if (!bgmEnabled && bgmSource.isPlaying) bgmSource.Pause();
            else if (bgmEnabled && bgmSource.clip != null && !bgmSource.isPlaying)
            {
                bgmSource.UnPause();
                if (!bgmSource.isPlaying) bgmSource.Play();
            }
        }

        private void ApplySfxAudioState()
        {
            if (sfxSource == null) return;
            sfxSource.volume = sfxVolume;
            sfxSource.mute = !sfxEnabled;
        }

        private void ApplyUiSfxAudioState()
        {
            if (uiSfxSource == null) return;
            uiSfxSource.volume = uiSfxVolume;
            uiSfxSource.mute = !uiSfxEnabled;
        }
    }
}
