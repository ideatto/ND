using System;
using System.Collections.Generic;

namespace ND.Framework
{
    public sealed class OwnedWagonInstance
    {
        public string InstanceId { get; }
        public string ContentId { get; }
        public float MaxLoad { get; }
        public int InventorySlotCount { get; }
        public int MinAnimals { get; }
        public int MaxAnimals { get; }

        public OwnedWagonInstance(
            string instanceId,
            string contentId,
            float maxLoad,
            int inventorySlotCount,
            int minAnimals,
            int maxAnimals)
        {
            InstanceId = NormalizeId(instanceId);
            ContentId = NormalizeId(contentId);
            MaxLoad = Math.Max(0f, maxLoad);
            InventorySlotCount = Math.Max(0, inventorySlotCount);
            MinAnimals = Math.Max(0, minAnimals);
            MaxAnimals = Math.Max(MinAnimals, maxAnimals);
        }

        private static string NormalizeId(string value) => value?.Trim() ?? string.Empty;
    }

    public sealed class OwnedDraftAnimalInstance
    {
        public string InstanceId { get; }
        public string ContentId { get; }

        public OwnedDraftAnimalInstance(string instanceId, string contentId)
        {
            InstanceId = NormalizeId(instanceId);
            ContentId = NormalizeId(contentId);
        }

        private static string NormalizeId(string value) => value?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Owns the runtime inventory boundary for transport instances independently from UI state.
    /// A SaveData-backed implementation can replace the fixture registration without changing S3.
    /// </summary>
    public sealed class OwnedTransportInventoryService
    {
        private readonly Dictionary<string, OwnedWagonInstance> wagons =
            new Dictionary<string, OwnedWagonInstance>(StringComparer.Ordinal);
        private readonly Dictionary<string, OwnedDraftAnimalInstance> animals =
            new Dictionary<string, OwnedDraftAnimalInstance>(StringComparer.Ordinal);

        public bool RegisterWagon(OwnedWagonInstance wagon)
        {
            if (wagon == null || string.IsNullOrEmpty(wagon.InstanceId))
                return false;

            wagons[wagon.InstanceId] = wagon;
            return true;
        }

        public bool RegisterAnimal(OwnedDraftAnimalInstance animal)
        {
            if (animal == null || string.IsNullOrEmpty(animal.InstanceId))
                return false;

            animals[animal.InstanceId] = animal;
            return true;
        }

        public bool TryGetWagon(string instanceId, out OwnedWagonInstance wagon)
        {
            return wagons.TryGetValue(NormalizeId(instanceId), out wagon);
        }

        public bool TryGetAnimal(string instanceId, out OwnedDraftAnimalInstance animal)
        {
            return animals.TryGetValue(NormalizeId(instanceId), out animal);
        }


        public bool OwnsAnimal(string instanceId)
        {
            return animals.ContainsKey(NormalizeId(instanceId));
        }

        private static string NormalizeId(string value) => value?.Trim() ?? string.Empty;
    }

    public enum CaravanCompositionDraftFailure
    {
        None,
        InvalidCaravan,
        WagonNotOwned,
        AnimalNotOwned,
        DuplicateAnimal,
        AssetAlreadyInUse,
        InvalidComposition
    }

    public sealed class CaravanCompositionSnapshot
    {
        public string CaravanId { get; }
        public string WagonInstanceId { get; }
        public IReadOnlyList<string> AnimalInstanceIds { get; }

        internal CaravanCompositionSnapshot(
            string caravanId,
            string wagonInstanceId,
            IReadOnlyList<string> animalInstanceIds)
        {
            CaravanId = caravanId;
            WagonInstanceId = wagonInstanceId;
            AnimalInstanceIds = animalInstanceIds;
        }
    }

    /// <summary>
    /// Keeps uncommitted S3 composition isolated by persistent caravanId and prevents a transport
    /// instance from being selected by two Caravan drafts at the same time.
    /// </summary>
    public sealed class CaravanCompositionDraftService
    {
        private sealed class DraftState
        {
            public string wagonInstanceId = string.Empty;
            public readonly List<string> animalInstanceIds = new List<string>();
        }

        private readonly OwnedTransportInventoryService inventory;
        private readonly Dictionary<string, DraftState> drafts =
            new Dictionary<string, DraftState>(StringComparer.Ordinal);

