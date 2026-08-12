using System;
using System.Collections.Generic;
using ND.Framework;
using FrameworkSaveData = ND.Framework.SaveData;
using FrameworkSaveResult = ND.Framework.SaveResult;
using FrameworkTutorialChapterProgress = ND.Framework.TutorialChapterProgressSaveData;
using FrameworkTutorialSaveData = ND.Framework.TutorialSaveData;

namespace ND.UI.Tutorial
{
    /// <summary>
    /// Persists tutorial identity and progress without coupling SaveData to presentation assets.
    /// </summary>
    public static class TutorialProgressStore
    {
        public const string TravelWorldMapFeatureId = "travel.world_map";
        public const string TravelTreadmillFeatureId = "travel.treadmill";

        public static bool IsFlowCompleted()
        {
            FrameworkTutorialSaveData tutorial = GetTutorial();
            return tutorial != null && (tutorial.isCompleted || tutorial.isSkipped);
        }

        public static bool IsChapterCompleted(string rawChapterId)
        {
            string chapterId = Normalize(rawChapterId);
            FrameworkTutorialSaveData tutorial = GetTutorial();
            return tutorial != null
                && !string.IsNullOrEmpty(chapterId)
                && tutorial.completedChapterIds.Contains(chapterId);
        }

        public static IReadOnlyList<string> GetCompletedStepIds(
            string rawChapterId,
            int presentationVersion)
        {
            string chapterId = Normalize(rawChapterId);
            FrameworkTutorialSaveData tutorial = GetTutorial();
            if (tutorial == null || string.IsNullOrEmpty(chapterId))
                return Array.Empty<string>();

            FrameworkTutorialChapterProgress progress = FindProgress(tutorial, chapterId);
            if (progress == null || progress.presentationVersion != Math.Max(0, presentationVersion))
                return Array.Empty<string>();

            return progress.completedStepIds != null
                ? progress.completedStepIds.ToArray()
                : Array.Empty<string>();
        }

        public static void SaveChapterProgress(
            string rawChapterId,
            int presentationVersion,
            IReadOnlyList<string> completedStepIds,
            int stepCount)
        {
            string chapterId = Normalize(rawChapterId);
            FrameworkTutorialSaveData tutorial = GetTutorial();
            if (tutorial == null || string.IsNullOrEmpty(chapterId)
                || IsChapterCompleted(chapterId))
                return;

            FrameworkTutorialChapterProgress progress = FindProgress(tutorial, chapterId);
            int restorableCount = Math.Min(
                completedStepIds?.Count ?? 0,
                Math.Max(0, stepCount - 1));

            if (restorableCount == 0)
            {
                if (progress != null)
                {
                    tutorial.chapterProgress.Remove(progress);
                    SaveCurrentData("clear chapter progress");
                }
                return;
            }

            if (progress == null)
            {
                progress = new FrameworkTutorialChapterProgress { chapterId = chapterId };
                tutorial.chapterProgress.Add(progress);
            }

            progress.presentationVersion = Math.Max(0, presentationVersion);
            progress.completedStepIds.Clear();
            for (int index = 0; index < restorableCount; index++)
            {
                string stepId = Normalize(completedStepIds[index]);
                if (!string.IsNullOrEmpty(stepId))
                    progress.completedStepIds.Add(stepId);
            }
            SaveCurrentData("save chapter progress");
        }

        public static void MarkChapterCompleted(string rawChapterId)
        {
            string chapterId = Normalize(rawChapterId);
            FrameworkTutorialSaveData tutorial = GetTutorial();
            if (tutorial == null || string.IsNullOrEmpty(chapterId)) return;

            bool changed = false;
            if (!tutorial.completedChapterIds.Contains(chapterId))
            {
                tutorial.completedChapterIds.Add(chapterId);
                changed = true;
            }

            FrameworkTutorialChapterProgress progress = FindProgress(tutorial, chapterId);
            if (progress != null)
            {
                tutorial.chapterProgress.Remove(progress);
                changed = true;
            }

            if (changed) SaveCurrentData("complete chapter");
        }

        public static void MarkFlowCompleted()
        {
            FrameworkTutorialSaveData tutorial = GetTutorial();
            if (tutorial == null || tutorial.isCompleted) return;
            tutorial.isCompleted = true;
            tutorial.chapterProgress.Clear();
            SaveCurrentData("complete tutorial flow");
        }

        public static bool IsFeatureLearned(string rawFeatureId)
        {
            string featureId = Normalize(rawFeatureId);
            FrameworkTutorialSaveData tutorial = GetTutorial();
            return tutorial != null
                && !string.IsNullOrEmpty(featureId)
                && tutorial.learnedFeatureIds.Contains(featureId);
        }

        public static void MarkFeatureLearned(string rawFeatureId)
        {
            string featureId = Normalize(rawFeatureId);
            FrameworkTutorialSaveData tutorial = GetTutorial();
            if (tutorial == null || string.IsNullOrEmpty(featureId)
                || tutorial.learnedFeatureIds.Contains(featureId))
                return;

            tutorial.learnedFeatureIds.Add(featureId);
            SaveCurrentData("learn tutorial feature");
        }

        private static FrameworkTutorialSaveData GetTutorial()
        {
            FrameworkSaveData data = FrameworkRoot.Instance?.CurrentSaveData;
            if (data == null) return null;
            if (data.tutorial == null) data.tutorial = new FrameworkTutorialSaveData();
            if (data.tutorial.completedChapterIds == null)
                data.tutorial.completedChapterIds = new List<string>();
            if (data.tutorial.learnedFeatureIds == null)
                data.tutorial.learnedFeatureIds = new List<string>();
            if (data.tutorial.chapterProgress == null)
                data.tutorial.chapterProgress = new List<FrameworkTutorialChapterProgress>();
            return data.tutorial;
        }

        private static FrameworkTutorialChapterProgress FindProgress(
            FrameworkTutorialSaveData tutorial,
            string chapterId)
        {
            for (int index = 0; index < tutorial.chapterProgress.Count; index++)
            {
                FrameworkTutorialChapterProgress progress = tutorial.chapterProgress[index];
                if (progress != null && string.Equals(
                        Normalize(progress.chapterId), chapterId, StringComparison.Ordinal))
                    return progress;
            }
            return null;
        }

        private static void SaveCurrentData(string operation)
        {
            FrameworkRoot root = FrameworkRoot.Instance;
            if (root?.CurrentSaveData == null || root.SaveService == null) return;

            FrameworkSaveResult result = root.SaveService.Save(root.CurrentSaveData);
            if (!result.Succeeded)
                FrameworkLog.Warning($"Tutorial progress could not {operation}: {result.Message}");
        }

        private static string Normalize(string value) => value?.Trim() ?? string.Empty;
    }
}
