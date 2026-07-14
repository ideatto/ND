/*
 * Technical Ownership
 * - Responsible Area: UI & Title Settings
 *
 * Script Purpose
 * - SoundManager가 Reset과 최초 적용에 사용하는 BGM/SFX 기본값을 ScriptableObject로 보관한다.
 *
 * Usage for Team Members
 * - Resources/SoundSettingsConfig asset을 Inspector에서 수정한다.
 * - Settings Reset 버튼은 이 asset 값으로 볼륨·토글을 되돌린다.
 *
 * Related Documentation
 * - Prefab wiring: TitleSettingsCanvas → SettingsUIManager → SoundManager
 */
using UnityEngine;

namespace ND.UI.Title
{
    /// <summary>
    /// SoundManager의 Reset 기본값을 정의하는 ScriptableObject이다.
    /// </summary>
    [CreateAssetMenu(fileName = ResourceName, menuName = "ND/UI/Title/Sound Settings Config")]
    public sealed class SoundSettingsConfig : ScriptableObject
    {
        public const string ResourceName = "SoundSettingsConfig";

        [Tooltip("BGM 기본 볼륨입니다. 범위는 0~1입니다.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float defaultBgmVolume = 1f;

        [Tooltip("SFX 기본 볼륨입니다. 범위는 0~1입니다.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float defaultSfxVolume = 1f;

        [Tooltip("BGM 기본 활성 여부입니다.")]
        [SerializeField]
        private bool defaultBgmEnabled = true;

        [Tooltip("SFX 기본 활성 여부입니다.")]
        [SerializeField]
        private bool defaultSfxEnabled = true;

        /// <summary>
        /// BGM 기본 볼륨이다. 범위: 0~1.
        /// </summary>
        public float DefaultBgmVolume => Mathf.Clamp01(defaultBgmVolume);

        /// <summary>
        /// SFX 기본 볼륨이다. 범위: 0~1.
        /// </summary>
        public float DefaultSfxVolume => Mathf.Clamp01(defaultSfxVolume);

        /// <summary>
        /// BGM 기본 활성 여부이다.
        /// </summary>
        public bool DefaultBgmEnabled => defaultBgmEnabled;

        /// <summary>
        /// SFX 기본 활성 여부이다.
        /// </summary>
        public bool DefaultSfxEnabled => defaultSfxEnabled;
    }
}
