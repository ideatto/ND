/*

 * Technical Ownership

 * - Responsible Discipline: Framework & Integration

 *

 * Script Purpose

 * - watch root 아래 공용 SO를 AssetDatabase로 스캔하고 catalog 등록 여부와 비교한다.

 * - Player용 SharedGameDataWatchInventory 스냅샷을 갱신한다.

 *

 * Main Features

 * - Town/Market/TradeItem/Wagon/DraftAnimal/Route 타입만 스캔한다.

 * - GUID 기준으로 catalog 미등록 에셋을 수집한다.

 * - ND/Framework/Refresh Shared Game Data Watch Inventory 메뉴로 inventory를 재생성한다.

 *

 * Important Notes

 * - Editor 전용이다. Player는 inventory 스냅샷으로 동일 정책을 적용한다.

 * - InGameTimePolicyConfig와 SandboxSharedGameDataCatalog는 스캔 대상에서 제외된다.

 */

#if UNITY_EDITOR

using System;

using System.Collections.Generic;

using UnityEditor;

using UnityEngine;



namespace ND.Framework.Editor

{

    /// <summary>

    /// SharedGameData catalog와 watch 폴더 SO를 비교하는 Editor 유틸리티이다.

    /// </summary>

    public static class SharedGameDataCatalogDriftChecker

    {

        private const string InventoryAssetPath =

            "Assets/_Project/11.CoreServices/Resources/SharedGameDataWatchInventory.asset";



        private static readonly string[] WatchedTypeFilters =

        {

            "t:TownData",

            "t:MarketData",

            "t:TradeItemData",

            "t:WagonData",

            "t:DraftAnimalData",

            "t:RouteData"

        };



        /// <summary>

        /// 현재 catalog와 디스크 watch root를 비교해 미등록 에셋 목록을 반환한다.

        /// SharedGameDataService가 reflection으로 호출한다.

        /// </summary>

        public static List<SharedGameDataDriftFinding> CollectUnregisteredAssets(

            SandboxSharedGameDataCatalog catalog)

        {

            var catalogGuids = CollectCatalogGuids(catalog);

            var watched = ScanWatchedAssets();

            return BuildUnregisteredFindings(watched, catalogGuids);

        }



        /// <summary>

        /// watch inventory 스냅샷과 catalog 등록 GUID를 Resources 에셋에 기록한다.

        /// </summary>

        [MenuItem("ND/Framework/Refresh Shared Game Data Watch Inventory")]

        public static void RefreshWatchInventoryMenu()

        {

            var catalog = Resources.Load<SandboxSharedGameDataCatalog>(

                SandboxSharedGameDataCatalog.ResourceName);

            if (catalog == null)

            {

                catalog = AssetDatabase.LoadAssetAtPath<SandboxSharedGameDataCatalog>(

                    "Assets/_Project/11.CoreServices/Resources/SandboxSharedGameDataCatalog.asset");

            }



            if (catalog == null)

            {

                Debug.LogError(

                    "[Framework] SharedGameDataWatchInventory refresh failed because SandboxSharedGameDataCatalog was not found.");

                return;

            }



            RefreshWatchInventory(catalog);

            Debug.Log("[Framework] SharedGameDataWatchInventory refreshed.");

        }



        /// <summary>

        /// 지정 catalog 기준으로 inventory 에셋을 생성하거나 갱신한다.

        /// </summary>

        public static SharedGameDataWatchInventory RefreshWatchInventory(

            SandboxSharedGameDataCatalog catalog)

        {

            if (catalog == null)

            {

                throw new ArgumentNullException(nameof(catalog));

            }



            var watched = ScanWatchedAssets();

            var entries = new SharedGameDataWatchInventory.Entry[watched.Count];

            for (var index = 0; index < watched.Count; index++)

            {

                var item = watched[index];

                entries[index] = new SharedGameDataWatchInventory.Entry

                {

                    assetGuid = item.AssetGuid,

                    assetPath = item.AssetPath,

                    typeName = item.TypeName,

                    dataId = item.DataId,

                    watchRootKind = item.WatchRootKind

                };

            }



            var catalogGuids = CollectCatalogGuids(catalog);

            var catalogGuidArray = new string[catalogGuids.Count];

            catalogGuids.CopyTo(catalogGuidArray);



            var inventory = AssetDatabase.LoadAssetAtPath<SharedGameDataWatchInventory>(InventoryAssetPath);

            if (inventory == null)

            {

                inventory = ScriptableObject.CreateInstance<SharedGameDataWatchInventory>();

                AssetDatabase.CreateAsset(inventory, InventoryAssetPath);

            }



            inventory.ReplaceSnapshot(entries, catalogGuidArray);

            EditorUtility.SetDirty(inventory);

            AssetDatabase.SaveAssets();

            AssetDatabase.Refresh();

            return inventory;

        }



