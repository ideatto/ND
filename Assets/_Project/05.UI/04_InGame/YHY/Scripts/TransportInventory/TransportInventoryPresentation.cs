using System;
using System.Collections.Generic;
using ND.Framework;
using UnityEngine;

namespace ND.UI.InGame.TransportInventory
{
    public enum TransportInventoryTab
    {
        Wagon,
        Animal
    }

    /// <summary>Render-only data for one fixed physical slot.</summary>
    public sealed class TransportInventorySlotViewData
    {
        public int SlotIndex;
        public TransportInventoryTab Tab;
        public bool IsUnlocked;
        public bool IsOccupied;
        public bool IsOverflow;
        public string InstanceId = string.Empty;
        public string ContentId = string.Empty;
        public string DisplayName = string.Empty;
        public string Description = string.Empty;
        public Sprite Icon;
        public long BaseBuyPrice;
        public bool HasDurability;
        public int CurrentDurability;
        public int MaxDurability;
        public int DurabilityPercent;
        public bool IsCatalogMissing;
        public bool IsAssigned;
        public string AssignedCaravanId = string.Empty;
        public string AssignedCaravanName = string.Empty;
        public string AssignmentText => IsAssigned
            ? $"{AssignedCaravanName} 장착 중"
            : string.Empty;
    }

    /// <summary>One tab's capacity, lock boundary, and complete slot list.</summary>
    public sealed class TransportInventoryPanelViewData
    {
        public TransportInventoryTab Tab;
        public string Title = string.Empty;
        public int UsedSlots;
        public int AvailableSlots;
        public int AbsoluteMaximumSlots;
        public int OverflowCount;
        public bool HasOverflow => OverflowCount > 0;
        public int LockedStartIndex;
        public int NextRequiredFarmLevel;
        public IReadOnlyList<TransportInventorySlotViewData> Slots = Array.Empty<TransportInventorySlotViewData>();
        public string CapacityText => $"{UsedSlots:00} / {AvailableSlots:00}";
    }

    /// <summary>Complete popup snapshot. It owns no mutable SaveData reference.</summary>
    public sealed class TransportInventoryPopupViewData
    {
        public string BuildingName = TransportInventoryFunction.BuildingDisplayName;
        public int FarmLevel;
        public bool CanOpen;
        public TransportInventoryPanelViewData Wagon;
        public TransportInventoryPanelViewData Animal;
    }

    public static class TransportInventoryViewDataBuilder
    {
        public static TransportInventoryPopupViewData Build(
            ND.Framework.PlayerSaveData player,
            ISharedGameDataProvider catalog)
        {
            return Build(new ND.Framework.SaveData { player = player ?? new ND.Framework.PlayerSaveData() }, catalog);
        }

        public static TransportInventoryPopupViewData Build(
            ND.Framework.SaveData save,
            ISharedGameDataProvider catalog)
        {
            save ??= new ND.Framework.SaveData();
            save.player ??= new ND.Framework.PlayerSaveData();
            TransportInventoryState state = TransportInventoryFunction.Evaluate(save);
            Dictionary<string, Assignment> assignments = BuildAssignments(save.caravans);
            return new TransportInventoryPopupViewData
            {
                FarmLevel = state.Level,
                CanOpen = state.CanOpen,
                Wagon = BuildWagons(save.player.wagonInventory, state, catalog, assignments),
                Animal = BuildAnimals(save.player.draftAnimalInventory, state, catalog, assignments)
            };
        }

