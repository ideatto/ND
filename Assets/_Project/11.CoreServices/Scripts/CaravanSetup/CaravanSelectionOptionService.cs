using System;
using System.Collections.Generic;

namespace ND.Framework
{
    /// <summary>
    /// Builds the departure-selection snapshot from the authoritative Caravan save collection.
    /// </summary>
    public sealed class CaravanSelectionOptionService
    {
        public TradePrepareCaravanOptionViewData[] CreateOptions(SaveData saveData)
        {
            if (saveData?.caravans == null || saveData.caravans.Count == 0)
                return Array.Empty<TradePrepareCaravanOptionViewData>();

            var options = new List<TradePrepareCaravanOptionViewData>(saveData.caravans.Count);
            for (int index = 0; index < saveData.caravans.Count; index++)
            {
                CaravanSaveData caravan = saveData.caravans[index];
                string caravanId = NormalizeId(caravan?.caravanId);
                if (string.IsNullOrEmpty(caravanId))
                    continue;

                string currentTownId = NormalizeId(caravan.currentTownId);
                bool hasCurrentTown = !string.IsNullOrEmpty(currentTownId);
                bool canSelect = caravan.state == JourneyState.Prepare && hasCurrentTown;
                options.Add(new TradePrepareCaravanOptionViewData
                {
                    caravanId = caravanId,
                    displayName = $"Caravan {index + 1}",
                    currentTownId = currentTownId,
                    state = caravan.state,
                    canSelect = canSelect,
                    disabledReason = canSelect
                        ? string.Empty
                        : !hasCurrentTown
                            ? "This Caravan has no valid departure town."
                            : "This Caravan is already traveling or awaiting settlement."
                });
            }

            return options.ToArray();
        }

        private static string NormalizeId(string value) => value?.Trim() ?? string.Empty;
    }
}
