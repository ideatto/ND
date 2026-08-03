using ND.Framework;
using NUnit.Framework;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;
using FrameworkCargoEntrySaveData = ND.Framework.CargoEntrySaveData;
using FrameworkTradeItemSaveData = ND.Framework.TradeItemSaveData;

public sealed class CaravanSavedCargoServiceTests
{
    [Test]
    public void CreateSnapshot_ReturnsEmptyForMissingCaravan()
    {
        CaravanSavedCargoSnapshot snapshot = new CaravanSavedCargoService().CreateSnapshot(null);

        Assert.That(snapshot.Items, Is.Empty);
        Assert.That(snapshot.BaselineSignature, Is.Empty);
    }

    [Test]
    public void CreateSnapshot_FiltersInvalidEntriesAndNormalizesId()
    {
        FrameworkCaravanSaveData caravan = CreateCaravan(
            null,
            Entry(" ", 2),
            Entry(" apple ", 3),
            Entry("cloth", 0));

        CaravanSavedCargoSnapshot snapshot = new CaravanSavedCargoService().CreateSnapshot(caravan);

        Assert.That(snapshot.Items.Count, Is.EqualTo(1));
        Assert.That(snapshot.Items[0].ItemId, Is.EqualTo("apple"));
        Assert.That(snapshot.Items[0].Quantity, Is.EqualTo(3));
    }

    [Test]
    public void CreateSnapshot_AggregatesDuplicatesAndKeepsFirstSavedItem()
    {
        FrameworkCargoEntrySaveData first = Entry("apple", 2, "First");
        FrameworkCargoEntrySaveData second = Entry("apple", 5, "Second");

        CaravanSavedCargoSnapshot snapshot = new CaravanSavedCargoService().CreateSnapshot(
            CreateCaravan(first, second));

        Assert.That(snapshot.Items.Count, Is.EqualTo(1));
        Assert.That(snapshot.Items[0].Quantity, Is.EqualTo(7));
        Assert.That(snapshot.Items[0].SavedItem.itemName, Is.EqualTo("First"));
    }

    [Test]
    public void CreateSnapshot_BaselineIsStableRegardlessOfEntryOrder()
    {
        var service = new CaravanSavedCargoService();

        string first = service.CreateSnapshot(CreateCaravan(Entry("cloth", 1), Entry("apple", 2)))
            .BaselineSignature;
        string second = service.CreateSnapshot(CreateCaravan(Entry("apple", 2), Entry("cloth", 1)))
            .BaselineSignature;

        Assert.That(first, Is.EqualTo("apple:2;cloth:1;"));
        Assert.That(second, Is.EqualTo(first));
    }

    private static FrameworkCaravanSaveData CreateCaravan(params FrameworkCargoEntrySaveData[] entries)
    {
        var caravan = new FrameworkCaravanSaveData();
        caravan.cargo.Clear();
        caravan.cargo.AddRange(entries);
        return caravan;
    }

    private static FrameworkCargoEntrySaveData Entry(string itemId, int quantity, string name = "Item")
    {
        return new FrameworkCargoEntrySaveData
        {
            item = new FrameworkTradeItemSaveData
            {
                itemId = itemId,
                itemName = name
            },
            quantity = quantity
        };
    }
}
