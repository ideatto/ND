using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// Scene-independent entry point for future Quest panels. UI buttons call this
    /// component; concrete panel objects subscribe to PanelRequested and ListChanged.
    /// </summary>
    public sealed class QuestPanelRuntimeBridge : MonoBehaviour
    {
        private Func<FrameworkRoot> getRoot;
        private string openQuestId = string.Empty;

        public event Action<TownQuestOpenResult> PanelRequested;
        public event Action<IReadOnlyList<TownQuestViewModel>> ListChanged;

        public void Initialize(Func<FrameworkRoot> rootAccessor)
        {
            getRoot = rootAccessor;
        }

        public void RequestTownQuestList(string townId)
        {
            FrameworkRoot root = getRoot?.Invoke();
            IReadOnlyList<TownQuestViewModel> quests =
                TownQuestPresentationService.GetActiveQuests(
                    root?.CurrentSaveData,
                    root?.SharedGameData,
                    townId);
            ListChanged?.Invoke(quests);
        }

        public void RequestOpenQuest(string questId)
        {
            FrameworkRoot root = getRoot?.Invoke();
            TownQuestOpenResult result = TownQuestPresentationService.OpenQuest(
                root?.CurrentSaveData,
                root?.SharedGameData,
                questId);
            openQuestId = result.PanelKind == TownQuestPanelKind.None
                ? string.Empty
                : questId ?? string.Empty;
            PanelRequested?.Invoke(result);
        }

        public QuestMutationResult AcceptOpenQuest()
        {
            FrameworkRoot root = getRoot?.Invoke();
            QuestMutationResult result = TownQuestFlowService.Accept(
                root?.CurrentSaveData,
                root?.SaveService,
                root?.GameTime,
                openQuestId);
            if (result.Succeeded) RequestOpenQuest(openQuestId);
            return result;
        }

        public QuestCompletionResult CompleteOpenQuest(
            string caravanId,
            QuestPaymentMode paymentMode)
        {
            FrameworkRoot root = getRoot?.Invoke();
            if (root?.SharedGameData == null ||
                !root.SharedGameData.TryGetQuest(
                    openQuestId, out SharedQuestDefinition quest))
                return new QuestCompletionResult
                {
                    FailureReason = QuestCompletionFailureReason.InvalidInput
                };

            QuestCompletionResult result = QuestRuntimeService.Complete(
                root.CurrentSaveData,
                root.SaveService,
                root.GameTime,
                quest,
                quest.SubmissionTownId,
                caravanId,
                paymentMode);
            if (result.Succeeded) ClosePanel();
            else RequestOpenQuest(openQuestId);
            return result;
        }

        public void RejectOffer()
        {
            ClosePanel();
        }

        public void CancelPanel()
        {
            ClosePanel();
        }

        private void ClosePanel()
        {
            openQuestId = string.Empty;
            PanelRequested?.Invoke(new TownQuestOpenResult());
        }
    }
}
