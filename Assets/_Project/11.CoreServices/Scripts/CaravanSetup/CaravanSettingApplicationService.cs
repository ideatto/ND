using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// Production application boundary for Caravan setting queries and commands.
    /// SaveData, persistence, shared definitions, and Unity content assets are explicit inputs.
    /// </summary>
    public sealed class CaravanSettingApplicationService :
        ICaravanSettingViewDataProvider,
        ICaravanSettingCommand,
        ICaravanLoadSettingViewDataProvider,
        ICaravanLoadSettingCommand,
        ICaravanCargoCatalogProvider,
        ITradePrepareCaravanOptionProvider
    {
        private readonly Func<SaveData> getSaveData;
        private readonly ISaveService saveService;
        private readonly Func<ISharedGameDataProvider> getSharedGameData;
        private readonly CaravanSaveQueryService saveQueries;
        private readonly CaravanMarketCatalogService marketCatalog;
        private readonly CaravanSelectionOptionService selectionOptions;
        private readonly CaravanSavedCargoService savedCargo;
        private Func<string, TradeItemData> resolveTradeItemAsset;

        public CaravanSettingApplicationService(
            Func<SaveData> getSaveData,
            ISaveService saveService,
            Func<ISharedGameDataProvider> getSharedGameData,
            Func<string, TradeItemData> resolveTradeItemAsset = null,
            CaravanSaveQueryService saveQueries = null,
            CaravanMarketCatalogService marketCatalog = null,
            CaravanSelectionOptionService selectionOptions = null,
            CaravanSavedCargoService savedCargo = null)
        {
            this.getSaveData = getSaveData ?? throw new ArgumentNullException(nameof(getSaveData));
            this.saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
            this.getSharedGameData = getSharedGameData ?? throw new ArgumentNullException(nameof(getSharedGameData));
            this.resolveTradeItemAsset = resolveTradeItemAsset;
            this.saveQueries = saveQueries ?? new CaravanSaveQueryService();
            this.marketCatalog = marketCatalog ?? new CaravanMarketCatalogService();
            this.selectionOptions = selectionOptions ?? new CaravanSelectionOptionService();
            this.savedCargo = savedCargo ?? new CaravanSavedCargoService();
        }

        /// <summary>
        /// Supplies the Unity content-object mapper owned by the scene/content composition layer.
        /// SharedGameData remains authoritative for IDs, stock, prices, and weight.
        /// </summary>
        public void ConfigureTradeItemAssetResolver(Func<string, TradeItemData> resolver)
        {
            resolveTradeItemAsset = resolver;
        }

        public CaravanSettingViewData GetSetting(string caravanId)
        {
            if (!TryGet(caravanId, out CaravanSaveQueryResult query)) return null;
            CaravanSaveData caravan = query.Caravan;
            bool editable = caravan.state == JourneyState.Prepare;
            string blocked = editable ? string.Empty : "Caravan settings can only be changed during Preparation.";
            WagonSaveData wagon = caravan.wagon;
            var animals = caravan.animals ?? new List<AnimalSaveData>();
            var animalViews = new DraftAnimalViewData[animals.Count];
            var animalIds = new string[animals.Count];
            for (int i = 0; i < animals.Count; i++)
            {
                AnimalSaveData animal = animals[i] ?? new AnimalSaveData();
                animalIds[i] = Normalize(animal.instanceId);
                animalViews[i] = new DraftAnimalViewData
                {
                    draftAnimalId = string.Empty,
                    draftAnimalInstanceId = animalIds[i],
                    displayName = animal.animalName ?? string.Empty,
                    animalType = animal.animalType,
                    baseMoveSpeed = animal.speed,
                    feedConsumption = animal.foodPerKm,
                    increaseOverLoad = animal.increaseOverLoad,
                    increaseMaxLoad = animal.increaseMaxLoad,
                    ownedAmount = 1,
                    selectedAmount = 1,
                    maxSelectableAmount = 1,
                    isEligibleForSelectedWagon = true,
                    canSelect = editable,
                    disabledReason = blocked
                };
            }

            string wagonInstanceId = Normalize(wagon?.instanceId);
            WagonViewData[] wagons = string.IsNullOrEmpty(wagonInstanceId)
                ? Array.Empty<WagonViewData>()
                : new[]
                {
                    new WagonViewData
                    {
                        wagonId = string.Empty,
                        wagonInstanceId = wagonInstanceId,
                        displayName = wagon.wagonName ?? string.Empty,
                        currentDurability = caravan.currentDurability,
                        maxDurability = Math.Max(0, wagon.maxDurability),
                        overLoad = wagon.overLoad,
                        maxLoad = wagon.maxLoad,
                        inventorySlotCount = Math.Max(0, wagon.inventorySlotCount),
                        minRequireAnimals = Math.Max(0, wagon.minAnimals),
                        maxPullAnimals = Math.Max(0, wagon.maxAnimals),
                        ownedAmount = 1,
                        isOwned = true,
                        canSelect = editable,
                        disabledReason = blocked
                    }
                };

            return new CaravanSettingViewData
            {
                caravanId = query.CaravanId,
                caravanDisplayName = query.DisplayName,
                state = caravan.state,
                canEdit = editable,
                editBlockedReason = blocked,
                selectedWagonInstanceId = wagonInstanceId,
                selectedAnimalInstanceIds = animalIds,
                wagons = wagons,
                draftAnimals = animalViews
            };
        }

        public CaravanSettingCommandResult Execute(CaravanSettingDraft draft)
        {
            if (draft == null || !TryGet(draft.caravanId, out CaravanSaveQueryResult query))
                return CaravanSettingCommandResult.Failure(CaravanSettingFailureCodes.InvalidDraft, "The Caravan setting request is invalid.");
            if (query.State != JourneyState.Prepare)
                return CaravanSettingCommandResult.Failure(CaravanSettingFailureCodes.CaravanNotEditable, "Caravan settings can only be changed during Preparation.");

            CaravanSaveData caravan = query.Caravan;
            string savedWagonId = Normalize(caravan.wagon?.instanceId);
            if (!string.Equals(savedWagonId, Normalize(draft.selectedWagonInstanceId), StringComparison.Ordinal))
                return CaravanSettingCommandResult.Failure(CaravanSettingFailureCodes.AssetNotOwned, "Unassigned wagon inventory is not available yet.");

            var animalsById = new Dictionary<string, AnimalSaveData>(StringComparer.Ordinal);
            foreach (AnimalSaveData animal in caravan.animals ?? new List<AnimalSaveData>())
            {
                string id = Normalize(animal?.instanceId);
                if (string.IsNullOrEmpty(id) || animalsById.ContainsKey(id))
                    return CaravanSettingCommandResult.Failure(CaravanSettingFailureCodes.InvalidComposition, "Saved Caravan animals contain invalid instance IDs.");
                animalsById.Add(id, animal);
            }

            var reordered = new List<AnimalSaveData>();
            foreach (string rawId in draft.SelectedAnimalInstanceIds)
            {
                string id = Normalize(rawId);
                if (!animalsById.TryGetValue(id, out AnimalSaveData animal))
                    return CaravanSettingCommandResult.Failure(CaravanSettingFailureCodes.AssetNotOwned, "Unassigned animal inventory is not available yet.");
                animalsById.Remove(id);
                reordered.Add(animal);
            }
            if (animalsById.Count != 0)
                return CaravanSettingCommandResult.Failure(CaravanSettingFailureCodes.InvalidComposition, "All currently assigned animals must remain assigned.");

            return Persist(query.Caravan, () =>
            {
                caravan.animals.Clear();
                caravan.animals.AddRange(reordered);
            })
                ? CaravanSettingCommandResult.Success()
                : CaravanSettingCommandResult.Failure(CaravanSettingFailureCodes.SaveFailed, "The Caravan setting could not be saved.");
        }

        public CaravanLoadSettingViewData GetLoadSetting(string caravanId)
        {
            if (!TryGet(caravanId, out CaravanSaveQueryResult query)) return null;
            CaravanSaveData caravan = query.Caravan;
            bool editable = caravan.state == JourneyState.Prepare;
            CaravanMarketCatalogSnapshot catalog = ResolveCatalog(query.CaravanId);
            TradeItemViewData[] available = BuildAvailableItems(catalog);
            CargoItemViewData[] planned = BuildCargo(caravan, catalog);
            float load = 0f;
            long cost = 0L;
            foreach (CargoItemViewData item in planned) { load += item.totalWeight; cost = AddClamped(cost, item.totalPurchasePrice); }
            return new CaravanLoadSettingViewData
            {
                caravanId = query.CaravanId,
                caravanDisplayName = query.DisplayName,
                currentTownId = query.CurrentTownId,
                state = caravan.state,
                canEdit = editable,
                editBlockedReason = editable ? string.Empty : "Caravan cargo can only be changed during Preparation.",
                availableItems = available,
                plannedItems = planned,
                currentLoad = load,
                overloadLimit = Math.Max(0f, caravan.wagon?.maxLoad ?? 0f) * 0.8f,
                maxLoad = Math.Max(0f, caravan.wagon?.maxLoad ?? 0f),
                usedInventorySlotCount = planned.Length,
                maxInventorySlotCount = Math.Max(0, caravan.wagon?.inventorySlotCount ?? 0),
                totalPlannedPurchaseCost = cost,
                estimatedCurrencyAfterPurchase = Math.Max(0L, (getSaveData()?.player?.tradingCurrency ?? 0L) - cost)
            };
        }

        CaravanLoadSettingCommandResult ICaravanLoadSettingCommand.Execute(CaravanLoadSettingDraft draft)
        {
            if (draft == null || !TryGet(draft.caravanId, out CaravanSaveQueryResult query))
                return CaravanLoadSettingCommandResult.Failure(CaravanLoadSettingFailureCodes.InvalidDraft, "The Caravan cargo request is invalid.");
            if (query.State != JourneyState.Prepare)
                return CaravanLoadSettingCommandResult.Failure(CaravanLoadSettingFailureCodes.CaravanNotEditable, "Caravan cargo can only be changed during Preparation.");
            CaravanMarketCatalogSnapshot catalog = ResolveCatalog(query.CaravanId);
            if (catalog == null)
                return CaravanLoadSettingCommandResult.Failure(CaravanLoadSettingFailureCodes.ServiceUnavailable, "The Caravan market catalog is unavailable.");

            var next = new List<CargoEntrySaveData>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            float weight = 0f;
            foreach (CaravanLoadItemDraft item in draft.items ?? new List<CaravanLoadItemDraft>())
            {
                string id = Normalize(item?.itemId);
                if (item == null || item.quantity <= 0 || !ids.Add(id)
                    || !catalog.TryGetItem(id, out CaravanMarketCatalogItem found)
                    || item.quantity > found.Stock)
                    return CaravanLoadSettingCommandResult.Failure(CaravanLoadSettingFailureCodes.ItemUnavailable, "The cargo plan contains an unavailable item.");
                weight += Math.Max(0f, found.Definition.Weight) * item.quantity;
                next.Add(new CargoEntrySaveData
                {
                    item = new TradeItemSaveData
                    {
                        itemId = id,
                        itemName = found.Definition.DisplayName ?? id,
                        weight = Math.Max(0f, found.Definition.Weight),
                        purchaseUnitPrice = found.BuyUnitPrice,
                        basePrice = Math.Max(0L, found.Definition.BaseSellPrice),
                        maxCount = Math.Max(1, found.Definition.MaxCount)
                    },
                    quantity = item.quantity
                });
            }
            int slotLimit = Math.Max(0, query.Caravan.wagon?.inventorySlotCount ?? 0);
            float weightLimit = Math.Max(0f, query.Caravan.wagon?.maxLoad ?? 0f);
            if (next.Count > slotLimit || weight > weightLimit)
                return CaravanLoadSettingCommandResult.Failure(CaravanLoadSettingFailureCodes.CargoCapacityExceeded, "The Caravan cargo plan exceeds capacity.");

            return Persist(query.Caravan, () => query.Caravan.cargo = next)
                ? CaravanLoadSettingCommandResult.Success()
                : CaravanLoadSettingCommandResult.Failure(CaravanLoadSettingFailureCodes.SaveFailed, "The Caravan cargo plan could not be saved.");
        }

        public CaravanCargoCatalogData GetCargoCatalog(string caravanId)
        {
            CaravanMarketCatalogSnapshot catalog = ResolveCatalog(caravanId);
            if (catalog == null) return null;
            var assets = new List<TradeItemData>();
            var stocks = new List<int>();
            var prices = new List<long>();
            foreach (CaravanMarketCatalogItem item in catalog.Items)
            {
                TradeItemData asset = resolveTradeItemAsset?.Invoke(item.Definition.Id);
                if (asset == null) continue;
                assets.Add(asset); stocks.Add(item.Stock); prices.Add(item.BuyUnitPrice);
            }
            return new CaravanCargoCatalogData { items = assets.ToArray(), stocks = stocks.ToArray(), buyUnitPrices = prices.ToArray() };
        }

        public TradePrepareCaravanOptionViewData[] GetOptions() => selectionOptions.CreateOptions(getSaveData());

        private bool TryGet(string caravanId, out CaravanSaveQueryResult result) => saveQueries.TryGet(getSaveData(), caravanId, out result);

        private CaravanMarketCatalogSnapshot ResolveCatalog(string caravanId)
        {
            marketCatalog.TryResolve(getSaveData(), getSharedGameData(), caravanId, out CaravanMarketCatalogSnapshot result);
            return result;
        }

        private bool Persist(CaravanSaveData caravan, Action mutation)
        {
            SaveData data = getSaveData();
            if (data == null || caravan == null) return false;
            string snapshot = JsonUtility.ToJson(data);
            try
            {
                mutation();
                SaveResult result = saveService.Save(data);
                if (result != null && result.Succeeded) return true;
            }
            catch (Exception exception)
            {
                FrameworkLog.Error($"Caravan setting save failed: {exception.Message}");
            }
            JsonUtility.FromJsonOverwrite(snapshot, data);
            return false;
        }

        private TradeItemViewData[] BuildAvailableItems(CaravanMarketCatalogSnapshot catalog)
        {
            if (catalog == null) return Array.Empty<TradeItemViewData>();
            var result = new TradeItemViewData[catalog.Items.Count];
            for (int i = 0; i < result.Length; i++)
            {
                CaravanMarketCatalogItem item = catalog.Items[i];
                result[i] = new TradeItemViewData
                {
                    itemId = item.Definition.Id,
                    displayName = item.Definition.DisplayName,
                    icon = item.Definition.Icon,
                    description = item.Definition.Description,
                    purchasePrice = item.BuyUnitPrice,
                    sellPrice = item.Definition.BaseSellPrice,
                    contentQuantityLimit = item.Stock,
                    hasAuthoritativeStock = true,
                    unitWeight = item.Definition.Weight,
                    canBuy = item.Stock > 0
                };
            }
            return result;
        }

        private CargoItemViewData[] BuildCargo(CaravanSaveData caravan, CaravanMarketCatalogSnapshot catalog)
        {
            CaravanSavedCargoSnapshot snapshot = savedCargo.CreateSnapshot(caravan);
            var result = new CargoItemViewData[snapshot.Items.Count];
            for (int i = 0; i < result.Length; i++)
            {
                CaravanSavedCargoItem item = snapshot.Items[i];
                CaravanMarketCatalogItem marketItem = null;
                catalog?.TryGetItem(item.ItemId, out marketItem);
                TradeItemData asset = resolveTradeItemAsset?.Invoke(item.ItemId);
                TradeItemSaveData savedItem = item.SavedItem ?? new TradeItemSaveData();
                float unitWeight = marketItem?.Definition?.Weight ?? savedItem.weight;
                result[i] = new CargoItemViewData
                {
                    itemId = item.ItemId,
                    displayName = asset != null ? asset.DisplayName : marketItem?.Definition?.DisplayName ?? savedItem.itemName,
                    icon = asset != null ? asset.Icon : marketItem?.Definition?.Icon,
                    quantity = item.Quantity,
                    unitWeight = unitWeight,
                    totalWeight = unitWeight * item.Quantity,
                    purchaseUnitPrice = savedItem.purchaseUnitPrice,
                    estimatedSellUnitPrice = marketItem?.Definition?.BaseSellPrice ?? savedItem.basePrice,
                    totalPurchasePrice = MultiplyClamped(savedItem.purchaseUnitPrice, item.Quantity)
                };
            }
            return result;
        }

        private static string Normalize(string value) => value?.Trim() ?? string.Empty;
        private static long MultiplyClamped(long value, int quantity) => value <= 0 || quantity <= 0 ? 0L : value > long.MaxValue / quantity ? long.MaxValue : value * quantity;
        private static long AddClamped(long left, long right) => right > 0 && left > long.MaxValue - right ? long.MaxValue : left + right;
    }
}
