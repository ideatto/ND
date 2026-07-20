using System;
using System.Collections.Generic;

// Pure calculator: this file intentionally has no SaveData dependency.
namespace ND.Economy
{
    /// <summary>
    /// Caravan cargo snapshot을 기준으로 건물 신축·강화 재료 비용을 계산한다.
    /// 입력 재고와 SaveData는 변경하지 않는다.
    /// </summary>
    public static class BuildingCostCalculator
    {
        public static BuildingCostResult Calculate(BuildingCostInput input)
        {
            BuildingCostResult result = CreateBaseResult(input);

            if (input == null || string.IsNullOrWhiteSpace(input.DisplayName) || input.CurrentLevel < 0)
            {
                return Fail(result, BuildingCostFailureReason.InvalidInput);
            }

            if (input.MaxLevel < 1 || input.CurrentLevel > input.MaxLevel || input.LevelCosts == null)
            {
                return Fail(result, BuildingCostFailureReason.InvalidDefinition);
            }

            if (input.CurrentLevel >= input.MaxLevel)
            {
                return Fail(result, BuildingCostFailureReason.AlreadyMaxLevel);
            }

            int targetLevel = input.CurrentLevel + 1;
            result.TargetLevel = targetLevel;

            BuildingLevelCost targetCost = null;
            for (int i = 0; i < input.LevelCosts.Count; i++)
            {
                BuildingLevelCost levelCost = input.LevelCosts[i];
                if (levelCost == null || levelCost.TargetLevel < 1 || levelCost.TargetLevel > input.MaxLevel)
                {
                    return Fail(result, BuildingCostFailureReason.InvalidDefinition);
                }

                if (levelCost.TargetLevel != targetLevel)
                {
                    continue;
                }

                if (targetCost != null)
                {
                    return Fail(result, BuildingCostFailureReason.InvalidDefinition);
                }

                targetCost = levelCost;
            }

            if (targetCost == null)
            {
                return Fail(result, BuildingCostFailureReason.LevelCostNotFound);
            }

            if (targetCost.Materials == null || targetCost.Materials.Count == 0)
            {
                return Fail(result, BuildingCostFailureReason.InvalidDefinition);
            }

            Dictionary<string, int> inventory;
            BuildingCostFailureReason inventoryFailure;
            if (!TryAggregateInventory(input.CaravanCargo, out inventory, out inventoryFailure))
            {
                return Fail(result, inventoryFailure);
            }

            HashSet<string> requiredItemIds = new HashSet<string>(StringComparer.Ordinal);
            bool hasMissingMaterial = false;

            for (int i = 0; i < targetCost.Materials.Count; i++)
            {
                BuildingMaterialRequirement requirement = targetCost.Materials[i];
                if (requirement == null || string.IsNullOrWhiteSpace(requirement.ItemId)
                    || requirement.Quantity <= 0 || !requiredItemIds.Add(requirement.ItemId))
                {
                    return FailWithoutMaterials(result, BuildingCostFailureReason.InvalidDefinition);
                }

                int quantityBefore;
                inventory.TryGetValue(requirement.ItemId, out quantityBefore);
                int missingQuantity = Math.Max(0, requirement.Quantity - quantityBefore);

                result.Materials.Add(new BuildingMaterialDelta
                {
                    ItemId = requirement.ItemId,
                    RequiredQuantity = requirement.Quantity,
                    QuantityBefore = quantityBefore,
                    QuantityAfter = Math.Max(0, quantityBefore - requirement.Quantity),
                    MissingQuantity = missingQuantity
                });

                hasMissingMaterial |= missingQuantity > 0;
            }

            if (hasMissingMaterial)
            {
                return Fail(result, BuildingCostFailureReason.InsufficientMaterials);
            }

            result.Success = true;
            result.FailureReason = BuildingCostFailureReason.None;
            return result;
        }

        private static bool TryAggregateInventory(
            List<InventoryItemAmount> source,
            out Dictionary<string, int> inventory,
            out BuildingCostFailureReason failureReason)
        {
            inventory = new Dictionary<string, int>(StringComparer.Ordinal);
            failureReason = BuildingCostFailureReason.None;

            if (source == null)
            {
                failureReason = BuildingCostFailureReason.InventoryCorrupted;
                return false;
            }

            for (int i = 0; i < source.Count; i++)
            {
                InventoryItemAmount entry = source[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.ItemId) || entry.Quantity < 0)
                {
                    failureReason = BuildingCostFailureReason.InventoryCorrupted;
                    return false;
                }

                if (entry.Category != global::TradeItemCategory.Material)
                {
                    continue;
                }

                int current;
                inventory.TryGetValue(entry.ItemId, out current);
                if (entry.Quantity > int.MaxValue - current)
                {
                    failureReason = BuildingCostFailureReason.Overflow;
                    return false;
                }

                inventory[entry.ItemId] = current + entry.Quantity;
            }

            return true;
        }

        private static BuildingCostResult CreateBaseResult(BuildingCostInput input)
        {
            int previousLevel = input != null ? Math.Max(0, input.CurrentLevel) : 0;
            return new BuildingCostResult
            {
                DisplayName = input != null ? input.DisplayName ?? string.Empty : string.Empty,
                PreviousLevel = previousLevel,
                TargetLevel = previousLevel
            };
        }

        private static BuildingCostResult Fail(
            BuildingCostResult result,
            BuildingCostFailureReason failureReason)
        {
            result.Success = false;
            result.FailureReason = failureReason;
            return result;
        }

        private static BuildingCostResult FailWithoutMaterials(
            BuildingCostResult result,
            BuildingCostFailureReason failureReason)
        {
            result.Materials.Clear();
            return Fail(result, failureReason);
        }
    }
}
