using System.Linq;
using ND.Framework;
using FrameworkSaveData = ND.Framework.SaveData;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;
using NUnit.Framework;

public sealed class WarehouseInventoryTransferTests
{
    [Test]
    public void Resolver_AggregatesByItemAndPurchasePrice()
    {
        var entries = new[]
        {
            Entry("apple", 10, 3),
            Entry("apple", 10, 2),
            Entry("apple", 8, 4),
            Entry("pear", 10, 99)
        };

        var groups = WarehousePriceGroupResolver.Resolve(entries, "apple");

        Assert.That(groups.Count, Is.EqualTo(2));
        Assert.That(groups[0].PurchaseUnitPrice, Is.EqualTo(8));
        Assert.That(groups[0].Quantity, Is.EqualTo(4));
        Assert.That(groups[1].PurchaseUnitPrice, Is.EqualTo(10));
        Assert.That(groups[1].Quantity, Is.EqualTo(5));
    }

    [Test]
    public void Transfer_SaveFailure_RestoresBothInventories()
    {
        FrameworkSaveData save = new FrameworkSaveData();
        save.player.homeInventory.Add(Entry("apple", 10, 5));
        var caravan = new FrameworkCaravanSaveData
        {
            caravanId = "caravan-test",
            currentTownId = "BaseCamp"
        };
        save.caravans.Add(caravan);
        var request = new WarehouseTransferRequest(
            caravan.caravanId, "BaseCamp", "apple", 10, 3,
            WarehouseTransferDirection.HomeToCargo, 30, 12, 100f);

        bool succeeded = WarehouseInventoryTransferService.TryTransfer(
            save, new FailingSaveService(), request, out WarehouseTransferFailure failure);

        Assert.That(succeeded, Is.False);
        Assert.That(failure, Is.EqualTo(WarehouseTransferFailure.SaveFailed));
        Assert.That(WarehousePriceGroupResolver.Resolve(save.player.homeInventory, "apple").Single().Quantity,
            Is.EqualTo(5));
        Assert.That(SaveDataLookup.TryGetCaravan(save, "caravan-test", out FrameworkCaravanSaveData restored), Is.True);
        Assert.That(restored.cargo, Is.Empty);
    }

    private static CargoEntrySaveData Entry(string id, long price, int quantity) =>
        new CargoEntrySaveData
        {
            item = new TradeItemSaveData
            {
                itemId = id,
                itemName = id,
                purchaseUnitPrice = price,
                weight = 1f,
                maxCount = 99
            },
            quantity = quantity
        };

    private sealed class FailingSaveService : ISaveService
    {
        public bool HasSaveData() => true;
        public FrameworkSaveData CreateNewGameData() => new FrameworkSaveData();
        public FrameworkSaveData Load() => new FrameworkSaveData();
        public SaveResult Save(FrameworkSaveData data) =>
            SaveResult.Failure(SaveFailureReason.WriteFailed, "Expected test failure.");
        public void ResetSaveData() { }
    }
}