        public CaravanCompositionDraftService(OwnedTransportInventoryService inventory)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        public CaravanCompositionDraftFailure TrySet(
            string caravanId,
            string wagonInstanceId,
            IReadOnlyList<string> animalInstanceIds)
        {
            CaravanCompositionDraftFailure failure = Validate(
                caravanId,
                wagonInstanceId,
                animalInstanceIds);
            if (failure != CaravanCompositionDraftFailure.None)
                return failure;

            string normalizedCaravanId = NormalizeId(caravanId);
            string normalizedWagonId = NormalizeId(wagonInstanceId);
            var normalizedAnimalIds = new List<string>();
            if (animalInstanceIds != null)
            {
                for (int index = 0; index < animalInstanceIds.Count; index++)
                    normalizedAnimalIds.Add(NormalizeId(animalInstanceIds[index]));
            }

            if (!drafts.TryGetValue(normalizedCaravanId, out DraftState state))
            {
                state = new DraftState();
                drafts.Add(normalizedCaravanId, state);
            }

            state.wagonInstanceId = normalizedWagonId;
            state.animalInstanceIds.Clear();
            state.animalInstanceIds.AddRange(normalizedAnimalIds);
            return CaravanCompositionDraftFailure.None;
        }

        public CaravanCompositionDraftFailure Validate(
            string caravanId,
            string wagonInstanceId,
            IReadOnlyList<string> animalInstanceIds)
        {
            string normalizedCaravanId = NormalizeId(caravanId);
            string normalizedWagonId = NormalizeId(wagonInstanceId);
            if (string.IsNullOrEmpty(normalizedCaravanId))
                return CaravanCompositionDraftFailure.InvalidCaravan;

            OwnedWagonInstance wagon = null;
            if (!string.IsNullOrEmpty(normalizedWagonId)
                && !inventory.TryGetWagon(normalizedWagonId, out wagon))
            {
                return CaravanCompositionDraftFailure.WagonNotOwned;
            }

            var normalizedAnimalIds = new List<string>();
            var uniqueAnimalIds = new HashSet<string>(StringComparer.Ordinal);
            if (animalInstanceIds != null)
            {
                for (int index = 0; index < animalInstanceIds.Count; index++)
                {
                    string animalId = NormalizeId(animalInstanceIds[index]);
                    if (!inventory.OwnsAnimal(animalId))
                        return CaravanCompositionDraftFailure.AnimalNotOwned;
                    if (!uniqueAnimalIds.Add(animalId))
                        return CaravanCompositionDraftFailure.DuplicateAnimal;
                    normalizedAnimalIds.Add(animalId);
                }
            }

            if (string.IsNullOrEmpty(normalizedWagonId))
            {
                if (normalizedAnimalIds.Count > 0)
                    return CaravanCompositionDraftFailure.InvalidComposition;
            }
            else if (normalizedAnimalIds.Count < wagon.MinAnimals
                || normalizedAnimalIds.Count > wagon.MaxAnimals)
            {
                return CaravanCompositionDraftFailure.InvalidComposition;
            }

            if (IsUsedByAnotherDraft(normalizedCaravanId, normalizedWagonId, normalizedAnimalIds))
                return CaravanCompositionDraftFailure.AssetAlreadyInUse;
            return CaravanCompositionDraftFailure.None;
        }

        public CaravanCompositionSnapshot GetOrCreate(
            string caravanId,
            string defaultWagonInstanceId,
            IReadOnlyList<string> defaultAnimalInstanceIds)
        {
            string normalizedCaravanId = NormalizeId(caravanId);
            if (!drafts.ContainsKey(normalizedCaravanId))
            {
                CaravanCompositionDraftFailure failure = TrySet(
                    normalizedCaravanId,
                    defaultWagonInstanceId,
                    defaultAnimalInstanceIds);
                if (failure != CaravanCompositionDraftFailure.None)
                    drafts[normalizedCaravanId] = new DraftState();
            }

            DraftState state = drafts[normalizedCaravanId];
            return new CaravanCompositionSnapshot(
                normalizedCaravanId,
                state.wagonInstanceId,
                state.animalInstanceIds.ToArray());
        }

        public void Clear(string caravanId)
        {
            string normalizedCaravanId = NormalizeId(caravanId);
            if (!string.IsNullOrEmpty(normalizedCaravanId))
                drafts.Remove(normalizedCaravanId);
        }


        private bool IsUsedByAnotherDraft(
            string caravanId,
            string wagonInstanceId,
            IReadOnlyList<string> animalInstanceIds)
        {
            foreach (KeyValuePair<string, DraftState> pair in drafts)
            {
                if (string.Equals(pair.Key, caravanId, StringComparison.Ordinal))
                    continue;

                if (!string.IsNullOrEmpty(wagonInstanceId)
                    && string.Equals(pair.Value.wagonInstanceId, wagonInstanceId, StringComparison.Ordinal))
                    return true;

                for (int index = 0; index < animalInstanceIds.Count; index++)
                {
                    if (pair.Value.animalInstanceIds.Contains(animalInstanceIds[index]))
                        return true;
                }
            }

            return false;
        }

        private static string NormalizeId(string value) => value?.Trim() ?? string.Empty;
    }
}
