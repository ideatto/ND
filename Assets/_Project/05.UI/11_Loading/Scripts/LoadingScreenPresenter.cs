using System.Collections;
using ND.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.Loading
{
    /// <summary>Coordinates framework preparation, deferred scene loading, and optional loading UI.</summary>
    public sealed class LoadingScreenPresenter : MonoBehaviour
    {
        private const string UserErrorMessage =
            "게임 데이터를 불러오지 못했습니다.\n타이틀로 돌아가 다시 시도해 주세요.";

        [Header("Timing")]
        [SerializeField, Min(0f)] private float minimumDisplaySeconds = 0.5f;
        [SerializeField, Min(0f)] private float postLoadHoldSeconds = 1f;
        [SerializeField, Min(0.01f)] private float progressVisualSpeed = 1f;

        [Header("UI References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image progressFillImage;
        [SerializeField] private Image progressFrameImage;
        [SerializeField] private TMP_Text progressPercentText;
        [SerializeField] private TMP_Text tipText;
        [SerializeField] private Button tipButton;
        [SerializeField] private GameObject errorPanel;
        [SerializeField] private TMP_Text errorText;

        [Header("Sprite Settings")]
        [SerializeField] private Sprite defaultBackgroundSprite;
        [SerializeField] private Sprite progressFillSprite;
        [SerializeField] private Sprite progressFrameSprite;

        [Header("Data")]
        [SerializeField] private LoadingTipDatabase tipDatabase;
        [SerializeField] private SeasonLoadingVisualDatabase seasonVisualDatabase;

        [Header("Behavior")]
        [SerializeField] private bool showProgressPercent = true;
        [SerializeField] private bool allowTipClickChange = true;
        [SerializeField] private bool refreshSeasonAfterPreparation = true;

        private FrameworkRoot frameworkRoot;
        private ISceneLoadOperation sceneLoadOperation;
        private LoadingTipSelector tipSelector;
        private Coroutine loadingCoroutine;
        private bool loadingStarted;
        private bool activationRequested;
        private bool hasFailed;
        private float displayedProgress;
        private float targetProgress;

        private void Awake()
        {
            frameworkRoot = FrameworkRoot.Instance;
            tipSelector = new LoadingTipSelector(
                tipDatabase != null ? tipDatabase.Tips : null,
                tipDatabase != null ? tipDatabase.FallbackTip : null);

            ApplyConfiguredSprites();
            ShowNextRandomTip();
            ApplySeasonBackground(GetCachedSeasonId());
            SetProgress(0f);
            SetErrorVisible(false);
            WarnForMissingUiReferences();
        }

        private void OnEnable()
        {
            if (tipButton != null)
            {
                tipButton.onClick.RemoveListener(OnTipClicked);
                tipButton.onClick.AddListener(OnTipClicked);
            }
        }

        private void Start()
        {
            if (!loadingStarted)
            {
                loadingStarted = true;
                loadingCoroutine = StartCoroutine(RunLoading());
            }
        }

        private void OnDisable()
        {
            if (tipButton != null)
            {
                tipButton.onClick.RemoveListener(OnTipClicked);
            }
        }

        private void OnDestroy()
        {
            if (loadingCoroutine != null)
            {
                StopCoroutine(loadingCoroutine);
                loadingCoroutine = null;
            }
        }

        public void OnTipClicked()
        {
            if (allowTipClickChange)
            {
                ShowNextRandomTip();
            }
        }

        public void ShowNextRandomTip()
        {
            if (tipText != null && tipSelector != null)
            {
                tipText.text = tipSelector.GetNextTip();
            }
        }

        /// <summary>Requests the framework title flow; ignored when framework scene flow is unavailable.</summary>
        public void ReturnToTitle()
        {
            var root = FrameworkRoot.Instance;
            root?.SceneFlow?.GoToTitle();
        }

        /// <summary>Runs preparation once and permits activation only after all visual timing gates complete.</summary>
        private IEnumerator RunLoading()
        {
            var startedAt = Time.realtimeSinceStartup;
            frameworkRoot = frameworkRoot != null ? frameworkRoot : FrameworkRoot.Instance;
            if (frameworkRoot == null)
            {
                Fail("FrameworkRoot is unavailable.");
                yield break;
            }

            var preparation = frameworkRoot.PrepareGameForInGame();
            if (!preparation.Succeeded)
            {
                Fail(preparation.ErrorSummary);
                yield break;
            }

            targetProgress = 0.2f;
            if (refreshSeasonAfterPreparation)
            {
                ApplySeasonBackground(GetOfficialSeasonId());
            }

            if (frameworkRoot.SceneFlow == null)
            {
                Fail("SceneFlow is unavailable.");
                yield break;
            }

            sceneLoadOperation = frameworkRoot.SceneFlow.BeginLoadScene(SceneNames.InGame, true);
            if (sceneLoadOperation == null)
            {
                Fail("InGame scene loading did not start.");
                yield break;
            }

            while (!hasFailed && !sceneLoadOperation.IsReadyForActivation)
            {
                targetProgress = LoadingProgress.MapSceneProgress(sceneLoadOperation.Progress01);
                AdvanceProgress();
                yield return null;
            }

            if (hasFailed || sceneLoadOperation == null)
            {
                Fail("Scene loading operation was lost.");
                yield break;
            }

            targetProgress = 1f;
            while (displayedProgress < 1f)
            {
                AdvanceProgress();
                yield return null;
            }

            while (Time.realtimeSinceStartup - startedAt < minimumDisplaySeconds)
            {
                yield return null;
            }

            var fullyDisplayedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - fullyDisplayedAt < postLoadHoldSeconds)
            {
                yield return null;
            }

            if (!hasFailed && !activationRequested)
            {
                activationRequested = true;
                sceneLoadOperation.AllowActivation();
            }
        }

        private void AdvanceProgress()
        {
            displayedProgress = LoadingProgress.MoveDisplayedProgress(
                displayedProgress,
                targetProgress,
                progressVisualSpeed * Time.unscaledDeltaTime);
            SetProgress(displayedProgress);
        }

        private void SetProgress(float value)
        {
            displayedProgress = Mathf.Clamp01(value);
            if (progressFillImage != null)
            {
                progressFillImage.fillAmount = displayedProgress;
            }

            if (progressPercentText != null)
            {
                progressPercentText.gameObject.SetActive(showProgressPercent);
                progressPercentText.text = $"{Mathf.RoundToInt(displayedProgress * 100f)}%";
            }
        }

        private string GetCachedSeasonId()
        {
            return frameworkRoot?.CurrentSaveData?.world?.currentSeasonId;
        }

        private string GetOfficialSeasonId()
        {
            if (frameworkRoot?.GameCalendar != null
                && frameworkRoot.GameCalendar.TryGetCurrent(out var snapshot))
            {
                return snapshot.SeasonId;
            }

            return GetCachedSeasonId();
        }

        private void ApplySeasonBackground(string seasonId)
        {
            if (backgroundImage == null)
            {
                return;
            }

            var resolved = seasonVisualDatabase != null
                ? seasonVisualDatabase.Resolve(seasonId)
                : null;
            backgroundImage.sprite = resolved != null ? resolved : defaultBackgroundSprite;
        }

        private void ApplyConfiguredSprites()
        {
            if (progressFillImage != null && progressFillSprite != null)
            {
                progressFillImage.sprite = progressFillSprite;
            }

            if (progressFrameImage != null && progressFrameSprite != null)
            {
                progressFrameImage.sprite = progressFrameSprite;
            }
        }

        private void Fail(string detail)
        {
            if (hasFailed)
            {
                return;
            }

            hasFailed = true;
            SetErrorVisible(true);
            if (errorText != null)
            {
                errorText.text = UserErrorMessage;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!string.IsNullOrEmpty(detail))
                {
                    errorText.text += $"\n\n{detail}";
                }
#endif
            }

            Debug.LogError($"Loading screen stopped: {detail}", this);
        }

        private void SetErrorVisible(bool visible)
        {
            if (errorPanel != null)
            {
                errorPanel.SetActive(visible);
            }
        }

        private void WarnForMissingUiReferences()
        {
            if (backgroundImage == null || progressFillImage == null || progressPercentText == null
                || tipText == null || tipButton == null || errorPanel == null || errorText == null)
            {
                Debug.LogWarning("LoadingScreenPresenter has unassigned optional UI references.", this);
            }
        }
    }
}
