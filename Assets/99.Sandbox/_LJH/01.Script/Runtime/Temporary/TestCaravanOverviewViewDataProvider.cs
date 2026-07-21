using System;

public sealed class TestCaravanOverviewViewDataProvider : ICaravanOverviewViewDataProvider
{
    // Keeps the temporary UI fixture aligned with the agreed maximum Caravan count.
    public const int SlotCount = 4;

    public const string PrepareCaravanId = "test-caravan-prepare";
    public const string TravelingCaravanId = "test-caravan-traveling";

    private readonly string focusedCaravanId;

    // Focuses an editable Caravan by default so a test UI can open its setting and load panels.
    public TestCaravanOverviewViewDataProvider()
        : this(PrepareCaravanId)
    {
    }

    // Allows tests to preview either occupied slot without treating Overview focus as a departure choice.
    public TestCaravanOverviewViewDataProvider(string focusedCaravanId)
    {
        this.focusedCaravanId = NormalizeFocusedCaravanId(focusedCaravanId);
    }

    public CaravanOverviewViewData GetOverview()
    {
        // A fresh object graph prevents UI-side changes from mutating later Provider results.
        return new CaravanOverviewViewData
        {
            focusedCaravanId = focusedCaravanId,
            caravans = new[]
            {
                CreatePrepareBlock(),
                CreateTravelingBlock(),
                CreateEmptyBlock(),
                CreateLockedBlock()
            }
        };
    }

    private static CaravanBlockViewData CreatePrepareBlock()
    {
        return new CaravanBlockViewData
        {
            slotIndex = 0,
            slotState = CaravanSlotState.Occupied,
            caravanId = PrepareCaravanId,
            displayName = "Preparation Caravan",
            state = JourneyState.Prepare,
            wagonContentId = "test-wagon-walk",
            animalIcons = Array.Empty<AnimalIconViewData>(),
            cargoIcons = Array.Empty<CargoIconViewData>()
        };
    }

    private static CaravanBlockViewData CreateTravelingBlock()
    {
        return new CaravanBlockViewData
        {
            slotIndex = 1,
            slotState = CaravanSlotState.Occupied,
            caravanId = TravelingCaravanId,
            displayName = "Traveling Caravan",
            state = JourneyState.Traveling,
            wagonContentId = "test-wagon-medium",
            animalIcons = new[]
            {
                new AnimalIconViewData
                {
                    animalContentId = "test-horse",
                    quantity = 2
                }
            },
            cargoIcons = new[]
            {
                new CargoIconViewData
                {
                    itemId = "test-grain",
                    quantity = 10
                }
            }
        };
    }

    private static CaravanBlockViewData CreateEmptyBlock()
    {
        return new CaravanBlockViewData
        {
            slotIndex = 2,
            slotState = CaravanSlotState.Empty
        };
    }

    private static CaravanBlockViewData CreateLockedBlock()
    {
        return new CaravanBlockViewData
        {
            slotIndex = 3,
            slotState = CaravanSlotState.Locked,
            lockedReason = "Complete the required quest to unlock this Caravan slot."
        };
    }

    private static string NormalizeFocusedCaravanId(string candidate)
    {
        // The fixture only advertises its two occupied IDs; any other value represents no selection.
        if (candidate == PrepareCaravanId || candidate == TravelingCaravanId)
        {
            return candidate;
        }

        return string.Empty;
    }
}
