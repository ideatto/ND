/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Sandbox ScriptableObject seed data를 로드하고 Framework 공용 데이터 view로 변환한다.
 * - InGame 진입 전에 공용 기준 데이터의 필수 ID와 참조 무결성을 검증한다.
 *
 * Main Features
 * - Resources catalog를 우선 로드하고, catalog가 없으면 Unity Editor에서 Sandbox asset path fallback을 사용한다.
 * - Town, Market, TradeItem, Wagon, DraftAnimal, Route 데이터를 ID 기반 스냅샷으로 변환한다.
 * - M0 차단 오류와 최신 계약 충돌 경고를 분리해 로그로 제공한다.
 *
 * Important Notes
 * - Player build에서는 Resources catalog가 필요하다. Editor path fallback은 Unity Editor 통합 확인용이다.
 * - Sandbox DraftAnimalData.IncreaseMaxLoad는 최신 계약상 사용하지 않으며 경고만 남긴다.
 */
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// 공용 게임 데이터 초기화와 검증을 담당하는 Framework 서비스이다.
    /// </summary>
    public sealed class SharedGameDataService
    {
        private const string BaseCampTownPath = "Assets/99.Sandbox/_LJH/02.SO/TownSO/Town_BaseCamp.asset";
        private const string DummyTownPath = "Assets/99.Sandbox/_LJH/02.SO/TownSO/TownData_Dummy.asset";
        private const string DummyMarketPath = "Assets/99.Sandbox/_LJH/02.SO/TownMarketSO/Market_DummyTown.asset";
        private const string DummyTradeItemPath = "Assets/99.Sandbox/_LJH/02.SO/TradeItemSO/TradeItem_Dummy.asset";
        private const string DummyWagonWithAnimalsPath = "Assets/99.Sandbox/_LJH/02.SO/WagonSO/Wagon_DummyWagonWithAnimals.asset";
        private const string DummyMountPath = "Assets/99.Sandbox/_LJH/02.SO/WagonSO/Wagon_DummyMount.asset";
        private const string DummyDraftAnimalPath = "Assets/99.Sandbox/_LJH/02.SO/DraftAnimalSO/DraftAnimal_Dummy.asset";
        private const string DummyRoutePath = "Assets/99.Sandbox/_LJH/02.SO/RouteSO/Route_Dummy.asset";

        private readonly SandboxSharedGameDataCatalog explicitCatalog;

        /// <summary>
        /// 마지막으로 검증에 성공한 공용 데이터 view이다.
        /// </summary>
        public ISharedGameDataProvider CurrentData { get; private set; }

        /// <summary>
        /// 마지막 로드에서 발생한 차단 오류 요약이다.
        /// </summary>
        public string LastErrorSummary { get; private set; } = string.Empty;

        /// <summary>
        /// 마지막 로드에서 발생한 경고 요약이다.
        /// </summary>
        public string LastWarningSummary { get; private set; } = string.Empty;

        public SharedGameDataService(SandboxSharedGameDataCatalog explicitCatalog = null)
        {
            this.explicitCatalog = explicitCatalog;
        }

        /// <summary>
        /// Sandbox seed data를 로드하고 공용 데이터 view를 생성한다.
        /// </summary>
        /// <returns>필수 데이터 검증을 통과하면 true, InGame 진입을 막아야 하면 false.</returns>
        public bool LoadInitialData()
        {
            LastErrorSummary = string.Empty;
            LastWarningSummary = string.Empty;
            CurrentData = null;

            var source = LoadSource();
            var errors = new List<string>();
            var warnings = new List<string>();

            ValidateSourcePresence(source, errors);
            if (errors.Count > 0)
            {
                LogResult(errors, warnings);
                return false;
            }

            var view = BuildView(source, errors, warnings);
            ValidateReferences(view, errors);

            if (errors.Count > 0)
            {
                LogResult(errors, warnings);
                return false;
            }

            CurrentData = view;
            LogResult(errors, warnings);
            FrameworkLog.Info($"Shared game data loaded. {CurrentData.Summary}");
            return true;
        }

        private SharedGameDataSource LoadSource()
        {
            var catalog = explicitCatalog != null
                ? explicitCatalog
                : Resources.Load<SandboxSharedGameDataCatalog>(SandboxSharedGameDataCatalog.ResourceName);

            if (catalog != null)
            {
                return new SharedGameDataSource(
                    catalog.Towns,
                    catalog.Markets,
                    catalog.TradeItems,
                    catalog.Wagons,
                    catalog.DraftAnimals,
                    catalog.Routes);
            }

            // Resources catalog가 아직 없을 때도 Editor 통합 단계에서 Sandbox seed data를 검증할 수 있게 한다.
            FrameworkLog.Warning("SandboxSharedGameDataCatalog resource was not found. Trying Unity Editor Sandbox path fallback.");
            return LoadEditorSandboxSource();
        }

        private static SharedGameDataSource LoadEditorSandboxSource()
        {
            return new SharedGameDataSource(
                new[]
                {
                    LoadAssetAtPath<global::TownData>(BaseCampTownPath),
                    LoadAssetAtPath<global::TownData>(DummyTownPath)
                },
                new[]
                {
                    LoadAssetAtPath<global::MarketData>(DummyMarketPath)
                },
                new[]
                {
                    LoadAssetAtPath<global::TradeItemData>(DummyTradeItemPath)
                },
                new[]
                {
                    LoadAssetAtPath<global::WagonData>(DummyWagonWithAnimalsPath),
                    LoadAssetAtPath<global::WagonData>(DummyMountPath)
                },
                new[]
                {
                    LoadAssetAtPath<global::DraftAnimalData>(DummyDraftAnimalPath)
                },
                new[]
                {
                    LoadAssetAtPath<global::RouteData>(DummyRoutePath)
                });
        }

        private static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object
        {
            // Runtime assembly가 UnityEditor에 직접 의존하지 않도록 reflection으로 Editor 전용 fallback만 수행한다.
            var assetDatabaseType = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            if (assetDatabaseType == null)
            {
                return null;
            }

            var loadMethod = assetDatabaseType.GetMethod(
                "LoadAssetAtPath",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(Type) },
                null);
            if (loadMethod == null)
            {
                return null;
            }

            return loadMethod.Invoke(null, new object[] { path, typeof(T) }) as T;
        }

        private static void ValidateSourcePresence(SharedGameDataSource source, List<string> errors)
        {
            if (source == null)
            {
                errors.Add("Shared game data source is missing.");
                return;
            }

            RequireAny(source.Towns, "TownData", errors);
            RequireAny(source.Markets, "MarketData", errors);
            RequireAny(source.TradeItems, "TradeItemData", errors);
            RequireAny(source.Wagons, "WagonData", errors);
            RequireAny(source.DraftAnimals, "DraftAnimalData", errors);
            RequireAny(source.Routes, "RouteData", errors);
        }

        private static void RequireAny<T>(T[] items, string label, List<string> errors) where T : UnityEngine.Object
        {
            if (items == null || items.Length == 0)
            {
                errors.Add($"{label} references are missing.");
                return;
            }

            var hasValidItem = false;
            for (var index = 0; index < items.Length; index++)
            {
                if (items[index] != null)
                {
                    hasValidItem = true;
                    break;
                }
            }

            if (!hasValidItem)
            {
                errors.Add($"{label} references are all null.");
            }
        }

        private static SharedGameDataView BuildView(SharedGameDataSource source, List<string> errors, List<string> warnings)
        {
            var towns = new Dictionary<string, SharedTownDefinition>();
            var markets = new Dictionary<string, SharedMarketDefinition>();
            var tradeItems = new Dictionary<string, SharedTradeItemDefinition>();
            var wagons = new Dictionary<string, SharedWagonDefinition>();
            var draftAnimals = new Dictionary<string, SharedDraftAnimalDefinition>();
            var routes = new Dictionary<string, SharedRouteDefinition>();

            AddTowns(source.Towns, towns, errors);
            AddMarkets(source.Markets, markets, errors);
            AddTradeItems(source.TradeItems, tradeItems, errors);
            AddWagons(source.Wagons, wagons, errors);
            AddDraftAnimals(source.DraftAnimals, draftAnimals, errors, warnings);
            AddRoutes(source.Routes, routes, errors);

            return new SharedGameDataView(towns, markets, tradeItems, wagons, draftAnimals, routes);
        }

        private static void AddTowns(global::TownData[] source, Dictionary<string, SharedTownDefinition> target, List<string> errors)
        {
            if (source == null)
            {
                return;
            }

            for (var index = 0; index < source.Length; index++)
            {
                var item = source[index];
                if (item == null)
                {
                    errors.Add($"TownData at index {index} is null.");
                    continue;
                }

                var id = item.TownId;
                if (!CanAddId(target, id, "TownData", errors))
                {
                    continue;
                }

                target.Add(id, new SharedTownDefinition
                {
                    Id = id,
                    DisplayName = item.DisplayName,
                    UnlockedByDefault = item.UnlockedByDefault,
                    MarketId = item.Market != null ? item.Market.MarketId : string.Empty,
                    AvailableRouteIds = ToRouteIds(item.AvailableRoutes),
                    CanContribute = item.CanContribute,
                    MaximumContributionLimit = item.MaximumContributionLimit
                });
            }
        }

        private static void AddMarkets(global::MarketData[] source, Dictionary<string, SharedMarketDefinition> target, List<string> errors)
        {
            if (source == null)
            {
                return;
            }

            for (var index = 0; index < source.Length; index++)
            {
                var item = source[index];
                if (item == null)
                {
                    errors.Add($"MarketData at index {index} is null.");
                    continue;
                }

                var id = item.MarketId;
                if (!CanAddId(target, id, "MarketData", errors))
                {
                    continue;
                }

                target.Add(id, new SharedMarketDefinition
                {
                    Id = id,
                    ItemMaxQuantity = item.ItemMaxQuantity,
                    ItemRenewalCycle = item.ItemRenewalCycle,
                    TradeItemIds = ToTradeItemIds(item.TradeItems),
                    DraftAnimalIds = ToDraftAnimalIds(item.DraftAnimalItems),
                    WagonIds = ToWagonIds(item.WagonItems),
                    LocalSpecialtyItemIds = ToTradeItemIds(item.LocalSpecialtyItems)
                });
            }
        }

        private static void AddTradeItems(global::TradeItemData[] source, Dictionary<string, SharedTradeItemDefinition> target, List<string> errors)
        {
            if (source == null)
            {
                return;
            }

            for (var index = 0; index < source.Length; index++)
            {
                var item = source[index];
                if (item == null)
                {
                    errors.Add($"TradeItemData at index {index} is null.");
                    continue;
                }

                var id = item.ItemId;
                if (!CanAddId(target, id, "TradeItemData", errors))
                {
                    continue;
                }

                target.Add(id, new SharedTradeItemDefinition
                {
                    Id = id,
                    DisplayName = item.DisplayName,
                    Rarity = item.Rarity.ToString(),
                    Category = item.Category.ToString(),
                    BaseBuyPrice = item.BaseBuyPrice,
                    BaseSellPrice = item.BaseSellPrice,
                    CanStack = item.CanStack,
                    MaxCount = item.MaxCount,
                    Weight = item.Weight,
                    IsConsumable = item.IsConsumable,
                    LocalSpecialty = item.LocalSpecialty
                });
            }
        }

        private static void AddWagons(global::WagonData[] source, Dictionary<string, SharedWagonDefinition> target, List<string> errors)
        {
            if (source == null)
            {
                return;
            }

            for (var index = 0; index < source.Length; index++)
            {
                var item = source[index];
                if (item == null)
                {
                    errors.Add($"WagonData at index {index} is null.");
                    continue;
                }

                var id = item.WagonId;
                if (!CanAddId(target, id, "WagonData", errors))
                {
                    continue;
                }

                if (item.Overload > item.MaxLoad)
                {
                    errors.Add($"WagonData '{id}' has BaseEfficientLoad greater than MaxLoad.");
                    continue;
                }

                target.Add(id, new SharedWagonDefinition
                {
                    Id = id,
                    DisplayName = item.DisplayName,
                    WagonType = item.WagonType.ToString(),
                    MaxDurability = item.MaxDurability,
                    BaseEfficientLoad = item.Overload,
                    MaxLoad = item.MaxLoad,
                    BaseMoveSpeed = item.BaseMoveSpeed,
                    InventorySlotCount = item.InventorySlotCount,
                    MaxPullAnimals = item.MaxPullAnimals,
                    MinRequireAnimals = item.MinRequireAnimals,
                    EligibleAnimalTypes = ToEnumNames(item.EligibleAnimalTypes),
                    BaseBuyPrice = item.BaseBuyPrice,
                    CanStack = item.CanStack,
                    MaxCount = item.MaxCount
                });
            }
        }

        private static void AddDraftAnimals(
            global::DraftAnimalData[] source,
            Dictionary<string, SharedDraftAnimalDefinition> target,
            List<string> errors,
            List<string> warnings)
        {
            if (source == null)
            {
                return;
            }

            for (var index = 0; index < source.Length; index++)
            {
                var item = source[index];
                if (item == null)
                {
                    errors.Add($"DraftAnimalData at index {index} is null.");
                    continue;
                }

                var id = item.DraftAnimalId;
                if (!CanAddId(target, id, "DraftAnimalData", errors))
                {
                    continue;
                }

                if (item.IncreaseMaxLoad > 0f)
                {
                    warnings.Add($"DraftAnimalData '{id}' IncreaseMaxLoad is ignored because draft animals must not increase physical maximum load.");
                }

                target.Add(id, new SharedDraftAnimalDefinition
                {
                    Id = id,
                    DisplayName = item.DisplayName,
                    AnimalType = item.AnimalType.ToString(),
                    FoodConsumptionPerSecond = item.FeedConsumption,
                    BaseMoveSpeed = item.BaseMoveSpeed,
                    AdditionalEfficientLoad = item.IncreaseOverLoad,
                    BaseBuyPrice = item.BaseBuyPrice,
                    CanStack = item.CanStack,
                    MaxCount = item.MaxCount
                });
            }
        }

        private static void AddRoutes(global::RouteData[] source, Dictionary<string, SharedRouteDefinition> target, List<string> errors)
        {
            if (source == null)
            {
                return;
            }

            for (var index = 0; index < source.Length; index++)
            {
                var item = source[index];
                if (item == null)
                {
                    errors.Add($"RouteData at index {index} is null.");
                    continue;
                }

                var id = item.RouteId;
                if (!CanAddId(target, id, "RouteData", errors))
                {
                    continue;
                }

                target.Add(id, new SharedRouteDefinition
                {
                    Id = id,
                    DisplayName = item.DisplayName,
                    FromTownId = item.FromTownId,
                    ToTownId = item.ToTownId,
                    UnlockedByDefault = item.UnlockedByDefault,
                    Distance = item.Distance,
                    DefaultElapsedTime = item.DefaultElapsedTime,
                    BaseRequiredFoodQuantity = item.BaseRequiredFoodQuantity,
                    BaseRequiredMercenaryPower = item.BaseRequiredMercenaryPower,
                    BaseRiskLevel = item.BaseRiskLevel,
                    MaxEventCount = item.MaxEventCount
                });
            }
        }

        private static bool CanAddId<T>(Dictionary<string, T> target, string id, string label, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add($"{label} has an empty ID.");
                return false;
            }

            if (target.ContainsKey(id))
            {
                errors.Add($"{label} has a duplicate ID: {id}");
                return false;
            }

            return true;
        }

        private static void ValidateReferences(SharedGameDataView view, List<string> errors)
        {
            ValidateTownReferences(view, errors);
            ValidateMarketReferences(view, errors);
            ValidateRouteReferences(view, errors);
        }

        private static void ValidateTownReferences(SharedGameDataView view, List<string> errors)
        {
            foreach (var townId in view.TownIds)
            {
                SharedTownDefinition town;
                if (!view.TryGetTown(townId, out town))
                {
                    continue;
                }

                SharedMarketDefinition referencedMarket;
                if (!string.IsNullOrEmpty(town.MarketId) && !view.TryGetMarket(town.MarketId, out referencedMarket))
                {
                    errors.Add($"Town '{town.Id}' references missing market '{town.MarketId}'.");
                }

                ValidateIdArray<SharedRouteDefinition>(
                    town.AvailableRouteIds,
                    view.TryGetRoute,
                    $"Town '{town.Id}' route",
                    errors);
            }
        }

        private static void ValidateMarketReferences(SharedGameDataView view, List<string> errors)
        {
            foreach (var marketId in view.MarketIds)
            {
                SharedMarketDefinition market;
                if (!view.TryGetMarket(marketId, out market))
                {
                    continue;
                }

                ValidateIdArray<SharedTradeItemDefinition>(
                    market.TradeItemIds,
                    view.TryGetTradeItem,
                    $"Market '{market.Id}' trade item",
                    errors);
                ValidateIdArray<SharedTradeItemDefinition>(
                    market.LocalSpecialtyItemIds,
                    view.TryGetTradeItem,
                    $"Market '{market.Id}' local specialty",
                    errors);
                ValidateIdArray<SharedWagonDefinition>(
                    market.WagonIds,
                    view.TryGetWagon,
                    $"Market '{market.Id}' wagon",
                    errors);
                ValidateIdArray<SharedDraftAnimalDefinition>(
                    market.DraftAnimalIds,
                    view.TryGetDraftAnimal,
                    $"Market '{market.Id}' draft animal",
                    errors);
            }
        }

        private static void ValidateRouteReferences(SharedGameDataView view, List<string> errors)
        {
            foreach (var routeId in view.RouteIds)
            {
                SharedRouteDefinition route;
                if (!view.TryGetRoute(routeId, out route))
                {
                    continue;
                }

                SharedTownDefinition fromTown;
                if (string.IsNullOrEmpty(route.FromTownId) || !view.TryGetTown(route.FromTownId, out fromTown))
                {
                    errors.Add($"Route '{route.Id}' references missing from-town '{route.FromTownId}'.");
                }

                SharedTownDefinition toTown;
                if (string.IsNullOrEmpty(route.ToTownId) || !view.TryGetTown(route.ToTownId, out toTown))
                {
                    errors.Add($"Route '{route.Id}' references missing to-town '{route.ToTownId}'.");
                }
            }
        }

        private delegate bool TryGetDefinition<T>(string id, out T definition);

        private static void ValidateIdArray<T>(
            string[] ids,
            TryGetDefinition<T> tryGet,
            string label,
            List<string> errors)
        {
            if (ids == null)
            {
                return;
            }

            for (var index = 0; index < ids.Length; index++)
            {
                var id = ids[index];
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                T definition;
                if (!tryGet(id, out definition))
                {
                    errors.Add($"{label} reference is missing: {id}");
                }
            }
        }

        private static string[] ToRouteIds(global::RouteData[] items)
        {
            if (items == null)
            {
                return new string[0];
            }

            var ids = new List<string>();
            for (var index = 0; index < items.Length; index++)
            {
                if (items[index] != null && !string.IsNullOrEmpty(items[index].RouteId))
                {
                    ids.Add(items[index].RouteId);
                }
            }

            return ids.ToArray();
        }

        private static string[] ToTradeItemIds(global::TradeItemData[] items)
        {
            if (items == null)
            {
                return new string[0];
            }

            var ids = new List<string>();
            for (var index = 0; index < items.Length; index++)
            {
                if (items[index] != null && !string.IsNullOrEmpty(items[index].ItemId))
                {
                    ids.Add(items[index].ItemId);
                }
            }

            return ids.ToArray();
        }

        private static string[] ToDraftAnimalIds(global::DraftAnimalData[] items)
        {
            if (items == null)
            {
                return new string[0];
            }

            var ids = new List<string>();
            for (var index = 0; index < items.Length; index++)
            {
                if (items[index] != null && !string.IsNullOrEmpty(items[index].DraftAnimalId))
                {
                    ids.Add(items[index].DraftAnimalId);
                }
            }

            return ids.ToArray();
        }

        private static string[] ToWagonIds(global::WagonData[] items)
        {
            if (items == null)
            {
                return new string[0];
            }

            var ids = new List<string>();
            for (var index = 0; index < items.Length; index++)
            {
                if (items[index] != null && !string.IsNullOrEmpty(items[index].WagonId))
                {
                    ids.Add(items[index].WagonId);
                }
            }

            return ids.ToArray();
        }

        private static string[] ToEnumNames<T>(T[] values)
        {
            if (values == null)
            {
                return new string[0];
            }

            var names = new string[values.Length];
            for (var index = 0; index < values.Length; index++)
            {
                names[index] = values[index].ToString();
            }

            return names;
        }

        private void LogResult(List<string> errors, List<string> warnings)
        {
            LastErrorSummary = JoinMessages(errors);
            LastWarningSummary = JoinMessages(warnings);

            for (var index = 0; index < warnings.Count; index++)
            {
                FrameworkLog.Warning(warnings[index]);
            }

            for (var index = 0; index < errors.Count; index++)
            {
                FrameworkLog.Error(errors[index]);
            }
        }

        private static string JoinMessages(List<string> messages)
        {
            if (messages == null || messages.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (var index = 0; index < messages.Count; index++)
            {
                if (index > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(messages[index]);
            }

            return builder.ToString();
        }

        private sealed class SharedGameDataSource
        {
            public readonly global::TownData[] Towns;
            public readonly global::MarketData[] Markets;
            public readonly global::TradeItemData[] TradeItems;
            public readonly global::WagonData[] Wagons;
            public readonly global::DraftAnimalData[] DraftAnimals;
            public readonly global::RouteData[] Routes;

            public SharedGameDataSource(
                global::TownData[] towns,
                global::MarketData[] markets,
                global::TradeItemData[] tradeItems,
                global::WagonData[] wagons,
                global::DraftAnimalData[] draftAnimals,
                global::RouteData[] routes)
            {
                Towns = towns ?? new global::TownData[0];
                Markets = markets ?? new global::MarketData[0];
                TradeItems = tradeItems ?? new global::TradeItemData[0];
                Wagons = wagons ?? new global::WagonData[0];
                DraftAnimals = draftAnimals ?? new global::DraftAnimalData[0];
                Routes = routes ?? new global::RouteData[0];
            }
        }
    }
}
