using ND.UI.Title;
using UnityEngine;
using UnityEngine.UI;

namespace ND.Audio
{
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonSound : MonoBehaviour
    {
        [Tooltip("클릭할 때 UI SFX 채널로 재생할 SoundCatalog ID입니다.")]
        [SerializeField] private string soundId;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (button == null) button = GetComponent<Button>();
            button.onClick.AddListener(PlayClickSound);
        }

        private void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(PlayClickSound);
        }

        private void PlayClickSound()
        {
            SoundManager.Instance?.PlayUiSfx(soundId);
        }
    }
}
