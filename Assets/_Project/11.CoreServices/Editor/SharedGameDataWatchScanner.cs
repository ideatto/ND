#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ND.Framework.Editor
{
    internal sealed class SharedGameDataScannedAsset
    {
        public string Guid;
        public string AssetPath;
        public SharedGameDataTypeDescriptor Descriptor;
        public string DataId;
        public SharedGameDataWatchRootKind RootKind;
        public ScriptableObject Asset;
        public string LoadError;
    }

    internal static class SharedGameDataWatchScanner
    {
        public static List<SharedGameDataScannedAsset> Scan()
        {
            var results = new List<SharedGameDataScannedAsset>();
            var seenGuids = new HashSet<string>(StringComparer.Ordinal);
            ScanRoot(SharedGameDataWatchRoots.ProjectDataRoot, results, seenGuids);
            ScanRoot(SharedGameDataWatchRoots.SandboxLegacyRoot, results, seenGuids);
            return results;
        }

        private static void ScanRoot(
            string rootFolder,
            List<SharedGameDataScannedAsset> results,
            HashSet<string> seenGuids)
        {
            if (!AssetDatabase.IsValidFolder(rootFolder))
            {
                return;
            }

            var descriptors = SharedGameDataTypeCatalog.Descriptors;
            for (var descriptorIndex = 0; descriptorIndex < descriptors.Count; descriptorIndex++)
            {
                var descriptor = descriptors[descriptorIndex];
                var guids = AssetDatabase.FindAssets(descriptor.AssetFilter, new[] { rootFolder });
                Array.Sort(guids, StringComparer.Ordinal);
                for (var guidIndex = 0; guidIndex < guids.Length; guidIndex++)
                {
                    var guid = guids[guidIndex];
                    if (!seenGuids.Add(guid))
                    {
                        continue;
                    }

                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)
                        || !SharedGameDataWatchRoots.TryResolveWatchRootKind(path, out var rootKind))
                    {
                        continue;
                    }

                    var asset = AssetDatabase.LoadAssetAtPath(path, descriptor.AssetType) as ScriptableObject;
                    results.Add(new SharedGameDataScannedAsset
                    {
                        Guid = guid,
                        AssetPath = NormalizePath(path),
                        Descriptor = descriptor,
                        DataId = asset != null ? descriptor.ReadDataId(asset) : string.Empty,
                        RootKind = rootKind,
                        Asset = asset,
                        LoadError = asset == null
                            ? "Supported asset could not be loaded as " + descriptor.TypeName
                            : string.Empty
                    });
                }
            }

            results.Sort(Compare);
        }

        private static int Compare(SharedGameDataScannedAsset left, SharedGameDataScannedAsset right)
        {
            var root = left.RootKind.CompareTo(right.RootKind);
            if (root != 0)
            {
                return root;
            }

            var type = left.Descriptor.Kind.CompareTo(right.Descriptor.Kind);
            if (type != 0)
            {
                return type;
            }

            var path = StringComparer.Ordinal.Compare(left.AssetPath, right.AssetPath);
            return path != 0 ? path : StringComparer.Ordinal.Compare(left.Guid, right.Guid);
        }

        internal static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
#endif
