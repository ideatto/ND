#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Framework.Editor
{
    internal enum SharedGameDataCatalogTypeKind
    {
        Town,
        Market,
        TradeItem,
        Wagon,
        DraftAnimal,
        Route,
        Quest,
        Build
    }

    internal sealed class SharedGameDataTypeDescriptor
    {
        public SharedGameDataTypeDescriptor(
            SharedGameDataCatalogTypeKind kind,
            Type assetType,
            string typeName,
            string assetFilter,
            string catalogFieldName,
            Func<ScriptableObject, string> readDataId)
        {
            Kind = kind;
            AssetType = assetType;
            TypeName = typeName;
            AssetFilter = assetFilter;
            CatalogFieldName = catalogFieldName;
            ReadDataId = readDataId;
        }

        public SharedGameDataCatalogTypeKind Kind { get; }
        public Type AssetType { get; }
        public string TypeName { get; }
        public string AssetFilter { get; }
        public string CatalogFieldName { get; }
        public Func<ScriptableObject, string> ReadDataId { get; }
    }

    internal static class SharedGameDataTypeCatalog
    {
        private static readonly SharedGameDataTypeDescriptor[] Items =
        {
            Create<global::TownData>(SharedGameDataCatalogTypeKind.Town, "towns", asset => asset.TownId),
            Create<global::MarketData>(SharedGameDataCatalogTypeKind.Market, "markets", asset => asset.MarketId),
            Create<global::TradeItemData>(SharedGameDataCatalogTypeKind.TradeItem, "tradeItems", asset => asset.ItemId),
            Create<global::WagonData>(SharedGameDataCatalogTypeKind.Wagon, "wagons", asset => asset.WagonId),
            Create<global::DraftAnimalData>(SharedGameDataCatalogTypeKind.DraftAnimal, "draftAnimals", asset => asset.DraftAnimalId),
            Create<global::RouteData>(SharedGameDataCatalogTypeKind.Route, "routes", asset => asset.RouteId),
            Create<global::QuestData>(SharedGameDataCatalogTypeKind.Quest, "quests", asset => asset.QuestId),
            Create<global::BuildData>(SharedGameDataCatalogTypeKind.Build, "builds", asset => asset.BuildId)
        };

        public static IReadOnlyList<SharedGameDataTypeDescriptor> Descriptors => Items;

        public static bool TryGetDescriptor(Type assetType, out SharedGameDataTypeDescriptor descriptor)
        {
            for (var index = 0; index < Items.Length; index++)
            {
                if (Items[index].AssetType == assetType)
                {
                    descriptor = Items[index];
                    return true;
                }
            }

            descriptor = null;
            return false;
        }

        private static SharedGameDataTypeDescriptor Create<T>(
            SharedGameDataCatalogTypeKind kind,
            string catalogFieldName,
            Func<T, string> readDataId)
            where T : ScriptableObject
        {
            return new SharedGameDataTypeDescriptor(
                kind,
                typeof(T),
                typeof(T).Name,
                "t:" + typeof(T).Name,
                catalogFieldName,
                asset => readDataId((T)asset));
        }
    }
}
#endif
