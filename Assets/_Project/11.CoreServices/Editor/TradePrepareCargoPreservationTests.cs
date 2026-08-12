#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FrameworkCargoEntrySaveData = ND.Framework.CargoEntrySaveData;
using FrameworkSaveData = ND.Framework.SaveData;
using FrameworkTradeItemSaveData = ND.Framework.TradeItemSaveData;

[InitializeOnLoad]
public sealed class TradePrepareCargoPreservationTests
{
    private const string ProbeSessionKey = "ND.TradePrepareCargoPreservationTests.20260721.v3";

    static TradePrepareCargoPreservationTests()
    {
        if (SessionState.GetBool(ProbeSessionKey, false))
            return;

        SessionState.SetBool(ProbeSessionKey, true);
        EditorApplication.delayCall += RunProbe;
    }

    private static void RunProbe()
    {
        try
        {
            var tests = new TradePrepareCargoPreservationTests();
            tests.CreateFinalCargoQuantities_IgnoresLegacyBuyDraftAndUsesSavedCargo();
            tests.CreateFinalCargoQuantities_LoadsEverySavedStackAndAggregatesDuplicateItems();
            tests.CreateFinalCargoQuantities_DoesNotDoubleCommittedCargoAfterDraftClears();
            tests.Create_PreservesSavedCargoMissingFromCurrentMarketCatalog();
            tests.Create_PreservesPurchasePriceGroupsWhenRuntimeCargoIsRebuilt();
            tests.Create_UsesLatestSavedCargoWhenDraftQuantityIsStale();
            tests.Create_DoesNotRestoreFeedAsDuplicateCargo();
            tests.ReplaceCargoPlan_PreservesPurchasePriceGroups();
            tests.ReplaceCargoPlan_EmptyPlanPublishesAuthoritativeTransition();
            tests.SelectingFourCaravans_DoesNotCarryPreviousCargo();
            tests.PurchaseDelta_ExcludesResidualCargoAndUsesCurrentPurchases();
            tests.PurchaseDelta_NoCurrentPurchaseCommitsZero();
            tests.PurchaseDelta_AccumulatesTransactionsAndPreservesPriceGroups();
            tests.PurchaseDelta_IsolatesCaravansAndClearsOnlyRequestedOwner();
            Debug.Log("Trade prepare cargo preservation probe passed (14/14).");
        }
        catch (Exception exception)
        {
            Debug.LogError("Trade prepare cargo preservation probe failed: " + exception);
        }
    }

