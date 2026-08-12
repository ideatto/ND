/*
 * Technical Ownership
 * - Responsible Area: UI & Title Settings
 *
 * Script Purpose
 * - InGame Settings 표시 패널이 활성화될 때 현재 설정값을 UI에 반영한다.
 */
using UnityEngine;

/// <summary>
/// 이 GameObject가 활성화될 때 연결된 Settings UI를 현재 매니저 상태와 동기화한다.
/// </summary>
public sealed class SettingsPanelSyncOnEnable : MonoBehaviour
{
    [Tooltip("현재 설정값을 표시할 Settings UI 관리자입니다.")]
    [SerializeField]
    private SettingsUIManager settingsUIManager;

    /// <summary>
    /// 표시 패널이 활성화될 때 한 번 호출되며 UI 변경 이벤트를 발생시키지 않는 동기화를 요청한다.
    /// </summary>
    private void OnEnable()
    {
        if (settingsUIManager != null)
        {
            settingsUIManager.RefreshUiFromCurrentSettings();
        }
    }
}
