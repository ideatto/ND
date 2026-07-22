using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides an in-memory S3 fixture for scene wiring and SmokeTest verification.
/// </summary>
/// <remarks>
/// This component never reads or writes SaveData. Replace it with Framework implementations of
/// ICaravanSettingViewDataProvider and ICaravanSettingCommand before production persistence tests.
/// </remarks>
[DisallowMultipleComponent]
public sealed class TestCaravanSettingService : MonoBehaviour,
    ICaravanSettingViewDataProvider,
    ICaravanSettingCommand
{
    public const string PrepareCaravanId = "test-caravan-prepare";
    public const string TravelingCaravanId = "test-caravan-traveling";
    public const string WagonContentId = "test-wagon-medium";
    public const string WagonInstanceId = "test-wagon-instance-01";
    public const string AnimalContentId = "test-horse";
    public const string FirstAnimalInstanceId = "test-horse-instance-01";
    public const string SecondAnimalInstanceId = "test-horse-instance-02";

    private string selectedWagonInstanceId = WagonInstanceId;
    private readonly List<string> selectedAnimalInstanceIds = new List<string>
    {
        FirstAnimalInstanceId,
        SecondAnimalInstanceId
    };

    public CaravanSettingViewData GetSetting(string caravanId)
    {
        string normalizedCaravanId = NormalizeId(caravanId);
        if (normalizedCaravanId == PrepareCaravanId)
        {
            return CreateSettingSnapshot(
                PrepareCaravanId,
                "Preparation Caravan",
                JourneyState.Prepare,
                true,
                string.Empty,
                selectedWagonInstanceId,
                selectedAnimalInstanceIds);
        }

        if (normalizedCaravanId == TravelingCaravanId)
        {
            return CreateSettingSnapshot(
                TravelingCaravanId,
                "Traveling Caravan",
                JourneyState.Traveling,
                false,
                "Caravan settings cannot be changed while the Caravan is traveling.",
                WagonInstanceId,
                new[] { FirstAnimalInstanceId, SecondAnimalInstanceId });
        }

        // A missing Caravan remains distinct from a valid but read-only Caravan.
        return null;
    }

    public CaravanSettingCommandResult Execute(CaravanSettingDraft draft)
    {
        if (draft == null || string.IsNullOrEmpty(NormalizeId(draft.caravanId)))
        {
            return CaravanSettingCommandResult.Failure(
                CaravanSettingFailureCodes.InvalidDraft,
                "The Caravan setting request is invalid.");
        }

        string caravanId = NormalizeId(draft.caravanId);
        if (caravanId != PrepareCaravanId && caravanId != TravelingCaravanId)
        {
            return CaravanSettingCommandResult.Failure(
                CaravanSettingFailureCodes.CaravanNotFound,
                "The selected Caravan could not be found.");
        }

        if (caravanId != PrepareCaravanId)
        {
            return CaravanSettingCommandResult.Failure(
                CaravanSettingFailureCodes.CaravanNotEditable,
                "Caravan settings can only be changed during Preparation.");
        }

        string wagonInstanceId = NormalizeId(draft.selectedWagonInstanceId);
        if (!string.IsNullOrEmpty(wagonInstanceId) && wagonInstanceId != WagonInstanceId)
        {
            return CaravanSettingCommandResult.Failure(
                CaravanSettingFailureCodes.AssetNotOwned,
                "The selected wagon is not available.");
        }

        var validatedAnimalIds = new List<string>();
        IReadOnlyList<string> draftAnimalIds = draft.SelectedAnimalInstanceIds;
        for (int index = 0; index < draftAnimalIds.Count; index++)
        {
            string animalInstanceId = NormalizeId(draftAnimalIds[index]);
            if (!IsOwnedAnimal(animalInstanceId))
            {
                return CaravanSettingCommandResult.Failure(
                    CaravanSettingFailureCodes.AssetNotOwned,
                    "One or more selected animals are not available.");
            }

            if (validatedAnimalIds.Contains(animalInstanceId))
            {
                return CaravanSettingCommandResult.Failure(
                    CaravanSettingFailureCodes.InvalidComposition,
                    "The same animal instance cannot be selected more than once.");
            }

            validatedAnimalIds.Add(animalInstanceId);
        }

        bool walking = string.IsNullOrEmpty(wagonInstanceId);
        if ((walking && validatedAnimalIds.Count > 0)
            || (!walking && (validatedAnimalIds.Count < 1 || validatedAnimalIds.Count > 2)))
        {
            return CaravanSettingCommandResult.Failure(
                CaravanSettingFailureCodes.InvalidComposition,
                "The selected wagon and animal composition is invalid.");
        }

        // Apply only after every validation passes so a failed test command cannot leave partial state.
        selectedWagonInstanceId = wagonInstanceId;
        selectedAnimalInstanceIds.Clear();
        selectedAnimalInstanceIds.AddRange(validatedAnimalIds);
        return CaravanSettingCommandResult.Success();
    }

    private CaravanSettingViewData CreateSettingSnapshot(
        string caravanId,
        string displayName,
        JourneyState state,
        bool canEdit,
        string blockedReason,
        string snapshotWagonInstanceId,
        IReadOnlyList<string> snapshotAnimalInstanceIds)
    {
        string[] animalIds = CopyIds(snapshotAnimalInstanceIds);
        return new CaravanSettingViewData
        {
            caravanId = caravanId,
            caravanDisplayName = displayName,
            state = state,
            canEdit = canEdit,
            editBlockedReason = canEdit ? string.Empty : blockedReason,
            selectedWagonInstanceId = snapshotWagonInstanceId,
            selectedAnimalInstanceIds = animalIds,
            wagons = new[]
            {
                new WagonViewData
                {
                    wagonId = WagonContentId,
                    wagonInstanceId = WagonInstanceId,
                    displayName = "Test Medium Wagon",
                    wagonType = WagonType.WagonWithAnimals,
                    maxLoad = 100f,
                    inventorySlotCount = 5,
                    minRequireAnimals = 1,
                    maxPullAnimals = 2,
                    eligibleAnimalTypes = new[] { DraftAnimalType.Horse },
                    ownedAmount = 1,
                    isOwned = true,
                    canSelect = canEdit,
                    disabledReason = canEdit ? string.Empty : blockedReason
                }
            },
            draftAnimals = new[]
            {
                CreateAnimalViewData(
                    FirstAnimalInstanceId,
                    ContainsId(animalIds, FirstAnimalInstanceId),
                    canEdit,
                    blockedReason),
                CreateAnimalViewData(
                    SecondAnimalInstanceId,
                    ContainsId(animalIds, SecondAnimalInstanceId),
                    canEdit,
                    blockedReason)
            }
        };
    }

    private static DraftAnimalViewData CreateAnimalViewData(
        string animalInstanceId,
        bool selected,
        bool canEdit,
        string blockedReason)
    {
        return new DraftAnimalViewData
        {
            draftAnimalId = AnimalContentId,
            draftAnimalInstanceId = animalInstanceId,
            displayName = "Test Horse",
            animalType = DraftAnimalType.Horse,
            ownedAmount = 1,
            selectedAmount = selected ? 1 : 0,
            maxSelectableAmount = 1,
            isEligibleForSelectedWagon = true,
            canSelect = canEdit,
            disabledReason = canEdit ? string.Empty : blockedReason
        };
    }

    private static bool IsOwnedAnimal(string animalInstanceId)
    {
        return animalInstanceId == FirstAnimalInstanceId
            || animalInstanceId == SecondAnimalInstanceId;
    }

    private static string[] CopyIds(IReadOnlyList<string> source)
    {
        if (source == null || source.Count == 0)
        {
            return Array.Empty<string>();
        }

        var copy = new string[source.Count];
        for (int index = 0; index < source.Count; index++)
        {
            copy[index] = source[index];
        }

        return copy;
    }

    private static bool ContainsId(IReadOnlyList<string> source, string target)
    {
        if (source == null)
        {
            return false;
        }

        for (int index = 0; index < source.Count; index++)
        {
            if (source[index] == target)
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
