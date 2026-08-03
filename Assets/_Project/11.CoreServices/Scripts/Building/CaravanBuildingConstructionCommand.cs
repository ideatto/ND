using System;
using System.Collections.Generic;
using ND.Economy;

namespace ND.Framework
{
    public enum BuildingPlacementFailureReason
    {
        None,
        InvalidArgument,
        SaveDataUnavailable,
        BuildingCollectionUnavailable,
        BuildingNotFound,
        DuplicateBuildingEntry,
        SaveServiceUnavailable,
        SaveFailed
    }

    /// <summary>건물 배치 SaveData 변경과 영속 저장의 결과를 반환한다.</summary>
    public sealed class BuildingPlacementResult
    {
        private BuildingPlacementResult(
            bool succeeded,
            BuildingPlacementFailureReason failureReason,
            SaveResult saveResult)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            SaveResult = saveResult;
        }

        public bool Succeeded { get; }
        public BuildingPlacementFailureReason FailureReason { get; }
        public SaveResult SaveResult { get; }

        internal static BuildingPlacementResult Success(SaveResult saveResult)
            => new BuildingPlacementResult(true, BuildingPlacementFailureReason.None, saveResult);

        internal static BuildingPlacementResult Failure(
            BuildingPlacementFailureReason failureReason,
            SaveResult saveResult = null)
            => new BuildingPlacementResult(false, failureReason, saveResult);
    }

    /// <summary>
    /// 이미 건설된 한 건물의 격자 배치만 변경하고 저장한다.
    /// 저장 실패 또는 예외 시 네 배치 필드를 명령 실행 전 값으로 복원한다.
    /// </summary>
    public sealed class BuildingPlacementCommand
    {
        private readonly Func<SaveData> getSaveData;
        private readonly ISaveService saveService;

        public BuildingPlacementCommand(Func<SaveData> getSaveData, ISaveService saveService)
        {
            this.getSaveData = getSaveData;
            this.saveService = saveService;
        }

        /// <returns>
        /// 디스크 저장까지 완료된 경우에만 성공한다. 예상 가능한 검증 실패는 예외 대신
        /// FailureReason으로 반환하며, 저장 실패 시 SaveResult도 함께 반환한다.
        /// </returns>
        public BuildingPlacementResult Execute(string displayName, int gridCellX, int gridCellZ, int yawStep)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return BuildingPlacementResult.Failure(BuildingPlacementFailureReason.InvalidArgument);

            SaveData saveData = getSaveData != null ? getSaveData() : null;
            if (saveData == null || saveData.player == null)
                return BuildingPlacementResult.Failure(BuildingPlacementFailureReason.SaveDataUnavailable);
            if (saveData.player.villageBuildings == null)
                return BuildingPlacementResult.Failure(BuildingPlacementFailureReason.BuildingCollectionUnavailable);
            if (saveService == null)
                return BuildingPlacementResult.Failure(BuildingPlacementFailureReason.SaveServiceUnavailable);

            VillageBuildingSaveData target = null;
            int matchCount = 0;
            foreach (VillageBuildingSaveData entry in saveData.player.villageBuildings)
            {
                if (entry == null || !string.Equals(entry.displayName, displayName, StringComparison.Ordinal))
                    continue;
                target = entry;
                matchCount++;
            }

            if (matchCount == 0)
                return BuildingPlacementResult.Failure(BuildingPlacementFailureReason.BuildingNotFound);
            if (matchCount != 1)
                return BuildingPlacementResult.Failure(BuildingPlacementFailureReason.DuplicateBuildingEntry);

            bool previousHasPlacement = target.hasPlacement;
            int previousGridCellX = target.gridCellX;
            int previousGridCellZ = target.gridCellZ;
            int previousYawStep = target.yawStep;

            try
            {
                target.hasPlacement = true;
                target.gridCellX = gridCellX;
                target.gridCellZ = gridCellZ;
                target.yawStep = NormalizeYawStep(yawStep);

                SaveResult saveResult = saveService.Save(saveData);
                if (saveResult != null && saveResult.Succeeded)
                    return BuildingPlacementResult.Success(saveResult);

                RestorePlacement(
                    target,
                    previousHasPlacement,
                    previousGridCellX,
                    previousGridCellZ,
                    previousYawStep);
                return BuildingPlacementResult.Failure(
                    BuildingPlacementFailureReason.SaveFailed,
                    saveResult);
            }
            catch (Exception exception)
            {
                RestorePlacement(
                    target,
                    previousHasPlacement,
                    previousGridCellX,
                    previousGridCellZ,
                    previousYawStep);
                return BuildingPlacementResult.Failure(
                    BuildingPlacementFailureReason.SaveFailed,
                    SaveResult.Failure(
                        SaveFailureReason.Unknown,
                        "Building placement save failed: " + exception.Message,
                        "VillageBuildingPlacement"));
            }
        }

        internal static int NormalizeYawStep(int yawStep)
        {
            int normalized = yawStep % 4;
            return normalized < 0 ? normalized + 4 : normalized;
        }

        private static void RestorePlacement(
            VillageBuildingSaveData target,
            bool hasPlacement,
            int gridCellX,
            int gridCellZ,
            int yawStep)
        {
            target.hasPlacement = hasPlacement;
            target.gridCellX = gridCellX;
            target.gridCellZ = gridCellZ;
            target.yawStep = yawStep;
        }
    }

    /// <summary>
    /// 본기지 Caravan cargo의 건축 재료 차감과 건물 레벨 변경을 하나의 저장 경계로 처리한다.
    /// 저장 실패 시 건물 레벨과 내구 배치 정보를 명령 실행 전 스냅샷으로 복원한다.
    /// 씬 오브젝트와 UI는 성공 결과를 받은 뒤 별도로 갱신한다.
    /// </summary>
    public static class CaravanBuildingConstructionCommand
    {
        public static SaveResult Execute(
            SaveData saveData,
            ISaveService saveService,
            IEnumerable<TradeItemData> tradeItemCatalog,
            BuildingCostDefinition definition,
            string baseTownId,
            out BuildingCostResult costResult)
        {
            costResult = null;
            SaveResult validationFailure = ValidateContext(saveData, saveService, definition, baseTownId);
            if (validationFailure != null)
            {
                return validationFailure;
            }

            int currentLevel;
            if (!TryGetCurrentLevel(saveData.player.villageBuildings, definition.DisplayName, out currentLevel))
            {
                return Invalid("Building save data is corrupted.", "VillageBuildings");
            }

            BuildingCostInput input;
            if (!CaravanBuildingCostInputFactory.TryCreate(
                    saveData,
                    tradeItemCatalog,
                    definition,
                    currentLevel,
                    out input))
            {
                return Invalid("Caravan cargo or trade item catalog is invalid.", "CaravanCargo");
            }

            costResult = BuildingCostCalculator.Calculate(input);
            if (!costResult.Success)
            {
                return Invalid(
                    "Building cost validation failed: " + costResult.FailureReason,
                    "BuildingCost");
            }

            List<CargoEntrySaveData> cargoSnapshot = CloneCargo(saveData.caravan.cargo);
            List<VillageBuildingSaveData> buildingSnapshot = CloneBuildings(saveData.player.villageBuildings);

            try
            {
                if (!TryConsumeMaterials(saveData.caravan.cargo, costResult.Materials)
                    || !TryUpsertBuilding(
                        saveData.player.villageBuildings,
                        costResult.DisplayName,
                        costResult.TargetLevel))
                {
                    Restore(saveData, cargoSnapshot, buildingSnapshot);
                    return Invalid("Building state changed before the command could be applied.", "BuildingConstruction");
                }

                SaveResult saveResult = saveService.Save(saveData);
                if (saveResult == null || !saveResult.Succeeded)
                {
                    Restore(saveData, cargoSnapshot, buildingSnapshot);
                    return saveResult ?? SaveResult.Failure(
                        SaveFailureReason.Unknown,
                        "Save service returned no result.",
                        "BuildingConstruction");
                }

                return saveResult;
            }
            catch (Exception exception)
            {
                Restore(saveData, cargoSnapshot, buildingSnapshot);
                return SaveResult.Failure(
                    SaveFailureReason.Unknown,
                    "Building construction failed: " + exception.Message,
                    "BuildingConstruction");
            }
        }

        private static SaveResult ValidateContext(
            SaveData saveData,
            ISaveService saveService,
            BuildingCostDefinition definition,
            string baseTownId)
        {
            if (saveData?.player == null || saveData.caravan?.cargo == null
                || saveData.player.villageBuildings == null
                || saveService == null || definition == null
                || string.IsNullOrWhiteSpace(definition.DisplayName)
                || string.IsNullOrWhiteSpace(baseTownId))
            {
                return Invalid("Building construction context is invalid.", "BuildingConstruction");
            }

            if (!string.Equals(saveData.player.currentTownId, baseTownId, StringComparison.Ordinal))
            {
                return Invalid("Building construction is only allowed at the base town.", "CurrentTown");
            }

            if (saveData.tradeProgress != null
                && (saveData.tradeProgress.state == TradeProgressState.Traveling
                    || saveData.tradeProgress.state == TradeProgressState.SettlementPending))
            {
                return Invalid("Building construction is not allowed during travel or pending settlement.", "TradeProgress");
            }

            return null;
        }

        private static bool TryGetCurrentLevel(
            List<VillageBuildingSaveData> buildings,
            string displayName,
            out int currentLevel)
        {
            currentLevel = 0;
            bool found = false;
            for (int i = 0; i < buildings.Count; i++)
            {
                VillageBuildingSaveData building = buildings[i];
                if (building == null || string.IsNullOrWhiteSpace(building.displayName) || building.level < 1)
                {
                    return false;
                }

                if (!string.Equals(building.displayName, displayName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (found)
                {
                    return false;
                }

                found = true;
                currentLevel = building.level;
            }

            return true;
        }

        private static bool TryConsumeMaterials(
            List<CargoEntrySaveData> cargo,
            List<BuildingMaterialDelta> materials)
        {
            for (int materialIndex = 0; materialIndex < materials.Count; materialIndex++)
            {
                BuildingMaterialDelta material = materials[materialIndex];
                int remaining = material.RequiredQuantity;

                for (int cargoIndex = 0; cargoIndex < cargo.Count && remaining > 0; cargoIndex++)
                {
                    CargoEntrySaveData entry = cargo[cargoIndex];
                    if (entry?.item == null
                        || !string.Equals(entry.item.itemId, material.ItemId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int consumed = Math.Min(entry.quantity, remaining);
                    entry.quantity -= consumed;
                    remaining -= consumed;
                }

                if (remaining > 0)
                {
                    return false;
                }
            }

            for (int i = cargo.Count - 1; i >= 0; i--)
            {
                if (cargo[i].quantity == 0)
                {
                    cargo.RemoveAt(i);
                }
            }

            return true;
        }

        private static bool TryUpsertBuilding(
            List<VillageBuildingSaveData> buildings,
            string displayName,
            int targetLevel)
        {
            for (int i = 0; i < buildings.Count; i++)
            {
                if (!string.Equals(buildings[i].displayName, displayName, StringComparison.Ordinal))
                {
                    continue;
                }

                buildings[i].level = targetLevel;
                return true;
            }

            buildings.Add(new VillageBuildingSaveData
            {
                displayName = displayName,
                level = targetLevel
            });
            return true;
        }

        private static List<CargoEntrySaveData> CloneCargo(List<CargoEntrySaveData> source)
        {
            var clone = new List<CargoEntrySaveData>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                CargoEntrySaveData entry = source[i];
                clone.Add(new CargoEntrySaveData
                {
                    item = CloneItem(entry.item),
                    quantity = entry.quantity
                });
            }

            return clone;
        }

        private static TradeItemSaveData CloneItem(TradeItemSaveData source)
        {
            return new TradeItemSaveData
            {
                itemId = source.itemId,
                itemName = source.itemName,
                weight = source.weight,
                purchaseUnitPrice = source.purchaseUnitPrice,
                basePrice = source.basePrice,
                maxCount = source.maxCount
            };
        }

        private static List<VillageBuildingSaveData> CloneBuildings(List<VillageBuildingSaveData> source)
        {
            var clone = new List<VillageBuildingSaveData>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                VillageBuildingSaveData building = source[i];
                clone.Add(new VillageBuildingSaveData
                {
                    displayName = building.displayName,
                    level = building.level,
                    hasPlacement = building.hasPlacement,
                    gridCellX = building.gridCellX,
                    gridCellZ = building.gridCellZ,
                    yawStep = building.yawStep
                });
            }

            return clone;
        }

        private static void Restore(
            SaveData saveData,
            List<CargoEntrySaveData> cargoSnapshot,
            List<VillageBuildingSaveData> buildingSnapshot)
        {
            if (saveData.caravan.cargo == null)
            {
                saveData.caravan.cargo = cargoSnapshot;
            }
            else
            {
                saveData.caravan.cargo.Clear();
                saveData.caravan.cargo.AddRange(cargoSnapshot);
            }

            if (saveData.player.villageBuildings == null)
            {
                saveData.player.villageBuildings = buildingSnapshot;
            }
            else
            {
                saveData.player.villageBuildings.Clear();
                saveData.player.villageBuildings.AddRange(buildingSnapshot);
            }
        }

        private static SaveResult Invalid(string message, string category)
        {
            return SaveResult.Failure(SaveFailureReason.InvalidData, message, category);
        }
    }
}
