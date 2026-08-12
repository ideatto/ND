using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.UI.Tutorial
{
    /// <summary>
    /// Observes successful Trade Preparation panel transitions without controlling its UI.
    /// The Summary panel is the final result for this chapter; departure remains user-owned.
    /// </summary>
    public sealed class TradePreparationTutorialAdapter : TutorialChapterBehaviour
    {
        private const string PreparationOpenedAction = "trade.preparation_opened";
        private const string CaravanConfirmedAction = "trade.caravan_confirmed";
        private const string RouteConfirmedAction = "trade.route_confirmed";
        private const string MercenaryConfirmedAction = "trade.mercenary_confirmed";

        private enum ChapterPhase
        {
            Intro,
            Actions,
            CompletionDialogue,
            Finished
        }

        [Header("Observed Panels")]
        [SerializeField] private GameObject caravanSelectionPanel;
        [SerializeField] private GameObject routeSelectionPanel;
        [SerializeField] private GameObject mercenarySelectionPanel;
        [SerializeField] private GameObject summaryPanel;

        [Header("Presentation")]
        [SerializeField] private TutorialChapterData chapterData;
        [SerializeField] private TutorialLogView logView;
        [SerializeField] private TutorialDialogueUI dialogueUI;

        private TutorialSequenceRunner runner;
        private ChapterPhase phase;
        private string visibleAction = string.Empty;
        private bool hasStarted;

        public override bool HasStarted => hasStarted;
        public override bool IsCompleted => phase == ChapterPhase.Finished;
        public event Action ChapterCompleted;

        private void Awake()
        {
            runner = new TutorialSequenceRunner(new[]
            {
                PreparationOpenedAction,
                CaravanConfirmedAction,
                RouteConfirmedAction,
                MercenaryConfirmedAction
            });
            BindProgress(runner, chapterData);
            runner.StateChanged += RefreshLog;
            runner.Completed += HandleSequenceCompleted;

        }

        private void OnEnable()
        {
            if (dialogueUI != null)
                dialogueUI.DialogueCompleted += HandleDialogueCompleted;
        }

        private void OnDisable()
        {
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
            if (phase == ChapterPhase.Actions)
                ObserveVisiblePanel();
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

        public void ResetChapter()
        {
            visibleAction = string.Empty;
            runner?.Reset();
        }

        private void BeginActions()
        {
            phase = ChapterPhase.Actions;
            visibleAction = string.Empty;
            if (logView != null)
                logView.gameObject.SetActive(true);

            ObserveVisiblePanel();
        }

        private void ObserveVisiblePanel()
        {
            string action = ResolveVisibleAction();
            if (string.Equals(action, visibleAction, StringComparison.Ordinal))
                return;

            visibleAction = action;
            SynchronizeProgress(action);
        }

        private void SynchronizeProgress(string action)
        {
            if (runner == null || runner.IsCompleted)
                return;

            runner.Reset();
            if (string.IsNullOrEmpty(action))
                return;

            runner.Handle(PreparationOpenedAction);
            if (string.Equals(action, PreparationOpenedAction, StringComparison.Ordinal))
                return;

            runner.Handle(CaravanConfirmedAction);
            if (string.Equals(action, CaravanConfirmedAction, StringComparison.Ordinal))
                return;

            runner.Handle(RouteConfirmedAction);
            if (string.Equals(action, RouteConfirmedAction, StringComparison.Ordinal))
                return;

            runner.Handle(MercenaryConfirmedAction);
        }

        private string ResolveVisibleAction()
        {
            if (summaryPanel != null && summaryPanel.activeInHierarchy)
                return MercenaryConfirmedAction;
            if (mercenarySelectionPanel != null && mercenarySelectionPanel.activeInHierarchy)
                return RouteConfirmedAction;
            if (routeSelectionPanel != null && routeSelectionPanel.activeInHierarchy)
                return CaravanConfirmedAction;
            if (caravanSelectionPanel != null && caravanSelectionPanel.activeInHierarchy)
                return PreparationOpenedAction;
            return string.Empty;
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
            else if (phase == ChapterPhase.CompletionDialogue)
                FinishChapter();
        }

        private void FinishChapter()
        {
            phase = ChapterPhase.Finished;
            ChapterCompleted?.Invoke();
            RaiseCompleted();
        }
    }
}
