using System;
using System.Collections.Generic;
using ND.Framework;
using ND.UI.CargoSell;
using ND.UI.Market;
using UnityEngine;

namespace ND.UI.Tutorial
{
    public sealed class ArrivalSellingTutorialAdapter : TutorialChapterBehaviour
    {
        private const string SaleOpenedAction = "selling.sale_opened";
        private const string DraftCreatedAction = "selling.draft_created";
        private const string SaleConfirmedAction = "selling.sale_confirmed";

        private enum ChapterPhase
        {
            WaitingForSuccessfulArrival,
            FailureDialogue,
            IntroDialogue,
            Actions,
            CompletionDialogue,
            Finished
        }

        [Header("Observed UI")]
        [SerializeField] private CaravanArrivalSaleController saleController;
        [SerializeField] private CargoSellPopupController cargoSellPopup;

        [Header("Presentation")]
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
                SaleOpenedAction,
                DraftCreatedAction,
                SaleConfirmedAction
            });
            BindProgress(runner, chapterData);
            runner.StateChanged += RefreshLog;
            runner.Completed += HandleSequenceCompleted;
        }

        private void OnEnable()
        {
            FrameworkEvents.CaravanJourneyStateChanged += HandleJourneyStateChanged;
            FrameworkEvents.TradeSettlementReady += HandleTradeSettlementReady;
            if (saleController != null)
            {
                saleController.SaleOpened += HandleSaleOpened;
                saleController.SettlementRequested += HandleSettlementRequested;
            }
            if (dialogueUI != null)
                dialogueUI.DialogueCompleted += HandleDialogueCompleted;
        }

        private void OnDisable()
        {
            FrameworkEvents.CaravanJourneyStateChanged -= HandleJourneyStateChanged;
            FrameworkEvents.TradeSettlementReady -= HandleTradeSettlementReady;
            if (saleController != null)
            {
                saleController.SaleOpened -= HandleSaleOpened;
                saleController.SettlementRequested -= HandleSettlementRequested;
            }
            if (dialogueUI != null)
                dialogueUI.DialogueCompleted -= HandleDialogueCompleted;
        }

        private void OnDestroy()
        {
            if (runner == null) return;
            runner.StateChanged -= RefreshLog;
            runner.Completed -= HandleSequenceCompleted;
        }

        private void Update()
        {
            if (!hasStarted || IsCompleted)
                return;

            if (phase == ChapterPhase.WaitingForSuccessfulArrival && HasSellingCaravan())
                StartIntroDialogue();

            if (phase != ChapterPhase.Actions || runner == null || runner.IsCompleted)
                return;

            if (runner.CurrentStepIndex == 0 && saleController != null && saleController.IsOpen)
                runner.Handle(SaleOpenedAction);
            if (runner.CurrentStepIndex == 1 && cargoSellPopup != null && cargoSellPopup.HasDraft)
                runner.Handle(DraftCreatedAction);
        }

        public override void BeginChapter()
        {
            if (hasStarted || IsCompleted) return;
            hasStarted = true;
            phase = ChapterPhase.WaitingForSuccessfulArrival;
            logView?.Configure(chapterData?.Title, GetStepLabels());
            RefreshLog();
            if (logView != null) logView.gameObject.SetActive(false);
        }

        private void HandleJourneyStateChanged(string _, JourneyState state)
        {
            if (hasStarted && phase == ChapterPhase.WaitingForSuccessfulArrival
                && state == JourneyState.Selling)
                StartIntroDialogue();
        }

        private void HandleTradeSettlementReady(string _, string __, JourneyResultData result)
        {
            if (!hasStarted || phase != ChapterPhase.WaitingForSuccessfulArrival
                || result == null || result.grade != JourneyResultGrade.Failed)
                return;

            phase = ChapterPhase.FailureDialogue;
            if (!StartDialogue(chapterData?.FailureDialogue))
                ReturnToArrivalWait();
        }

        private void StartIntroDialogue()
        {
            if (phase != ChapterPhase.WaitingForSuccessfulArrival) return;
            phase = ChapterPhase.IntroDialogue;
            if (!StartDialogue(chapterData?.IntroDialogue)) BeginActions();
        }

        private void BeginActions()
        {
            phase = ChapterPhase.Actions;
            if (logView != null) logView.gameObject.SetActive(true);
        }

        private void HandleSaleOpened(string _, string __)
        {
            if (phase == ChapterPhase.Actions && runner?.CurrentStepIndex == 0)
                runner.Handle(SaleOpenedAction);
        }

        private void HandleSettlementRequested(string _, string __)
        {
            if (phase == ChapterPhase.Actions && runner?.CurrentStepIndex == 2)
                runner.Handle(SaleConfirmedAction);
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
            if (phase == ChapterPhase.FailureDialogue) ReturnToArrivalWait();
            else if (phase == ChapterPhase.IntroDialogue) BeginActions();
            else if (phase == ChapterPhase.CompletionDialogue) FinishChapter();
        }

        private void ReturnToArrivalWait()
        {
            phase = ChapterPhase.WaitingForSuccessfulArrival;
            if (logView != null) logView.gameObject.SetActive(false);
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
            for (int i = 0; i < count; i++) labels[i] = chapterData.Steps[i]?.Label ?? string.Empty;
            return labels;
        }

        private static bool HasSellingCaravan()
        {
            IReadOnlyList<ND.Framework.CaravanSaveData> caravans =
                FrameworkRoot.Instance?.CurrentSaveData?.caravans;
            if (caravans == null) return false;
            for (int i = 0; i < caravans.Count; i++)
                if (caravans[i] != null && caravans[i].state == JourneyState.Selling) return true;
            return false;
        }

        private void RefreshLog() => logView?.Render(runner?.CurrentStepIndex ?? 0);

        private void FinishChapter()
        {
            phase = ChapterPhase.Finished;
            RaiseCompleted();
        }
    }
}
