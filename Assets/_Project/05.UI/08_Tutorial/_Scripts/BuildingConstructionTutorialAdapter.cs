using System;
using System.Collections.Generic;
using ND.Framework;
using UnityEngine;

namespace ND.UI.Tutorial
{
    public sealed class BuildingConstructionTutorialAdapter : TutorialChapterBehaviour
    {
        public enum ChapterMode
        {
            BaseCampSequence,
            RequiredBuildingsFreeOrder,
            BaseCampUpgradeSequence
        }

        private enum ChapterPhase { Intro, Actions, CompletionDialogue, Finished }

        private const string CatalogOpened = "building.catalog_opened";
        private const string CatalogDismissed = "building.catalog_dismissed";
        private const string DetailDismissed = "building.detail_dismissed";
        private const string ConfirmationDismissed = "building.confirmation_dismissed";
        private const string BuildingSelected = "building.selected";
        private const string ConstructionCommitted = "building.construction_committed";
        private const string WarehouseOpened = "warehouse.panel_opened";
        private const string WarehouseItemStored = "warehouse.item_stored";

        [SerializeField] private ChapterMode mode;
        [SerializeField] private TutorialSignalStore signalStore;
        [SerializeField] private TutorialChapterData chapterData;
        [SerializeField] private TutorialLogView logView;
        [SerializeField] private TutorialDialogueUI dialogueUI;

        private TutorialSequenceRunner sequence;
        private readonly bool[] requiredCompleted = new bool[3];
        private ChapterPhase phase;
        private bool hasStarted;

        public override bool HasStarted => hasStarted;
        public override bool IsCompleted => phase == ChapterPhase.Finished;

        private void Awake()
        {
            if (!UsesSequence)
            {
                BindProgress(null, chapterData);
                return;
            }
            sequence = new TutorialSequenceRunner(mode == ChapterMode.BaseCampUpgradeSequence
                ? new[]
                {
                    WarehouseOpened,
                    WarehouseItemStored,
                    CatalogOpened,
                    "building.basecamp_upgrade_selected",
                    "building.basecamp_upgraded"
                }
                : new[]
                {
                    CatalogOpened,
                    "building.basecamp_selected",
                    "building.basecamp_built"
                });
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
            SeedExistingBuildings();
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
            if (AreActionsCompleted()) HandleActionsCompleted();
        }

        private void HandleSignal(TutorialSignalStore.Signal signal)
        {
            if (phase != ChapterPhase.Actions) return;

            if (UsesSequence)
            {
                if (mode != ChapterMode.BaseCampUpgradeSequence
                    && (signal.ActionId == CatalogDismissed
                        || signal.ActionId == DetailDismissed
                        || signal.ActionId == ConfirmationDismissed))
                    sequence?.Reset();
                else if (mode == ChapterMode.BaseCampUpgradeSequence
                         && signal.ActionId == WarehouseOpened)
                    HandleExpectedUpgradeStep(WarehouseOpened, 0);
                else if (mode == ChapterMode.BaseCampUpgradeSequence
                         && signal.ActionId == WarehouseItemStored)
                    HandleExpectedUpgradeStep(WarehouseItemStored, 1);
                else if (signal.ActionId == CatalogOpened)
                {
                    if (mode == ChapterMode.BaseCampUpgradeSequence)
                        HandleExpectedUpgradeStep(CatalogOpened, 2);
                    else
                        sequence?.Handle(CatalogOpened);
                }
                else if (signal.ActionId == BuildingSelected
                         && string.Equals(signal.ContextId, "BaseCamp", StringComparison.Ordinal))
                {
                    if (mode == ChapterMode.BaseCampUpgradeSequence)
                        HandleExpectedUpgradeStep("building.basecamp_upgrade_selected", 3);
                    else
                        sequence?.Handle("building.basecamp_selected");
                }
                else if (signal.ActionId == ConstructionCommitted
                         && string.Equals(signal.ContextId, "BaseCamp", StringComparison.Ordinal)
                         && ParseLevel(signal.SourceId) >= RequiredBaseCampLevel)
                {
                    if (mode == ChapterMode.BaseCampUpgradeSequence)
                        HandleExpectedUpgradeStep("building.basecamp_upgraded", 4);
                    else
                        sequence?.Handle("building.basecamp_built");
                }
                return;
            }

            if (signal.ActionId != ConstructionCommitted || ParseLevel(signal.SourceId) < 1) return;
            MarkRequired(signal.ContextId);
            RefreshLog();
            if (AreActionsCompleted()) HandleActionsCompleted();
        }

