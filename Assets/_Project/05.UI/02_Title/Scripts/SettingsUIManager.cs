/*
 * Technical Ownership
 * - Responsible Area: UI & Title Settings
 *
 * Script Purpose
 * - Title Settings 패널 open/close와 Sound/Display 매니저 위임 API를 제공한다.
 * - Prefab UnityEvent가 씬에 매니저 참조 없이 SettingsUIManager만 바라보게 한다.
 * - 패널을 열거나 Reset할 때 매니저(SO 기본값 포함) 현재 상태를 UI 컨트롤에 동기화한다.
 *
 * Main Features
 * - Settings 패널 토글·열기·닫기.
 * - BGM/SFX 볼륨·토글, 창 모드·해상도, Reset을 SoundManager / DisplayManager에 전달한다.
 * - OpenOption / ResetAllSettings 시 Slider·Toggle·Dropdown을 WithoutNotify로 동기화한다.
 *
 * Usage for Team Members — Prefab wiring
 * - TitleSettingsCanvas의 SettingsUIManager Inspector에 UI 참조를 연결한다.
 *   - Bgm Slider / Sfx Slider / Bgm Toggle / Sfx Toggle
 *   - Window Mode Dropdown / Resolution Dropdown
 * - 컨트롤 → SettingsUIManager 이벤트:
 *   - BGM Slider.onValueChanged(float) → SetBgmVolume
 *   - SFX Slider.onValueChanged(float) → SetSfxVolume
 *   - BGM Toggle.onValueChanged(bool) → SetBgmEnabled
 *   - SFX Toggle.onValueChanged(bool) → SetSfxEnabled
 *   - Window Mode Dropdown.onValueChanged(int) → SetWindowMode
 *     (옵션 순서: 창모드 / 전체 창모드 / 전체모드)
 *   - Resolution Dropdown.onValueChanged(int) → SetResolution
 *     (옵션 순서: 1280x720 / 1920x1080 / 2560x1440)
 *   - Settings_Reset_Button.onClick → ResetAllSettings
 * - Exit Button은 TitleSceneController.ExitGame에 연결한다.
 *
 * SO default / Reset workflow
 * - 기본값: Resources/SoundSettingsConfig, Resources/DisplaySettingsConfig Inspector 수정.
 * - 매니저가 Awake에서 SO를 적용하고, OpenOption이 UI에 반영한다.
 * - ResetAllSettings → 매니저 ResetToDefaults() 후 UI를 다시 동기화한다.
 *
 * Main Public APIs
 * - ToggleOption / OpenOption / CloseOption
 * - SetBgmVolume / SetSfxVolume / SetBgmEnabled / SetSfxEnabled
 * - SetWindowMode / SetResolution / ResetAllSettings
 */
using ND.UI.Title;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Title Settings 패널 UI와 Sound/Display 매니저 사이의 얇은 위임 controller이다.
/// </summary>
public class SettingsUIManager : MonoBehaviour
{
    [Header("Option UI")]
    [Tooltip("Settings 패널 루트 GameObject입니다.")]
    [SerializeField]
    private GameObject optionPanel;

    [SerializeField]
    private bool isOptionOpen;

    [Header("Sound Controls")]
    [Tooltip("BGM 볼륨 Slider입니다. 비어 있으면 동기화를 건너뜁니다.")]
    [SerializeField]
    private Slider bgmSlider;

    [Tooltip("SFX 볼륨 Slider입니다. 비어 있으면 동기화를 건너뜁니다.")]
    [SerializeField]
    private Slider sfxSlider;

    [Tooltip("BGM 활성 Toggle입니다. 비어 있으면 동기화를 건너뜁니다.")]
    [SerializeField]
    private Toggle bgmToggle;

    [Tooltip("SFX 활성 Toggle입니다. 비어 있으면 동기화를 건너뜁니다.")]
    [SerializeField]
    private Toggle sfxToggle;

    [Header("Display Controls")]
    [Tooltip("창 모드 TMP_Dropdown입니다. 옵션 순서는 WindowDisplayMode enum과 같아야 합니다.")]
    [SerializeField]
    private TMP_Dropdown windowModeDropdown;

    [Tooltip("해상도 TMP_Dropdown입니다. 옵션 순서는 ResolutionPreset enum과 같아야 합니다.")]
    [SerializeField]
    private TMP_Dropdown resolutionDropdown;

    private void Start()
    {
        CloseOption();
    }

    public void ToggleOption()
    {
        if (isOptionOpen)
        {
            CloseOption();
            return;
        }

        OpenOption();
    }

    /// <summary>
    /// Settings 패널을 연다. 활성화 전에 매니저 상태를 UI에 반영해 Prefab 기본값이 매니저를 덮지 않게 한다.
    /// </summary>
    public void OpenOption()
    {
        // 패널이 켜지기 전에 동기화해야 Slider/Toggle 활성화 이벤트가 Prefab 값(0 등)으로 매니저를 덮지 않는다.
        SyncUiFromManagers();

        isOptionOpen = true;
        if (optionPanel != null)
        {
            optionPanel.SetActive(true);
        }
    }

