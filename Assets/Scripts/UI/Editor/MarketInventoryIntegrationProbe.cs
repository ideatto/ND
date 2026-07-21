#if ND_MARKET_SAVE_SCHEMA_VNEXT
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
using UnityEditor;
using UnityEngine;
using FrameworkPlayerSaveData = ND.Framework.PlayerSaveData;
using FrameworkSaveData = ND.Framework.SaveData;

[InitializeOnLoad]
public static class MarketInventoryIntegrationProbe
{
    private const string SessionKey = "ND.MarketInventoryIntegrationProbe.20260714.v1";
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
            VerifyJsonCurrencyCargoRoundTrip(catalog, checks);
            VerifyDeltaTransactionAndRollback(catalog, checks);
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
        Assert(MarketInventoryMutationSession.TryOpen(
            firstSave, firstService, new FixedTimeProvider(time.AddSeconds(61)), "town-a", catalog,
            3, 10, 60d, 77, out MarketInventoryMutationSession refreshed, out string refreshError), refreshError);
        long nextRefresh = firstSave.world.marketInventories[0].refreshIndex;
        Assert(nextRefresh > previousRefresh, "Elapsed refresh interval must advance refreshIndex.");
        Assert(refreshed.View.Stocks.Count == 3, "Refreshed inventory must preserve configured slot count.");
        checks.Add("time_based_refresh");
    }

    private static void VerifyJsonCurrencyCargoRoundTrip(TradeItemData[] catalog, List<string> checks)
    {
        var saveService = new JsonSaveService();
        FrameworkSaveData save = NewSave(1000L);
        DateTime time = new DateTime(2026, 7, 14, 1, 0, 0, DateTimeKind.Utc);
        Assert(MarketInventoryMutationSession.TryOpen(
            save, saveService, new FixedTimeProvider(time), "town-json", catalog,
            4, 20, 3600d, 99, out MarketInventoryMutationSession session, out string openError), openError);

        MarketStockView selected = session.View.Stocks.First(stock => stock.Quantity >= 1);
        int quantity = Math.Min(2, selected.Quantity);
        var requests = new List<CargoPurchaseRequest>
        {
            new CargoPurchaseRequest
            {
                Item = selected.Item,
                Quantity = quantity,
                UnitPrice = selected.UnitPrice
            }
        };
        long expectedCost = selected.UnitPrice * quantity;
        int stockBefore = selected.Quantity;

        CargoIntegrationResult draft = session.PersistDraft(requests);
        Assert(draft.Success, "Draft persistence failed: " + draft.ErrorCode);
        FrameworkSaveData draftReloaded = saveService.Load();
        Assert(draftReloaded.caravan.cargo.Count == 1, "Draft cargo must be serialized.");
        Assert(draftReloaded.caravan.cargo[0].item.itemId == selected.Item.ItemId, "Cargo itemId must round-trip.");
        Assert(draftReloaded.caravan.cargo[0].quantity == quantity, "Cargo quantity must round-trip.");
        Assert(draftReloaded.player.tradingCurrency == 1000L, "Draft persistence must not debit currency.");
        checks.Add("draft_cargo_json_round_trip");

        CargoIntegrationResult committed = session.Commit(requests);
        Assert(committed.Success, "Commit failed: " + committed.ErrorCode);
        Assert(committed.TradingCurrencyAfter == 1000L - expectedCost, "CurrencyWallet purchase debit mismatch.");
        FrameworkSaveData committedReloaded = saveService.Load();
        MarketInventorySaveData market = committedReloaded.world.marketInventories
            .First(value => value.marketId == "town-json");
        MarketStockSaveData stock = market.stocks.First(value => value.itemId == selected.Item.ItemId);
        Assert(stock.quantity == stockBefore - quantity, "Committed stock was not decremented.");
        Assert(committedReloaded.world.marketPurchasePreparation.isCommitted, "Commit marker was not serialized.");
        Assert(committedReloaded.caravan.cargo[0].item.itemId == selected.Item.ItemId, "Committed cargo was not serialized.");
        checks.Add("currency_wallet_commit_and_market_stock_json");

        CargoIntegrationResult reopened = session.ReopenCommittedAsDraft(requests);
        Assert(reopened.Success, "Reopen failed: " + reopened.ErrorCode);
        Assert(reopened.TradingCurrencyAfter == 1000L, "Reopen must refund committed currency.");
        Assert(session.Stocks.First(value => value.Item.ItemId == selected.Item.ItemId).Quantity == stockBefore,
            "Reopen must restore market stock.");
        checks.Add("committed_purchase_reopen_refund");

        CargoIntegrationResult recommitted = session.Commit(requests);
        Assert(recommitted.Success, "Recommit failed: " + recommitted.ErrorCode);
        CargoIntegrationResult cancelled = session.CancelPreparation(requests);
        Assert(cancelled.Success, "Cancel failed: " + cancelled.ErrorCode);
        Assert(cancelled.TradingCurrencyAfter == 1000L, "Cancel must refund the committed purchase.");
        FrameworkSaveData cancelledReloaded = saveService.Load();
        Assert(cancelledReloaded.caravan.cargo.Count == 0, "Cancel must clear persisted cargo.");
        Assert(!cancelledReloaded.world.marketPurchasePreparation.isCommitted,
            "Cancel must clear the committed marker.");
        checks.Add("cancel_refund_and_cargo_clear");
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
        var failingService = new MemorySaveService(failingSave) { FailSaves = true };
        Assert(MarketInventoryMutationSession.TryOpen(
            failingSave, failingService, new FixedTimeProvider(time), "town-rollback", catalog,
            4, 20, 3600d, 102, out MarketInventoryMutationSession failingSession, out string failingError), failingError);
        MarketStockView failingPurchase = failingSession.View.Stocks.First(stock => stock.Item.ItemId != "cloth");
        int failingStockBefore = failingPurchase.Quantity;
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
#endif