    [Test]
    public void CreateFinalCargoQuantities_LoadsEverySavedStackAndAggregatesDuplicateItems()
    {
        FrameworkSaveData saveData = CreateSaveCargo("apple", 2);
        AddSaveCargo(saveData, "cloth", 4);
        AddSaveCargo(saveData, "apple", 3);

        var draft = new TradePrepareDraft();
        draft.selectedBuyItems.Add(new TradeItemBundle { itemId = "bread", quantity = 99 });

        Dictionary<string, int> result =
            TradePrepareCaravanFactory.CreateFinalCargoQuantities(draft, saveData);

        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result["apple"], Is.EqualTo(5));
        Assert.That(result["cloth"], Is.EqualTo(4));
        Assert.That(result.ContainsKey("bread"), Is.False);
    }

    [Test]
    public void CreateFinalCargoQuantities_IgnoresLegacyBuyDraftAndUsesSavedCargo()
    {
        FrameworkSaveData saveData = CreateSaveCargo("material", 3);
        var draft = new TradePrepareDraft();
        draft.selectedBuyItems.Add(new TradeItemBundle { itemId = "material", quantity = 2 });
        draft.selectedBuyItems.Add(new TradeItemBundle { itemId = "food", quantity = 1 });

        Dictionary<string, int> result =
            TradePrepareCaravanFactory.CreateFinalCargoQuantities(draft, saveData);

        Assert.That(result["material"], Is.EqualTo(3));
        Assert.That(result.ContainsKey("food"), Is.False);
    }

    [Test]
    public void CreateFinalCargoQuantities_DoesNotDoubleCommittedCargoAfterDraftClears()
    {
        FrameworkSaveData saveData = CreateSaveCargo("material", 5);

        Dictionary<string, int> result = TradePrepareCaravanFactory.CreateFinalCargoQuantities(
            new TradePrepareDraft(),
            saveData);

        Assert.That(result["material"], Is.EqualTo(5));
    }

    [Test]
    public void Create_PreservesSavedCargoMissingFromCurrentMarketCatalog()
    {
        FrameworkSaveData saveData = CreateSaveCargo("remote-material", 4);
        CaravanData result = TradePrepareCaravanFactory.CreatePreview(
            new TradePrepareDraft(),
            new TradePrepareBuildContext { saveData = saveData });

        Assert.That(result.cargo.Count, Is.EqualTo(1));
        Assert.That(result.cargo[0].item.id, Is.EqualTo("remote-material"));
        Assert.That(result.cargo[0].quantity, Is.EqualTo(4));
        Assert.That(result.cargo[0].item.weight, Is.EqualTo(2f));
    }

    [Test]
    public void Create_PreservesPurchasePriceGroupsWhenRuntimeCargoIsRebuilt()
    {
        FrameworkSaveData saveData = CreateSaveCargo("Bread", 3, 0L);
        AddSaveCargo(saveData, "Bread", 1, 50L);

        CaravanData result = TradePrepareCaravanFactory.CreatePreview(
            new TradePrepareDraft(),
            new TradePrepareBuildContext { saveData = saveData });

        Assert.That(result.cargo.Count, Is.EqualTo(2));
        Assert.That(result.cargo.Sum(entry => entry.quantity), Is.EqualTo(4));
        Assert.That(result.cargo.Single(entry => entry.item.purchaseUnitPrice == 0L).quantity,
            Is.EqualTo(3));
        Assert.That(result.cargo.Single(entry => entry.item.purchaseUnitPrice == 50L).quantity,
            Is.EqualTo(1));

        var roundTrip = new ND.Framework.CaravanSaveData();
        ND.Framework.CaravanSaveDataMapper.CopyToSave(result, roundTrip);
        Assert.That(roundTrip.cargo.Count, Is.EqualTo(2));
        Assert.That(roundTrip.cargo.Single(entry => entry.item.purchaseUnitPrice == 0L).quantity,
            Is.EqualTo(3));
        Assert.That(roundTrip.cargo.Single(entry => entry.item.purchaseUnitPrice == 50L).quantity,
            Is.EqualTo(1));
    }

    [Test]
    public void Create_UsesLatestSavedCargoWhenDraftQuantityIsStale()
    {
        FrameworkSaveData saveData = CreateSaveCargo("Bread", 3, 0L);
        AddSaveCargo(saveData, "Bread", 1, 50L);
        var draft = new TradePrepareDraft { hasAuthoritativeCargoPlan = true };
        draft.selectedBuyItems.Add(new TradeItemBundle
        {
            itemId = "Bread", quantity = 9, purchaseUnitPrice = 0L
        });

        CaravanData result = TradePrepareCaravanFactory.CreatePreview(
            draft,
            new TradePrepareBuildContext { saveData = saveData });

        Assert.That(result.cargo.Sum(entry => entry.quantity), Is.EqualTo(4));
        Assert.That(result.cargo.Single(entry => entry.item.purchaseUnitPrice == 0L).quantity,
            Is.EqualTo(3));
        Assert.That(result.cargo.Single(entry => entry.item.purchaseUnitPrice == 50L).quantity,
            Is.EqualTo(1));
    }

    [Test]
    public void Create_DoesNotRestoreFeedAsDuplicateCargo()
    {
        FrameworkSaveData saveData = CreateSaveCargo("Stover", 12, 1L);
        TradeItemData feed = ScriptableObject.CreateInstance<TradeItemData>();
        try
        {
            SetPrivateField(feed, "itemId", "Stover");
            SetPrivateField(feed, "displayName", "Stover");
            SetPrivateField(feed, "category", TradeItemCategory.DraftAnimalsFood);
            SetPrivateField(feed, "weight", 0.1f);
            SetPrivateField(feed, "maxCount", 99);

            CaravanData result = TradePrepareCaravanFactory.CreatePreview(
                new TradePrepareDraft(),
                new TradePrepareBuildContext
                {
                    saveData = saveData,
                    tradeItems = new[] { feed }
                });

            Assert.That(result.foodAmount, Is.EqualTo(12));
            Assert.That(result.cargo, Is.Empty);
            Assert.That(CaravanCalculator.GetCurrentLoad(result), Is.EqualTo(1.2f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(feed);
        }
    }

    private static void SetPrivateField<T>(TradeItemData target, string fieldName, T value)
    {
        typeof(TradeItemData).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(target, value);
    }

    private static FrameworkSaveData CreateSaveCargo(string itemId, int quantity)
    {
        var saveData = new FrameworkSaveData();
        AddSaveCargo(saveData, itemId, quantity);
        return saveData;
    }

    private static FrameworkSaveData CreateSaveCargo(string itemId, int quantity, long purchaseUnitPrice)
    {
        var saveData = new FrameworkSaveData();
        AddSaveCargo(saveData, itemId, quantity, purchaseUnitPrice);
        return saveData;
    }

    private static void AddSaveCargo(FrameworkSaveData saveData, string itemId, int quantity)
    {
        AddSaveCargo(saveData, itemId, quantity, 0L);
    }

    private static void AddSaveCargo(
        FrameworkSaveData saveData,
        string itemId,
        int quantity,
        long purchaseUnitPrice)
    {
        saveData.caravan.cargo.Add(new FrameworkCargoEntrySaveData
        {
            quantity = quantity,
            item = new FrameworkTradeItemSaveData
            {
                itemId = itemId,
                itemName = itemId,
                weight = 2f,
                purchaseUnitPrice = purchaseUnitPrice,
                basePrice = 10L,
                maxCount = 99
            }
        });
    }

    [Test]
    public void ReplaceCargoPlan_EmptyPlanPublishesAuthoritativeTransition()
    {
        var store = new TradePrepareDraftStore();
        store.Reset("town-a");
        store.SelectDepartureCaravan("caravan-2");

        int changed = 0;
        store.DraftChanged += _ => changed++;
        store.ReplaceCargoPlan(Array.Empty<CargoItemViewData>());

        Assert.That(changed, Is.EqualTo(1));
        Assert.That(store.Current.hasAuthoritativeCargoPlan, Is.True);
        Assert.That(store.Current.selectedBuyItems, Is.Empty);
    }

    [Test]
    public void ReplaceCargoPlan_PreservesPurchasePriceGroups()
    {
        var store = new TradePrepareDraftStore();
        store.Reset("town-a");
        store.SelectDepartureCaravan("caravan-1");

        store.ReplaceCargoPlan(new[]
        {
            new CargoItemViewData { itemId = "Bread", quantity = 3, purchaseUnitPrice = 0L },
            new CargoItemViewData { itemId = "Bread", quantity = 1, purchaseUnitPrice = 50L }
        });

        TradePrepareDraft snapshot = store.Current;
        Assert.That(snapshot.selectedBuyItems.Count, Is.EqualTo(2));
        Assert.That(snapshot.selectedBuyItems.Single(item => item.purchaseUnitPrice == 0L).quantity,
            Is.EqualTo(3));
        Assert.That(snapshot.selectedBuyItems.Single(item => item.purchaseUnitPrice == 50L).quantity,
            Is.EqualTo(1));
    }

    [Test]
    public void SelectingFourCaravans_DoesNotCarryPreviousCargo()
    {
        var store = new TradePrepareDraftStore();
        store.Reset("town-a");

        for (int index = 1; index <= 4; index++)
        {
            string caravanId = "caravan-" + index;
            store.SelectDepartureCaravan(caravanId);
            CargoItemViewData[] plan = index % 2 == 0
                ? Array.Empty<CargoItemViewData>()
                : new[]
                {
                    new CargoItemViewData
                    {
                        itemId = "item-" + index,
                        quantity = index
                    }
                };

            store.ReplaceCargoPlan(plan);
            TradePrepareDraft snapshot = store.Current;

            Assert.That(snapshot.departureCaravanId, Is.EqualTo(caravanId));
            Assert.That(snapshot.hasAuthoritativeCargoPlan, Is.True);
            Assert.That(snapshot.selectedBuyItems.Count, Is.EqualTo(plan.Length));
            if (plan.Length > 0)
            {
                Assert.That(snapshot.selectedBuyItems[0].itemId, Is.EqualTo("item-" + index));
                Assert.That(snapshot.selectedBuyItems[0].quantity, Is.EqualTo(index));
            }
        }
    }

    [Test]
    public void PurchaseDelta_ExcludesResidualCargoAndUsesCurrentPurchases()
    {
        var draft = new TradePrepareDraft { departureCaravanId = "caravan-a" };
        draft.selectedBuyItems.Add(new TradeItemBundle
        {
            itemId = "apple", quantity = 5, purchaseUnitPrice = 10L
        });
        var store = new TradePreparePurchaseDeltaStore();
        store.RecordPurchase("caravan-a", 24L, new[]
        {
            new TradeItemBundle { itemId = "apple", quantity = 2, purchaseUnitPrice = 12L }
        });

        TradePrepareCommitData commit = CreateCommit(draft, store, "caravan-a", 74L);

        Assert.That(commit.purchaseCost, Is.EqualTo(24L));
        Assert.That(commit.purchasedItems, Has.Length.EqualTo(1));
        Assert.That(commit.purchasedItems[0].quantity, Is.EqualTo(2));
        Assert.That(draft.selectedBuyItems[0].quantity, Is.EqualTo(5));
    }

    [Test]
    public void PurchaseDelta_NoCurrentPurchaseCommitsZero()
    {
        var draft = new TradePrepareDraft { departureCaravanId = "caravan-a" };
        draft.selectedBuyItems.Add(new TradeItemBundle
        {
            itemId = "apple", quantity = 5, purchaseUnitPrice = 10L
        });

        TradePrepareCommitData commit = CreateCommit(
            draft, new TradePreparePurchaseDeltaStore(), "caravan-a", 50L);

        Assert.That(commit.purchaseCost, Is.Zero);
        Assert.That(commit.purchasedItems, Is.Empty);
    }

    [Test]
    public void PurchaseDelta_AccumulatesTransactionsAndPreservesPriceGroups()
    {
        var store = new TradePreparePurchaseDeltaStore();
        store.RecordPurchase("caravan-a", 20L, new[]
        {
            new TradeItemBundle { itemId = "item", quantity = 2, purchaseUnitPrice = 10L }
        });
        store.RecordPurchase("caravan-a", 24L, new[]
        {
            new TradeItemBundle { itemId = "item", quantity = 2, purchaseUnitPrice = 12L }
        });
        store.RecordPurchase("caravan-a", 10L, new[]
        {
            new TradeItemBundle { itemId = "item", quantity = 1, purchaseUnitPrice = 10L }
        });

        Assert.That(store.TryGet("caravan-a", out long cost, out TradeItemBundle[] items), Is.True);
        Assert.That(cost, Is.EqualTo(54L));
        Assert.That(items, Has.Length.EqualTo(2));
        Assert.That(items.Single(item => item.purchaseUnitPrice == 10L).quantity, Is.EqualTo(3));
        Assert.That(items.Single(item => item.purchaseUnitPrice == 12L).quantity, Is.EqualTo(2));
    }

    [Test]
    public void PurchaseDelta_IsolatesCaravansAndClearsOnlyRequestedOwner()
    {
        var store = new TradePreparePurchaseDeltaStore();
        store.RecordPurchase("caravan-a", 100L, Array.Empty<TradeItemBundle>());
        store.RecordPurchase("caravan-b", 40L, Array.Empty<TradeItemBundle>());

        store.Clear("caravan-a");

        Assert.That(store.TryGet("caravan-a", out _, out _), Is.False);
        Assert.That(store.TryGet("caravan-b", out long cost, out _), Is.True);
        Assert.That(cost, Is.EqualTo(40L));
    }

    private static TradePrepareCommitData CreateCommit(
        TradePrepareDraft draft,
        TradePreparePurchaseDeltaStore store,
        string caravanId,
        long cargoValuation)
    {
        MethodInfo method = typeof(TradePrepareStartAdapter).GetMethod(
            "CreateCommitData",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return (TradePrepareCommitData)method.Invoke(null, new object[]
        {
            draft,
            new TradePrepareViewData { totalPurchaseCost = cargoValuation },
            "trade",
            "route",
            caravanId,
            store
        });
    }
}
#endif