    public void CloseOption()
    {
        isOptionOpen = false;
        if (optionPanel != null)
        {
            optionPanel.SetActive(false);
        }
    }

    /// <summary>
    /// BGM 볼륨 Slider 이벤트용 위임이다.
    /// </summary>
    public void SetBgmVolume(float volume)
    {
        if (SoundManager.Instance == null)
        {
            Debug.LogWarning("[SettingsUIManager] SoundManager.Instance is null. SetBgmVolume ignored.");
            return;
        }

        SoundManager.Instance.SetBgmVolume(volume);
    }

    /// <summary>
    /// SFX 볼륨 Slider 이벤트용 위임이다.
    /// </summary>
    public void SetSfxVolume(float volume)
    {
        if (SoundManager.Instance == null)
        {
            Debug.LogWarning("[SettingsUIManager] SoundManager.Instance is null. SetSfxVolume ignored.");
            return;
        }

        SoundManager.Instance.SetSfxVolume(volume);
    }

    /// <summary>
    /// BGM 활성 Toggle 이벤트용 위임이다.
    /// </summary>
    public void SetBgmEnabled(bool enabled)
    {
        if (SoundManager.Instance == null)
        {
            Debug.LogWarning("[SettingsUIManager] SoundManager.Instance is null. SetBgmEnabled ignored.");
            return;
        }

        SoundManager.Instance.SetBgmEnabled(enabled);
    }

    /// <summary>
    /// SFX 활성 Toggle 이벤트용 위임이다.
    /// </summary>
    public void SetSfxEnabled(bool enabled)
    {
        if (SoundManager.Instance == null)
        {
            Debug.LogWarning("[SettingsUIManager] SoundManager.Instance is null. SetSfxEnabled ignored.");
            return;
        }

        SoundManager.Instance.SetSfxEnabled(enabled);
    }

    /// <summary>
    /// 창 모드 Dropdown 이벤트용 위임이다. index는 WindowDisplayMode enum 순서이다.
    /// </summary>
    public void SetWindowMode(int modeIndex)
    {
        if (DisplayManager.Instance == null)
        {
            Debug.LogWarning("[SettingsUIManager] DisplayManager.Instance is null. SetWindowMode ignored.");
            return;
        }

        DisplayManager.Instance.SetWindowMode(modeIndex);
    }

    /// <summary>
    /// 해상도 Dropdown 이벤트용 위임이다. index는 ResolutionPreset enum 순서이다.
    /// </summary>
    public void SetResolution(int presetIndex)
    {
        if (DisplayManager.Instance == null)
        {
            Debug.LogWarning("[SettingsUIManager] DisplayManager.Instance is null. SetResolution ignored.");
            return;
        }

        DisplayManager.Instance.SetResolution(presetIndex);
    }

    /// <summary>
    /// Sound와 Display 설정을 ScriptableObject 기본값으로 되돌린 뒤 UI를 동기화한다.
    /// </summary>
    public void ResetAllSettings()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.ResetToDefaults();
        }
        else
        {
            Debug.LogWarning("[SettingsUIManager] SoundManager.Instance is null. Sound reset skipped.");
        }

        if (DisplayManager.Instance != null)
        {
            DisplayManager.Instance.ResetToDefaults();
        }
        else
        {
            Debug.LogWarning("[SettingsUIManager] DisplayManager.Instance is null. Display reset skipped.");
        }

        SyncUiFromManagers();
    }

    /// <summary>
    /// SoundManager / DisplayManager의 현재 값을 Settings UI 컨트롤에 반영한다.
    /// WithoutNotify를 사용해 동기화 중 onValueChanged가 다시 매니저를 덮지 않게 한다.
    /// </summary>
    private void SyncUiFromManagers()
    {
        if (SoundManager.Instance != null)
        {
            if (bgmSlider != null)
            {
                bgmSlider.SetValueWithoutNotify(SoundManager.Instance.BgmVolume);
            }

            if (sfxSlider != null)
            {
                sfxSlider.SetValueWithoutNotify(SoundManager.Instance.SfxVolume);
            }

            if (bgmToggle != null)
            {
                bgmToggle.SetIsOnWithoutNotify(SoundManager.Instance.BgmEnabled);
            }

            if (sfxToggle != null)
            {
                sfxToggle.SetIsOnWithoutNotify(SoundManager.Instance.SfxEnabled);
            }
        }

        if (DisplayManager.Instance != null)
        {
            if (windowModeDropdown != null)
            {
                windowModeDropdown.SetValueWithoutNotify((int)DisplayManager.Instance.CurrentWindowMode);
            }

            if (resolutionDropdown != null)
            {
                resolutionDropdown.SetValueWithoutNotify((int)DisplayManager.Instance.CurrentResolution);
            }
        }
    }
}