        private static TransportInventoryPanelViewData BuildWagons(
            IReadOnlyList<OwnedWagonSaveData> source,
            TransportInventoryState state,
            ISharedGameDataProvider catalog,
            IReadOnlyDictionary<string, Assignment> assignments)
        {
            var items = ValidWagons(source);
            var slots = CreateEmptySlots(
                TransportInventoryTab.Wagon,
                Math.Max(TransportInventoryFunction.MaximumLevel * TransportInventoryFunction.WagonSlotsPerLevel, items.Count),
                state.WagonSlotCount);

            for (int index = 0; index < items.Count; index++)
            {
                OwnedWagonSaveData owned = items[index];
                SharedWagonDefinition definition = null;
                bool found = catalog != null && catalog.TryGetWagon(owned.contentId, out definition) && definition != null;
                int maximum = Math.Max(0, definition?.MaxDurability ?? 0);
                int current = Math.Max(0, Math.Min(owned.currentDurability, maximum));
                TransportInventorySlotViewData slot = slots[index];
                slot.IsOccupied = true;
                slot.IsOverflow = index >= state.WagonSlotCount;
                slot.InstanceId = owned.instanceId.Trim();
                slot.ContentId = owned.contentId.Trim();
                slot.DisplayName = found ? definition.DisplayName ?? owned.contentId : "알 수 없는 마차";
                slot.Description = found ? definition.Description ?? string.Empty : "카탈로그에서 마차 정보를 찾을 수 없습니다.";
                slot.Icon = definition?.Icon;
                slot.BaseBuyPrice = Math.Max(0L, definition?.BaseBuyPrice ?? 0L);
                slot.HasDurability = true;
                slot.CurrentDurability = current;
                slot.MaxDurability = maximum;
                slot.DurabilityPercent = maximum > 0 ? Mathf.RoundToInt(current * 100f / maximum) : 0;
                slot.IsCatalogMissing = !found;
                ApplyAssignment(slot, assignments);
            }

            return CreatePanel(TransportInventoryTab.Wagon, "마차 보관함", items.Count,
                state.WagonSlotCount, TransportInventoryFunction.WagonSlotsPerLevel, slots);
        }

        private static TransportInventoryPanelViewData BuildAnimals(
            IReadOnlyList<OwnedDraftAnimalSaveData> source,
            TransportInventoryState state,
            ISharedGameDataProvider catalog,
            IReadOnlyDictionary<string, Assignment> assignments)
        {
            var items = ValidAnimals(source);
            var slots = CreateEmptySlots(
                TransportInventoryTab.Animal,
                Math.Max(TransportInventoryFunction.MaximumLevel * TransportInventoryFunction.DraftAnimalSlotsPerLevel, items.Count),
                state.DraftAnimalSlotCount);

            for (int index = 0; index < items.Count; index++)
            {
                OwnedDraftAnimalSaveData owned = items[index];
                SharedDraftAnimalDefinition definition = null;
                bool found = catalog != null && catalog.TryGetDraftAnimal(owned.contentId, out definition) && definition != null;
                TransportInventorySlotViewData slot = slots[index];
                slot.IsOccupied = true;
                slot.IsOverflow = index >= state.DraftAnimalSlotCount;
                slot.InstanceId = owned.instanceId.Trim();
                slot.ContentId = owned.contentId.Trim();
                slot.DisplayName = found ? definition.DisplayName ?? owned.contentId : "알 수 없는 동물";
                slot.Description = found ? definition.Description ?? string.Empty : "카탈로그에서 동물 정보를 찾을 수 없습니다.";
                slot.Icon = definition?.Icon;
                slot.BaseBuyPrice = Math.Max(0L, definition?.BaseBuyPrice ?? 0L);
                slot.HasDurability = false;
                slot.IsCatalogMissing = !found;
                ApplyAssignment(slot, assignments);
            }

            return CreatePanel(TransportInventoryTab.Animal, "동물 보관함", items.Count,
                state.DraftAnimalSlotCount, TransportInventoryFunction.DraftAnimalSlotsPerLevel, slots);
        }

        private static TransportInventoryPanelViewData CreatePanel(
            TransportInventoryTab tab,
            string title,
            int used,
            int available,
            int slotsPerLevel,
            List<TransportInventorySlotViewData> slots)
        {
            int lockedStart = available;
            int currentLevel = slotsPerLevel > 0 ? available / slotsPerLevel : 0;
            return new TransportInventoryPanelViewData
            {
                Tab = tab,
                Title = title,
                UsedSlots = used,
                AvailableSlots = available,
                AbsoluteMaximumSlots = slotsPerLevel * TransportInventoryFunction.MaximumLevel,
                OverflowCount = Math.Max(0, used - available),
                LockedStartIndex = Math.Min(lockedStart, slots.Count),
                NextRequiredFarmLevel = currentLevel < TransportInventoryFunction.MaximumLevel ? currentLevel + 1 : 0,
                Slots = slots
            };
        }

