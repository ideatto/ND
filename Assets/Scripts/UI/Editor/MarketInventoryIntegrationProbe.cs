/*
 * Technical Ownership
 * - Responsible Area: UI / Market Inventory Integration
 *
 * Script Purpose
 * - Editor 시작 시 MarketInventorySession과 JsonSaveService 연동을 자동 검증한다.
 * - 결정적 재고 생성, draft/commit JSON round-trip, 환전·재고·취소 흐름을 Temp JSON 결과로 기록한다.
 *
 * Main Features
 * - VerifyDeterministicRefresh: 동일 seed/주기 재고 일치와 refreshIndex 갱신 검증
 * - VerifyJsonCurrencyCargoRoundTrip: draft/commit/reopen/cancel JSON 영속화 검증
 * - MemorySaveService: 디스크 없이 ISaveService 계약을 흉내 내는 in-memory 테스트 대역
 *
 * Usage for Team Members
 * - ND_MARKET_SAVE_SCHEMA_VNEXT define이 켜진 Editor에서 자동 실행된다.
 * - 실패 시 Temp/market-integration-test-result.json과 Console을 확인한다.
 * - 실행 후 persistentDataPath의 save_data.json은 원본으로 복원한다.
 *
 * Important Notes
 * - SessionState로 Editor 세션당 1회만 실행한다.
 * - MemorySaveService.Save는 메모리 참조만 갱신하고 항상 SaveResult.Success()를 반환한다.
 * - JsonSaveService 경로는 실제 save_data.json을 사용하므로 finally에서 원본을 복원한다.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ND.Framework;
using ND.Framework.CargoLoading;
using ND.UI.Market;
using UnityEditor;
using UnityEngine;
using FrameworkPlayerSaveData = ND.Framework.PlayerSaveData;
using FrameworkSaveData = ND.Framework.SaveData;

[InitializeOnLoad]
public static class MarketInventoryIntegrationProbe
{
    private const string SessionKey = "ND.MarketInventoryIntegrationProbe.20260722.v20";
    private const string ResultFileName = "market-integration-test-result.json";

    static MarketInventoryIntegrationProbe()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += Run;
    }

    private static void Run()
    {
        string resultPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", ResultFileName);
        string savePath = Path.Combine(Application.persistentDataPath, "save_data.json");
        bool hadOriginalSave = File.Exists(savePath);
        byte[] originalSave = hadOriginalSave ? File.ReadAllBytes(savePath) : null;
        var checks = new List<string>();

        try
        {
            TradeItemData[] catalog = CreateCatalog();
            VerifyDeterministicRefresh(catalog, checks);
            VerifyInventoryRefreshSaveFailureRollsBack(catalog, checks);
            VerifyDeltaTransactionAndRollback(catalog, checks);
            VerifyRejectedTransactionsDoNotMutate(catalog, checks);
            VerifyPanelDraftIsolation(catalog, checks);
            VerifyTownMarketAccessBoundaries(checks);
            VerifyTownMarketScreenTransitions(checks);
            VerifyCurrentTownMarketResolution(checks);
            VerifySavedCaravanCargoWeightLimit(checks);
            VerifyDestinationMarketCanSellCarriedForeignItem(catalog, checks);
            WriteResult(resultPath, true, checks, string.Empty);
            Debug.Log("Market inventory integration probe passed.");
        }
        catch (Exception exception)
        {
            WriteResult(resultPath, false, checks, exception.ToString());
            Debug.LogError("Market inventory integration probe failed: " + exception);
        }
        finally
        {
            try
            {
                if (hadOriginalSave)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(savePath));
                    File.WriteAllBytes(savePath, originalSave);
                }
                else if (File.Exists(savePath))
                {
                    File.Delete(savePath);
                }
            }
            catch (Exception restoreException)
            {
                Debug.LogError("Probe could not restore the original save file: " + restoreException);
            }
        }
    }

    private static void VerifyDeterministicRefresh(TradeItemData[] catalog, List<string> checks)
    {
        DateTime time = new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc);
        var firstSave = NewSave(1000L);
        var secondSave = NewSave(1000L);
        var firstService = new MemorySaveService(firstSave);
        var secondService = new MemorySaveService(secondSave);

        Assert(MarketInventoryMutationSession.TryOpen(
            firstSave, firstService, new FixedTimeProvider(time), "town-a", catalog,
            3, 10, 60d, 77, out MarketInventoryMutationSession first, out string firstError), firstError);
        Assert(MarketInventoryMutationSession.TryOpen(
            secondSave, secondService, new FixedTimeProvider(time), "town-a", catalog,
            3, 10, 60d, 77, out MarketInventoryMutationSession second, out string secondError), secondError);

        string firstSignature = Signature(first.View.Stocks);
        string secondSignature = Signature(second.View.Stocks);
        Assert(firstSignature == secondSignature, "Same town, period, and seed must generate the same inventory.");
        Assert(first.View.Stocks.Any(stock => IsFood(stock.Item)), "Generated inventory must contain food.");
        checks.Add("deterministic_generation_and_required_food");

        long previousRefresh = firstSave.world.marketInventories[0].refreshIndex;
        firstSave.world.marketPurchasePreparation.marketId = "town-a";
        firstSave.world.marketPurchasePreparation.isCommitted = true;
        firstSave.caravan.cargo.Add(CreateCargo(catalog[0], 1));
        Assert(MarketInventoryMutationSession.TryOpen(
            firstSave, firstService, new FixedTimeProvider(time.AddSeconds(61)), "town-a", catalog,
            3, 10, 60d, 77, out MarketInventoryMutationSession refreshed, out string refreshError), refreshError);
        long nextRefresh = firstSave.world.marketInventories[0].refreshIndex;
        Assert(nextRefresh > previousRefresh,
            "Elapsed refresh interval must advance refreshIndex even when a legacy purchase preparation remains in the save.");
        Assert(refreshed.View.Stocks.Count == 3, "Refreshed inventory must preserve configured slot count.");
        checks.Add("time_based_refresh_ignores_legacy_purchase_preparation");
    }

    private static void VerifyInventoryRefreshSaveFailureRollsBack(
        TradeItemData[] catalog,
        List<string> checks)
    {
        DateTime time = new DateTime(2026, 7, 22, 1, 0, 0, DateTimeKind.Utc);
        FrameworkSaveData save = NewSave(1000L);
        var service = new MemorySaveService(save);

        Assert(MarketInventoryMutationSession.TryOpen(
            save, service, new FixedTimeProvider(time), "town-refresh-rollback", catalog,
            3, 10, 60d, 78, out _, out string initialError), initialError);
        string beforeFailedRefresh = JsonUtility.ToJson(save.world.marketInventories[0]);

        service.FailSaves = true;
        Assert(!MarketInventoryMutationSession.TryOpen(
                save, service, new FixedTimeProvider(time.AddSeconds(61)), "town-refresh-rollback", catalog,
                3, 10, 60d, 78, out _, out string refreshError)
            && refreshError == MarketInventoryMutationSession.ErrorSaveFailed,
            "A failed inventory refresh save must reject opening the market with SAVE_FAILED.");
        Assert(JsonUtility.ToJson(save.world.marketInventories[0]) == beforeFailedRefresh,
            "A failed inventory refresh save must restore the previous market inventory.");

        FrameworkSaveData newSave = NewSave(1000L);
        var newService = new MemorySaveService(newSave) { FailSaves = true };
        Assert(!MarketInventoryMutationSession.TryOpen(
                newSave, newService, new FixedTimeProvider(time), "town-new-inventory-failure", catalog,
                3, 10, 60d, 79, out _, out string creationError)
            && creationError == MarketInventoryMutationSession.ErrorSaveFailed,
            "A failed initial inventory save must reject opening the market with SAVE_FAILED.");
        Assert(newSave.world.marketInventories.Count == 0,
            "A failed initial inventory save must remove the unsaved generated inventory.");
        checks.Add("inventory_refresh_save_failure_rolls_back");
    }

    private static FrameworkSaveData NewSave(long currency)
    {
        return new FrameworkSaveData
        {
            player = new FrameworkPlayerSaveData
            {
                currentTownId = "town-a",
                tradingCurrency = currency
            }
        };
    }

    private static void VerifyTownMarketAccessBoundaries(List<string> checks)
    {
        FrameworkSaveData save = NewSave(1000L);
        save.player.currentTownId = "river-town";
        save.tradeProgress.state = ND.Framework.TradeProgressState.Completed;
        var sharedData = new MarketAccessSharedData("river-town", "river-market");

        Assert(MarketTradePanelController.ValidateTownMarketAccess(
                save, sharedData, "river-market") == string.Empty,
            "Current town market must be accessible after settlement claim.");
        Assert(MarketTradePanelController.ValidateTownMarketAccess(
                save, sharedData, "other-market") == MarketTradePanelController.ErrorTownMarketMismatch,
            "A market that does not belong to the current town must be rejected.");

        save.tradeProgress.state = ND.Framework.TradeProgressState.Traveling;
        Assert(MarketTradePanelController.ValidateTownMarketAccess(
                save, sharedData, "river-market") == MarketTradePanelController.ErrorNotInTown,
            "Market access must be rejected while traveling.");
        checks.Add("town_market_access_state_and_id_validation");
    }

    private static void VerifyTownMarketScreenTransitions(List<string> checks)
    {
        Assert((int)InGameScreenState.Town == 3 && (int)InGameScreenState.Market == 4,
            "Market must remain appended after Town so existing serialized screen values do not change.");

        var router = new InGameScreenStateRouter();
        router.RequestScreen(InGameScreenState.Town);
        int openCalls = 0;

        Assert(!TownMarketScreenTransition.TryOpen(
                router.CurrentScreenState,
                () => { openCalls++; return false; },
                router),
            "A failed market panel open must reject the Town to Market transition.");
        Assert(router.CurrentScreenState == InGameScreenState.Town && openCalls == 1,
            "A failed market panel open must leave the screen in Town.");

        Assert(TownMarketScreenTransition.TryOpen(
                router.CurrentScreenState,
                () => { openCalls++; return true; },
                router),
            "A valid Town market entry must succeed.");
        Assert(router.CurrentScreenState == InGameScreenState.Market && openCalls == 2,
            "A valid Town market entry must request the Market screen exactly once.");
        Assert(!TownMarketScreenTransition.TryOpen(
                router.CurrentScreenState,
                () => { openCalls++; return true; },
                router) && openCalls == 2,
            "Market entry must be rejected when the current screen is not Town.");

        int closeCalls = 0;
        Assert(TownMarketScreenTransition.TryClose(
                router.CurrentScreenState,
                () => closeCalls++,
                router),
            "A valid Market return must succeed.");
        Assert(router.CurrentScreenState == InGameScreenState.Town && closeCalls == 1,
            "A valid Market return must close the panel and request Town exactly once.");
        Assert(!TownMarketScreenTransition.TryClose(
                router.CurrentScreenState,
                () => closeCalls++,
                router) && closeCalls == 1,
            "Market return must be rejected when the current screen is not Market.");
        Assert(TownTradePreparationEntryController.CanBeginFromScreen(InGameScreenState.Town),
            "Trade preparation entry must be allowed from the Town screen.");
        Assert(!TownTradePreparationEntryController.CanBeginFromScreen(InGameScreenState.Market),
            "Trade preparation entry must be blocked until the Market screen returns to Town.");

        FrameworkSaveData restoredSave = NewSave(1000L);
        restoredSave.tradeProgress.state = ND.Framework.TradeProgressState.Completed;
        Assert(InGameScreenStateRouter.MapFromSaveData(restoredSave) == InGameScreenState.Town,
            "SaveData restoration must map a settled current town to Town, never transient Market.");
        router.RequestScreen(InGameScreenState.Market);
        router.RefreshFromSaveData(restoredSave);
        Assert(router.CurrentScreenState == InGameScreenState.Town,
            "Refreshing transient Market from SaveData must restore the Town screen.");
        checks.Add("town_market_transient_screen_transitions");
    }

    private static void VerifyCurrentTownMarketResolution(List<string> checks)
    {
        FrameworkSaveData save = NewSave(1000L);
        save.player.currentTownId = "river-town";
        var sharedData = new MarketAccessSharedData("river-town", "river-market");
        MarketData other = CreateMarket("other-market");
        MarketData expected = CreateMarket("river-market");

        Assert(MarketTradePanelController.TryResolveCurrentTownMarket(
                save,
                sharedData,
                new[] { other, expected },
                out MarketData resolved,
                out string error),
            error);
        Assert(ReferenceEquals(resolved, expected),
            "Current town MarketId must resolve the matching MarketData asset.");

        save.player.currentTownId = "hill-town";
        var nextTownData = new MarketAccessSharedData("hill-town", "other-market");
        Assert(MarketTradePanelController.TryResolveCurrentTownMarket(
                save,
                nextTownData,
                new[] { other, expected },
                out MarketData nextResolved,
                out string nextError),
            nextError);
        Assert(ReferenceEquals(nextResolved, other),
            "Reopening after travel must resolve the newly current town's MarketData.");

        save.player.currentTownId = "river-town";

        Assert(!MarketTradePanelController.TryResolveCurrentTownMarket(
                save,
                sharedData,
                new[] { other },
                out _,
                out string missingError) &&
            missingError == MarketTradePanelController.ErrorMarketDataMissing,
            "Missing current-town MarketData must return a specific error.");
        checks.Add("current_town_market_data_resolution");
    }

    private static void VerifySavedCaravanCargoWeightLimit(List<string> checks)
    {
        FrameworkSaveData save = NewSave(1000L);
        Assert(Mathf.Approximately(
                MarketTradePanelController.ResolveMaximumCargoWeight(save),
                0f),
            "A caravan without an equipped wagon must have zero cargo capacity.");

        save.caravan.wagon.wagonName = "test-wagon";
        save.caravan.wagon.maxLoad = 73f;
        save.caravan.wagon.inventorySlotCount = 6;

        float result = MarketTradePanelController.ResolveMaximumCargoWeight(save);

        Assert(Mathf.Approximately(result, 73f),
            "Market cargo validation must use the saved caravan maximum load.");
        Assert(MarketTradePanelController.ResolveMaximumCargoSlots(save) == 6,
            "Market cargo validation must use the saved caravan slot limit.");
        checks.Add("saved_caravan_maximum_cargo_weight");
    }

    private static void VerifyDestinationMarketCanSellCarriedForeignItem(
        TradeItemData[] catalog,
        List<string> checks)
    {
        TradeItemData cloth = catalog.First(item => item.ItemId == "cloth");
        TradeItemData[] destinationStockCatalog = catalog
            .Where(item => item.ItemId != cloth.ItemId)
            .ToArray();
        FrameworkSaveData save = NewSave(1000L);
        save.caravan.cargo.Add(CreateCargo(cloth, 2));
        var service = new MemorySaveService(save);

        Assert(MarketInventoryMutationSession.TryOpen(
            save,
            service,
            new FixedTimeProvider(new DateTime(2026, 7, 22, 0, 0, 0, DateTimeKind.Utc)),
            "destination-market",
            destinationStockCatalog,
            catalog,
            destinationStockCatalog.Length,
            20,
            3600d,
            105,
            out MarketInventoryMutationSession session,
            out string error), error);
        Assert(session.View.Stocks.All(stock => stock.Item.ItemId != cloth.ItemId),
            "Foreign cargo must not be generated as destination market stock.");

        MarketTransactionResult result = MarketTransactionCommand.Execute(
            session,
            new[] { new MarketTransactionLine { ItemId = cloth.ItemId, SellQuantity = 1 } },
            100f,
            10);

        Assert(result.Success, "Destination market could not buy carried foreign cargo: " + result.ErrorCode);
        Assert(CargoQuantity(save, cloth.ItemId) == 1,
            "Selling foreign cargo must remove only the sold quantity.");
        checks.Add("destination_market_sells_carried_foreign_item");
    }

    private static void VerifyPanelDraftIsolation(TradeItemData[] catalog, List<string> checks)
    {
        FrameworkSaveData save = NewSave(1000L);
        TradeItemData cloth = catalog.First(item => item.ItemId == "cloth");
        save.caravan.cargo.Add(CreateCargo(cloth, 3));
        var service = new MemorySaveService(save);
        Assert(MarketInventoryMutationSession.TryOpen(
            save,
            service,
            new FixedTimeProvider(new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc)),
            "town-panel",
            catalog,
            4,
            20,
            3600d,
            20260721,
            out MarketInventoryMutationSession session,
            out string error), error);

        // Selling must remain possible even when the current caravan has no available
        // weight or slot capacity, because the transaction reduces rather than adds cargo.
        var model = new MarketTradePanelModel(session, 0f, 0);
        string beforeDraft = JsonUtility.ToJson(save);
        Assert(model.SetSellDraft("cloth", 2), "Panel model could not stage a valid sale.");
        Assert(JsonUtility.ToJson(save) == beforeDraft, "Panel draft must not mutate SaveData.");
        Assert(model.DraftPurchaseCost == 0L && model.DraftSaleRevenue == cloth.BaseSellPrice * 2L,
            "Panel draft preview money does not match the staged sale.");
        Assert(model.ProjectedTradingCurrency == 1000L + cloth.BaseSellPrice * 2L,
            "Panel draft preview currency does not match the staged sale.");
        Assert(Mathf.Approximately(model.CurrentCargoWeight, cloth.Weight * 3f) &&
            Mathf.Approximately(model.ProjectedCargoWeight, cloth.Weight),
            "Panel draft preview cargo weight does not match the staged sale.");
        Assert(model.CanCommit && string.IsNullOrEmpty(model.DraftValidationError),
            "A valid panel draft must be committable.");

        model.CancelDraft();
        Assert(!model.HasDraft, "Panel cancel must clear presentation draft.");
        Assert(JsonUtility.ToJson(save) == beforeDraft, "Panel cancel must preserve SaveData.");

        Assert(model.SetSellDraft("cloth", 2), "Panel model could not restage sale before commit.");
        MarketTransactionResult result = model.Commit();
        Assert(result.Success, "Panel model commit failed: " + result.ErrorCode);
        Assert(CargoQuantity(save, "cloth") == 1, "Panel commit must apply only the staged sale delta.");
        Assert(!model.HasDraft, "Successful panel commit must refresh with an empty draft.");

        FrameworkSaveData failingSave = NewSave(1000L);
        failingSave.caravan.cargo.Add(CreateCargo(cloth, 3));
        var failingService = new MemorySaveService(failingSave);
        Assert(MarketInventoryMutationSession.TryOpen(
            failingSave,
            failingService,
            new FixedTimeProvider(new DateTime(2026, 7, 21, 1, 0, 0, DateTimeKind.Utc)),
            "town-panel-failure",
            catalog,
            4,
            20,
            3600d,
            20260722,
            out MarketInventoryMutationSession failingSession,
            out string failingError), failingError);
        var failingModel = new MarketTradePanelModel(failingSession, 100f, 10);
        Assert(failingModel.SetSellDraft("cloth", 2),
            "Panel model could not stage a sale for save-failure validation.");
        string beforeFailedCommit = JsonUtility.ToJson(failingSave);
        failingService.FailSaves = true;

        MarketTransactionResult failed = failingModel.Commit();

        Assert(!failed.Success && failed.ErrorCode == MarketInventoryMutationSession.ErrorSaveFailed,
            "Panel commit must expose SAVE_FAILED to the UI.");
        Assert(JsonUtility.ToJson(failingSave) == beforeFailedCommit,
            "Panel commit save failure must restore SaveData.");
        Assert(failingModel.HasDraft &&
            failingModel.Items.First(item => item.ItemId == "cloth").SellDraftQuantity == 2,
            "Panel commit save failure must preserve the draft for retry.");

        failingService.FailSaves = false;
        MarketTransactionResult retried = failingModel.Commit();

        Assert(retried.Success, "A preserved panel draft must commit after save recovery: " + retried.ErrorCode);
        Assert(!failingModel.HasDraft, "A successful retry must clear the preserved draft.");
        Assert(CargoQuantity(failingSave, "cloth") == 1,
            "A successful retry must apply the preserved sale exactly once.");
        Assert(failingSave.player.tradingCurrency == 1000L + cloth.BaseSellPrice * 2L,
            "A successful retry must credit the preserved sale exactly once.");
        checks.Add("market_panel_draft_isolation_cancel_commit_and_failed_retry");
    }

    private static void VerifyDeltaTransactionAndRollback(TradeItemData[] catalog, List<string> checks)
    {
        DateTime time = new DateTime(2026, 7, 14, 2, 0, 0, DateTimeKind.Utc);
        FrameworkSaveData save = NewSave(1000L);
        save.caravan.cargo.Add(CreateCargo(catalog.First(item => item.ItemId == "cloth"), 3));
        save.caravan.cargo.Add(CreateCargo(catalog.First(item => item.ItemId == "bread"), 2));
        var service = new MemorySaveService(save);
        Assert(MarketInventoryMutationSession.TryOpen(
            save, service, new FixedTimeProvider(time), "town-delta", catalog,
            4, 20, 3600d, 101, out MarketInventoryMutationSession session, out string error), error);

        MarketStockView purchase = session.View.Stocks.First(stock => stock.Item.ItemId != "cloth");
        int purchaseStockBefore = purchase.Quantity;
        long expectedCurrency = 1000L - purchase.UnitPrice + catalog.First(item => item.ItemId == "cloth").BaseSellPrice * 2L;
        MarketTransactionResult result = MarketTransactionCommand.Execute(
            session,
            new[]
            {
                new MarketTransactionLine { ItemId = purchase.Item.ItemId, BuyQuantity = 1 },
                new MarketTransactionLine { ItemId = "cloth", SellQuantity = 2 }
            },
            100f);

        Assert(result.Success, "Delta transaction failed: " + result.ErrorCode);
        Assert(save.player.tradingCurrency == expectedCurrency, "Delta transaction currency mismatch.");
        Assert(CargoQuantity(save, "cloth") == 1, "Sale must remove only the sold quantity.");
        Assert(CargoQuantity(save, "bread") == 2, "Unrelated cargo must be preserved.");
        Assert(CargoQuantity(save, purchase.Item.ItemId) >= 1, "Purchase must add only the bought quantity.");
        Assert(session.View.Stocks.First(stock => stock.Item.ItemId == purchase.Item.ItemId).Quantity == purchaseStockBefore - 1,
            "Purchase must decrement market stock.");
        checks.Add("market_transaction_delta_preserves_unrelated_cargo");

        FrameworkSaveData failingSave = NewSave(1000L);
        failingSave.caravan.cargo.Add(CreateCargo(catalog.First(item => item.ItemId == "cloth"), 3));
        var failingService = new MemorySaveService(failingSave);
        Assert(MarketInventoryMutationSession.TryOpen(
            failingSave, failingService, new FixedTimeProvider(time), "town-rollback", catalog,
            4, 20, 3600d, 102, out MarketInventoryMutationSession failingSession, out string failingError), failingError);
        MarketStockView failingPurchase = failingSession.View.Stocks.First(stock => stock.Item.ItemId != "cloth");
        int failingStockBefore = failingPurchase.Quantity;
        failingService.FailSaves = true;
        MarketTransactionResult failed = MarketTransactionCommand.Execute(
            failingSession,
            new[] { new MarketTransactionLine { ItemId = failingPurchase.Item.ItemId, BuyQuantity = 1 } },
            100f);

        Assert(!failed.Success && failed.ErrorCode == MarketInventoryMutationSession.ErrorSaveFailed,
            "Save failure must be reported by the transaction command.");
        Assert(failingSave.player.tradingCurrency == 1000L, "Save failure must restore currency.");
        Assert(CargoQuantity(failingSave, "cloth") == 3 && CargoQuantity(failingSave, failingPurchase.Item.ItemId) == 0,
            "Save failure must restore cargo.");
        Assert(failingSession.View.Stocks.First(stock => stock.Item.ItemId == failingPurchase.Item.ItemId).Quantity == failingStockBefore,
            "Save failure must restore market stock.");
        checks.Add("market_transaction_save_failure_rolls_back");
    }

        private static CargoEntrySaveData CreateCargo(TradeItemData item, int quantity)
    {
        return new CargoEntrySaveData
        {
            quantity = quantity,
            item = new TradeItemSaveData
            {
                itemId = item.ItemId,
                itemName = item.DisplayName,
                weight = item.Weight,
                basePrice = item.BaseBuyPrice,
                maxCount = item.MaxCount
            }
        };
        }

        private static void VerifyRejectedTransactionsDoNotMutate(
            TradeItemData[] catalog,
            List<string> checks)
        {
            DateTime time = new DateTime(2026, 7, 14, 3, 0, 0, DateTimeKind.Utc);

            VerifyRejectedTransaction(
                catalog,
                time,
                0L,
                null,
                session =>
                {
                    MarketStockView stock = session.View.Stocks.First(value => value.Quantity > 0);
                    return new MarketTransactionLine { ItemId = stock.Item.ItemId, BuyQuantity = 1 };
                },
                float.PositiveInfinity,
                MarketInventoryMutationSession.ErrorCurrency);

            VerifyRejectedTransaction(
                catalog,
                time,
                1000L,
                null,
                session => new MarketTransactionLine { ItemId = "cloth", SellQuantity = 1 },
                float.PositiveInfinity,
                MarketInventoryMutationSession.ErrorInsufficientCargo);

            VerifyRejectedTransaction(
                catalog,
                time,
                1000L,
                null,
                session =>
                {
                    MarketStockView stock = session.View.Stocks.First();
                    return new MarketTransactionLine
                    {
                        ItemId = stock.Item.ItemId,
                        BuyQuantity = stock.Quantity + 1
                    };
                },
                float.PositiveInfinity,
                MarketInventoryMutationSession.ErrorInsufficientStock);

            VerifyRejectedTransaction(
                catalog,
                time,
                1000L,
                null,
                session =>
                {
                    MarketStockView stock = session.View.Stocks.First(value => value.Quantity > 0);
                    return new MarketTransactionLine { ItemId = stock.Item.ItemId, BuyQuantity = 1 };
                },
                0f,
                MarketInventoryMutationSession.ErrorCargoWeight);

            FrameworkSaveData slotSave = NewSave(1000L);
            TradeItemData cloth = catalog.First(item => item.ItemId == "cloth");
            slotSave.caravan.cargo.Add(CreateCargo(cloth, cloth.MaxCount));
            var slotService = new MemorySaveService(slotSave);
            Assert(MarketInventoryMutationSession.TryOpen(
                slotSave, slotService, new FixedTimeProvider(time), "town-slot-rejection",
                catalog, 4, 20, 3600d, 104,
                out MarketInventoryMutationSession slotSession, out string slotError), slotError);
            MarketStockView slotPurchase = slotSession.View.Stocks.First(stock => stock.Item.ItemId != "cloth");
            string slotBefore = JsonUtility.ToJson(slotSave);
            MarketTransactionResult slotResult = MarketTransactionCommand.Execute(
                slotSession,
                new[] { new MarketTransactionLine { ItemId = slotPurchase.Item.ItemId, BuyQuantity = 1 } },
                10000f,
                1);
            Assert(!slotResult.Success && slotResult.ErrorCode == MarketInventoryMutationSession.ErrorCargoSlots,
                "A purchase exceeding the saved wagon slot limit must be rejected.");
            Assert(JsonUtility.ToJson(slotSave) == slotBefore,
                "Slot-limit rejection must not mutate SaveData.");

            checks.Add("market_transaction_rejections_preserve_save_data");
        }

        private static void VerifyRejectedTransaction(
            TradeItemData[] catalog,
            DateTime time,
            long currency,
            CargoEntrySaveData cargo,
            Func<MarketInventoryMutationSession, MarketTransactionLine> createLine,
            float maximumCargoWeight,
            string expectedError)
        {
            FrameworkSaveData save = NewSave(currency);
            if (cargo != null)
            {
                save.caravan.cargo.Add(cargo);
            }

            var service = new MemorySaveService(save);
            Assert(MarketInventoryMutationSession.TryOpen(
                save, service, new FixedTimeProvider(time), "town-rejection-" + expectedError,
                catalog, 4, 20, 3600d, 103, out MarketInventoryMutationSession session, out string error), error);

            string before = JsonUtility.ToJson(save);
            MarketTransactionResult result = MarketTransactionCommand.Execute(
                session,
                new[] { createLine(session) },
                maximumCargoWeight);

            Assert(!result.Success && result.ErrorCode == expectedError,
                "Rejected transaction returned an unexpected result. Expected: " + expectedError
                + ", Actual: " + result.ErrorCode);
            Assert(JsonUtility.ToJson(save) == before,
                "Rejected transaction mutated SaveData: " + expectedError);
        }

        private static int CargoQuantity(FrameworkSaveData save, string itemId)
    {
        return save.caravan.cargo
            .Where(entry => entry?.item != null && entry.item.itemId == itemId)
            .Sum(entry => entry.quantity);
    }

    private static TradeItemData[] CreateCatalog()
    {
        return new[]
        {
            CreateItem("bread", "Bread", TradeItemCategory.Food, 10L, 1f),
            CreateItem("apple", "Apple", TradeItemCategory.Food, 12L, 1f),
            CreateItem("cloth", "Cloth", TradeItemCategory.Material, 30L, 2f),
            CreateItem("fish", "Fish", TradeItemCategory.Material, 20L, 2f),
            CreateItem("wheat", "Wheat", TradeItemCategory.Material, 8L, 1f)
        };
    }

    private static TradeItemData CreateItem(
        string id,
        string displayName,
        TradeItemCategory category,
        long buyPrice,
        float weight)
    {
        TradeItemData item = ScriptableObject.CreateInstance<TradeItemData>();
        SetField(item, "itemId", id);
        SetField(item, "displayName", displayName);
        SetField(item, "category", category);
        SetField(item, "baseBuyPrice", buyPrice);
        SetField(item, "baseSellPrice", buyPrice + 5L);
        SetField(item, "canStack", true);
        SetField(item, "maxCount", 99);
        SetField(item, "weight", weight);
        return item;
    }

    private static MarketData CreateMarket(string marketId)
    {
        MarketData market = ScriptableObject.CreateInstance<MarketData>();
        SetField(market, "marketId", marketId);
        return market;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().FullName, fieldName);
        field.SetValue(target, value);
    }

    private static string Signature(IReadOnlyList<MarketStockView> values)
    {
        return string.Join("|", values.Select(value =>
            value.Item.ItemId + ":" + value.Quantity + ":" + value.UnitPrice));
    }

    private static bool IsFood(TradeItemData item)
    {
        return item.Category == TradeItemCategory.Food
            || item.Category == TradeItemCategory.DraftAnimalsFood;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void WriteResult(string path, bool success, List<string> checks, string error)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var result = new ProbeResult
        {
            success = success,
            checks = checks.ToArray(),
            error = error ?? string.Empty,
            completedUtc = DateTime.UtcNow.ToString("O")
        };
        File.WriteAllText(path, JsonUtility.ToJson(result, true));
    }

    [Serializable]
    private sealed class ProbeResult
    {
        public bool success;
        public string[] checks;
        public string error;
        public string completedUtc;
    }

    private sealed class FixedTimeProvider : IGameTimeProvider
    {
        public FixedTimeProvider(DateTime currentUtc)
        {
            CurrentUtc = currentUtc;
        }

        public DateTime CurrentUtc { get; }
    }

    private sealed class MarketAccessSharedData : ISharedGameDataProvider
    {
        private readonly SharedTownDefinition town;

        public MarketAccessSharedData(string townId, string marketId)
        {
            town = new SharedTownDefinition { Id = townId, MarketId = marketId };
        }

        public bool IsLoaded => true;
        public string Summary => "Market access test data";
        public int TownCount => 1;
        public int MarketCount => 1;
        public int TradeItemCount => 0;
        public int WagonCount => 0;
        public int DraftAnimalCount => 0;
        public int RouteCount => 0;
        public IReadOnlyList<string> TownIds => new[] { town.Id };
        public IReadOnlyList<string> MarketIds => new[] { town.MarketId };
        public IReadOnlyList<string> TradeItemIds => Array.Empty<string>();
        public IReadOnlyList<string> WagonIds => Array.Empty<string>();
        public IReadOnlyList<string> DraftAnimalIds => Array.Empty<string>();
        public IReadOnlyList<string> RouteIds => Array.Empty<string>();

        public bool TryGetTown(string id, out SharedTownDefinition value)
        {
            value = string.Equals(id, town.Id, StringComparison.Ordinal) ? town : null;
            return value != null;
        }

        public bool TryGetMarket(string id, out SharedMarketDefinition value) { value = null; return false; }
        public bool TryGetTradeItem(string id, out SharedTradeItemDefinition value) { value = null; return false; }
        public bool TryGetWagon(string id, out SharedWagonDefinition value) { value = null; return false; }
        public bool TryGetDraftAnimal(string id, out SharedDraftAnimalDefinition value) { value = null; return false; }
        public bool TryGetRoute(string id, out SharedRouteDefinition value) { value = null; return false; }
    }

    /// <summary>
    /// 디스크 없이 SaveData 참조만 유지하는 ISaveService 테스트 대역이다.
    /// </summary>
    /// <remarks>
    /// Save(...)는 메모리 참조를 교체하고 항상 SaveResult.Success()를 반환한다.
    /// JsonSaveService의 실패 분기는 이 대역에서 검증하지 않는다.
    /// </remarks>
    private sealed class MemorySaveService : ISaveService
    {
        private FrameworkSaveData data;

        public MemorySaveService(FrameworkSaveData data)
        {
            this.data = data;
        }

        public bool FailSaves { get; set; }

        public bool HasSaveData() => data != null;
        public FrameworkSaveData CreateNewGameData() => data = NewSave(1000L);
        public FrameworkSaveData Load() => data;

        /// <returns>메모리 참조 갱신 후 항상 성공 결과.</returns>
        public SaveResult Save(FrameworkSaveData value)
        {
            if (FailSaves)
                return SaveResult.Failure(SaveFailureReason.WriteFailed, "Forced probe failure.", "marketTransaction");

            data = value;
            return SaveResult.Success();
        }

        public void ResetSaveData() => data = null;
    }
}
