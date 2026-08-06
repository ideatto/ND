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
            SaveData save = getSaveData();
            ISharedGameDataProvider shared = getSharedGameData();
            WagonSaveData wagon = caravan.wagon;
            string wagonInstanceId = Normalize(wagon?.instanceId);
            string selectedWagonContentId = ResolveWagonContentId(wagon, shared);
            var wagonViews = new List<WagonViewData>();
            bool currentWagonInInventory = false;
            foreach (OwnedWagonSaveData owned in save?.player?.wagonInventory ?? new List<OwnedWagonSaveData>())
            {
                if (owned == null || string.IsNullOrWhiteSpace(owned.instanceId)) continue;
                bool selected = string.Equals(Normalize(owned.instanceId), wagonInstanceId, StringComparison.Ordinal);
                currentWagonInInventory |= selected;
                wagonViews.Add(CreateWagonView(Normalize(owned.instanceId), Normalize(owned.contentId),
                    selected ? caravan.currentDurability : owned.currentDurability, selected ? wagon : null,
                    shared, editable, blocked));
            }
            // Legacy saves may still keep the equipped instance outside the ownership inventory.
            if (!string.IsNullOrEmpty(wagonInstanceId) && !currentWagonInInventory)
                wagonViews.Add(CreateWagonView(wagonInstanceId, selectedWagonContentId, caravan.currentDurability, wagon, shared, editable, blocked));

            var animals = caravan.animals ?? new List<AnimalSaveData>();
            var animalIds = new string[animals.Count];
            var animalViews = new List<DraftAnimalViewData>();
            for (int i = 0; i < animals.Count; i++)
            {
                AnimalSaveData animal = animals[i];
                animalIds[i] = Normalize(animal?.instanceId);
            }
            foreach (OwnedDraftAnimalSaveData owned in save?.player?.draftAnimalInventory ?? new List<OwnedDraftAnimalSaveData>())
            {
                if (owned == null || string.IsNullOrWhiteSpace(owned.instanceId)) continue;
                string id = Normalize(owned.instanceId);
                AnimalSaveData selectedSave = animals.Find(value => string.Equals(Normalize(value?.instanceId), id, StringComparison.Ordinal));
                animalViews.Add(CreateAnimalView(id, Normalize(owned.contentId), selectedSave, shared, selectedWagonContentId, editable, blocked, selectedSave != null));
            }
            // Legacy saves may still keep equipped animals outside the ownership inventory.
            foreach (AnimalSaveData animal in animals)
                if (animal != null && !animalViews.Exists(view => string.Equals(view.draftAnimalInstanceId, Normalize(animal.instanceId), StringComparison.Ordinal)))
                    animalViews.Add(CreateAnimalView(Normalize(animal.instanceId), ResolveAnimalContentId(animal, shared), animal, shared, selectedWagonContentId, editable, blocked, true));

            return new CaravanSettingViewData
            {
                caravanId = query.CaravanId,
                caravanDisplayName = query.DisplayName,
                state = caravan.state,
                canEdit = editable,
                editBlockedReason = blocked,
                selectedWagonInstanceId = wagonInstanceId,
                selectedAnimalInstanceIds = animalIds,
                wagons = wagonViews.ToArray(),
                draftAnimals = animalViews.ToArray()
            };
        }

        public CaravanSettingCommandResult Execute(CaravanSettingDraft draft)
        {
            if (draft == null || !TryGet(draft.caravanId, out CaravanSaveQueryResult query))
                return CaravanSettingCommandResult.Failure(CaravanSettingFailureCodes.InvalidDraft, "The Caravan setting request is invalid.");
            if (query.State != JourneyState.Prepare)
                return CaravanSettingCommandResult.Failure(CaravanSettingFailureCodes.CaravanNotEditable, "Caravan settings can only be changed during Preparation.");

            SaveData save = getSaveData();
            CaravanSaveData caravan = query.Caravan;
            ISharedGameDataProvider shared = getSharedGameData();
            if (!TryBuildComposition(save, caravan, draft, shared, out WagonSaveData nextWagon,
                    out int nextDurability, out List<AnimalSaveData> nextAnimals,
                    out List<OwnedWagonSaveData> nextWagonInventory,
                    out List<OwnedDraftAnimalSaveData> nextAnimalInventory,
                    out string failureCode, out string error))
                return CaravanSettingCommandResult.Failure(failureCode, error);

            bool persisted = Persist(query.Caravan, () =>
            {
                save.player.wagonInventory = nextWagonInventory;
                save.player.draftAnimalInventory = nextAnimalInventory;
                caravan.wagon = nextWagon;
                caravan.currentDurability = nextDurability;
                caravan.animals = nextAnimals;
            });
            if (!persisted)
            {
                // Json rollback recreates list elements, so runtime indexes must discard old object references.
                FrameworkEvents.RaiseTransportInventoryChanged();
                return CaravanSettingCommandResult.Failure(CaravanSettingFailureCodes.SaveFailed, "The Caravan setting could not be saved.");
            }
            FrameworkEvents.RaiseTransportInventoryChanged();
            return CaravanSettingCommandResult.Success();
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

        private static bool TryBuildComposition(
            SaveData save,
            CaravanSaveData caravan,
            CaravanSettingDraft draft,
            ISharedGameDataProvider shared,
            out WagonSaveData nextWagon,
            out int nextDurability,
            out List<AnimalSaveData> nextAnimals,
            out List<OwnedWagonSaveData> nextWagons,
            out List<OwnedDraftAnimalSaveData> nextOwnedAnimals,
            out string failureCode,
            out string error)
        {
            nextWagon = null;
            nextDurability = 0;
            nextAnimals = null;
            nextWagons = null;
            nextOwnedAnimals = null;
            failureCode = CaravanSettingFailureCodes.InvalidComposition;
            error = string.Empty;
            if (save?.player == null || caravan == null || shared == null)
            {
                error = "Transport inventory or content data is unavailable.";
                return false;
            }

            var wagonCandidates = new Dictionary<string, OwnedWagonSaveData>(StringComparer.Ordinal);
            var animalCandidates = new Dictionary<string, OwnedDraftAnimalSaveData>(StringComparer.Ordinal);
            var usedByOtherCaravan = new HashSet<string>(StringComparer.Ordinal);
            foreach (CaravanSaveData other in save.caravans ?? new List<CaravanSaveData>())
            {
                if (other == null || ReferenceEquals(other, caravan)) continue;
                string otherWagonId = Normalize(other.wagon?.instanceId);
                if (!string.IsNullOrEmpty(otherWagonId)) usedByOtherCaravan.Add(otherWagonId);
                foreach (AnimalSaveData otherAnimal in other.animals ?? new List<AnimalSaveData>())
                {
                    string otherAnimalId = Normalize(otherAnimal?.instanceId);
                    if (!string.IsNullOrEmpty(otherAnimalId)) usedByOtherCaravan.Add(otherAnimalId);
                }
            }
            string currentWagonId = Normalize(caravan.wagon?.instanceId);
            foreach (OwnedWagonSaveData owned in save.player.wagonInventory ?? new List<OwnedWagonSaveData>())
            {
                string id = Normalize(owned?.instanceId);
                string contentId = Normalize(owned?.contentId);
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(contentId)
                    || !shared.TryGetWagon(contentId, out _) || !wagonCandidates.TryAdd(id, CopyOwnedWagon(owned)))
                {
                    error = "Wagon inventory contains invalid or duplicate identity data.";
                    return false;
                }
            }
            if (!string.IsNullOrEmpty(currentWagonId))
            {
                string contentId = ResolveWagonContentId(caravan.wagon, shared);
                if (string.IsNullOrEmpty(contentId))
                {
                    error = "The assigned wagon has invalid identity or content data.";
                    return false;
                }
                if (wagonCandidates.TryGetValue(currentWagonId, out OwnedWagonSaveData currentOwned))
                {
                    if (!string.Equals(currentOwned.contentId, contentId, StringComparison.Ordinal))
                    {
                        error = "The assigned wagon conflicts with the owned inventory identity data.";
                        return false;
                    }
                    currentOwned.currentDurability = Math.Max(0, caravan.currentDurability);
                }
                else
                    wagonCandidates.Add(currentWagonId, new OwnedWagonSaveData
                    {
                        instanceId = currentWagonId,
                        contentId = contentId,
                        currentDurability = Math.Max(0, caravan.currentDurability)
                    });
            }

            foreach (OwnedDraftAnimalSaveData owned in save.player.draftAnimalInventory ?? new List<OwnedDraftAnimalSaveData>())
            {
                string id = Normalize(owned?.instanceId);
                string contentId = Normalize(owned?.contentId);
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(contentId)
                    || !shared.TryGetDraftAnimal(contentId, out _) || !animalCandidates.TryAdd(id, CopyOwnedAnimal(owned)))
                {
                    error = "Draft-animal inventory contains invalid or duplicate identity data.";
                    return false;
                }
            }
            var assignedAnimalIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (AnimalSaveData animal in caravan.animals ?? new List<AnimalSaveData>())
            {
                string id = Normalize(animal?.instanceId);
                string contentId = ResolveAnimalContentId(animal, shared);
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(contentId) || !assignedAnimalIds.Add(id))
                {
                    error = "The assigned draft animals contain invalid or duplicate identity data.";
                    return false;
                }
                if (animalCandidates.TryGetValue(id, out OwnedDraftAnimalSaveData ownedAnimal))
                {
                    if (!string.Equals(ownedAnimal.contentId, contentId, StringComparison.Ordinal))
                    {
                        error = "An assigned draft animal conflicts with the owned inventory identity data.";
                        return false;
                    }
                }
                else
                    animalCandidates.Add(id, new OwnedDraftAnimalSaveData { instanceId = id, contentId = contentId });
            }
            foreach (string wagonId in wagonCandidates.Keys)
            {
                if (!animalCandidates.ContainsKey(wagonId)) continue;
                error = "A transport instance ID is shared by both a wagon and a draft animal.";
                return false;
            }

            string selectedWagonId = Normalize(draft.selectedWagonInstanceId);
            OwnedWagonSaveData selectedWagon = null;
            SharedWagonDefinition wagonDefinition = null;
            if (!string.IsNullOrEmpty(selectedWagonId)
                && (!wagonCandidates.TryGetValue(selectedWagonId, out selectedWagon)
                    || usedByOtherCaravan.Contains(selectedWagonId)
                    || !shared.TryGetWagon(selectedWagon.contentId, out wagonDefinition)))
            {
                error = "The selected wagon is not owned or its content no longer exists.";
                return false;
            }

            var selectedAnimals = new List<OwnedDraftAnimalSaveData>();
            if (draft.AnimalRequests.Count > 0)
            {
                if (!TryResolveRequestedAnimals(caravan, draft.AnimalRequests, animalCandidates,
                        save.player.draftAnimalInventory, usedByOtherCaravan, shared,
                        wagonDefinition, selectedAnimals, out error))
                    return false;
            }
            else
            {
                var selectedAnimalIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (string rawId in draft.SelectedAnimalInstanceIds)
                {
                    string id = Normalize(rawId);
                    if (string.IsNullOrEmpty(selectedWagonId) || !selectedAnimalIds.Add(id)
                        || !animalCandidates.TryGetValue(id, out OwnedDraftAnimalSaveData selected)
                        || usedByOtherCaravan.Contains(id)
                        || !shared.TryGetDraftAnimal(selected.contentId, out SharedDraftAnimalDefinition animalDefinition)
                        || !IsAnimalEligible(wagonDefinition, animalDefinition))
                    {
                        error = "The selected draft-animal composition is invalid, unavailable, or incompatible with the wagon.";
                        return false;
                    }
                    selectedAnimals.Add(selected);
                }
            }
            if (wagonDefinition != null
                && (selectedAnimals.Count < Math.Max(0, wagonDefinition.MinRequireAnimals)
                    || selectedAnimals.Count > Math.Max(0, wagonDefinition.MaxPullAnimals)))
            {
                error = "The selected draft-animal count is outside the wagon's allowed range.";
                return false;
            }
            if (!ValidateExistingCargo(caravan.cargo, wagonDefinition, out error))
            {
                failureCode = CaravanSettingFailureCodes.CargoCapacityExceeded;
                return false;
            }

            nextWagons = new List<OwnedWagonSaveData>();
            foreach (OwnedWagonSaveData candidate in wagonCandidates.Values)
                nextWagons.Add(CopyOwnedWagon(candidate));
            nextOwnedAnimals = new List<OwnedDraftAnimalSaveData>();
            foreach (OwnedDraftAnimalSaveData candidate in animalCandidates.Values)
                nextOwnedAnimals.Add(CopyOwnedAnimal(candidate));

            nextWagon = selectedWagon == null ? new WagonSaveData() : CreateWagonSave(selectedWagon, wagonDefinition);
            nextDurability = selectedWagon == null ? 0 : Math.Min(Math.Max(0, selectedWagon.currentDurability), Math.Max(0, wagonDefinition.MaxDurability));
            nextAnimals = new List<AnimalSaveData>(selectedAnimals.Count);
            foreach (OwnedDraftAnimalSaveData selected in selectedAnimals)
            {
                shared.TryGetDraftAnimal(selected.contentId, out SharedDraftAnimalDefinition definition);
                nextAnimals.Add(CreateAnimalSave(selected, definition));
            }
            return true;
        }

        private static bool ValidateExistingCargo(
            IReadOnlyList<CargoEntrySaveData> cargo,
            SharedWagonDefinition wagon,
            out string error)
        {
            error = string.Empty;
            int usedSlots = 0;
            float totalWeight = 0f;
            foreach (CargoEntrySaveData entry in cargo ?? Array.Empty<CargoEntrySaveData>())
            {
                if (entry?.item == null || entry.quantity <= 0 || string.IsNullOrWhiteSpace(entry.item.itemId))
                {
                    error = "The existing cargo contains invalid save data.";
                    return false;
                }
                usedSlots++;
                totalWeight += Math.Max(0f, entry.item.weight) * entry.quantity;
            }

            if (usedSlots == 0) return true;
            if (wagon == null
                || usedSlots > Math.Max(0, wagon.InventorySlotCount)
                || totalWeight > Math.Max(0f, wagon.MaxLoad))
            {
                error = "The existing cargo exceeds the selected wagon capacity.";
                return false;
            }
            return true;
        }

        private static bool TryResolveRequestedAnimals(
            CaravanSaveData caravan,
            IReadOnlyList<CaravanDraftAnimalRequest> requests,
            IReadOnlyDictionary<string, OwnedDraftAnimalSaveData> candidates,
            IReadOnlyList<OwnedDraftAnimalSaveData> inventoryOrder,
            ISet<string> usedByOtherCaravan,
            ISharedGameDataProvider shared,
            SharedWagonDefinition wagonDefinition,
            ICollection<OwnedDraftAnimalSaveData> result,
            out string error)
        {
            error = string.Empty;
            if (wagonDefinition == null)
            {
                error = "Draft animals cannot be assigned without a wagon.";
                return false;
            }

            var requestedCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (CaravanDraftAnimalRequest request in requests)
            {
                string contentId = Normalize(request?.contentId);
                if (string.IsNullOrEmpty(contentId) || request.quantity <= 0
                    || requestedCounts.ContainsKey(contentId)
                    || !shared.TryGetDraftAnimal(contentId, out SharedDraftAnimalDefinition definition)
                    || !IsAnimalEligible(wagonDefinition, definition))
                {
                    error = "The requested draft-animal composition is invalid or incompatible with the wagon.";
                    return false;
                }
                requestedCounts.Add(contentId, request.quantity);
            }

            var selectedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (AnimalSaveData assigned in caravan.animals ?? new List<AnimalSaveData>())
                TryTakeRequestedAnimal(assigned?.instanceId, candidates, usedByOtherCaravan, requestedCounts, selectedIds, result);
            foreach (OwnedDraftAnimalSaveData candidate in inventoryOrder ?? Array.Empty<OwnedDraftAnimalSaveData>())
                TryTakeRequestedAnimal(candidate?.instanceId, candidates, usedByOtherCaravan, requestedCounts, selectedIds, result);

            foreach (int remaining in requestedCounts.Values)
            {
                if (remaining == 0) continue;
                error = "The requested draft-animal quantity is not available.";
                return false;
            }
            return true;
        }

        private static void TryTakeRequestedAnimal(
            string rawInstanceId,
            IReadOnlyDictionary<string, OwnedDraftAnimalSaveData> candidates,
            ISet<string> usedByOtherCaravan,
            IDictionary<string, int> requestedCounts,
            ISet<string> selectedIds,
            ICollection<OwnedDraftAnimalSaveData> result)
        {
            string instanceId = Normalize(rawInstanceId);
            if (string.IsNullOrEmpty(instanceId) || usedByOtherCaravan.Contains(instanceId)
                || selectedIds.Contains(instanceId) || !candidates.TryGetValue(instanceId, out OwnedDraftAnimalSaveData candidate)
                || !requestedCounts.TryGetValue(candidate.contentId, out int remaining) || remaining <= 0)
                return;

            selectedIds.Add(instanceId);
            requestedCounts[candidate.contentId] = remaining - 1;
            result.Add(candidate);
        }

        private static WagonViewData CreateWagonView(string instanceId, string contentId, int durability, WagonSaveData saved,
            ISharedGameDataProvider shared, bool editable, string blocked)
        {
            SharedWagonDefinition definition = null;
            if (shared != null) shared.TryGetWagon(contentId, out definition);
            return new WagonViewData
            {
                wagonId = contentId,
                wagonInstanceId = instanceId,
                displayName = definition?.DisplayName ?? saved?.wagonName ?? contentId,
                currentDurability = Math.Max(0, durability),
                maxDurability = Math.Max(0, definition?.MaxDurability ?? saved?.maxDurability ?? 0),
                overLoad = definition?.BaseEfficientLoad ?? saved?.overLoad ?? 0f,
                maxLoad = definition?.MaxLoad ?? saved?.maxLoad ?? 0f,
                inventorySlotCount = Math.Max(0, definition?.InventorySlotCount ?? saved?.inventorySlotCount ?? 0),
                minRequireAnimals = Math.Max(0, definition?.MinRequireAnimals ?? saved?.minAnimals ?? 0),
                maxPullAnimals = Math.Max(0, definition?.MaxPullAnimals ?? saved?.maxAnimals ?? 0),
                ownedAmount = 1,
                isOwned = true,
                canSelect = editable && definition != null,
                disabledReason = definition == null ? "Wagon content is unavailable." : blocked
            };
        }

        private static DraftAnimalViewData CreateAnimalView(string instanceId, string contentId, AnimalSaveData saved,
            ISharedGameDataProvider shared, string selectedWagonContentId, bool editable, string blocked, bool selected)
        {
            SharedDraftAnimalDefinition definition = null;
            SharedWagonDefinition wagon = null;
            if (shared != null)
            {
                shared.TryGetDraftAnimal(contentId, out definition);
                shared.TryGetWagon(selectedWagonContentId, out wagon);
            }
            bool eligible = definition != null && IsAnimalEligible(wagon, definition);
            return new DraftAnimalViewData
            {
                draftAnimalId = contentId,
                draftAnimalInstanceId = instanceId,
                displayName = definition?.DisplayName ?? saved?.animalName ?? contentId,
                animalType = ParseAnimalType(definition?.AnimalType, saved?.animalType ?? default),
                baseMoveSpeed = definition?.BaseMoveSpeed ?? saved?.speed ?? 0f,
                feedConsumption = definition?.FoodConsumptionPerSecond ?? saved?.foodPerKm ?? 0f,
                increaseOverLoad = definition?.AdditionalEfficientLoad ?? saved?.increaseOverLoad ?? 0f,
                increaseMaxLoad = saved?.increaseMaxLoad ?? 0f,
                ownedAmount = 1,
                selectedAmount = selected ? 1 : 0,
                maxSelectableAmount = 1,
                isEligibleForSelectedWagon = eligible,
                canSelect = editable && eligible,
                disabledReason = definition == null ? "Draft-animal content is unavailable." : eligible ? blocked : "This animal is not eligible for the selected wagon."
            };
        }

        private static bool IsAnimalEligible(SharedWagonDefinition wagon, SharedDraftAnimalDefinition animal)
        {
            if (wagon == null || animal == null || wagon.MaxPullAnimals <= 0) return false;
            string[] eligible = wagon.EligibleAnimalTypes ?? Array.Empty<string>();
            for (int i = 0; i < eligible.Length; i++)
                if (string.Equals(Normalize(eligible[i]), Normalize(animal.AnimalType), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static WagonSaveData CreateWagonSave(OwnedWagonSaveData owned, SharedWagonDefinition definition) => new WagonSaveData
        {
            instanceId = owned.instanceId,
            contentId = owned.contentId,
            wagonName = definition.DisplayName ?? owned.contentId,
            overLoad = definition.BaseEfficientLoad,
            maxLoad = definition.MaxLoad,
            minAnimals = Math.Max(0, definition.MinRequireAnimals),
            maxAnimals = Math.Max(0, definition.MaxPullAnimals),
            speedModifier = definition.BaseMoveSpeed,
            maxDurability = Math.Max(0, definition.MaxDurability),
            inventorySlotCount = Math.Max(0, definition.InventorySlotCount)
        };

        private static AnimalSaveData CreateAnimalSave(OwnedDraftAnimalSaveData owned, SharedDraftAnimalDefinition definition) => new AnimalSaveData
        {
            instanceId = owned.instanceId,
            contentId = owned.contentId,
            animalName = definition.DisplayName ?? owned.contentId,
            animalType = ParseAnimalType(definition.AnimalType, default),
            speed = definition.BaseMoveSpeed,
            foodPerKm = definition.FoodConsumptionPerSecond,
            increaseOverLoad = definition.AdditionalEfficientLoad
        };

        private static OwnedWagonSaveData CopyOwnedWagon(OwnedWagonSaveData value) => new OwnedWagonSaveData
        {
            instanceId = value.instanceId,
            contentId = value.contentId,
            currentDurability = Math.Max(0, value.currentDurability)
        };

        private static OwnedDraftAnimalSaveData CopyOwnedAnimal(OwnedDraftAnimalSaveData value) => new OwnedDraftAnimalSaveData
        {
            instanceId = value.instanceId,
            contentId = value.contentId
        };

        private static string ResolveWagonContentId(WagonSaveData value, ISharedGameDataProvider shared)
        {
            string id = Normalize(value?.contentId);
            if (!string.IsNullOrEmpty(id) && shared != null && shared.TryGetWagon(id, out _)) return id;
            string legacy = Normalize(value?.wagonName);
            return shared != null && shared.TryGetWagon(legacy, out _) ? legacy : string.Empty;
        }

        private static string ResolveAnimalContentId(AnimalSaveData value, ISharedGameDataProvider shared)
        {
            string id = Normalize(value?.contentId);
            if (!string.IsNullOrEmpty(id) && shared != null && shared.TryGetDraftAnimal(id, out _)) return id;
            string legacy = Normalize(value?.animalName);
            return shared != null && shared.TryGetDraftAnimal(legacy, out _) ? legacy : string.Empty;
        }

        private static DraftAnimalType ParseAnimalType(string value, DraftAnimalType fallback) =>
            Enum.TryParse(value, true, out DraftAnimalType parsed) ? parsed : fallback;

        private static string Normalize(string value) => value?.Trim() ?? string.Empty;
        private static long MultiplyClamped(long value, int quantity) => value <= 0 || quantity <= 0 ? 0L : value > long.MaxValue / quantity ? long.MaxValue : value * quantity;
        private static long AddClamped(long left, long right) => right > 0 && left > long.MaxValue - right ? long.MaxValue : left + right;
    }
}
