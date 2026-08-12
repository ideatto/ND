using System;
using System.Collections.Generic;
using ND.Framework;
using UnityEngine;

namespace ND.UI.Tutorial
{
    /// <summary>
    /// Observes a saved departure, the world map opening, and the additive treadmill panel.
    /// It does not depend on either feature's button location.
    /// </summary>
    public sealed class TravelAndMapTutorialAdapter : TutorialChapterBehaviour
    {
        private const string DepartureSucceededAction = "travel.departure_succeeded";
        private const string WorldMapOpenedAction = "travel.world_map_opened";
        private const string TreadmillOpenedAction = "travel.treadmill_opened";

        private enum ChapterPhase
        {
            Intro,
            Actions,
            MissedActionsDialogue,
            CompletionDialogue,
            Finished
        }

        [Header("Observed UI")]
        [SerializeField] private SlidePanel worldMapPanel;

        [Header("Fallback")]
        [SerializeField] private RepeatableTradeTutorialController repeatableController;

        [Header("Presentation")]
        [SerializeField] private TutorialChapterData chapterData;
        [SerializeField] private TutorialLogView logView;
        [SerializeField] private TutorialDialogueUI dialogueUI;

        private TutorialSequenceRunner runner;
        private ChapterPhase phase;
        private bool hasStarted;
        private bool wasWorldMapOpen;
        private bool wasTreadmillOpen;
        private TreadmillPanel treadmillPanel;

        public override bool HasStarted => hasStarted;
        public override bool IsCompleted => phase == ChapterPhase.Finished;
        public event Action ChapterCompleted;

        private void Awake()
        {
            runner = new TutorialSequenceRunner(new[]
            {
                DepartureSucceededAction,
                WorldMapOpenedAction,
                TreadmillOpenedAction
            });
            BindProgress(runner, chapterData);
            runner.StateChanged += RefreshLog;
            runner.Completed += HandleSequenceCompleted;
        }

        private void OnEnable()
        {
            FrameworkEvents.CaravanJourneyStateChanged += HandleJourneyStateChanged;
            if (dialogueUI != null)
                dialogueUI.DialogueCompleted += HandleDialogueCompleted;
        }

        private void OnDisable()
        {
            FrameworkEvents.CaravanJourneyStateChanged -= HandleJourneyStateChanged;
            if (dialogueUI != null)
                dialogueUI.DialogueCompleted -= HandleDialogueCompleted;
        }

        private void OnDestroy()
        {
            if (runner == null)
                return;

            runner.StateChanged -= RefreshLog;
            runner.Completed -= HandleSequenceCompleted;
        }

        private void Update()
        {
            if (phase != ChapterPhase.Actions || runner == null || runner.IsCompleted)
                return;

            if (runner.CurrentStepIndex >= 1)
                ObserveWorldMap();
            if (runner.CurrentStepIndex >= 2)
                ObserveTreadmill();
        }

        public override void BeginChapter()
        {
            if (hasStarted || IsCompleted)
                return;

            hasStarted = true;
            logView?.Configure(chapterData?.Title, GetStepLabels());
            RefreshLog();
            if (logView != null)
                logView.gameObject.SetActive(false);

            phase = ChapterPhase.Intro;
            if (!StartDialogue(chapterData?.IntroDialogue))
                BeginActions();
        }

        private void BeginActions()
        {
            phase = ChapterPhase.Actions;
            wasWorldMapOpen = worldMapPanel != null && worldMapPanel.IsOpen;
            wasTreadmillOpen = false;
            if (logView != null)
                logView.gameObject.SetActive(true);
        }

        private void HandleJourneyStateChanged(string _, JourneyState state)
        {
            if (phase != ChapterPhase.Actions)
                return;

            if (state == JourneyState.Traveling)
            {
                runner?.Handle(DepartureSucceededAction);
                return;
            }

            if (state == JourneyState.Selling && runner != null && !runner.IsCompleted)
                HandleArrivalWithMissedActions();
        }

        private void HandleArrivalWithMissedActions()
        {
            phase = ChapterPhase.MissedActionsDialogue;
            if (!StartDialogue(chapterData?.FailureDialogue))
                FinishChapter();
        }

        private void ObserveWorldMap()
        {
            bool isOpen = worldMapPanel != null && worldMapPanel.IsOpen;
            if (isOpen && !wasWorldMapOpen)
                runner.Handle(WorldMapOpenedAction);
            wasWorldMapOpen = isOpen;
        }

        private void ObserveTreadmill()
        {
            bool isOpen = ResolveTreadmillOpen();
            if (isOpen && !wasTreadmillOpen)
                runner.Handle(TreadmillOpenedAction);
            wasTreadmillOpen = isOpen;
        }

        private bool ResolveTreadmillOpen()
        {
            if (treadmillPanel == null)
            {
                treadmillPanel = UnityEngine.Object.FindAnyObjectByType<TreadmillPanel>(
                    FindObjectsInactive.Include);
            }
            return treadmillPanel != null && treadmillPanel.IsOpen;
        }

        private void HandleSequenceCompleted()
        {
            RefreshLog();
            phase = ChapterPhase.CompletionDialogue;
            if (!StartDialogue(chapterData?.CompletionDialogue))
                FinishChapter();
        }

        private void RefreshLog()
        {
            logView?.Render(runner?.CurrentStepIndex ?? 0);
        }

        private string[] GetStepLabels()
        {
            int count = chapterData?.Steps?.Count ?? 0;
            var labels = new string[count];
            for (int index = 0; index < count; index++)
                labels[index] = chapterData.Steps[index]?.Label ?? string.Empty;
            return labels;
        }

        private bool StartDialogue(
            IReadOnlyList<TutorialDialogueUI.DialogueLine> sourceLines)
        {
            if (dialogueUI == null || sourceLines == null || sourceLines.Count == 0)
                return false;

            var lines = new List<TutorialDialogueUI.DialogueLine>();
            for (int index = 0; index < sourceLines.Count; index++)
            {
                TutorialDialogueUI.DialogueLine source = sourceLines[index];
                if (source == null || string.IsNullOrWhiteSpace(source.message))
                    continue;

                lines.Add(new TutorialDialogueUI.DialogueLine
                {
                    npcName = source.npcName,
                    message = source.message,
                    emotion = source.emotion,
                    npcSprite = source.npcSprite,
                    illustrationSprite = source.illustrationSprite
                });
            }

            if (lines.Count == 0)
                return false;

            if (logView != null)
                logView.gameObject.SetActive(false);
            dialogueUI.StartDialogue(lines);
            return true;
        }

        private void HandleDialogueCompleted()
        {
            if (!hasStarted || (dialogueUI != null && dialogueUI.IsPlaying))
                return;

            if (phase == ChapterPhase.Intro)
                BeginActions();
            else if (phase == ChapterPhase.MissedActionsDialogue)
                FinishChapter();
            else if (phase == ChapterPhase.CompletionDialogue)
                FinishChapter();
        }

        private void FinishChapter()
        {
            if (phase == ChapterPhase.MissedActionsDialogue)
            {
                int completedSteps = runner?.CurrentStepIndex ?? 0;
                repeatableController?.Activate(
                    completedSteps >= 2,
                    completedSteps >= 3);
            }

            phase = ChapterPhase.Finished;
            ChapterCompleted?.Invoke();
            RaiseCompleted();
        }
    }
}
