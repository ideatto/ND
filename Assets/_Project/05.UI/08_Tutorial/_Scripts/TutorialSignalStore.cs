using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.UI.Tutorial
{
    public sealed class TutorialSignalStore : MonoBehaviour
    {
        public readonly struct Signal
        {
            public Signal(long sequence, string actionId, string contextId, string sourceId)
            {
                Sequence = sequence;
                ActionId = actionId ?? string.Empty;
                ContextId = contextId ?? string.Empty;
                SourceId = sourceId ?? string.Empty;
            }

            public long Sequence { get; }
            public string ActionId { get; }
            public string ContextId { get; }
            public string SourceId { get; }
        }

        [SerializeField, Min(16)] private int capacity = 128;

        private readonly List<Signal> signals = new List<Signal>();
        private long nextSequence = 1;

        public IReadOnlyList<Signal> Signals => signals;
        public event Action<Signal> SignalRecorded;

        public Signal Record(string actionId, string contextId = "", string sourceId = "")
        {
            if (string.IsNullOrWhiteSpace(actionId))
                throw new ArgumentException("Tutorial action ID is required.", nameof(actionId));

            var signal = new Signal(nextSequence++, actionId.Trim(), contextId, sourceId);
            signals.Add(signal);
            while (signals.Count > Mathf.Max(16, capacity))
                signals.RemoveAt(0);

            SignalRecorded?.Invoke(signal);
            return signal;
        }
    }
}
