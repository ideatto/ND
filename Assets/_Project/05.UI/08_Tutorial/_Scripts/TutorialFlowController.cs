using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ND.UI.Tutorial
{
    /// <summary>
    /// Starts available tutorial chapters in serialized order. Disabled or unassigned entries
    /// are reserved sequence slots and are skipped until their feature becomes available.
    /// </summary>
    public sealed class TutorialFlowController : MonoBehaviour
    {
        [Serializable]
        public sealed class ChapterEntry
        {
            [SerializeField] private string chapterId = string.Empty;
            [SerializeField] private bool available = true;
            [SerializeField] private TutorialChapterBehaviour chapter;

            public string ChapterId => chapterId?.Trim() ?? string.Empty;
            public bool Available => available;
            public TutorialChapterBehaviour Chapter => chapter;
        }

        [SerializeField] private bool beginOnStart = true;
        [SerializeField] private TutorialPresentationCoordinator presentationCoordinator;
        [SerializeField] private List<ChapterEntry> chapters = new List<ChapterEntry>();

        private int currentIndex = -1;
        private TutorialChapterBehaviour currentChapter;
        private Coroutine transitionRoutine;
        private bool hasBegun;
        private bool isCompleted;
        private bool transitionPending;

        public bool HasBegun => hasBegun;
        public bool IsCompleted => isCompleted;
        public string CurrentChapterId =>
            currentIndex >= 0 && currentIndex < chapters.Count
                ? chapters[currentIndex]?.ChapterId ?? string.Empty
                : string.Empty;

        public event Action<string> ChapterStarted;
        public event Action<string> ChapterCompleted;
        public event Action FlowCompleted;

        private void Start()
        {
            if (beginOnStart)
                BeginFlow();
        }

        private void OnEnable()
        {
            if (transitionPending)
            {
                ScheduleNextChapter();
                return;
            }

            if (!hasBegun
                || isCompleted
                || currentIndex < 0
                || currentIndex >= chapters.Count)
            {
                return;
            }

            ChapterEntry entry = chapters[currentIndex];
            if (entry == null || !entry.Available || entry.Chapter == null)
                return;

            currentChapter = entry.Chapter;
            currentChapter.Completed -= HandleChapterCompleted;
            currentChapter.Completed += HandleChapterCompleted;
        }

        private void OnDisable()
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            UnsubscribeCurrent();
        }

        public void BeginFlow()
        {
            if (hasBegun || isCompleted)
                return;

            HideTutorialLogs();

            if (TutorialProgressStore.IsFlowCompleted())
            {
                hasBegun = true;
                isCompleted = true;
                currentIndex = chapters.Count;
                presentationCoordinator?.Release(this);
                FlowCompleted?.Invoke();
                return;
            }

            if (presentationCoordinator != null
                && !presentationCoordinator.TryAcquire(this))
                return;

            hasBegun = true;
            AdvanceToNextChapter();
        }

        private static void HideTutorialLogs()
        {
            TutorialLogView[] logViews = UnityEngine.Object.FindObjectsByType<TutorialLogView>(
                FindObjectsInactive.Include);
            for (int index = 0; index < logViews.Length; index++)
            {
                if (logViews[index] != null)
                    logViews[index].gameObject.SetActive(false);
            }
        }

        private void AdvanceToNextChapter()
        {
            transitionPending = false;
            UnsubscribeCurrent();

            for (int index = currentIndex + 1; index < chapters.Count; index++)
            {
                ChapterEntry entry = chapters[index];
                if (entry == null || !entry.Available || entry.Chapter == null)
                    continue;

                currentIndex = index;
                currentChapter = entry.Chapter;
                if (TutorialProgressStore.IsChapterCompleted(entry.ChapterId)
                    || currentChapter.IsCompleted)
                {
                    ChapterCompleted?.Invoke(entry.ChapterId);
                    continue;
                }

                currentChapter.RestorePersistedProgress(entry.ChapterId);
                currentChapter.Completed += HandleChapterCompleted;
                ChapterStarted?.Invoke(entry.ChapterId);
                currentChapter.BeginChapter();
                return;
            }

            isCompleted = true;
            currentIndex = chapters.Count;
            TutorialProgressStore.MarkFlowCompleted();
            presentationCoordinator?.Release(this);
            FlowCompleted?.Invoke();
        }

        private void HandleChapterCompleted(TutorialChapterBehaviour chapter)
        {
            if (transitionPending || chapter == null || chapter != currentChapter)
                return;

            string completedId = CurrentChapterId;
            transitionPending = true;
            UnsubscribeCurrent();
            TutorialProgressStore.MarkChapterCompleted(completedId);
            ChapterCompleted?.Invoke(completedId);
            ScheduleNextChapter();
        }

        private void ScheduleNextChapter()
        {
            if (!isActiveAndEnabled || transitionRoutine != null)
                return;

            transitionRoutine = StartCoroutine(AdvanceOnNextFrame());
        }

        private IEnumerator AdvanceOnNextFrame()
        {
            yield return null;
            transitionRoutine = null;

            if (transitionPending && !isCompleted)
                AdvanceToNextChapter();
        }

        private void UnsubscribeCurrent()
        {
            if (currentChapter != null)
                currentChapter.Completed -= HandleChapterCompleted;

            currentChapter = null;
        }
    }
}
