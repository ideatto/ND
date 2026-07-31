using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Framework
{
    [CreateAssetMenu(
        fileName = "QuestGenerationSettings",
        menuName = "ND/Quest/Generation Settings")]
    public sealed class QuestGenerationSettings : ScriptableObject
    {
        public const string ResourceName = "QuestGenerationSettings";

        [Serializable]
        private sealed class TownCapacity
        {
            public string townId = string.Empty;
            [Min(0)] public int maxActiveQuests = 1;
        }

        [SerializeField, Min(0)] private int defaultMaxActiveQuestsPerTown = 1;
        [SerializeField] private List<TownCapacity> townOverrides =
            new List<TownCapacity>();

        public int GetMaxActiveQuests(string townId)
        {
            for (int i = 0; i < townOverrides.Count; i++)
            {
                TownCapacity entry = townOverrides[i];
                if (entry != null &&
                    string.Equals(entry.townId, townId, StringComparison.Ordinal))
                    return Math.Max(0, entry.maxActiveQuests);
            }
            return Math.Max(0, defaultMaxActiveQuestsPerTown);
        }
    }
}
