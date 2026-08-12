using System;
using System.Collections.Generic;

namespace ND.UI.Tutorial
{
    /// <summary>
    /// Matches a fixed, ordered set of tutorial actions. Registered actions received out of
    /// order reset the sequence; unrelated actions are ignored.
    /// </summary>
    public sealed class TutorialSequenceRunner
    {
        private readonly string[] actionIds;
        private readonly HashSet<string> registeredActions;

        public TutorialSequenceRunner(IEnumerable<string> orderedActionIds)
        {
            if (orderedActionIds == null)
                throw new ArgumentNullException(nameof(orderedActionIds));

            var actions = new List<string>();
            registeredActions = new HashSet<string>(StringComparer.Ordinal);
            foreach (string rawActionId in orderedActionIds)
            {
                string actionId = Normalize(rawActionId);
                if (string.IsNullOrEmpty(actionId))
                    throw new ArgumentException("Tutorial action IDs cannot be empty.", nameof(orderedActionIds));
                if (!registeredActions.Add(actionId))
                    throw new ArgumentException($"Duplicate tutorial action ID: {actionId}", nameof(orderedActionIds));
                actions.Add(actionId);
            }

            if (actions.Count == 0)
                throw new ArgumentException("A tutorial sequence requires at least one action.", nameof(orderedActionIds));

            actionIds = actions.ToArray();
        }

        public int StepCount => actionIds.Length;
        public int CurrentStepIndex { get; private set; }
        public bool IsCompleted => CurrentStepIndex >= actionIds.Length;

        public IReadOnlyList<string> GetCompletedActionIds()
        {
            int count = Math.Min(CurrentStepIndex, actionIds.Length);
            var completed = new string[count];
            Array.Copy(actionIds, completed, count);
            return completed;
        }

        public event Action StateChanged;
        public event Action Completed;

        public bool Handle(string rawActionId)
        {
            if (IsCompleted)
                return false;

            string actionId = Normalize(rawActionId);
            if (!registeredActions.Contains(actionId))
                return false;

            if (!string.Equals(actionIds[CurrentStepIndex], actionId, StringComparison.Ordinal))
            {
                Reset();
                return false;
            }

            CurrentStepIndex++;
            StateChanged?.Invoke();
            if (IsCompleted)
                Completed?.Invoke();
            return true;
        }

        public void Reset()
        {
            if (CurrentStepIndex == 0)
                return;

            CurrentStepIndex = 0;
            StateChanged?.Invoke();
        }

        public void RestoreCompletedActionIds(IReadOnlyList<string> completedActionIds)
        {
            int restoredCount = 0;
            int maximum = Math.Max(0, actionIds.Length - 1);
            int requestedCount = Math.Min(completedActionIds?.Count ?? 0, maximum);
            while (restoredCount < requestedCount
                && string.Equals(
                    actionIds[restoredCount],
                    Normalize(completedActionIds[restoredCount]),
                    StringComparison.Ordinal))
            {
                restoredCount++;
            }

            if (CurrentStepIndex == restoredCount) return;
            CurrentStepIndex = restoredCount;
            StateChanged?.Invoke();
        }

        private static string Normalize(string value) => value?.Trim() ?? string.Empty;
    }
}
