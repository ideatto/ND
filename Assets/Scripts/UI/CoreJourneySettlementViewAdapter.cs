#if false // Disabled until the 01.Core and 11.CoreServices owners provide an approved UI contract.
using System;

[Serializable]
public sealed class CoreJourneySettlementViewData
{
    public JourneyResultGrade Grade;
    public JourneyFailureReason FailureReason;
    public int CargoLost;
    public float FoodConsumed;
    public float FoodLostByEvent;
    public float TravelSeconds;
    public int MercenaryCount;
    public int BattlesFought;
    public int CurrentDurability;
    public int MaximumDurability;
    public float DurabilityLost;
}

public static class CoreJourneySettlementViewAdapter
{
    public static CoreJourneySettlementViewData Create(JourneyResultData journey, CaravanData caravan)
    {
        if (journey == null)
            return null;

        return new CoreJourneySettlementViewData
        {
            Grade = journey.grade,
            FailureReason = journey.failureReason,
            CargoLost = Math.Max(0, journey.cargoLost),
            FoodConsumed = Math.Max(0f, journey.foodConsumed),
            FoodLostByEvent = caravan == null ? 0f : Math.Max(0f, caravan.runFoodLost),
            TravelSeconds = Math.Max(0f, journey.travelSeconds),
            MercenaryCount = caravan?.mercenaries?.Count ?? 0,
            BattlesFought = caravan == null ? 0 : Math.Max(0, caravan.runBattlesFought),
            CurrentDurability = caravan == null ? 0 : Math.Max(0, caravan.currentDurability),
            MaximumDurability = caravan?.wagon == null ? 0 : Math.Max(0, caravan.wagon.maxDurability),
            DurabilityLost = Math.Max(0f, journey.durabilityLost)
        };
    }
}
#endif
