using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Economy
{
    public enum BuildingCostCatalogFailureReason
    {
        None,
        EmptyCatalog,
        InvalidBuildingDefinition,
        DuplicateBuilding,
        MissingLevelCost,
        DuplicateLevelCost,
        InvalidMaterialRequirement,
        DuplicateMaterialRequirement,
        InvalidTradeItemCatalog,
        TradeItemNotFound,
        TradeItemIsNotMaterial
    }

    [Serializable]
    public sealed class BuildingCostCatalogFinding
    {
        public BuildingCostCatalogFailureReason Reason;
        public string BuildingDisplayName = string.Empty;
        public int TargetLevel;
        public string ItemId = string.Empty;
        public string Message = string.Empty;
    }

    /// <summary>
    /// 건물 displayName별 레벨 비용 정의를 보관하고 Content 입력을 검증한다.
    /// 조회 결과는 복제본이므로 runtime 호출자가 catalog asset을 변경할 수 없다.
    /// </summary>
    [CreateAssetMenu(fileName = ResourceName, menuName = "ND/Economy/Building Cost Catalog")]
    public sealed class BuildingCostCatalog : ScriptableObject
    {
        public const string ResourceName = "BuildingCostCatalog";

        [SerializeField]
        private List<BuildingCostDefinition> definitions = new List<BuildingCostDefinition>();

        public int Count => definitions != null ? definitions.Count : 0;

        public bool TryGetDefinition(string displayName, out BuildingCostDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(displayName) || definitions == null)
            {
                return false;
            }

            BuildingCostDefinition found = null;
            for (int i = 0; i < definitions.Count; i++)
            {
                BuildingCostDefinition candidate = definitions[i];
                if (candidate == null
                    || !string.Equals(candidate.DisplayName, displayName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (found != null)
                {
                    return false;
                }

                found = candidate;
            }

            if (found == null)
            {
                return false;
            }

            definition = CloneDefinition(found);
            return true;
        }

        public bool Validate(
            IEnumerable<TradeItemData> tradeItemCatalog,
            out List<BuildingCostCatalogFinding> findings)
        {
            findings = new List<BuildingCostCatalogFinding>();
            Dictionary<string, TradeItemData> itemsById;
            if (!TryBuildTradeItemCatalog(tradeItemCatalog, findings, out itemsById))
            {
                return false;
            }

            if (definitions == null || definitions.Count == 0)
            {
                AddFinding(findings, BuildingCostCatalogFailureReason.EmptyCatalog,
                    string.Empty, 0, string.Empty, "Building cost catalog is empty.");
                return false;
            }

            var buildingNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definitions.Count; i++)
            {
                ValidateDefinition(definitions[i], buildingNames, itemsById, findings);
            }

            return findings.Count == 0;
        }

        private static void ValidateDefinition(
            BuildingCostDefinition definition,
            HashSet<string> buildingNames,
            Dictionary<string, TradeItemData> itemsById,
            List<BuildingCostCatalogFinding> findings)
        {
            string displayName = definition?.DisplayName ?? string.Empty;
            if (definition == null || string.IsNullOrWhiteSpace(displayName)
                || definition.MaxLevel < 1 || definition.LevelCosts == null)
            {
                AddFinding(findings, BuildingCostCatalogFailureReason.InvalidBuildingDefinition,
                    displayName, 0, string.Empty, "Building definition is invalid.");
                return;
            }

            if (!buildingNames.Add(displayName))
            {
                AddFinding(findings, BuildingCostCatalogFailureReason.DuplicateBuilding,
                    displayName, 0, string.Empty, "Building displayName is duplicated.");
            }

            var levels = new HashSet<int>();
            for (int i = 0; i < definition.LevelCosts.Count; i++)
            {
                BuildingLevelCost levelCost = definition.LevelCosts[i];
                if (levelCost == null || levelCost.TargetLevel < 1
                    || levelCost.TargetLevel > definition.MaxLevel || levelCost.Materials == null
                    || levelCost.Materials.Count == 0)
                {
                    AddFinding(findings, BuildingCostCatalogFailureReason.InvalidBuildingDefinition,
                        displayName, levelCost?.TargetLevel ?? 0, string.Empty,
                        "Building level cost is invalid.");
                    continue;
                }

                if (!levels.Add(levelCost.TargetLevel))
                {
                    AddFinding(findings, BuildingCostCatalogFailureReason.DuplicateLevelCost,
                        displayName, levelCost.TargetLevel, string.Empty,
                        "Target level cost is duplicated.");
                }

                ValidateMaterials(displayName, levelCost, itemsById, findings);
            }

            for (int level = 1; level <= definition.MaxLevel; level++)
            {
                if (!levels.Contains(level))
                {
                    AddFinding(findings, BuildingCostCatalogFailureReason.MissingLevelCost,
                        displayName, level, string.Empty, "Target level cost is missing.");
                }
            }
        }

        private static void ValidateMaterials(
            string displayName,
            BuildingLevelCost levelCost,
            Dictionary<string, TradeItemData> itemsById,
            List<BuildingCostCatalogFinding> findings)
        {
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < levelCost.Materials.Count; i++)
            {
                BuildingMaterialRequirement material = levelCost.Materials[i];
                string itemId = material?.ItemId ?? string.Empty;
                if (material == null || string.IsNullOrWhiteSpace(itemId) || material.Quantity <= 0)
                {
                    AddFinding(findings, BuildingCostCatalogFailureReason.InvalidMaterialRequirement,
                        displayName, levelCost.TargetLevel, itemId,
                        "Material requirement is invalid.");
                    continue;
                }

                if (!itemIds.Add(itemId))
                {
                    AddFinding(findings, BuildingCostCatalogFailureReason.DuplicateMaterialRequirement,
                        displayName, levelCost.TargetLevel, itemId,
                        "Material requirement is duplicated in the same level.");
                }

                TradeItemData item;
                if (!itemsById.TryGetValue(itemId, out item))
                {
                    AddFinding(findings, BuildingCostCatalogFailureReason.TradeItemNotFound,
                        displayName, levelCost.TargetLevel, itemId,
                        "Material item was not found in the trade item catalog.");
                }
                else if (item.Category != global::TradeItemCategory.Material)
                {
                    AddFinding(findings, BuildingCostCatalogFailureReason.TradeItemIsNotMaterial,
                        displayName, levelCost.TargetLevel, itemId,
                        "Building cost item is not TradeItemCategory.Material.");
                }
            }
        }

        private static bool TryBuildTradeItemCatalog(
            IEnumerable<TradeItemData> source,
            List<BuildingCostCatalogFinding> findings,
            out Dictionary<string, TradeItemData> itemsById)
        {
            itemsById = new Dictionary<string, TradeItemData>(StringComparer.Ordinal);
            if (source == null)
            {
                AddFinding(findings, BuildingCostCatalogFailureReason.InvalidTradeItemCatalog,
                    string.Empty, 0, string.Empty, "Trade item catalog is null.");
                return false;
            }

            bool valid = true;
            foreach (TradeItemData item in source)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ItemId)
                    || itemsById.ContainsKey(item.ItemId))
                {
                    valid = false;
                    AddFinding(findings, BuildingCostCatalogFailureReason.InvalidTradeItemCatalog,
                        string.Empty, 0, item?.ItemId ?? string.Empty,
                        "Trade item catalog contains a null, empty, or duplicated item.");
                    continue;
                }

                itemsById.Add(item.ItemId, item);
            }

            return valid;
        }

        private static BuildingCostDefinition CloneDefinition(BuildingCostDefinition source)
        {
            var clone = new BuildingCostDefinition
            {
                DisplayName = source.DisplayName ?? string.Empty,
                MaxLevel = source.MaxLevel,
                LevelCosts = new List<BuildingLevelCost>()
            };

            if (source.LevelCosts == null)
            {
                clone.LevelCosts = null;
                return clone;
            }

            for (int i = 0; i < source.LevelCosts.Count; i++)
            {
                BuildingLevelCost level = source.LevelCosts[i];
                if (level == null)
                {
                    clone.LevelCosts.Add(null);
                    continue;
                }

                var levelClone = new BuildingLevelCost
                {
                    TargetLevel = level.TargetLevel,
                    Materials = new List<BuildingMaterialRequirement>()
                };
                if (level.Materials == null)
                {
                    levelClone.Materials = null;
                }
                else
                {
                    for (int materialIndex = 0; materialIndex < level.Materials.Count; materialIndex++)
                    {
                        BuildingMaterialRequirement material = level.Materials[materialIndex];
                        levelClone.Materials.Add(material == null
                            ? null
                            : new BuildingMaterialRequirement
                            {
                                ItemId = material.ItemId ?? string.Empty,
                                Quantity = material.Quantity
                            });
                    }
                }

                clone.LevelCosts.Add(levelClone);
            }

            return clone;
        }

        private static void AddFinding(
            List<BuildingCostCatalogFinding> findings,
            BuildingCostCatalogFailureReason reason,
            string buildingDisplayName,
            int targetLevel,
            string itemId,
            string message)
        {
            findings.Add(new BuildingCostCatalogFinding
            {
                Reason = reason,
                BuildingDisplayName = buildingDisplayName ?? string.Empty,
                TargetLevel = targetLevel,
                ItemId = itemId ?? string.Empty,
                Message = message ?? string.Empty
            });
        }
    }
}
