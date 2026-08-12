using System.Collections.Generic;
using UnityEngine;

namespace ND.UI.Tutorial
{
    /// <summary>
    /// Retains independent travel signals even while another tutorial owns the shared UI.
    /// Presentation is replayed through the scheduler when the shared panel becomes available.
    /// </summary>
    public sealed class RepeatableTradeTutorialController : MonoBehaviour
    {
        private enum ReminderState
        {
            Disabled,
            Idle,
            Pending,
            Active,
            DialoguePending,
            CompletionDialogue,
            MissedDialogue
        }

        [Header("Runtime")]
        [SerializeField] private TutorialSignalStore signalStore;
        [SerializeField] private TutorialPresentationScheduler presentationScheduler;
        [SerializeField] private TutorialPresentationScheduler supplementalScheduler;

        [Header("Presentation")]
        [SerializeField] private TutorialChapterData chapterData;
        [SerializeField] private TutorialLogView logView;
        [SerializeField] private TutorialDialogueUI dialogueUI;

        private readonly List<string> activeFlagIds = new List<string>();
        private readonly List<string> activeLabels = new List<string>();
        private readonly List<bool> activeStates = new List<bool>();

        private ReminderState state;
        private string targetCaravanId = string.Empty;
        private bool worldMapLearned;
        private bool treadmillLearned;
        private bool journeyEnded;
        [SerializeField] private bool activated;

        public bool WorldMapLearned => worldMapLearned;
        public bool TreadmillLearned => treadmillLearned;
        public bool IsLearned => worldMapLearned && treadmillLearned;

        public void Activate(
            bool initialWorldMapLearned,
            bool initialTreadmillLearned)
        {
            if (initialWorldMapLearned)
                TutorialProgressStore.MarkFeatureLearned(
                    TutorialProgressStore.TravelWorldMapFeatureId);
            if (initialTreadmillLearned)
                TutorialProgressStore.MarkFeatureLearned(
                    TutorialProgressStore.TravelTreadmillFeatureId);
            worldMapLearned |= initialWorldMapLearned
                || TutorialProgressStore.IsFeatureLearned(
                    TutorialProgressStore.TravelWorldMapFeatureId);
            treadmillLearned |= initialTreadmillLearned
                || TutorialProgressStore.IsFeatureLearned(
                    TutorialProgressStore.TravelTreadmillFeatureId);
            SetActivated(true);
        }

        public void SetActivated(bool value)
        {
            if (activated == value) return;

            activated = value;
            if (!activated)
            {
                StopReminder();
                state = ReminderState.Disabled;
                return;
            }

            if (state == ReminderState.Disabled)
            {
                state = ReminderState.Pending;
                RequestSupplementalPresentation();
            }
        }

        private void OnEnable()
        {
            worldMapLearned |= TutorialProgressStore.IsFeatureLearned(
                TutorialProgressStore.TravelWorldMapFeatureId);
            treadmillLearned |= TutorialProgressStore.IsFeatureLearned(
                TutorialProgressStore.TravelTreadmillFeatureId);
            state = activated ? ReminderState.Idle : ReminderState.Disabled;
            if (signalStore != null)
                signalStore.SignalRecorded += HandleSignalRecorded;
            if (dialogueUI != null)
                dialogueUI.DialogueCompleted += HandleDialogueCompleted;
        }

        private void OnDisable()
        {
            if (signalStore != null)
                signalStore.SignalRecorded -= HandleSignalRecorded;
            if (dialogueUI != null)
                dialogueUI.DialogueCompleted -= HandleDialogueCompleted;
            StopReminder();
        }

        private void HandleSignalRecorded(TutorialSignalStore.Signal signal)
        {
            switch (signal.ActionId)
            {
                case "travel.departure_succeeded":
                    BeginJourney(signal.ContextId);
                    break;
                case "travel.world_map_opened":
                    if (IsTarget(signal.ContextId)) SetWorldMapLearned();
                    break;
                case "travel.treadmill_opened":
                    if (IsTarget(signal.ContextId)) SetTreadmillLearned();
                    break;
                case "travel.journey_ended":
                    if (IsTarget(signal.ContextId)) EndJourney();
                    break;
            }
        }

        private void BeginJourney(string caravanId)
        {
            if (!activated || IsLearned) return;

            targetCaravanId = caravanId ?? string.Empty;
            journeyEnded = false;
            if (state == ReminderState.Idle)
            {
                state = ReminderState.Pending;
                RequestSupplementalPresentation();
            }
        }

        private void RequestSupplementalPresentation()
        {
            if (supplementalScheduler == null)
                HandleSupplementalPresentationGranted();
            else
                supplementalScheduler.RequestPresentation(
                    this, HandleSupplementalPresentationGranted);
        }

        private void HandleSupplementalPresentationGranted()
        {
            if (state != ReminderState.Pending) return;

            if (IsLearned)
            {
                BeginDialogue(
                    chapterData?.CompletionDialogue,
                    ReminderState.CompletionDialogue);
                return;
            }
            BuildActiveFlags();
            state = ReminderState.Active;
            logView?.Configure(chapterData?.Title, activeLabels);
            RefreshLog();
            if (logView != null) logView.gameObject.SetActive(true);
        }

