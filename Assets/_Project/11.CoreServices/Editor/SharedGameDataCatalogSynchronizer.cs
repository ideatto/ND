#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ND.Framework.Editor
{
    internal enum CatalogSyncAction
    {
        Error,
        Warning,
        Remove,
        Add,
        Keep,
        Ignore
    }

    internal sealed class CatalogSyncRecord
    {
        public CatalogSyncAction Action;
        public SharedGameDataTypeDescriptor Descriptor;
        public string Guid;
        public string AssetPath;
        public string DataId;
        public SharedGameDataWatchRootKind? RootKind;
        public ScriptableObject Asset;
        public int ExistingArrayIndex = -1;
        public string Reason;
    }

    internal sealed class CatalogSyncTarget
    {
        public SharedGameDataTypeDescriptor Descriptor;
        public readonly List<ScriptableObject> Assets = new List<ScriptableObject>();
        public readonly List<string> Guids = new List<string>();
    }

    internal sealed class CatalogSyncPlan
    {
        public SandboxSharedGameDataCatalog Catalog;
        public readonly List<CatalogSyncRecord> Records = new List<CatalogSyncRecord>();
        public readonly List<CatalogSyncTarget> Targets = new List<CatalogSyncTarget>();
        public bool HasErrors => Count(CatalogSyncAction.Error) != 0;
        public bool HasSemanticChanges =>
            Count(CatalogSyncAction.Add) != 0 || Count(CatalogSyncAction.Remove) != 0;
        public int Count(CatalogSyncAction action)
        {
            var count = 0;
            for (var index = 0; index < Records.Count; index++)
            {
                if (Records[index].Action == action)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public static class SharedGameDataCatalogSynchronizer
    {
        internal const string CatalogAssetPath =
            "Assets/_Project/11.CoreServices/Resources/SandboxSharedGameDataCatalog.asset";
        private const string PreviewMenu = "ND/Framework/Preview Shared Game Data Catalog Sync";
        private const string SyncMenu = "ND/Framework/Sync Shared Game Data Catalog";

        [MenuItem(PreviewMenu)]
        public static void Preview()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning("[CatalogSync] Preview skipped while the Editor asset database is unstable.");
                return;
            }

            PrintPlan(BuildPlan(), "Preview");
        }

        [MenuItem(SyncMenu)]
        public static void Sync()
        {
            var plan = BuildPlan();
            PrintPlan(plan, "Sync");
            if (plan.HasErrors)
            {
                Debug.LogError("[CatalogSync] Sync blocked by validation errors.");
                return;
            }

            if (!plan.HasSemanticChanges)
            {
                Debug.Log("[CatalogSync] No-op success. Catalog and Watch Inventory were not modified.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Sync Shared Game Data Catalog",
                    $"Apply {plan.Count(CatalogSyncAction.Add)} addition(s) and " +
                    $"{plan.Count(CatalogSyncAction.Remove)} removal(s)?",
                    "Sync",
                    "Cancel"))
            {
                Debug.Log("[CatalogSync] Sync cancelled without mutation.");
                return;
            }

            Undo.RecordObject(plan.Catalog, "Sync Shared Game Data Catalog");
            var serializedCatalog = new SerializedObject(plan.Catalog);
            serializedCatalog.Update();
            for (var targetIndex = 0; targetIndex < plan.Targets.Count; targetIndex++)
            {
                var target = plan.Targets[targetIndex];
                var property = serializedCatalog.FindProperty(target.Descriptor.CatalogFieldName);
                property.arraySize = target.Assets.Count;
                for (var assetIndex = 0; assetIndex < target.Assets.Count; assetIndex++)
                {
                    property.GetArrayElementAtIndex(assetIndex).objectReferenceValue =
                        target.Assets[assetIndex];
                }
            }

            serializedCatalog.ApplyModifiedProperties();
            EditorUtility.SetDirty(plan.Catalog);
            AssetDatabase.SaveAssets();
            if (!VerifyAppliedPlan(plan))
            {
                Debug.LogError(
                    "[CatalogSync] Catalog was saved, but verification failed. " +
                    "Watch Inventory was not refreshed.");
                return;
            }

            var refresh =
                SharedGameDataCatalogDriftChecker.TryRefreshWatchInventory(plan.Catalog);
            if (!refresh.Success)
            {
                Debug.LogError(
                    "[CatalogSync] CatalogApplied=true, InventoryRefreshed=false. " +
                    refresh.Message + " Resolve the problem and run " +
                    "'ND/Framework/Refresh Shared Game Data Watch Inventory' again.");
                return;
            }

            Debug.Log("[CatalogSync] CatalogApplied=true, InventoryRefreshed=true.");
        }

        [MenuItem(SyncMenu, true)]
        public static bool ValidateSync()
        {
            return !EditorApplication.isCompiling
                && !EditorApplication.isUpdating
                && !EditorApplication.isPlaying
                && !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static CatalogSyncPlan BuildPlan()
        {
            var plan = new CatalogSyncPlan
            {
                Catalog = AssetDatabase.LoadAssetAtPath<SandboxSharedGameDataCatalog>(
                    CatalogAssetPath)
            };
            if (plan.Catalog == null)
            {
                AddRecord(plan, CatalogSyncAction.Error, null, null, -1,
                    "Production catalog is missing or has the wrong type.");
                return plan;
            }

            var scanned = SharedGameDataWatchScanner.Scan();
            var scannedByGuid = new Dictionary<string, SharedGameDataScannedAsset>(
                StringComparer.Ordinal);
            for (var index = 0; index < scanned.Count; index++)
            {
                var item = scanned[index];
                scannedByGuid[item.Guid] = item;
                if (!string.IsNullOrEmpty(item.LoadError))
                {
                    AddRecord(plan, CatalogSyncAction.Error, item.Descriptor, item, -1,
                        item.LoadError);
                }
            }

            var targetGuids = new Dictionary<string, SharedGameDataTypeDescriptor>(
                StringComparer.Ordinal);
            var serializedCatalog = new SerializedObject(plan.Catalog);
            serializedCatalog.Update();
            var descriptors = SharedGameDataTypeCatalog.Descriptors;
            for (var descriptorIndex = 0; descriptorIndex < descriptors.Count; descriptorIndex++)
            {
                var descriptor = descriptors[descriptorIndex];
                var target = new CatalogSyncTarget { Descriptor = descriptor };
                plan.Targets.Add(target);
                var property = serializedCatalog.FindProperty(descriptor.CatalogFieldName);
                if (property == null || !property.isArray)
                {
                    AddRecord(plan, CatalogSyncAction.Error, descriptor, null, -1,
                        "Serialized catalog field is missing or is not an array.");
                    continue;
                }

                var localGuids = new HashSet<string>(StringComparer.Ordinal);
                for (var arrayIndex = 0; arrayIndex < property.arraySize; arrayIndex++)
                {
                    var asset = property.GetArrayElementAtIndex(arrayIndex).objectReferenceValue
                        as ScriptableObject;
                    if (asset == null)
                    {
                        AddRecord(plan, CatalogSyncAction.Remove, descriptor, null, arrayIndex,
                            "Null or unresolved catalog reference.", CatalogSyncAction.Warning);
                        continue;
                    }

                    var path = SharedGameDataWatchScanner.NormalizePath(
                        AssetDatabase.GetAssetPath(asset));
                    var guid = string.IsNullOrEmpty(path)
                        ? string.Empty
                        : AssetDatabase.AssetPathToGUID(path);
                    if (asset.GetType() != descriptor.AssetType)
                    {
                        AddCatalogRecord(plan, CatalogSyncAction.Error, descriptor, asset, guid,
                            path, arrayIndex, "Object type does not match catalog array.");
                        continue;
                    }

                    if (string.IsNullOrEmpty(guid))
                    {
                        AddCatalogRecord(plan, CatalogSyncAction.Remove, descriptor, asset, guid,
                            path, arrayIndex, "Catalog reference no longer resolves to an asset.",
                            CatalogSyncAction.Warning);
                        continue;
                    }

                    if (!localGuids.Add(guid))
                    {
                        AddCatalogRecord(plan, CatalogSyncAction.Error, descriptor, asset, guid,
                            path, arrayIndex, "Duplicate valid reference in one catalog array.");
                        continue;
                    }

                    if (targetGuids.TryGetValue(guid, out var otherDescriptor))
                    {
                        AddCatalogRecord(plan, CatalogSyncAction.Error, descriptor, asset, guid,
                            path, arrayIndex, "Same GUID is registered in multiple arrays: " +
                            otherDescriptor.TypeName + " and " + descriptor.TypeName + ".");
                        continue;
                    }

                    targetGuids.Add(guid, descriptor);
                    target.Assets.Add(asset);
                    target.Guids.Add(guid);
                    scannedByGuid.TryGetValue(guid, out var scannedItem);
                    var action = CatalogSyncAction.Keep;
                    var reason = "Registered asset retained in existing order.";
                    if (scannedItem == null
                        && !SharedGameDataWatchRoots.TryResolveWatchRootKind(path, out _))
                    {
                        reason = "Registered asset is outside managed watch roots and was retained.";
                        AddCatalogRecord(plan, CatalogSyncAction.Warning, descriptor, asset, guid,
                            path, arrayIndex, reason);
                    }

                    AddCatalogRecord(plan, action, descriptor, asset, guid, path, arrayIndex, reason);
                }
            }

            AddScannedAssets(plan, scanned, targetGuids);
            ValidateTargetIds(plan);
            SortRecords(plan.Records);
            return plan;
        }

        private static void AddScannedAssets(
            CatalogSyncPlan plan,
            List<SharedGameDataScannedAsset> scanned,
            Dictionary<string, SharedGameDataTypeDescriptor> targetGuids)
        {
            for (var index = 0; index < scanned.Count; index++)
            {
                var item = scanned[index];
                if (targetGuids.ContainsKey(item.Guid) || item.Asset == null)
                {
                    continue;
                }

                if (item.RootKind == SharedGameDataWatchRootKind.SandboxLegacy)
                {
                    var reason = string.IsNullOrEmpty(item.DataId)
                        ? "Unregistered SandboxLegacy asset ignored; its ID is empty."
                        : "Unregistered SandboxLegacy asset ignored.";
                    AddRecord(plan, CatalogSyncAction.Ignore, item.Descriptor, item, -1,
                        reason, CatalogSyncAction.Warning);
                    continue;
                }

                var target = FindTarget(plan, item.Descriptor);
                if (target == null)
                {
                    AddRecord(plan, CatalogSyncAction.Error, item.Descriptor, item, -1,
                        "Planner could not determine a valid target array.");
                    continue;
                }

                target.Assets.Add(item.Asset);
                target.Guids.Add(item.Guid);
                targetGuids[item.Guid] = item.Descriptor;
                AddRecord(plan, CatalogSyncAction.Add, item.Descriptor, item, -1,
                    "Unregistered ProjectData asset.");
            }
        }

        private static void ValidateTargetIds(CatalogSyncPlan plan)
        {
            for (var targetIndex = 0; targetIndex < plan.Targets.Count; targetIndex++)
            {
                var target = plan.Targets[targetIndex];
                var ids = new Dictionary<string, string>(StringComparer.Ordinal);
                for (var assetIndex = 0; assetIndex < target.Assets.Count; assetIndex++)
                {
                    var asset = target.Assets[assetIndex];
                    var id = target.Descriptor.ReadDataId(asset);
                    var guid = target.Guids[assetIndex];
                    var path = AssetDatabase.GetAssetPath(asset);
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        AddCatalogRecord(plan, CatalogSyncAction.Error, target.Descriptor, asset,
                            guid, path, assetIndex,
                            "Target catalog entry has a null, empty, or whitespace-only data ID.");
                    }
                    else if (ids.TryGetValue(id, out var firstGuid))
                    {
                        AddCatalogRecord(plan, CatalogSyncAction.Error, target.Descriptor, asset,
                            guid, path, assetIndex,
                            "Duplicate target data ID within the same type; first GUID=" +
                            firstGuid + ".");
                    }
                    else
                    {
                        ids.Add(id, guid);
                    }
                }

                for (var recordIndex = 0; recordIndex < plan.Records.Count; recordIndex++)
                {
                    var record = plan.Records[recordIndex];
                    if (record.Action == CatalogSyncAction.Ignore
                        && record.Descriptor == target.Descriptor
                        && !string.IsNullOrEmpty(record.DataId)
                        && ids.ContainsKey(record.DataId))
                    {
                        record.Reason += " Its ID duplicates a target catalog ID.";
                    }
                }
            }
        }

        private static CatalogSyncTarget FindTarget(
            CatalogSyncPlan plan,
            SharedGameDataTypeDescriptor descriptor)
        {
            for (var index = 0; index < plan.Targets.Count; index++)
            {
                if (plan.Targets[index].Descriptor == descriptor)
                {
                    return plan.Targets[index];
                }
            }

            return null;
        }

        private static bool VerifyAppliedPlan(CatalogSyncPlan plan)
        {
            var serializedCatalog = new SerializedObject(plan.Catalog);
            serializedCatalog.Update();
            for (var targetIndex = 0; targetIndex < plan.Targets.Count; targetIndex++)
            {
                var target = plan.Targets[targetIndex];
                var property = serializedCatalog.FindProperty(target.Descriptor.CatalogFieldName);
                if (property == null || property.arraySize != target.Guids.Count)
                {
                    return false;
                }

                for (var index = 0; index < property.arraySize; index++)
                {
                    var asset = property.GetArrayElementAtIndex(index).objectReferenceValue;
                    var path = AssetDatabase.GetAssetPath(asset);
                    if (!string.Equals(
                            AssetDatabase.AssetPathToGUID(path),
                            target.Guids[index],
                            StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void PrintPlan(CatalogSyncPlan plan, string stage)
        {
            Debug.Log(
                $"[CatalogSync] {stage}\nCatalog: {CatalogAssetPath}\n" +
                $"Keep: {plan.Count(CatalogSyncAction.Keep)}\n" +
                $"Add: {plan.Count(CatalogSyncAction.Add)}\n" +
                $"Remove: {plan.Count(CatalogSyncAction.Remove)}\n" +
                $"Ignore: {plan.Count(CatalogSyncAction.Ignore)}\n" +
                $"Warnings: {plan.Count(CatalogSyncAction.Warning)}\n" +
                $"Errors: {plan.Count(CatalogSyncAction.Error)}");
            for (var index = 0; index < plan.Records.Count; index++)
            {
                var record = plan.Records[index];
                var message =
                    $"[CatalogSync][{record.Action}]\n" +
                    $"Type: {(record.Descriptor != null ? record.Descriptor.TypeName : "Catalog")}\n" +
                    $"Id: {record.DataId}\nRoot: {record.RootKind}\nPath: {record.AssetPath}\n" +
                    $"Guid: {record.Guid}\nReason: {record.Reason}";
                if (record.Action == CatalogSyncAction.Error)
                {
                    Debug.LogError(message);
                }
                else if (record.Action == CatalogSyncAction.Warning)
                {
                    Debug.LogWarning(message);
                }
                else
                {
                    Debug.Log(message);
                }
            }
        }

        private static void AddRecord(
            CatalogSyncPlan plan,
            CatalogSyncAction action,
            SharedGameDataTypeDescriptor descriptor,
            SharedGameDataScannedAsset item,
            int existingIndex,
            string reason,
            CatalogSyncAction? additionalAction = null)
        {
            plan.Records.Add(new CatalogSyncRecord
            {
                Action = action,
                Descriptor = descriptor,
                Guid = item != null ? item.Guid : string.Empty,
                AssetPath = item != null ? item.AssetPath : string.Empty,
                DataId = item != null ? item.DataId : string.Empty,
                RootKind = item != null ? item.RootKind : (SharedGameDataWatchRootKind?)null,
                Asset = item != null ? item.Asset : null,
                ExistingArrayIndex = existingIndex,
                Reason = reason
            });
            if (additionalAction.HasValue)
            {
                AddRecord(plan, additionalAction.Value, descriptor, item, existingIndex, reason);
            }
        }

        private static void AddCatalogRecord(
            CatalogSyncPlan plan,
            CatalogSyncAction action,
            SharedGameDataTypeDescriptor descriptor,
            ScriptableObject asset,
            string guid,
            string path,
            int existingIndex,
            string reason,
            CatalogSyncAction? additionalAction = null)
        {
            var root = SharedGameDataWatchRoots.TryResolveWatchRootKind(path, out var rootKind)
                ? rootKind
                : (SharedGameDataWatchRootKind?)null;
            plan.Records.Add(new CatalogSyncRecord
            {
                Action = action,
                Descriptor = descriptor,
                Guid = guid,
                AssetPath = SharedGameDataWatchScanner.NormalizePath(path),
                DataId = asset != null ? descriptor.ReadDataId(asset) : string.Empty,
                RootKind = root,
                Asset = asset,
                ExistingArrayIndex = existingIndex,
                Reason = reason
            });
            if (additionalAction.HasValue)
            {
                AddCatalogRecord(plan, additionalAction.Value, descriptor, asset, guid, path,
                    existingIndex, reason);
            }
        }

        private static void SortRecords(List<CatalogSyncRecord> records)
        {
            records.Sort((left, right) =>
            {
                var action = left.Action.CompareTo(right.Action);
                if (action != 0) return action;
                var leftKind = left.Descriptor != null ? (int)left.Descriptor.Kind : -1;
                var rightKind = right.Descriptor != null ? (int)right.Descriptor.Kind : -1;
                var type = leftKind.CompareTo(rightKind);
                if (type != 0) return type;
                var existing = left.ExistingArrayIndex.CompareTo(right.ExistingArrayIndex);
                if (existing != 0) return existing;
                var path = StringComparer.Ordinal.Compare(left.AssetPath, right.AssetPath);
                return path != 0 ? path : StringComparer.Ordinal.Compare(left.Guid, right.Guid);
            });
        }
    }
}
#endif
