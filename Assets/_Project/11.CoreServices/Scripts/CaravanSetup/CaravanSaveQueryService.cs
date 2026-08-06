using System;

namespace ND.Framework
{
    public sealed class CaravanSaveQueryResult
    {
        public CaravanSaveData Caravan { get; }
        public int SaveIndex { get; }
        public string CaravanId { get; }
        public string DisplayName { get; }
        public string CurrentTownId { get; }
        public JourneyState State => Caravan.state;

        internal CaravanSaveQueryResult(CaravanSaveData caravan, int saveIndex)
        {
            Caravan = caravan;
            SaveIndex = saveIndex;
            CaravanId = NormalizeId(caravan.caravanId);
            string savedDisplayName = caravan.displayName?.Trim() ?? string.Empty;
            DisplayName = string.IsNullOrEmpty(savedDisplayName)
                ? $"Caravan {caravan.slotIndex + 1}"
                : savedDisplayName;
            CurrentTownId = NormalizeId(caravan.currentTownId);
        }

        private static string NormalizeId(string value) => value?.Trim() ?? string.Empty;
    }

    public sealed class CaravanSaveQueryService
    {
        public bool TryGet(SaveData saveData, string caravanId, out CaravanSaveQueryResult result)
        {
            result = null;
            string normalizedCaravanId = NormalizeId(caravanId);
            if (string.IsNullOrEmpty(normalizedCaravanId) || saveData?.caravans == null)
                return false;

            for (int index = 0; index < saveData.caravans.Count; index++)
            {
                CaravanSaveData caravan = saveData.caravans[index];
                if (caravan == null
                    || !string.Equals(NormalizeId(caravan.caravanId), normalizedCaravanId, StringComparison.Ordinal))
                    continue;

                result = new CaravanSaveQueryResult(caravan, index);
                return true;
            }

            return false;
        }

        private static string NormalizeId(string value) => value?.Trim() ?? string.Empty;
    }
}