        private static List<SharedGameDataDriftFinding> BuildUnregisteredFindings(

            List<WatchedAsset> watched,

            HashSet<string> catalogGuids)

        {

            var findings = new List<SharedGameDataDriftFinding>();

            for (var index = 0; index < watched.Count; index++)

            {

                var item = watched[index];

                if (catalogGuids.Contains(item.AssetGuid))

                {

                    continue;

                }



                findings.Add(new SharedGameDataDriftFinding

                {

                    AssetGuid = item.AssetGuid,

                    AssetPath = item.AssetPath,

                    TypeName = item.TypeName,

                    DataId = item.DataId,

                    WatchRootKind = item.WatchRootKind

                });

            }



            return findings;

        }



        private static HashSet<string> CollectCatalogGuids(SandboxSharedGameDataCatalog catalog)

        {

            var guids = new HashSet<string>(StringComparer.Ordinal);

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

            return guids;

        }



        private static void AddObjectGuids(UnityEngine.Object[] objects, HashSet<string> guids)

        {

            if (objects == null)

            {

                return;

            }



            for (var index = 0; index < objects.Length; index++)

            {

                var asset = objects[index];

                if (asset == null)

                {

                    continue;

                }



                var path = AssetDatabase.GetAssetPath(asset);

                if (string.IsNullOrEmpty(path))

                {

                    continue;

                }



                var guid = AssetDatabase.AssetPathToGUID(path);

                if (!string.IsNullOrEmpty(guid))

                {

                    guids.Add(guid);

                }

            }

        }



        private static List<WatchedAsset> ScanWatchedAssets()

        {

            var results = new List<WatchedAsset>();

            var seenGuids = new HashSet<string>(StringComparer.Ordinal);



            ScanRoot(SharedGameDataWatchRoots.ProjectDataRoot, results, seenGuids);

            ScanRoot(SharedGameDataWatchRoots.SandboxLegacyRoot, results, seenGuids);

            return results;

        }



        private static void ScanRoot(

            string rootFolder,

            List<WatchedAsset> results,

            HashSet<string> seenGuids)

        {

            if (!AssetDatabase.IsValidFolder(rootFolder))

            {

                return;

            }



            for (var filterIndex = 0; filterIndex < WatchedTypeFilters.Length; filterIndex++)

            {

                var filter = WatchedTypeFilters[filterIndex];

                var guids = AssetDatabase.FindAssets(filter, new[] { rootFolder });

                for (var guidIndex = 0; guidIndex < guids.Length; guidIndex++)

                {

                    var guid = guids[guidIndex];

                    if (!seenGuids.Add(guid))

                    {

                        continue;

                    }



                    var path = AssetDatabase.GUIDToAssetPath(guid);

                    if (string.IsNullOrEmpty(path)

                        || !SharedGameDataWatchRoots.TryResolveWatchRootKind(path, out var watchRootKind))

                    {

                        continue;

                    }



                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                    if (asset == null)

                    {

                        continue;

                    }



                    // catalog / time policy 등 비대상 SO가 타입 필터에 걸리지 않도록 한 번 더 확인한다.

                    if (!TryReadWatchedIdentity(asset, out var typeName, out var dataId))

                    {

                        continue;

                    }



                    results.Add(new WatchedAsset

                    {

                        AssetGuid = guid,

                        AssetPath = path,

                        TypeName = typeName,

                        DataId = dataId,

                        WatchRootKind = watchRootKind

                    });

                }

            }

        }



        private static bool TryReadWatchedIdentity(

            ScriptableObject asset,

            out string typeName,

            out string dataId)

        {

            switch (asset)

            {

                case global::TownData town:

                    typeName = nameof(TownData);

                    dataId = town.TownId;

                    return true;

                case global::MarketData market:

                    typeName = nameof(MarketData);

                    dataId = market.MarketId;

                    return true;

                case global::TradeItemData tradeItem:

                    typeName = nameof(TradeItemData);

                    dataId = tradeItem.ItemId;

                    return true;

                case global::WagonData wagon:

                    typeName = nameof(WagonData);

                    dataId = wagon.WagonId;

                    return true;

                case global::DraftAnimalData draftAnimal:

                    typeName = nameof(DraftAnimalData);

                    dataId = draftAnimal.DraftAnimalId;

                    return true;

                case global::RouteData route:

                    typeName = nameof(RouteData);

                    dataId = route.RouteId;

                    return true;

                default:

                    typeName = string.Empty;

                    dataId = string.Empty;

                    return false;

            }

        }



        private sealed class WatchedAsset

        {

            public string AssetGuid;

            public string AssetPath;

            public string TypeName;

            public string DataId;

            public SharedGameDataWatchRootKind WatchRootKind;

        }

    }

}

#endif


