using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using ND.Framework;
using ND.Framework.CargoLoading;
using FrameworkSaveData = ND.Framework.SaveData;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;
using NUnit.Framework;
using UnityEngine;

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
        save.caravans.Clear();
        save.player.currentTownId = WarehouseFunction.BaseTownId;
        save.player.homeInventory.Add(Entry("apple", 10, 5));
        var caravan = new FrameworkCaravanSaveData
        {
            caravanId = "caravan-test",
            slotIndex = 0,
            currentTownId = WarehouseFunction.BaseTownId
        };
        save.caravans.Add(caravan);
        var request = new WarehouseTransferRequest(
            caravan.caravanId, WarehouseFunction.BaseTownId, "apple", 10, 3,
            WarehouseTransferDirection.HomeToCargo, 30, 12, 100f);

        bool succeeded = WarehouseInventoryTransferService.TryTransfer(
            save, new FailingSaveService(), request, out WarehouseTransferFailure failure);

        Assert.That(succeeded, Is.False);
        Assert.That(failure, Is.EqualTo(WarehouseTransferFailure.SaveFailed));
        Assert.That(WarehousePriceGroupResolver.Resolve(save.player.homeInventory, "apple").Single().Quantity,
            Is.EqualTo(5));
        Assert.That(SaveDataLookup.TryGetCaravan(
            save, "caravan-test", out FrameworkCaravanSaveData restored), Is.True);
        Assert.That(restored.cargo, Is.Empty);
    }

[Test]
    public void Transfer_NonPrepareCaravan_IsRejectedBeforeMutation()
    {
        FrameworkSaveData save = new FrameworkSaveData();
        save.caravans.Clear();
        save.player.currentTownId = WarehouseFunction.BaseTownId;
        save.player.homeInventory.Add(Entry("apple", 10, 5));
        var caravan = new FrameworkCaravanSaveData
        {
            caravanId = "caravan-busy",
            slotIndex = 0,
            currentTownId = WarehouseFunction.BaseTownId,
            state = JourneyState.Traveling
        };
        save.caravans.Add(caravan);
        var request = new WarehouseTransferRequest(
            caravan.caravanId, WarehouseFunction.BaseTownId, "apple", 10, 1,
            WarehouseTransferDirection.HomeToCargo, 30, 12, 100f);

        bool succeeded = WarehouseInventoryTransferService.TryTransfer(
            save, new FailingSaveService(), request, out WarehouseTransferFailure failure);

        Assert.That(succeeded, Is.False);
        Assert.That(failure, Is.EqualTo(WarehouseTransferFailure.CaravanBusy));
        Assert.That(WarehousePriceGroupResolver.Resolve(save.player.homeInventory, "apple").Single().Quantity,
            Is.EqualTo(5));
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


[Test]
    public void MarketBuy_ExtendsOnlyMatchingPriceGroup()
    {
        var caravan = new FrameworkCaravanSaveData();
        caravan.cargo.Add(Entry("apple", 8, 3));
        caravan.cargo.Add(Entry("apple", 10, 4));
        object session = CreateMarketSessionForCargo(caravan);

        InvokeMarketCargoDelta(session, "apple", 2, 10);

        var groups = WarehousePriceGroupResolver.Resolve(caravan.cargo, "apple");
        Assert.That(groups.Single(group => group.PurchaseUnitPrice == 8).Quantity, Is.EqualTo(3));
        Assert.That(groups.Single(group => group.PurchaseUnitPrice == 10).Quantity, Is.EqualTo(6));
    }

    [Test]
    public void MarketSell_DrainsStoredOrderWithoutMergingRemainingGroups()
    {
        var caravan = new FrameworkCaravanSaveData();
        caravan.cargo.Add(Entry("apple", 8, 2));
        caravan.cargo.Add(Entry("apple", 10, 5));
        object session = CreateMarketSessionForCargo(caravan);

        InvokeMarketCargoDelta(session, "apple", -3, 0);

        var groups = WarehousePriceGroupResolver.Resolve(caravan.cargo, "apple");
        Assert.That(groups.Count, Is.EqualTo(1));
        Assert.That(groups[0].PurchaseUnitPrice, Is.EqualTo(10));
        Assert.That(groups[0].Quantity, Is.EqualTo(4));
    }

private static object CreateMarketSessionForCargo(FrameworkCaravanSaveData caravan)
    {
        object session = FormatterServices.GetUninitializedObject(typeof(MarketInventoryMutationSession));
        FieldInfo target = typeof(MarketInventoryMutationSession).GetField(
            "targetCaravan", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo catalog = typeof(MarketInventoryMutationSession).GetField(
            "catalogById", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(target, Is.Not.Null);
        Assert.That(catalog, Is.Not.Null);

        var item = ScriptableObject.CreateInstance<TradeItemData>();
        SetPrivateField(item, "itemId", "apple");
        SetPrivateField(item, "displayName", "Apple");
        SetPrivateField(item, "baseBuyPrice", 7L);
        SetPrivateField(item, "maxCount", 99);
        SetPrivateField(item, "weight", 1f);

        target.SetValue(session, caravan);
        catalog.SetValue(session, new Dictionary<string, TradeItemData>(StringComparer.Ordinal)
        {
            ["apple"] = item
        });
        return session;
    }

    private static void InvokeMarketCargoDelta(object session, string itemId, int delta, long price)
    {
        MethodInfo method = typeof(MarketInventoryMutationSession).GetMethod(
            "ApplyCargoDelta", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(session, new object[] { itemId, delta, price });
    }


private static void SetPrivateField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }


[Test]
    public void MarketBuy_DifferentPriceCreatesNewGroup()
    {
        var caravan = new FrameworkCaravanSaveData();
        caravan.cargo.Add(Entry("apple", 8, 3));
        object session = CreateMarketSessionForCargo(caravan);

        InvokeMarketCargoDelta(session, "apple", 2, 12);

        var groups = WarehousePriceGroupResolver.Resolve(caravan.cargo, "apple");
        Assert.That(groups.Count, Is.EqualTo(2));
        CargoEntrySaveData purchased = caravan.cargo.Single(entry =>
            entry.item.purchaseUnitPrice == 12);
        Assert.That(purchased.quantity, Is.EqualTo(2));
        Assert.That(purchased.item.basePrice, Is.EqualTo(7));
    }
}