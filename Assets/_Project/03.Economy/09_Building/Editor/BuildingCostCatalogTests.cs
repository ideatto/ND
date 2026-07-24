using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ND.Economy.Editor
{
    public static class BuildingCostCatalogTests
    {
        public static void RunAll()
        {
            Validate_AcceptsCompleteMaterialDefinitions();
            Validate_ReportsDuplicateBuildingAndMissingLevel();
            Validate_RejectsNonMaterialAndUnknownItems();
            TryGetDefinition_ReturnsIsolatedClone();
        }

        private static void Validate_AcceptsCompleteMaterialDefinitions()
        {
            BuildingCostCatalog catalog = CreateCatalog(CreateDefinition("Workshop", 2));
            TradeItemData wood = CreateItem("wood", global::TradeItemCategory.Material);
            try
            {
                bool valid = catalog.Validate(new[] { wood }, out List<BuildingCostCatalogFinding> findings);
                Check(valid, "Complete material definitions should be valid.");
                CheckEqual(0, findings.Count, "Valid finding count");
            }
            finally
            {
                Destroy(catalog, wood);
            }
        }

        private static void Validate_ReportsDuplicateBuildingAndMissingLevel()
        {
            BuildingCostDefinition first = CreateDefinition("Workshop", 2);
            first.LevelCosts.RemoveAt(1);
            BuildingCostCatalog catalog = CreateCatalog(first, CreateDefinition("Workshop", 1));
            TradeItemData wood = CreateItem("wood", global::TradeItemCategory.Material);
            try
            {
                bool valid = catalog.Validate(new[] { wood }, out List<BuildingCostCatalogFinding> findings);
                Check(!valid, "Duplicate building and missing level should fail.");
                Check(Contains(findings, BuildingCostCatalogFailureReason.DuplicateBuilding),
                    "Duplicate building finding is required.");
                Check(Contains(findings, BuildingCostCatalogFailureReason.MissingLevelCost),
                    "Missing level finding is required.");
            }
            finally
            {
                Destroy(catalog, wood);
            }
        }

        private static void Validate_RejectsNonMaterialAndUnknownItems()
        {
            BuildingCostDefinition definition = CreateDefinition("Workshop", 1);
            definition.LevelCosts[0].Materials.Add(new BuildingMaterialRequirement
            {
                ItemId = "missing",
                Quantity = 1
            });
            BuildingCostCatalog catalog = CreateCatalog(definition);
            TradeItemData wood = CreateItem("wood", global::TradeItemCategory.Valuable);
            try
            {
                bool valid = catalog.Validate(new[] { wood }, out List<BuildingCostCatalogFinding> findings);
                Check(!valid, "Non-material and missing items should fail.");
                Check(Contains(findings, BuildingCostCatalogFailureReason.TradeItemIsNotMaterial),
                    "Non-material finding is required.");
                Check(Contains(findings, BuildingCostCatalogFailureReason.TradeItemNotFound),
                    "Missing item finding is required.");
            }
            finally
            {
                Destroy(catalog, wood);
            }
        }

        private static void TryGetDefinition_ReturnsIsolatedClone()
        {
            BuildingCostDefinition source = CreateDefinition("Workshop", 1);
            BuildingCostCatalog catalog = CreateCatalog(source);
            try
            {
                Check(catalog.TryGetDefinition("Workshop", out BuildingCostDefinition clone),
                    "Definition lookup should succeed.");
                clone.LevelCosts[0].Materials[0].Quantity = 999;
                CheckEqual(1, source.LevelCosts[0].Materials[0].Quantity,
                    "Catalog source must not be mutated through lookup result");
                Check(!catalog.TryGetDefinition("Unknown", out _),
                    "Unknown building lookup should fail.");
            }
            finally
            {
                Destroy(catalog);
            }
        }

        private static BuildingCostDefinition CreateDefinition(string name, int maxLevel)
        {
            var definition = new BuildingCostDefinition
            {
                DisplayName = name,
                MaxLevel = maxLevel
            };
            for (int level = 1; level <= maxLevel; level++)
            {
                definition.LevelCosts.Add(new BuildingLevelCost
                {
                    TargetLevel = level,
                    Materials =
                    {
                        new BuildingMaterialRequirement { ItemId = "wood", Quantity = level }
                    }
                });
            }
            return definition;
        }

        private static BuildingCostCatalog CreateCatalog(params BuildingCostDefinition[] definitions)
        {
            BuildingCostCatalog catalog = ScriptableObject.CreateInstance<BuildingCostCatalog>();
            FieldInfo field = typeof(BuildingCostCatalog).GetField(
                "definitions",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(catalog, new List<BuildingCostDefinition>(definitions));
            return catalog;
        }

        private static TradeItemData CreateItem(string itemId, global::TradeItemCategory category)
        {
            TradeItemData item = ScriptableObject.CreateInstance<TradeItemData>();
            var serialized = new SerializedObject(item);
            serialized.FindProperty("itemId").stringValue = itemId;
            serialized.FindProperty("category").enumValueIndex = (int)category;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private static bool Contains(
            List<BuildingCostCatalogFinding> findings,
            BuildingCostCatalogFailureReason reason)
        {
            for (int i = 0; i < findings.Count; i++)
            {
                if (findings[i].Reason == reason) return true;
            }
            return false;
        }

        private static void Destroy(params UnityEngine.Object[] objects)
        {
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void CheckEqual<T>(T expected, T actual, string name)
        {
            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException($"{name}: expected {expected}, actual {actual}.");
            }
        }
    }
}
