using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.UI.Tutorial
{
    /// <summary>Common runtime boundary for one ordered tutorial chapter.</summary>
    public abstract class TutorialChapterBehaviour : MonoBehaviour
    {
        private TutorialSequenceRunner progressRunner;
        private TutorialChapterData progressChapterData;
        private string progressChapterId = string.Empty;
        private bool isRestoringProgress;

        public abstract bool HasStarted { get; }
        public abstract bool IsCompleted { get; }

        public event Action<TutorialChapterBehaviour> Completed;

        public abstract void BeginChapter();

        protected void BindProgress(
            TutorialSequenceRunner runner,
            TutorialChapterData chapterData)
        {
            if (progressRunner != null)
                progressRunner.StateChanged -= PersistProgress;

            progressRunner = runner;
            progressChapterData = chapterData;
            progressChapterId = chapterData?.ChapterId?.Trim() ?? string.Empty;
            if (progressRunner != null)
                progressRunner.StateChanged += PersistProgress;
        }

        internal void RestorePersistedProgress(string flowChapterId)
        {
            string normalizedFlowId = flowChapterId?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(normalizedFlowId))
                progressChapterId = normalizedFlowId;
            if (string.IsNullOrEmpty(progressChapterId)) return;

            IReadOnlyList<string> completedStepIds =
                TutorialProgressStore.GetCompletedStepIds(
                    progressChapterId,
                    progressChapterData?.PresentationVersion ?? 0);
            if (progressRunner != null)
            {
                isRestoringProgress = true;
                progressRunner.RestoreCompletedActionIds(completedStepIds);
                isRestoringProgress = false;
            }
            RestoreCustomProgress(completedStepIds);
        }

        protected virtual void RestoreCustomProgress(IReadOnlyList<string> completedStepIds)
        {
        }

        protected void PersistCustomProgress(IReadOnlyList<string> completedStepIds, int stepCount)
        {
            if (!HasStarted || string.IsNullOrEmpty(progressChapterId)) return;
            TutorialProgressStore.SaveChapterProgress(
                progressChapterId,
                progressChapterData?.PresentationVersion ?? 0,
                completedStepIds,
                stepCount);
        }

        private void PersistProgress()
        {
            if (isRestoringProgress || !HasStarted || progressRunner == null
                || string.IsNullOrEmpty(progressChapterId))
                return;

            TutorialProgressStore.SaveChapterProgress(
                progressChapterId,
                progressChapterData?.PresentationVersion ?? 0,
                progressRunner.GetCompletedActionIds(),
                progressRunner.StepCount);
        }

        protected void RaiseCompleted()
        {
            Completed?.Invoke(this);
        }
    }
}
