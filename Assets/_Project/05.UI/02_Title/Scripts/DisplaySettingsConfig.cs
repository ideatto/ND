/*
 * Technical Ownership
 * - Responsible Area: UI & Title Settings
 *
 * Script Purpose
 * - DisplayManager가 Reset과 최초 적용에 사용하는 창 모드·해상도 기본값을 ScriptableObject로 보관한다.
 *
 * Usage for Team Members
 * - Resources/DisplaySettingsConfig asset을 Inspector에서 수정한다.
 * - Settings Reset 버튼은 이 asset 값으로 창 모드·해상도를 되돌린다.
 *
 * Related Documentation
 * - Prefab wiring: TitleSettingsCanvas → SettingsUIManager → DisplayManager
 */
using UnityEngine;

namespace ND.UI.Title
{
    /// <summary>
    /// 게임 윈도우 표시 모드이다.
    /// </summary>
    public enum WindowDisplayMode
    {
        Windowed = 0,
        BorderlessFullscreen = 1,
        ExclusiveFullscreen = 2
    }

    /// <summary>
    /// Settings에서 선택 가능한 해상도 프리셋이다.
    /// </summary>
    public enum ResolutionPreset
    {
        Res1280x720 = 0,
        Res1920x1080 = 1,
        Res2560x1440 = 2
    }

    /// <summary>
    /// DisplayManager의 Reset 기본값을 정의하는 ScriptableObject이다.
    /// </summary>
    [CreateAssetMenu(fileName = ResourceName, menuName = "ND/UI/Title/Display Settings Config")]
    public sealed class DisplaySettingsConfig : ScriptableObject
    {
        public const string ResourceName = "DisplaySettingsConfig";

        [Tooltip("기본 창 모드입니다. 창모드 / 전체 창모드 / 전체모드.")]
        [SerializeField]
        private WindowDisplayMode defaultWindowMode = WindowDisplayMode.Windowed;

        [Tooltip("기본 해상도 프리셋입니다.")]
        [SerializeField]
        private ResolutionPreset defaultResolution = ResolutionPreset.Res1920x1080;

        /// <summary>
        /// 기본 창 모드이다.
        /// </summary>
        public WindowDisplayMode DefaultWindowMode => defaultWindowMode;

        /// <summary>
        /// 기본 해상도 프리셋이다.
        /// </summary>
        public ResolutionPreset DefaultResolution => defaultResolution;
    }
}
