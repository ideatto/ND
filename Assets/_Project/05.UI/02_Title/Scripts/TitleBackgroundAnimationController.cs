/*
 * Technical Ownership
 * - Responsible Area: UI & Title
 *
 * Script Purpose
 * - Title 배경의 보트, 로고, 플레이어 Pivot에 결정적인 반복 애니메이션을 적용한다.
 * - 활성화 시점의 Transform을 기준으로 계산하고 비활성화 시 원래 상태로 복원한다.
 */
using UnityEngine;

namespace ND.UI.Title
{
    public sealed class TitleBackgroundAnimationController : MonoBehaviour
    {
        [Header("General")]
        [Tooltip("게임 시간 배율과 무관하게 Title 배경 애니메이션을 재생합니다.")]
        [SerializeField] private bool useUnscaledTime = true;
        [Tooltip("컴포넌트가 활성화될 때 애니메이션 재생을 시작합니다.")]
        [SerializeField] private bool playOnEnable = true;

        [Header("Boat Idle Sway")]
        [SerializeField] private bool boatEnabled = true;
        [SerializeField] private RectTransform boatTarget;
        [Tooltip("초기 회전을 기준으로 한 최대 회전 각도입니다.")]
        [Min(0f)]
        [SerializeField] private float boatRotationAmplitude = 1.2f;
        [Tooltip("보트 회전이 한 번 왕복하는 시간(초)입니다.")]
        [Min(0.01f)]
        [SerializeField] private float boatRotationDuration = 5.2f;
        [Tooltip("초기 위치를 기준으로 한 최대 수직 이동량(UI 단위)입니다.")]
        [Min(0f)]
        [SerializeField] private float boatVerticalAmplitude = 2f;
        [Tooltip("보트 수직 이동이 한 번 왕복하는 시간(초)입니다.")]
        [Min(0.01f)]
        [SerializeField] private float boatVerticalDuration = 4.4f;

        [Header("Logo Interval Wiggle")]
        [SerializeField] private bool logoEnabled = true;
        [SerializeField] private RectTransform logoTarget;
        [Tooltip("초기 회전을 기준으로 한 최대 흔들림 각도입니다.")]
        [Min(0f)]
        [SerializeField] private float logoRotationAmplitude = 1.2f;
        [Tooltip("감쇠 흔들림이 지속되는 시간(초)입니다.")]
        [Min(0.01f)]
        [SerializeField] private float logoWiggleDuration = 1.4f;
        [Tooltip("흔들림 사이에 초기 회전으로 머무는 시간(초)입니다.")]
        [Min(0f)]
        [SerializeField] private float logoPauseDuration = 4f;
        [SerializeField] private bool logoPlayImmediately = true;

        [Header("Player Breath")]
        [SerializeField] private bool playerEnabled = true;
        [SerializeField] private RectTransform playerTarget;
        [Tooltip("초기 크기를 기준으로 한 최대 확대 비율입니다.")]
        [Min(0f)]
        [SerializeField] private float playerScaleAmount = 0.015f;
        [Tooltip("플레이어가 한 번 확대되고 복원되는 시간(초)입니다.")]
        [Min(0.01f)]
        [SerializeField] private float playerDuration = 3f;

        private Vector2 initialBoatAnchoredPosition;
        private Quaternion initialBoatRotation;
        private Quaternion initialLogoRotation;
        private Vector3 initialPlayerScale;
        private float elapsedTime;
        private float logoElapsedTime;
        private bool initialStateCaptured;
        private bool isPlaying;

        private void OnEnable()
        {
            CaptureInitialState();
            ResetAnimationState();
            isPlaying = playOnEnable;
        }

        private void Update()
        {
            if (!isPlaying)
            {
                return;
            }

            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsedTime += deltaTime;
            logoElapsedTime += deltaTime;

            AnimateBoat();
            AnimateLogo();
            AnimatePlayer();
        }

