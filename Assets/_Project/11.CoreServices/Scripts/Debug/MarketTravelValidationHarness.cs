#if ND_MARKET_SAVE_SCHEMA_VNEXT && (UNITY_EDITOR || DEVELOPMENT_BUILD)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ND.Framework;
using ND.Framework.CargoLoading;
using UnityEngine;
using FrameworkCargoEntrySaveData = ND.Framework.CargoEntrySaveData;
using FrameworkSaveData = ND.Framework.SaveData;
using FrameworkSaveResult = ND.Framework.SaveResult;
using FrameworkTradeItemSaveData = ND.Framework.TradeItemSaveData;

/// <summary>
/// Debug-only harness that exercises the real SaveData and Market transaction path.
/// It never reads or mutates CargoLoadingPanelController's private presentation state.
/// </summary>
public sealed class MarketTravelValidationHarness : MonoBehaviour
{
    [Serializable]
    private sealed class CargoSeed
    {
        public TradeItemData item;
        [Min(0)] public int quantity;
    }

    [Serializable]
    private sealed class TransactionSeed
    {
        public TradeItemData item;
        [Min(0)] public int buyQuantity;
        [Min(0)] public int sellQuantity;
    }

    [Serializable]
    private sealed class CargoDebugLine
    {
        public string itemId = string.Empty;
        public string itemName = string.Empty;
        public int quantity;
        public float unitWeight;
    }

    [Serializable]
    private sealed class MarketDebugLine
    {
        public string itemId = string.Empty;
        public string itemName = string.Empty;
        public int quantity;
        public long unitPrice;
    }

    [Header("Market session")]
    [SerializeField] private MarketData marketData;
    [SerializeField] private int worldSeed = 20260721;

    [Header("Test input")]
    [SerializeField] private CargoSeed[] cargoSeeds = Array.Empty<CargoSeed>();
    [SerializeField] private TransactionSeed[] transactionSeeds = Array.Empty<TransactionSeed>();
    [SerializeField, Min(0f)] private float maximumCargoWeight = 100f;

    [Header("Market change monitor")]
    [SerializeField] private bool monitorMarketChanges;
    [SerializeField, Min(0.25f)] private float monitorIntervalSeconds = 1f;
    [SerializeField] private bool observeFrameworkEvents = true;

    [Header("Inspector output (read only)")]
    [SerializeField] private string activeMarketId = string.Empty;
    [SerializeField] private int marketCatalogCount;
    [SerializeField] private int marketMaximumStock;
    [SerializeField] private float marketRefreshIntervalSeconds;
    [SerializeField] private long tradingCurrency;
    [SerializeField] private List<CargoDebugLine> cargo = new List<CargoDebugLine>();
    [SerializeField] private List<MarketDebugLine> marketStocks = new List<MarketDebugLine>();
    [SerializeField] private string checkpointName = string.Empty;
    [SerializeField] private long checkpointCurrency;
    [SerializeField] private List<CargoDebugLine> checkpointCargo = new List<CargoDebugLine>();

    [NonSerialized] private string originalSaveJson = string.Empty;
    private MarketInventoryMutationSession marketCommands;
    private MarketInventorySession marketView;
    private float nextMonitorTime;
    private bool originalCaptured;
    private bool cargoSeedSucceeded;
    private bool transactionSucceeded;

    private void OnEnable()
    {
        if (!observeFrameworkEvents)
            return;

        FrameworkEvents.InGameScreenChanged += OnInGameScreenChanged;
        FrameworkEvents.TradeSettlementReady += OnTradeSettlementReady;
    }

    private void OnDisable()
    {
        FrameworkEvents.InGameScreenChanged -= OnInGameScreenChanged;
        FrameworkEvents.TradeSettlementReady -= OnTradeSettlementReady;
    }

    private void Update()
    {
        if (!monitorMarketChanges || Time.realtimeSinceStartup < nextMonitorTime)
            return;

        nextMonitorTime = Time.realtimeSinceStartup + monitorIntervalSeconds;
        RefreshInspectorState(logMarketChanges: true);
    }

