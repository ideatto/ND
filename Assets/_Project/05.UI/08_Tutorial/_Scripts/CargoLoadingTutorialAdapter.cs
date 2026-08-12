using System;
using System.Collections.Generic;
using ND.Framework.CargoLoading;
using ND.UI.Market;
using UnityEngine;

namespace ND.UI.Tutorial
{
    /// <summary>
    /// Adapts the existing Cargo draft and Market commit events to the first tutorial chapter.
    /// It observes only public UI events and never mutates Cargo, Market, Quest, or SaveData.
    /// </summary>
    public sealed class CargoLoadingTutorialAdapter : TutorialChapterBehaviour
    {
        private const string CargoDraftLoadedAction = "cargo.draft_loaded";
        private const string CargoPurchaseCommittedAction = "cargo.purchase_committed";
        private const string CargoPanelOpenedAction = "cargo.panel_opened";

        private enum ChapterPhase
        {
            Intro,
            Actions,
            CompletionDialogue,
            Finished
        }

        [Header("Sources")]
        [SerializeField] private CargoLoadingPanelController cargoPanel;
        [SerializeField] private MarketTradePanelController marketPanel;

        [Header("Presentation")]
        [SerializeField] private TutorialChapterData chapterData;
        [SerializeField] private TutorialLogView logView;
        [SerializeField] private TutorialDialogueUI dialogueUI;

        private TutorialSequenceRunner runner;
        private bool hasCargoDraft;
        private bool wasCargoPanelVisible;
        private ChapterPhase phase;
        private bool hasStarted;

        public override bool HasStarted => hasStarted;
        public override bool IsCompleted => phase == ChapterPhase.Finished;
        public event Action ChapterCompleted;

        private void Awake()
        {
            runner = new TutorialSequenceRunner(new[]
            {
                CargoPanelOpenedAction,
                CargoDraftLoadedAction,
                CargoPurchaseCommittedAction
            });
            BindProgress(runner, chapterData);
            runner.StateChanged += RefreshLog;
            runner.Completed += HandleChapterCompleted;

        }

        private void OnEnable()
        {
            if (cargoPanel != null)
                cargoPanel.LoadChanged += HandleLoadChanged;
            if (marketPanel != null)
                marketPanel.TransactionCompleted += HandleTransactionCompleted;
            if (dialogueUI != null)
                dialogueUI.DialogueCompleted += HandleDialogueCompleted;
        }

        private void OnDisable()
        {
            if (cargoPanel != null)
                cargoPanel.LoadChanged -= HandleLoadChanged;
            if (marketPanel != null)
                marketPanel.TransactionCompleted -= HandleTransactionCompleted;
            if (dialogueUI != null)
                dialogueUI.DialogueCompleted -= HandleDialogueCompleted;
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

        private void Update()
        {
            if (phase == ChapterPhase.Actions)
                ObserveCargoPanelVisibility();
        }

        private void OnDestroy()
        {
            if (runner == null)
                return;

            runner.StateChanged -= RefreshLog;
            runner.Completed -= HandleChapterCompleted;
        }

        public void ResetChapter()
        {
            hasCargoDraft = false;
            runner?.Reset();
        }

        private void BeginActions()
        {
            phase = ChapterPhase.Actions;
            if (logView != null)
                logView.gameObject.SetActive(true);

            wasCargoPanelVisible = false;
            ObserveCargoPanelVisibility();
        }

        private void ObserveCargoPanelVisibility()
        {
            bool isVisible = cargoPanel != null && cargoPanel.gameObject.activeInHierarchy;
            if (isVisible && !wasCargoPanelVisible)
                RecordCargoPanelOpened();

            wasCargoPanelVisible = isVisible;
        }

        private void RecordCargoPanelOpened()
        {
            if (runner == null || runner.IsCompleted)
                return;

            if (runner.CurrentStepIndex == 0)
                runner.Handle(CargoPanelOpenedAction);
        }

        private void HandleLoadChanged(CargoLoadingPanelController.CargoChangeSnapshot snapshot)
        {
            if (phase != ChapterPhase.Actions)
                return;

            if (runner != null
                && runner.CurrentStepIndex == 0
                && cargoPanel != null
                && cargoPanel.gameObject.activeInHierarchy)
            {
                runner.Handle(CargoPanelOpenedAction);
            }

            bool hasItems = false;
            if (snapshot?.items != null)
            {
                for (int index = 0; index < snapshot.items.Count; index++)
                {
                    CargoLoadingPanelController.CargoSelection item = snapshot.items[index];
                    if (!string.IsNullOrWhiteSpace(item.itemId) && item.quantity > 0)
                    {
                        hasItems = true;
                        break;
                    }
                }
            }

            if (!hasItems)
            {
                if (runner != null && runner.IsCompleted)
                    return;

                if (!hasCargoDraft)
                    return;

                ResetChapter();
                if (cargoPanel != null && cargoPanel.gameObject.activeInHierarchy)
                    RecordCargoPanelOpened();
                return;
            }

            if (hasCargoDraft)
                return;

            hasCargoDraft = true;
            runner?.Handle(CargoDraftLoadedAction);
        }

        private void HandleTransactionCompleted(MarketTransactionResult result)
        {
            if (phase != ChapterPhase.Actions
                || result == null
                || !result.Success
                || !hasCargoDraft)
                return;

            bool purchasedCargo = false;
            if (result.Items != null)
            {
                for (int index = 0; index < result.Items.Count; index++)
                {
                    MarketTransactionItemSummary item = result.Items[index];
                    if (item != null && item.BuyQuantity > 0)
                    {
                        purchasedCargo = true;
                        break;
                    }
                }
            }

            if (purchasedCargo)
                runner?.Handle(CargoPurchaseCommittedAction);
        }

        private void RefreshLog()
        {
            logView?.Render(runner?.CurrentStepIndex ?? 0);
        }

        private void HandleChapterCompleted()
        {
            RefreshLog();
            phase = ChapterPhase.CompletionDialogue;
            if (!StartDialogue(chapterData?.CompletionDialogue))
                FinishChapter();
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