        private static List<TransportInventorySlotViewData> CreateEmptySlots(
            TransportInventoryTab tab, int count, int available)
        {
            var result = new List<TransportInventorySlotViewData>(count);
            for (int index = 0; index < count; index++)
            {
                result.Add(new TransportInventorySlotViewData
                {
                    SlotIndex = index,
                    Tab = tab,
                    IsUnlocked = index < available
                });
            }
            return result;
        }

        private static List<OwnedWagonSaveData> ValidWagons(IReadOnlyList<OwnedWagonSaveData> source)
        {
            var result = new List<OwnedWagonSaveData>();
            if (source == null) return result;
            for (int index = 0; index < source.Count; index++)
            {
                OwnedWagonSaveData value = source[index];
                if (value != null && !string.IsNullOrWhiteSpace(value.instanceId) && !string.IsNullOrWhiteSpace(value.contentId))
                    result.Add(value);
            }
            return result;
        }

        private static List<OwnedDraftAnimalSaveData> ValidAnimals(IReadOnlyList<OwnedDraftAnimalSaveData> source)
        {
            var result = new List<OwnedDraftAnimalSaveData>();
            if (source == null) return result;
            for (int index = 0; index < source.Count; index++)
            {
                OwnedDraftAnimalSaveData value = source[index];
                if (value != null && !string.IsNullOrWhiteSpace(value.instanceId) && !string.IsNullOrWhiteSpace(value.contentId))
                    result.Add(value);
            }
            return result;
        }

        private sealed class Assignment
        {
            public string CaravanId;
            public string CaravanName;
            public int? CurrentDurability;
        }

        private static Dictionary<string, Assignment> BuildAssignments(IReadOnlyList<ND.Framework.CaravanSaveData> caravans)
        {
            var result = new Dictionary<string, Assignment>(StringComparer.Ordinal);
            if (caravans == null) return result;
            foreach (ND.Framework.CaravanSaveData caravan in caravans)
            {
                if (caravan == null) continue;
                var assignment = new Assignment
                {
                    CaravanId = caravan.caravanId?.Trim() ?? string.Empty,
                    CaravanName = ResolveCaravanName(caravan)
                };
                string wagonId = caravan.wagon?.instanceId?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(wagonId))
                {
                    result.TryAdd(wagonId, new Assignment
                    {
                        CaravanId = assignment.CaravanId,
                        CaravanName = assignment.CaravanName,
                        CurrentDurability = Math.Max(0, caravan.currentDurability)
                    });
                }
                foreach (ND.Framework.AnimalSaveData animal in caravan.animals ?? new List<ND.Framework.AnimalSaveData>())
                {
                    string animalId = animal?.instanceId?.Trim() ?? string.Empty;
                    if (!string.IsNullOrEmpty(animalId)) result.TryAdd(animalId, assignment);
                }
            }
            return result;
        }

        private static void ApplyAssignment(
            TransportInventorySlotViewData slot,
            IReadOnlyDictionary<string, Assignment> assignments)
        {
            if (assignments == null || !assignments.TryGetValue(slot.InstanceId, out Assignment assignment)) return;
            slot.IsAssigned = true;
            slot.AssignedCaravanId = assignment.CaravanId;
            slot.AssignedCaravanName = assignment.CaravanName;
            if (slot.HasDurability && assignment.CurrentDurability.HasValue)
            {
                slot.CurrentDurability = Math.Min(Math.Max(0, assignment.CurrentDurability.Value), slot.MaxDurability);
                slot.DurabilityPercent = slot.MaxDurability > 0
                    ? Mathf.RoundToInt(slot.CurrentDurability * 100f / slot.MaxDurability)
                    : 0;
            }
        }
    
        private static string ResolveCaravanName(ND.Framework.CaravanSaveData caravan)
        {
            string displayName = caravan?.displayName?.Trim() ?? string.Empty;
            string caravanId = caravan?.caravanId?.Trim() ?? string.Empty;
            return !string.IsNullOrEmpty(displayName)
                && !string.Equals(displayName, caravanId, StringComparison.Ordinal)
                    ? displayName
                    : $"Caravan {Math.Max(0, caravan?.slotIndex ?? 0) + 1}";
        }
}
}
