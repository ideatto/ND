using NUnit.Framework;

public sealed class CaravanCargoSlotGroupingTests
{
    [Test]
    public void GetUsedSlots_SharesPhysicalStackAcrossPurchasePriceGroups()
    {
        var caravan = new CaravanData();
        caravan.cargo.Add(Cargo("Bread", 7, 10, 0L));
        caravan.cargo.Add(Cargo("Bread", 1, 10, 50L));

        Assert.That(CaravanCalculator.GetUsedSlots(caravan), Is.EqualTo(1));
    }

    [Test]
    public void GetUsedSlots_DoesNotMergeDifferentItemIds()
    {
        var caravan = new CaravanData();
        caravan.cargo.Add(Cargo("Bread", 7, 10, 0L));
        caravan.cargo.Add(Cargo("Stover", 1, 10, 0L));

        Assert.That(CaravanCalculator.GetUsedSlots(caravan), Is.EqualTo(2));
    }

    private static CargoEntry Cargo(string itemId, int quantity, int maxCount, long purchasePrice)
    {
        return new CargoEntry
        {
            item = new imsiTradeItemData
            {
                id = itemId,
                maxCount = maxCount,
                purchaseUnitPrice = purchasePrice
            },
            quantity = quantity
        };
    }
}