        private void OnDisable()
        {
            RestoreInitialState();
            elapsedTime = 0f;
            logoElapsedTime = 0f;
            isPlaying = false;
        }

        private void CaptureInitialState()
        {
            if (boatTarget != null)
            {
                initialBoatAnchoredPosition = boatTarget.anchoredPosition;
                initialBoatRotation = boatTarget.localRotation;
            }

            if (logoTarget != null)
            {
                initialLogoRotation = logoTarget.localRotation;
            }

            if (playerTarget != null)
            {
                initialPlayerScale = playerTarget.localScale;
            }

            initialStateCaptured = true;
        }

        private void ResetAnimationState()
        {
            elapsedTime = 0f;
            logoElapsedTime = logoPlayImmediately ? 0f : -Mathf.Max(0f, logoPauseDuration);
            RestoreInitialState();
        }

        private void AnimateBoat()
        {
            if (!boatEnabled || boatTarget == null)
            {
                return;
            }

            float rotationDuration = Mathf.Max(0.01f, boatRotationDuration);
            float verticalDuration = Mathf.Max(0.01f, boatVerticalDuration);
            float rotationPhase = elapsedTime * Mathf.PI * 2f / rotationDuration;
            float verticalPhase = elapsedTime * Mathf.PI * 2f / verticalDuration;
            float rotationOffset = Mathf.Sin(rotationPhase) * Mathf.Max(0f, boatRotationAmplitude);
            float verticalOffset = Mathf.Sin(verticalPhase) * Mathf.Max(0f, boatVerticalAmplitude);

            boatTarget.localRotation = initialBoatRotation * Quaternion.Euler(0f, 0f, rotationOffset);
            boatTarget.anchoredPosition = initialBoatAnchoredPosition + Vector2.up * verticalOffset;
        }

        private void AnimateLogo()
        {
            if (!logoEnabled || logoTarget == null)
            {
                return;
            }

            if (logoElapsedTime < 0f)
            {
                logoTarget.localRotation = initialLogoRotation;
                return;
            }

            float wiggleDuration = Mathf.Max(0.01f, logoWiggleDuration);
            float pauseDuration = Mathf.Max(0f, logoPauseDuration);
            float cycleDuration = wiggleDuration + pauseDuration;
            float cycleTime = cycleDuration > 0f ? logoElapsedTime % cycleDuration : 0f;
            if (cycleTime >= wiggleDuration)
            {
                logoTarget.localRotation = initialLogoRotation;
                return;
            }

            float t = cycleTime / wiggleDuration;
            float damping = 1f - t;
            float angleOffset = Mathf.Sin(t * Mathf.PI * 2f * 3f)
                * Mathf.Max(0f, logoRotationAmplitude)
                * damping;
            logoTarget.localRotation = initialLogoRotation * Quaternion.Euler(0f, 0f, angleOffset);
        }

        private void AnimatePlayer()
        {
            if (!playerEnabled || playerTarget == null)
            {
                return;
            }

            float duration = Mathf.Max(0.01f, playerDuration);
            float phase = elapsedTime * Mathf.PI * 2f / duration;
            float normalized = (Mathf.Sin(phase - Mathf.PI * 0.5f) + 1f) * 0.5f;
            float multiplier = 1f + normalized * Mathf.Max(0f, playerScaleAmount);
            playerTarget.localScale = Vector3.Scale(initialPlayerScale, new Vector3(multiplier, multiplier, 1f));
        }

        private void RestoreInitialState()
        {
            if (!initialStateCaptured)
            {
                return;
            }

            if (boatTarget != null)
            {
                boatTarget.anchoredPosition = initialBoatAnchoredPosition;
                boatTarget.localRotation = initialBoatRotation;
            }

            if (logoTarget != null)
            {
                logoTarget.localRotation = initialLogoRotation;
            }

            if (playerTarget != null)
            {
                playerTarget.localScale = initialPlayerScale;
            }
        }
    }
}