    [ContextMenu("Validation/Capture Original Save")]
    public void CaptureOriginalSave()
    {
        originalCaptured = false;
        FrameworkSaveData saveData = GetSaveData();
        if (saveData == null)
            return;

        originalSaveJson = JsonUtility.ToJson(saveData);
        File.WriteAllText(GetBackupPath(), originalSaveJson);
        originalCaptured = true;
        Debug.Log("[Market Validation] Original SaveData snapshot captured.", this);
    }

    [ContextMenu("Validation/Seed Test Cargo")]
    public void SeedTestCargo()
    {
        cargoSeedSucceeded = false;
        if (!TryOpenMarket())
            return;

        FrameworkSaveData saveData = GetSaveData();
        string rollbackJson = JsonUtility.ToJson(saveData);
        if (string.IsNullOrEmpty(originalSaveJson))
        {
            originalSaveJson = rollbackJson;
            File.WriteAllText(GetBackupPath(), originalSaveJson);
        }

        foreach (CargoSeed seed in cargoSeeds ?? Array.Empty<CargoSeed>())
        {
            if (seed?.item == null || string.IsNullOrWhiteSpace(seed.item.ItemId))
                continue;

            List<FrameworkCargoEntrySaveData> matches = saveData.caravan.cargo
                .Where(entry => entry?.item != null && entry.item.itemId == seed.item.ItemId)
                .ToList();
            foreach (FrameworkCargoEntrySaveData match in matches)
                saveData.caravan.cargo.Remove(match);

            if (seed.quantity <= 0)
                continue;

            saveData.caravan.cargo.Add(new FrameworkCargoEntrySaveData
            {
                quantity = seed.quantity,
                item = new FrameworkTradeItemSaveData
                {
                    itemId = seed.item.ItemId,
                    itemName = seed.item.DisplayName,
                    weight = seed.item.Weight,
                    basePrice = Math.Max(0L, seed.item.BaseBuyPrice),
                    maxCount = seed.item.MaxCount
                }
            });
        }

        FrameworkSaveResult result = FrameworkRoot.Instance.SaveService.Save(saveData);
        if (result == null || !result.Succeeded)
        {
            JsonUtility.FromJsonOverwrite(rollbackJson, saveData);
            Debug.LogError("[Market Validation] Test cargo seed save failed; state was restored.", this);
            return;
        }

        RefreshInspectorState(logMarketChanges: false);
        cargoSeedSucceeded = true;
        Debug.Log("[Market Validation] Test cargo seeded through SaveData.", this);
    }

    [ContextMenu("Validation/Execute Market Transaction")]
    public void ExecuteMarketTransaction()
    {
        transactionSucceeded = false;
        if (!TryOpenMarket())
            return;

        if (!TryBuildTransactionLines(out List<MarketTransactionLine> lines))
            return;

        MarketTransactionResult result = MarketTransactionCommand.Execute(
            marketCommands,
            lines,
            maximumCargoWeight);
        RefreshInspectorState(logMarketChanges: true);
        if (!result.Success)
        {
            Debug.LogError("[Market Validation] Transaction rejected: " + result.ErrorCode, this);
            return;
        }

        Debug.Log(
            $"[Market Validation] Transaction saved. Cost={result.PurchaseCost}, Revenue={result.SaleRevenue}, Currency={result.TradingCurrencyAfter}",
            this);
        transactionSucceeded = true;
    }

    [ContextMenu("Validation/Run Test Setup")]
    public void RunTestSetup()
    {
        FrameworkRoot root = FrameworkRoot.Instance;
        if (root == null || root.SharedGameData == null || !root.SharedGameData.IsLoaded)
        {
            Debug.LogError("[Market Validation] Test setup requires loaded SharedGameData.", this);
            return;
        }

        CaptureOriginalSave();
        if (!originalCaptured)
            return;

        SeedTestCargo();
        if (!cargoSeedSucceeded)
            return;

        ExecuteMarketTransaction();
        if (!transactionSucceeded)
            return;

        CaptureBeforeDeparture();
        Debug.Log("[Market Validation] Test setup completed. Start the trade through the existing UI.", this);
    }

