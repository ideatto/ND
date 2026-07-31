/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Shared watch roots are scanned through the common descriptor/scanner contract.
 * - The player-facing watch inventory snapshot is refreshed on explicit request.
 */
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ND.Framework.Editor
{
    internal readonly struct SharedGameDataWatchInventoryRefreshResult
    {
        public SharedGameDataWatchInventoryRefreshResult(
            bool success,
            string message,
            SharedGameDataWatchInventory inventory)
        {
            Success = success;
            Message = message;
            Inventory = inventory;
        }

        public bool Success { get; }
        public string Message { get; }
        public SharedGameDataWatchInventory Inventory { get; }
    }

    public static class SharedGameDataCatalogDriftChecker
    {
        private const string InventoryAssetPath =
            "Assets/_Project/11.CoreServices/Resources/SharedGameDataWatchInventory.asset";

        public static List<SharedGameDataDriftFinding> CollectUnregisteredAssets(
            SandboxSharedGameDataCatalog catalog)
        {
            var catalogGuids = CollectCatalogGuids(catalog);
            var watched = SharedGameDataWatchScanner.Scan();
            var findings = new List<SharedGameDataDriftFinding>();
            for (var index = 0; index < watched.Count; index++)
            {
                var item = watched[index];
                if (catalogGuids.Contains(item.Guid))
                {
                    continue;
                }

                findings.Add(new SharedGameDataDriftFinding
                {
                    AssetGuid = item.Guid,
                    AssetPath = item.AssetPath,
                    TypeName = item.Descriptor.TypeName,
                    DataId = item.DataId,
                    WatchRootKind = item.RootKind
                });
            }

            return findings;
        }

        [MenuItem("ND/Framework/Refresh Shared Game Data Watch Inventory")]
        public static void RefreshWatchInventoryMenu()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SandboxSharedGameDataCatalog>(
                SharedGameDataCatalogSynchronizer.CatalogAssetPath);
            var result = TryRefreshWatchInventory(catalog);
            if (result.Success)
            {
                Debug.Log("[Framework] " + result.Message);
            }
            else
            {
                Debug.LogError("[Framework] " + result.Message);
            }
        }

        public static SharedGameDataWatchInventory RefreshWatchInventory(
            SandboxSharedGameDataCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var result = TryRefreshWatchInventory(catalog);
            if (!result.Success)
            {
                throw new InvalidOperationException(result.Message);
            }

            return result.Inventory;
        }

        internal static SharedGameDataWatchInventoryRefreshResult TryRefreshWatchInventory(
            SandboxSharedGameDataCatalog catalog)
        {
            if (catalog == null)
            {
                return new SharedGameDataWatchInventoryRefreshResult(
                    false,
                    "SharedGameDataWatchInventory refresh failed because the production catalog was not found.",
                    null);
            }

            try
            {
                var watched = SharedGameDataWatchScanner.Scan();
                var entries = new SharedGameDataWatchInventory.Entry[watched.Count];
                for (var index = 0; index < watched.Count; index++)
                {
                    var item = watched[index];
                    entries[index] = new SharedGameDataWatchInventory.Entry
                    {
                        assetGuid = item.Guid,
                        assetPath = item.AssetPath,
                        typeName = item.Descriptor.TypeName,
                        dataId = item.DataId,
                        watchRootKind = item.RootKind
                    };
                }

                var catalogGuids = CollectCatalogGuidSequence(catalog).ToArray();
                var inventory = AssetDatabase.LoadAssetAtPath<SharedGameDataWatchInventory>(
                    InventoryAssetPath);
                if (inventory == null)
                {
                    inventory = ScriptableObject.CreateInstance<SharedGameDataWatchInventory>();
                    AssetDatabase.CreateAsset(inventory, InventoryAssetPath);
                }

                inventory.ReplaceSnapshot(entries, catalogGuids);
                EditorUtility.SetDirty(inventory);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return new SharedGameDataWatchInventoryRefreshResult(
                    true,
                    "SharedGameDataWatchInventory refreshed.",
                    inventory);
            }
            catch (Exception exception)
            {
                return new SharedGameDataWatchInventoryRefreshResult(
                    false,
                    "SharedGameDataWatchInventory refresh failed: " + exception.Message,
                    null);
            }
        }

        private static HashSet<string> CollectCatalogGuids(SandboxSharedGameDataCatalog catalog)
        {
            return new HashSet<string>(CollectCatalogGuidSequence(catalog), StringComparer.Ordinal);
        }

        private static List<string> CollectCatalogGuidSequence(
            SandboxSharedGameDataCatalog catalog)
        {
            var guids = new List<string>();
            if (catalog == null)
            {
                return guids;
            }

            AddObjectGuids(catalog.Towns, guids);
            AddObjectGuids(catalog.Markets, guids);
            AddObjectGuids(catalog.TradeItems, guids);
            AddObjectGuids(catalog.Wagons, guids);
            AddObjectGuids(catalog.DraftAnimals, guids);
            AddObjectGuids(catalog.Routes, guids);
            AddObjectGuids(catalog.Quests, guids);
            AddObjectGuids(catalog.Builds, guids);
            return guids;
        }

        private static void AddObjectGuids(UnityEngine.Object[] objects, List<string> guids)
        {
            if (objects == null)
            {
                return;
            }

            for (var index = 0; index < objects.Length; index++)
            {
                var path = AssetDatabase.GetAssetPath(objects[index]);
                var guid = string.IsNullOrEmpty(path)
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid))
                {
                    guids.Add(guid);
                }
            }
        }
    }
}
#endif
