using System.Collections.Generic;
using UnityEngine;

namespace ND.UI.Tutorial
{
    public sealed class DecorationInstallationTutorialAdapter : TutorialChapterBehaviour
    {
        private const string DecorationInstalled = "building.decoration_installed";

        private enum ChapterPhase { Intro, Actions, CompletionDialogue, Finished }

        [SerializeField] private TutorialSignalStore signalStore;
        [SerializeField] private TutorialChapterData chapterData;
        [SerializeField] private TutorialLogView logView;
        [SerializeField] private TutorialDialogueUI dialogueUI;

        private TutorialSequenceRunner sequence;
        private ChapterPhase phase;
        private bool hasStarted;

        public override bool HasStarted => hasStarted;
        public override bool IsCompleted => phase == ChapterPhase.Finished;

        private void Awake()
        {
            sequence = new TutorialSequenceRunner(new[] { DecorationInstalled });
            BindProgress(sequence, chapterData);
            sequence.StateChanged += RefreshLog;
            sequence.Completed += HandleActionsCompleted;
        }

        private void OnEnable()
        {
            if (signalStore != null) signalStore.SignalRecorded += HandleSignal;
            if (dialogueUI != null) dialogueUI.DialogueCompleted += HandleDialogueCompleted;
        }

        private void OnDisable()
        {
            if (signalStore != null) signalStore.SignalRecorded -= HandleSignal;
            if (dialogueUI != null) dialogueUI.DialogueCompleted -= HandleDialogueCompleted;
        }

        private void OnDestroy()
        {
            if (sequence == null) return;
            sequence.StateChanged -= RefreshLog;
            sequence.Completed -= HandleActionsCompleted;
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
            RefreshLog();
        }

        private void HandleSignal(TutorialSignalStore.Signal signal)
        {
            if (phase == ChapterPhase.Actions && signal.ActionId == DecorationInstalled)
                sequence?.Handle(signal.ActionId);
        }

        private void HandleActionsCompleted()
        {
            if (phase != ChapterPhase.Actions) return;
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
            for (int i = 0; i < count; i++)
                labels[i] = chapterData.Steps[i]?.Label ?? string.Empty;
            return labels;
        }

        private void RefreshLog() => logView?.Render(sequence?.CurrentStepIndex ?? 0);

        private void FinishChapter()
        {
            phase = ChapterPhase.Finished;
            RaiseCompleted();
        }
    }
}
