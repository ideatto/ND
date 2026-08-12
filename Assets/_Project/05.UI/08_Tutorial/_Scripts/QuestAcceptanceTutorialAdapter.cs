using System.Collections.Generic;
using UnityEngine;

namespace ND.UI.Tutorial
{
    public sealed class QuestAcceptanceTutorialAdapter : TutorialChapterBehaviour
    {
        private const string MenuOpenedAction = "quest.menu_opened";
        private const string MenuClosedAction = "quest.menu_closed";
        private const string CaravanSelectedAction = "quest.caravan_context_selected";
        private const string QuestSelectedAction = "quest.offer_opened";
        private const string QuestAcceptedAction = "quest.accepted";

        private enum ChapterPhase { Intro, Actions, CompletionDialogue, Finished }

        [SerializeField] private TutorialSignalStore signalStore;
        [SerializeField] private TutorialChapterData chapterData;
        [SerializeField] private TutorialLogView logView;
        [SerializeField] private TutorialDialogueUI dialogueUI;

        private TutorialSequenceRunner runner;
        private ChapterPhase phase;
        private bool hasStarted;

        public override bool HasStarted => hasStarted;
        public override bool IsCompleted => phase == ChapterPhase.Finished;

        private void Awake()
        {
            runner = new TutorialSequenceRunner(new[]
            {
                MenuOpenedAction, CaravanSelectedAction, QuestSelectedAction, QuestAcceptedAction
            });
            BindProgress(runner, chapterData);
            runner.StateChanged += RefreshLog;
            runner.Completed += HandleSequenceCompleted;
        }

        private void OnEnable()
        {
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
        }

        private void OnDestroy()
        {
            if (runner == null) return;
            runner.StateChanged -= RefreshLog;
            runner.Completed -= HandleSequenceCompleted;
        }

        public override void BeginChapter()
        {
            if (hasStarted || IsCompleted) return;
            hasStarted = true;
            logView?.Configure(chapterData?.Title, GetStepLabels());
            RefreshLog();
            if (logView != null) logView.gameObject.SetActive(false);
            phase = ChapterPhase.Intro;
            if (!StartDialogue(chapterData?.IntroDialogue)) BeginActions();
        }

        private void BeginActions()
        {
            phase = ChapterPhase.Actions;
            if (logView != null) logView.gameObject.SetActive(true);
        }

        private void HandleSignalRecorded(TutorialSignalStore.Signal signal)
        {
            if (signal.ActionId == MenuClosedAction)
            {
                if (phase == ChapterPhase.Actions)
                    runner?.Reset();
                return;
            }

            switch (signal.ActionId)
            {
                case MenuOpenedAction:
                case CaravanSelectedAction:
                case QuestSelectedAction:
                case QuestAcceptedAction:
                    Handle(signal.ActionId);
                    break;
            }
        }
        private void Handle(string actionId)
        {
            if (phase != ChapterPhase.Actions || runner == null)
                return;

            int actionIndex = GetActionIndex(actionId);
            if (actionIndex >= 0 && actionIndex < runner.CurrentStepIndex)
                return;

            runner.Handle(actionId);
        }

        private static int GetActionIndex(string actionId)
        {
            switch (actionId)
            {
                case MenuOpenedAction: return 0;
                case CaravanSelectedAction: return 1;
                case QuestSelectedAction: return 2;
                case QuestAcceptedAction: return 3;
                default: return -1;
            }
        }

        private void HandleSequenceCompleted()
        {
            RefreshLog();
            phase = ChapterPhase.CompletionDialogue;
            if (!StartDialogue(chapterData?.CompletionDialogue)) FinishChapter();
        }

        private void HandleDialogueCompleted()
        {
            if (!hasStarted || (dialogueUI != null && dialogueUI.IsPlaying)) return;
            if (phase == ChapterPhase.Intro) BeginActions();
            else if (phase == ChapterPhase.CompletionDialogue) FinishChapter();
        }

        private bool StartDialogue(IReadOnlyList<TutorialDialogueUI.DialogueLine> source)
        {
            if (dialogueUI == null || source == null || source.Count == 0) return false;
            var lines = new List<TutorialDialogueUI.DialogueLine>();
            foreach (TutorialDialogueUI.DialogueLine line in source)
            {
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
            if (lines.Count == 0) return false;
            if (logView != null) logView.gameObject.SetActive(false);
            dialogueUI.StartDialogue(lines);
            return true;
        }

        private string[] GetStepLabels()
        {
            int count = chapterData?.Steps?.Count ?? 0;
            var labels = new string[count];
            for (int index = 0; index < count; index++)
                labels[index] = chapterData.Steps[index]?.Label ?? string.Empty;
            return labels;
        }

        private void RefreshLog() => logView?.Render(runner?.CurrentStepIndex ?? 0);
        private void FinishChapter()
        {
            phase = ChapterPhase.Finished;
            RaiseCompleted();
        }
    }
}
