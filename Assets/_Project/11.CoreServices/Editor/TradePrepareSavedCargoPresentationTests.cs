using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class TradePrepareSavedCargoPresentationTests
{
    private readonly List<TradeItemData> createdItems = new List<TradeItemData>();

    [TearDown]
    public void TearDown()
    {
        foreach (TradeItemData item in createdItems)
            UnityEngine.Object.DestroyImmediate(item);
        createdItems.Clear();
    }

    [Test]
    public void SavedCargoOutsideCurrentMarket_UsesViewDataWithoutJoiningShopProducts()
    {
        var loaded = new[]
        {
            Cargo("wood", 8, 0.1f),
            Cargo("ore", 69, 0.42f)
        };

        TradeItemViewData[] restored = Invoke<TradeItemViewData[]>(
            "BuildOwnedCargoSelection",
            loaded,
            new Dictionary<string, int>());

        Assert.That(restored.Select(item => (item.itemId, item.ownedAmount)),
            Is.EquivalentTo(new[] { ("wood", 8), ("ore", 69) }));
        Assert.That(restored.Sum(item => item.ownedAmount * item.unitWeight),
            Is.EqualTo(29.78f).Within(0.001f));
    }

    private T Invoke<T>(string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(TradePrepareUiRuntimeBinding).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (T)method.Invoke(null, arguments);
    }

private TradeItemData CreateItem(string itemId, float weight = 1f)
    {
        TradeItemData item = ScriptableObject.CreateInstance<TradeItemData>();
        typeof(TradeItemData).GetField("itemId", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(item, itemId);
        typeof(TradeItemData).GetField("weight", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(item, weight);
        typeof(TradeItemData).GetField("maxCount", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(item, 99);
        typeof(TradeItemData).GetField("canStack", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(item, true);
        createdItems.Add(item);
        return item;
    }

    private static CargoItemViewData Cargo(string itemId, int quantity, float unitWeight)
    {
        return new CargoItemViewData
        {
            itemId = itemId,
            displayName = itemId,
            quantity = quantity,
            unitWeight = unitWeight,
            totalWeight = quantity * unitWeight
        };
    }


[Test]
    public void CargoPanel_SameItemSlotCancelsOnlyReservedQuantity()
    {
        TradeItemData apple = CreateItem("apple", 1f);
        GameObject host = new GameObject("CargoPanelStateTest");
        try
        {
            CargoLoadingPanelController panel = host.AddComponent<CargoLoadingPanelController>();
            panel.Configure(1000, 30f, 0, new[] { apple }, new[] { 10 }, new long[] { 1 });
            panel.SetDetachedInventorySlotLimit(5);
            panel.RestoreSavedCargo(new[]
            {
                new TradeItemViewData
                {
                    itemId = "apple",
                    displayName = "apple",
                    ownedAmount = 8,
                    unitWeight = 1f
                }
            }, false);
            panel.RestoreSelectedCargo(new[]
            {
                new TradeItemViewData { itemId = "apple", selectedBuyAmount = 3 }
            }, false, false);

            Assert.That(panel.SavedLoad, Is.EqualTo(8f).Within(0.001f));
            Assert.That(panel.ReservedLoad, Is.EqualTo(3f).Within(0.001f));
            Assert.That(panel.CurrentLoad, Is.EqualTo(11f).Within(0.001f));
            Assert.That(panel.BuildCargoSelections().Select(item => (item.itemId, item.quantity)),
                Is.EquivalentTo(new[] { ("apple", 11) }));

            panel.DecrementLoadedSlot(0);
            Assert.That(panel.SavedLoad, Is.EqualTo(8f).Within(0.001f));
            Assert.That(panel.ReservedLoad, Is.EqualTo(2f).Within(0.001f));
            panel.ClearLoadedSlot(0);
            Assert.That(panel.SavedLoad, Is.EqualTo(8f).Within(0.001f));
            Assert.That(panel.ReservedLoad, Is.Zero);
            Assert.That(panel.BuildCargoSelections().Single().quantity, Is.EqualTo(8));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }



}
