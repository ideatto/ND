#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
            Debug.Log("Trade prepare cargo preservation probe passed (4/4).");
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

    private static FrameworkSaveData CreateSaveCargo(string itemId, int quantity)
    {
        var saveData = new FrameworkSaveData();
        AddSaveCargo(saveData, itemId, quantity);
        return saveData;
    }

    private static void AddSaveCargo(FrameworkSaveData saveData, string itemId, int quantity)
    {
        saveData.caravan.cargo.Add(new FrameworkCargoEntrySaveData
        {
            quantity = quantity,
            item = new FrameworkTradeItemSaveData
            {
                itemId = itemId,
                itemName = itemId,
                weight = 2f,
                basePrice = 10L,
                maxCount = 99
            }
        });
    }
}
#endif
