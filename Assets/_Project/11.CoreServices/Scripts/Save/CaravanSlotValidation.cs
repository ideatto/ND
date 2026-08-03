using System;
using System.Collections.Generic;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;

/// <summary>
/// Projects persisted Caravans into the four authoritative UI slots without mutating SaveData.
/// Invalid ranges, duplicate slots, empty IDs, and duplicate IDs fail closed.
/// </summary>
public sealed class CaravanSlotValidationResult
{
    private readonly FrameworkCaravanSaveData[] caravansBySlot;
    private readonly bool[] conflictedSlots;

    internal CaravanSlotValidationResult(
        FrameworkCaravanSaveData[] caravansBySlot,
        bool[] conflictedSlots,
        bool hasInvalidEntries)
    {
        this.caravansBySlot = caravansBySlot;
        this.conflictedSlots = conflictedSlots;
        HasInvalidEntries = hasInvalidEntries;
    }

    public int SlotCount => caravansBySlot.Length;
    public bool HasInvalidEntries { get; }

    public FrameworkCaravanSaveData GetCaravanAt(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < caravansBySlot.Length
            ? caravansBySlot[slotIndex]
            : null;
    }

    public bool IsConflicted(int slotIndex)
    {
        return slotIndex >= 0
            && slotIndex < conflictedSlots.Length
            && conflictedSlots[slotIndex];
    }

    public bool TryGetCaravan(string caravanId, out FrameworkCaravanSaveData caravan)
    {
        caravan = null;
        if (string.IsNullOrWhiteSpace(caravanId)) return false;

        for (int slotIndex = 0; slotIndex < caravansBySlot.Length; slotIndex++)
        {
            FrameworkCaravanSaveData candidate = caravansBySlot[slotIndex];
            if (candidate != null
                && string.Equals(candidate.caravanId, caravanId, StringComparison.Ordinal))
            {
                caravan = candidate;
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Shared read-only Caravan slot policy used by Main UI and Warehouse UI.
/// </summary>
public static class CaravanSlotValidation
{
    public const int SlotCount = 4;

    public static CaravanSlotValidationResult Validate(
        IReadOnlyList<FrameworkCaravanSaveData> caravans)
    {
        var candidatesBySlot = new List<FrameworkCaravanSaveData>[SlotCount];
        var validBySlot = new FrameworkCaravanSaveData[SlotCount];
        var conflictedSlots = new bool[SlotCount];
        bool hasInvalidEntries = false;

        for (int index = 0; index < (caravans?.Count ?? 0); index++)
        {
            FrameworkCaravanSaveData caravan = caravans[index];
            if (caravan == null) continue;

            if (caravan.slotIndex < 0 || caravan.slotIndex >= SlotCount)
            {
                hasInvalidEntries = true;
                continue;
            }

            if (candidatesBySlot[caravan.slotIndex] == null)
                candidatesBySlot[caravan.slotIndex] = new List<FrameworkCaravanSaveData>();
            candidatesBySlot[caravan.slotIndex].Add(caravan);
        }

        var uniqueIdSlots = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
        {
            List<FrameworkCaravanSaveData> candidates = candidatesBySlot[slotIndex];
            if (candidates == null || candidates.Count == 0) continue;

            if (candidates.Count != 1 || string.IsNullOrWhiteSpace(candidates[0].caravanId))
            {
                conflictedSlots[slotIndex] = true;
                hasInvalidEntries = true;
                continue;
            }

            FrameworkCaravanSaveData candidate = candidates[0];
            validBySlot[slotIndex] = candidate;
            if (!uniqueIdSlots.TryGetValue(candidate.caravanId, out List<int> slots))
            {
                slots = new List<int>();
                uniqueIdSlots.Add(candidate.caravanId, slots);
            }
            slots.Add(slotIndex);
        }

        foreach (KeyValuePair<string, List<int>> pair in uniqueIdSlots)
        {
            if (pair.Value.Count == 1) continue;
            hasInvalidEntries = true;
            for (int index = 0; index < pair.Value.Count; index++)
            {
                int slotIndex = pair.Value[index];
                validBySlot[slotIndex] = null;
                conflictedSlots[slotIndex] = true;
            }
        }

        return new CaravanSlotValidationResult(validBySlot, conflictedSlots, hasInvalidEntries);
    }
}
