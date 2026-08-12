using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.UI.Tutorial
{
    public sealed class QuestCompletionReminderController : MonoBehaviour
    {
        private enum ReminderState
        {
            Idle,
            Pending,
            Active,
            CompletionPending,
            CompletionDialogue
        }

        [SerializeField] private TutorialSignalStore signalStore;
        [SerializeField] private TutorialPresentationScheduler presentationScheduler;
        [SerializeField] private TutorialPresentationScheduler supplementalScheduler;
        [SerializeField] private TutorialLogView logView;
        [SerializeField] private TutorialDialogueUI dialogueUI;
        [SerializeField] private TutorialChapterData completionChapterData;
        [SerializeField] private string title = "퀘스트를 해결하세요";
        [SerializeField] private string caravanLabel = "조건을 충족한 캐러반을 선택하세요";
        [SerializeField] private string paymentLabel = "결제 방식을 선택해 퀘스트를 완료하세요";

        private ReminderState state;
        private string targetQuestId = string.Empty;
        private bool caravanSelected;

        private void OnEnable()
        {
            if (signalStore != null)
                signalStore.SignalRecorded += HandleSignalRecorded;
        }

        private void OnDisable()
        {
            if (signalStore != null)
                signalStore.SignalRecorded -= HandleSignalRecorded;
            StopReminder();
        }

        private void HandleSignalRecorded(TutorialSignalStore.Signal signal)
        {
            switch (signal.ActionId)
            {
                case "quest.payment_opened":
                    if (state == ReminderState.CompletionPending
                        || state == ReminderState.CompletionDialogue) return;
                    targetQuestId = signal.ContextId;
                    caravanSelected = false;
                    state = ReminderState.Pending;
                    RequestSupplementalPresentation();
                    break;
                case "quest.payment_caravan_selected":
                    if (state == ReminderState.Idle) return;
                    caravanSelected = true;
                    if (state == ReminderState.Active) RefreshLog();
                    break;
                case "quest.rewards_committed":
                    HandleQuestCompleted(signal.ContextId);
                    break;
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

            state = ReminderState.Active;
            logView?.Configure(title, new[] { caravanLabel, paymentLabel });
            RefreshLog();
            if (logView != null) logView.gameObject.SetActive(true);
        }

        private void HandleQuestCompleted(string questId)
        {
            if (state == ReminderState.CompletionPending
                || state == ReminderState.CompletionDialogue) return;
            if (!string.IsNullOrEmpty(targetQuestId)
                && !string.Equals(targetQuestId, questId, StringComparison.Ordinal)) return;

            targetQuestId = questId ?? string.Empty;
            if (state == ReminderState.Active)
                logView?.Render(2);

            if ((supplementalScheduler == null
                    || supplementalScheduler.IsActive(this))
                && logView != null)
                logView.gameObject.SetActive(false);
            supplementalScheduler?.Release(this);
            state = ReminderState.CompletionPending;
            if (presentationScheduler == null)
                ShowCompletionDialogue();
            else
                presentationScheduler.RequestPresentation(
                    this, ShowCompletionDialogue);
        }

        private void ShowCompletionDialogue()
        {
            if (state != ReminderState.CompletionPending) return;

            if (dialogueUI == null || completionChapterData == null
                || completionChapterData.CompletionDialogue.Count == 0)
            {
                StopReminder();
                return;
            }

            state = ReminderState.CompletionDialogue;
            dialogueUI.DialogueCompleted -= HandleCompletionDialogueClosed;
            dialogueUI.DialogueCompleted += HandleCompletionDialogueClosed;
            dialogueUI.StartDialogue(
                new List<TutorialDialogueUI.DialogueLine>(
                    completionChapterData.CompletionDialogue));
        }

        private void HandleCompletionDialogueClosed()
        {
            if (state != ReminderState.CompletionDialogue) return;
            StopReminder();
        }

        private void RefreshLog() => logView?.Render(caravanSelected ? 1 : 0);
        private void StopReminder()
        {
            if (dialogueUI != null)
                dialogueUI.DialogueCompleted -= HandleCompletionDialogueClosed;

            if ((supplementalScheduler == null
                    || supplementalScheduler.IsActive(this))
                && logView != null)
                logView.gameObject.SetActive(false);
            supplementalScheduler?.Release(this);
            presentationScheduler?.Release(this);
            state = ReminderState.Idle;
            targetQuestId = string.Empty;
            caravanSelected = false;
        }
    }
}
