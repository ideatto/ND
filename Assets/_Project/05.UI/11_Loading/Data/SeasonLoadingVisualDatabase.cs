using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.UI.Loading
{
    [CreateAssetMenu(fileName = "SeasonLoadingVisualDatabase", menuName = "ND/Loading/Season Visual Database")]
    public sealed class SeasonLoadingVisualDatabase : ScriptableObject
    {
        [SerializeField] private Sprite defaultBackground;
        [SerializeField] private List<SeasonLoadingVisualEntry> entries = new List<SeasonLoadingVisualEntry>();

        public Sprite DefaultBackground => defaultBackground;

        /// <summary>Returns the first matching non-null visual, or the shared default.</summary>
        public Sprite Resolve(string seasonId)
        {
            if (string.IsNullOrEmpty(seasonId))
            {
                return defaultBackground;
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry != null
                    && string.Equals(entry.SeasonId, seasonId, StringComparison.Ordinal)
                    && entry.BackgroundSprite != null)
                {
                    return entry.BackgroundSprite;
                }
            }

            return defaultBackground;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry != null && !string.IsNullOrEmpty(entry.SeasonId) && !seenIds.Add(entry.SeasonId))
                {
                    Debug.LogWarning($"Duplicate loading season ID uses the first valid sprite: {entry.SeasonId}", this);
                }
            }
        }
#endif
    }
}