    private bool TryBuildTransactionLines(out List<MarketTransactionLine> lines)
    {
        lines = new List<MarketTransactionLine>();
        var catalogIds = new HashSet<string>(
            marketData.TradeItems
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.ItemId))
                .Select(item => item.ItemId),
            StringComparer.Ordinal);
        var quantities = new Dictionary<string, MarketTransactionLine>(StringComparer.Ordinal);

        try
        {
            foreach (TransactionSeed seed in transactionSeeds ?? Array.Empty<TransactionSeed>())
            {
                if (seed?.item == null || string.IsNullOrWhiteSpace(seed.item.ItemId))
                    continue;

                string itemId = seed.item.ItemId;
                if (seed.buyQuantity < 0 || seed.sellQuantity < 0)
                {
                    Debug.LogError($"[Market Validation] {itemId}: quantity cannot be negative.", this);
                    return false;
                }

                if (seed.buyQuantity == 0 && seed.sellQuantity == 0)
                    continue;

                if (!catalogIds.Contains(itemId))
                {
                    Debug.LogError($"[Market Validation] {itemId}: item is not included in MarketData.TradeItems.", this);
                    return false;
                }

                if (!quantities.TryGetValue(itemId, out MarketTransactionLine line))
                {
                    line = new MarketTransactionLine { ItemId = itemId };
                    quantities.Add(itemId, line);
                }

                line.BuyQuantity = checked(line.BuyQuantity + seed.buyQuantity);
                line.SellQuantity = checked(line.SellQuantity + seed.sellQuantity);
            }
        }
        catch (OverflowException)
        {
            Debug.LogError("[Market Validation] Transaction quantities overflowed Int32.", this);
            return false;
        }

        foreach (MarketTransactionLine line in quantities.Values)
        {
            if (line.BuyQuantity > 0 && line.SellQuantity > 0)
            {
                Debug.LogError(
                    $"[Market Validation] {line.ItemId}: buy and sell cannot be requested in the same transaction.",
                    this);
                return false;
            }
        }

        lines = quantities.Values.ToList();
        if (lines.Count == 0)
        {
            Debug.LogError("[Market Validation] Add at least one positive buy or sell quantity to Transaction Seeds.", this);
            return false;
        }

        return true;
    }

    [ContextMenu("Validation/Capture Before Departure")]
    public void CaptureBeforeDeparture()
    {
        RefreshInspectorState(logMarketChanges: false);
        checkpointName = "BeforeDeparture";
        checkpointCurrency = tradingCurrency;
        checkpointCargo = CloneCargoLines(cargo);
        Debug.Log("[Market Validation] Before-departure checkpoint captured.", this);
    }

    [ContextMenu("Validation/Validate After Departure")]
    public void ValidateAfterDeparture()
    {
        ValidateAgainstCheckpoint("AfterDeparture");
    }

    [ContextMenu("Validation/Validate After Settlement Claim")]
    public void ValidateAfterSettlementClaim()
    {
        ValidateAgainstCheckpoint("AfterSettlementClaim");
    }

    [ContextMenu("Validation/Refresh Inspector State")]
    public void RefreshInspectorState()
    {
        RefreshInspectorState(logMarketChanges: true);
    }

    [ContextMenu("Validation/Restore Original Save")]
    public void RestoreOriginalSave()
    {
        FrameworkSaveData saveData = GetSaveData();
        if (saveData == null)
            return;

        if (string.IsNullOrEmpty(originalSaveJson) && File.Exists(GetBackupPath()))
            originalSaveJson = File.ReadAllText(GetBackupPath());

        if (string.IsNullOrEmpty(originalSaveJson))
        {
            Debug.LogWarning("[Market Validation] No original snapshot is available.", this);
            return;
        }

        JsonUtility.FromJsonOverwrite(originalSaveJson, saveData);
        FrameworkSaveResult result = FrameworkRoot.Instance.SaveService.Save(saveData);
        if (result == null || !result.Succeeded)
        {
            Debug.LogError("[Market Validation] Original snapshot restore could not be saved.", this);
            return;
        }

        marketCommands = null;
        marketView = null;
        if (File.Exists(GetBackupPath()))
            File.Delete(GetBackupPath());
        RefreshInspectorState(logMarketChanges: false);
        Debug.Log("[Market Validation] Original SaveData snapshot restored.", this);
    }

    private void ValidateAgainstCheckpoint(string phase)
    {
        RefreshInspectorState(logMarketChanges: false);
        if (checkpointCargo == null || string.IsNullOrEmpty(checkpointName))
        {
            Debug.LogWarning("[Market Validation] Capture a checkpoint before validation.", this);
            return;
        }

        var before = checkpointCargo.ToDictionary(line => line.itemId, line => line.quantity, StringComparer.Ordinal);
        var after = cargo.ToDictionary(line => line.itemId, line => line.quantity, StringComparer.Ordinal);
        foreach (string itemId in before.Keys.Union(after.Keys).OrderBy(value => value, StringComparer.Ordinal))
        {
            before.TryGetValue(itemId, out int beforeQuantity);
            after.TryGetValue(itemId, out int afterQuantity);
            int delta = afterQuantity - beforeQuantity;
            if (delta != 0)
                Debug.Log($"[Market Validation/{phase}] Cargo {itemId}: {beforeQuantity} -> {afterQuantity} (delta {delta:+#;-#;0})", this);
        }

        Debug.Log(
            $"[Market Validation/{phase}] Currency {checkpointCurrency} -> {tradingCurrency}. Cargo path comparison completed.",
            this);
    }

    private void OnInGameScreenChanged(InGameScreenState state)
    {
        if (!observeFrameworkEvents || string.IsNullOrEmpty(checkpointName))
            return;

        switch (state)
        {
            case InGameScreenState.Traveling:
                ValidateAgainstCheckpoint("AfterDeparture");
                break;
            case InGameScreenState.Settlement:
                ValidateAgainstCheckpoint("AtSettlement");
                break;
            case InGameScreenState.Town:
                ValidateAgainstCheckpoint("AfterSettlementClaim");
                FrameworkSaveData saveData = GetSaveData();
                Debug.Log(
                    "[Market Validation/AfterSettlementClaim] CurrentTownId="
                    + (saveData?.player?.currentTownId ?? string.Empty),
                    this);
                break;
        }
    }

    private void OnTradeSettlementReady(string tradeId, JourneyResultData result)
    {
        if (!observeFrameworkEvents || string.IsNullOrEmpty(checkpointName))
            return;

        Debug.Log(
            $"[Market Validation] Settlement ready. TradeId={tradeId}, Grade={result?.grade.ToString() ?? "None"}",
            this);
    }

    private void RefreshInspectorState(bool logMarketChanges)
    {
        if (!TryOpenMarket())
            return;

        List<MarketDebugLine> previousMarket = CloneMarketLines(marketStocks);
        tradingCurrency = marketView.TradingCurrency;
        cargo = marketView.SavedCargo
            .Where(line => line != null && line.Quantity > 0)
            .Select(line => new CargoDebugLine
            {
                itemId = line.ItemId,
                itemName = line.Item != null ? line.Item.DisplayName : line.ItemId,
                quantity = line.Quantity,
                unitWeight = line.Weight
            })
            .OrderBy(line => line.itemId, StringComparer.Ordinal)
            .ToList();
        marketStocks = marketView.Stocks
            .Where(stock => stock != null && stock.Item != null)
            .Select(stock => new MarketDebugLine
            {
                itemId = stock.Item.ItemId,
                itemName = stock.Item.DisplayName,
                quantity = stock.Quantity,
                unitPrice = stock.UnitPrice
            })
            .OrderBy(line => line.itemId, StringComparer.Ordinal)
            .ToList();

        if (logMarketChanges)
            LogMarketChanges(previousMarket, marketStocks);
    }

    private bool TryOpenMarket()
    {
        FrameworkRoot root = FrameworkRoot.Instance;
        if (root == null || root.CurrentSaveData == null || root.SaveService == null || root.GameTime == null)
        {
            Debug.LogWarning("[Market Validation] FrameworkRoot is not ready.", this);
            return false;
        }

        if (marketData == null)
        {
            Debug.LogError("[Market Validation] Assign a MarketData asset.", this);
            return false;
        }

        TradeItemData[] marketCatalog = marketData.TradeItems
            .Where(item => item != null)
            .ToArray();
        if (string.IsNullOrWhiteSpace(marketData.MarketId) || marketCatalog.Length == 0)
        {
            Debug.LogError("[Market Validation] MarketData requires a MarketId and at least one TradeItem.", this);
            return false;
        }

        activeMarketId = marketData.MarketId;
        marketCatalogCount = marketCatalog.Length;
        marketMaximumStock = Math.Max(1, marketData.ItemMaxQuantity);
        marketRefreshIntervalSeconds = Math.Max(1f, marketData.ItemRenewalCycle);
        if (!MarketInventoryMutationSession.TryOpen(
                root.CurrentSaveData,
                root.SaveService,
                root.GameTime,
                activeMarketId,
                marketCatalog,
                marketCatalogCount,
                marketMaximumStock,
                marketRefreshIntervalSeconds,
                worldSeed,
                out marketCommands,
                out string error))
        {
            Debug.LogError("[Market Validation] Market open failed: " + error, this);
            return false;
        }

        marketView = marketCommands.View;
        return true;
    }

    private FrameworkSaveData GetSaveData()
    {
        FrameworkRoot root = FrameworkRoot.Instance;
        if (root == null || root.CurrentSaveData == null)
        {
            Debug.LogWarning("[Market Validation] FrameworkRoot SaveData is not ready.", this);
            return null;
        }

        return root.CurrentSaveData;
    }

    private static string GetBackupPath()
    {
        return Path.Combine(Application.temporaryCachePath, "nd-market-validation-original-save.json");
    }

    private void LogMarketChanges(IReadOnlyList<MarketDebugLine> before, IReadOnlyList<MarketDebugLine> after)
    {
        var previous = (before ?? Array.Empty<MarketDebugLine>())
            .ToDictionary(line => line.itemId, line => line, StringComparer.Ordinal);
        foreach (MarketDebugLine current in after ?? Array.Empty<MarketDebugLine>())
        {
            if (!previous.TryGetValue(current.itemId, out MarketDebugLine old))
            {
                Debug.Log($"[Market Validation] Market item added: {current.itemId}, stock={current.quantity}, price={current.unitPrice}", this);
                continue;
            }

            if (old.unitPrice != current.unitPrice)
                Debug.Log($"[Market Validation] Price changed: {current.itemId}, {old.unitPrice} -> {current.unitPrice}", this);
            if (old.quantity != current.quantity)
                Debug.Log($"[Market Validation] Stock changed: {current.itemId}, {old.quantity} -> {current.quantity}", this);
        }
    }

    private static List<CargoDebugLine> CloneCargoLines(IEnumerable<CargoDebugLine> source)
    {
        return (source ?? Enumerable.Empty<CargoDebugLine>())
            .Select(line => new CargoDebugLine
            {
                itemId = line.itemId,
                itemName = line.itemName,
                quantity = line.quantity,
                unitWeight = line.unitWeight
            })
            .ToList();
    }

    private static List<MarketDebugLine> CloneMarketLines(IEnumerable<MarketDebugLine> source)
    {
        return (source ?? Enumerable.Empty<MarketDebugLine>())
            .Select(line => new MarketDebugLine
            {
                itemId = line.itemId,
                itemName = line.itemName,
                quantity = line.quantity,
                unitPrice = line.unitPrice
            })
            .ToList();
    }
}
#endif