        private void SetWorldMapLearned()
        {
            if (worldMapLearned) return;
            worldMapLearned = true;
            TutorialProgressStore.MarkFeatureLearned(
                TutorialProgressStore.TravelWorldMapFeatureId);
            HandleProgressChanged();
        }

        private void SetTreadmillLearned()
        {
            if (treadmillLearned) return;
            treadmillLearned = true;
            TutorialProgressStore.MarkFeatureLearned(
                TutorialProgressStore.TravelTreadmillFeatureId);
            HandleProgressChanged();
        }

        private void HandleProgressChanged()
        {
            if (state == ReminderState.Active) RefreshLog();
            if (!IsLearned) return;

            if (state == ReminderState.Active)
                BeginDialogue(
                    chapterData?.CompletionDialogue,
                    ReminderState.CompletionDialogue);
        }

        private void EndJourney()
        {
            journeyEnded = true;

            if (IsLearned)
            {
                BeginDialogue(
                    chapterData?.CompletionDialogue,
                    ReminderState.CompletionDialogue);
                return;
            }

            targetCaravanId = string.Empty;
            journeyEnded = false;
        }

        private void BeginDialogue(
            IReadOnlyList<TutorialDialogueUI.DialogueLine> source,
            ReminderState dialogueState)
        {
            if (logView != null) logView.gameObject.SetActive(false);
            supplementalScheduler?.Release(this);

            if (dialogueUI == null || source == null || source.Count == 0)
            {
                StopReminder();
                return;
            }

            pendingDialogue = source;
            pendingDialogueState = dialogueState;
            state = ReminderState.DialoguePending;
            if (!dialogueUI.IsPlaying)
                StartPendingDialogue();
        }

        private IReadOnlyList<TutorialDialogueUI.DialogueLine> pendingDialogue;
        private ReminderState pendingDialogueState;

        private void StartPendingDialogue()
        {
            if (state != ReminderState.DialoguePending
                || dialogueUI == null
                || dialogueUI.IsPlaying
                || pendingDialogue == null)
                return;

            var lines = new List<TutorialDialogueUI.DialogueLine>();
            for (int index = 0; index < pendingDialogue.Count; index++)
            {
                TutorialDialogueUI.DialogueLine line = pendingDialogue[index];
                if (line == null || string.IsNullOrWhiteSpace(line.message)) continue;
                lines.Add(new TutorialDialogueUI.DialogueLine
                {
                    npcName = line.npcName,
                    message = line.message,
                    emotion = line.emotion,
                    npcSprite = line.npcSprite,
                    illustrationSprite = line.illustrationSprite
                });
            }
            if (lines.Count == 0)
            {
                StopReminder();
                return;
            }

            state = pendingDialogueState;
            dialogueUI.StartDialogue(lines);
        }

        private void HandleDialogueCompleted()
        {
            if (state == ReminderState.DialoguePending)
            {
                StartPendingDialogue();
                return;
            }

            if (state != ReminderState.CompletionDialogue
                && state != ReminderState.MissedDialogue) return;
            if (state == ReminderState.CompletionDialogue)
                activated = false;
            StopReminder();
            if (!activated)
                state = ReminderState.Disabled;
        }

        private void BuildActiveFlags()
        {
            activeFlagIds.Clear();
            activeLabels.Clear();
            if (!worldMapLearned)
            {
                activeFlagIds.Add("travel.world_map_opened");
                activeLabels.Add(FindStepLabel("open_world_map"));
            }
            if (!treadmillLearned)
            {
                activeFlagIds.Add("travel.treadmill_opened");
                activeLabels.Add(FindStepLabel("open_treadmill"));
            }
        }

        private string FindStepLabel(string stepId)
        {
            if (chapterData?.Steps != null)
            {
                for (int index = 0; index < chapterData.Steps.Count; index++)
                {
                    TutorialChapterData.StepPresentation step = chapterData.Steps[index];
                    if (step != null && step.StepId == stepId) return step.Label;
                }
            }
            return stepId;
        }

        private void RefreshLog()
        {
            activeStates.Clear();
            for (int index = 0; index < activeFlagIds.Count; index++)
            {
                activeStates.Add(activeFlagIds[index] == "travel.world_map_opened"
                    ? worldMapLearned
                    : treadmillLearned);
            }
            logView?.RenderIndependent(activeStates);
        }

        private bool IsTarget(string contextId) =>
            state != ReminderState.Idle
            && string.Equals(targetCaravanId, contextId ?? string.Empty,
                System.StringComparison.Ordinal);

        private void StopReminder()
        {
            if ((supplementalScheduler == null
                    || supplementalScheduler.IsActive(this))
                && logView != null)
                logView.gameObject.SetActive(false);
            supplementalScheduler?.Release(this);
            presentationScheduler?.Release(this);
            targetCaravanId = string.Empty;
            journeyEnded = false;
            pendingDialogue = null;
            state = activated ? ReminderState.Idle : ReminderState.Disabled;
        }
    }
}