        private void HandleExpectedUpgradeStep(string actionId, int stepIndex)
        {
            if (sequence != null && sequence.CurrentStepIndex == stepIndex)
                sequence.Handle(actionId);
        }

        private void SeedExistingBuildings()
        {
            var buildings = FrameworkRoot.Instance?.CurrentSaveData?.player?.villageBuildings;
            if (buildings == null) return;
            foreach (VillageBuildingSaveData building in buildings)
            {
                if (building == null || building.level < 1) continue;
                if (mode == ChapterMode.BaseCampSequence
                    && string.Equals(building.displayName, "베이스 캠프", StringComparison.Ordinal))
                {
                    sequence?.Handle(CatalogOpened);
                    sequence?.Handle("building.basecamp_selected");
                    sequence?.Handle("building.basecamp_built");
                }
                else if (mode == ChapterMode.RequiredBuildingsFreeOrder)
                {
                    if (building.displayName == "목장") requiredCompleted[0] = true;
                    else if (building.displayName == "창고") requiredCompleted[1] = true;
                    else if (building.displayName == "오두막") requiredCompleted[2] = true;
                }
            }
        }

        private void MarkRequired(string buildId)
        {
            if (buildId == "Farm") requiredCompleted[0] = true;
            else if (buildId == "Warehouse") requiredCompleted[1] = true;
            else if (buildId == "Cottage") requiredCompleted[2] = true;
            PersistRequiredProgress();
        }

        protected override void RestoreCustomProgress(IReadOnlyList<string> completedStepIds)
        {
            if (mode != ChapterMode.RequiredBuildingsFreeOrder || completedStepIds == null)
                return;

            for (int index = 0; index < completedStepIds.Count; index++)
            {
                if (completedStepIds[index] == "build_farm") requiredCompleted[0] = true;
                else if (completedStepIds[index] == "build_warehouse") requiredCompleted[1] = true;
                else if (completedStepIds[index] == "build_cottage") requiredCompleted[2] = true;
            }
        }

        private void PersistRequiredProgress()
        {
            if (mode != ChapterMode.RequiredBuildingsFreeOrder) return;
            var completed = new List<string>();
            if (requiredCompleted[0]) completed.Add("build_farm");
            if (requiredCompleted[1]) completed.Add("build_warehouse");
            if (requiredCompleted[2]) completed.Add("build_cottage");
            PersistCustomProgress(completed, requiredCompleted.Length);
        }

        private bool AreActionsCompleted() => UsesSequence
            ? sequence != null && sequence.IsCompleted
            : requiredCompleted[0] && requiredCompleted[1] && requiredCompleted[2];

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
                    npcName = line.npcName, message = line.message,
                    emotion = line.emotion,
                    npcSprite = line.npcSprite, illustrationSprite = line.illustrationSprite
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

        private void RefreshLog()
        {
            if (UsesSequence) logView?.Render(sequence?.CurrentStepIndex ?? 0);
            else logView?.RenderIndependent(requiredCompleted);
        }

        private void FinishChapter()
        {
            phase = ChapterPhase.Finished;
            RaiseCompleted();
        }

        private static int ParseLevel(string sourceId) => int.TryParse(sourceId, out int level) ? level : 0;

        private bool UsesSequence => mode == ChapterMode.BaseCampSequence
            || mode == ChapterMode.BaseCampUpgradeSequence;

        private int RequiredBaseCampLevel => mode == ChapterMode.BaseCampUpgradeSequence ? 2 : 1;
    }
}
