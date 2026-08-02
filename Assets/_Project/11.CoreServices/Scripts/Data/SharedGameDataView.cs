/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Sandbox ScriptableObject에서 읽은 공용 기준 데이터를 Framework가 안정적으로 노출할 수 있는 스냅샷으로 보관한다.
 * - ID 기반 lookup을 제공해 SaveData와 공용 기준 데이터의 연결 지점을 만든다.
 *
 * Main Features
 * - Town, Market, TradeItem, Wagon, DraftAnimal, Route 정의를 사전 형태로 보관한다.
 * - 외부 시스템에는 Sandbox 원본 객체 대신 Framework 정의 객체를 반환한다.
 *
 * Important Notes
 * - DraftAnimalDefinition.AdditionalEfficientLoad는 Sandbox의 IncreaseOverLoad를 사용한다.
 * - Sandbox의 IncreaseMaxLoad는 최신 1차 빌드 규칙과 충돌하므로 저장하지 않는다.
 * - SharedTradeItemDefinition.PriceModifiers는 Economy M1 가격 계산용 스냅샷이다.
 */
using System.Collections.Generic;
using ND.Economy;
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// 검증된 공용 기준 데이터의 Framework-facing view이다.
    /// </summary>
    public sealed class SharedGameDataView : ISharedGameDataProvider
    {
        private readonly Dictionary<string, SharedTownDefinition> towns;
        private readonly Dictionary<string, SharedMarketDefinition> markets;
        private readonly Dictionary<string, SharedTradeItemDefinition> tradeItems;
        private readonly Dictionary<string, SharedWagonDefinition> wagons;
        private readonly Dictionary<string, SharedDraftAnimalDefinition> draftAnimals;
        private readonly Dictionary<string, SharedRouteDefinition> routes;
        private readonly Dictionary<string, SharedQuestDefinition> quests;

        public SharedGameDataView(
            Dictionary<string, SharedTownDefinition> towns,
            Dictionary<string, SharedMarketDefinition> markets,
            Dictionary<string, SharedTradeItemDefinition> tradeItems,
            Dictionary<string, SharedWagonDefinition> wagons,
            Dictionary<string, SharedDraftAnimalDefinition> draftAnimals,
            Dictionary<string, SharedRouteDefinition> routes,
            Dictionary<string, SharedQuestDefinition> quests = null)
        {
            this.towns = towns ?? new Dictionary<string, SharedTownDefinition>();
            this.markets = markets ?? new Dictionary<string, SharedMarketDefinition>();
            this.tradeItems = tradeItems ?? new Dictionary<string, SharedTradeItemDefinition>();
            this.wagons = wagons ?? new Dictionary<string, SharedWagonDefinition>();
            this.draftAnimals = draftAnimals ?? new Dictionary<string, SharedDraftAnimalDefinition>();
            this.routes = routes ?? new Dictionary<string, SharedRouteDefinition>();
            this.quests = quests ?? new Dictionary<string, SharedQuestDefinition>();
        }

        public bool IsLoaded => true;

        public int TownCount => towns.Count;

        public int MarketCount => markets.Count;

        public int TradeItemCount => tradeItems.Count;

        public int WagonCount => wagons.Count;

        public int DraftAnimalCount => draftAnimals.Count;

        public int RouteCount => routes.Count;

        public int QuestCount => quests.Count;

        public IReadOnlyList<string> TownIds => new List<string>(towns.Keys);

        public IReadOnlyList<string> MarketIds => new List<string>(markets.Keys);

        public IReadOnlyList<string> TradeItemIds => new List<string>(tradeItems.Keys);

        public IReadOnlyList<string> WagonIds => new List<string>(wagons.Keys);

        public IReadOnlyList<string> DraftAnimalIds => new List<string>(draftAnimals.Keys);

        public IReadOnlyList<string> RouteIds => new List<string>(routes.Keys);

        public IReadOnlyList<string> QuestIds => new List<string>(quests.Keys);

        public string Summary =>
            $"Towns: {TownCount}, Markets: {MarketCount}, TradeItems: {TradeItemCount}, Wagons: {WagonCount}, DraftAnimals: {DraftAnimalCount}, Routes: {RouteCount}, Quests: {QuestCount}";

        public bool TryGetTown(string id, out SharedTownDefinition town)
        {
            return TryGetValue(towns, id, out town);
        }

        public bool TryGetMarket(string id, out SharedMarketDefinition market)
        {
            return TryGetValue(markets, id, out market);
        }

        public bool TryGetTradeItem(string id, out SharedTradeItemDefinition tradeItem)
        {
            return TryGetValue(tradeItems, id, out tradeItem);
        }

        public bool TryGetWagon(string id, out SharedWagonDefinition wagon)
        {
            return TryGetValue(wagons, id, out wagon);
        }

        public bool TryGetDraftAnimal(string id, out SharedDraftAnimalDefinition draftAnimal)
        {
            return TryGetValue(draftAnimals, id, out draftAnimal);
        }

        public bool TryGetRoute(string id, out SharedRouteDefinition route)
        {
            return TryGetValue(routes, id, out route);
        }

        public bool TryGetQuest(string id, out SharedQuestDefinition quest)
        {
            return TryGetValue(quests, id, out quest);
        }

        public IReadOnlyList<SharedQuestDefinition> GetQuestsForTown(string townId)
        {
            var result = new List<SharedQuestDefinition>();
            if (string.IsNullOrWhiteSpace(townId)) return result;
            foreach (SharedQuestDefinition quest in quests.Values)
            {
                if (quest != null &&
                    string.Equals(
                        quest.SubmissionTownId,
                        townId,
                        System.StringComparison.Ordinal))
                {
                    result.Add(quest);
                }
            }
            result.Sort((left, right) =>
                string.CompareOrdinal(left?.Id, right?.Id));
            return result;
        }

        private static bool TryGetValue<T>(Dictionary<string, T> source, string id, out T value)
        {
            value = default(T);
            return !string.IsNullOrEmpty(id)
                && source != null
                && source.TryGetValue(id, out value);
        }
    }

    public sealed class SharedTownDefinition
    {
        public string Id;
        public string DisplayName;
        public bool UnlockedByDefault;
        public string MarketId;
        public string[] AvailableRouteIds;
        public bool CanContribute;
        public float MaximumContributionLimit;
    }

    public sealed class SharedMarketDefinition
    {
        public string Id;
        public int ItemMaxQuantity;
        public float ItemRenewalCycle;
        public string[] TradeItemIds;
        public string[] DraftAnimalIds;
        public string[] WagonIds;
        public string[] LocalSpecialtyItemIds;
    }

    public sealed class SharedTradeItemDefinition
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public Sprite Icon;
        public string Rarity;
        public string Category;
        public long BaseBuyPrice;
        public long BaseSellPrice;
        public bool CanStack;
        public int MaxCount;
        public float Weight;
        public bool IsConsumable;
        public bool LocalSpecialty;
        public List<PriceModifierInput> PriceModifiers = new List<PriceModifierInput>();
    }

    public sealed class SharedWagonDefinition
    {
        public string Id;
        public string DisplayName;
        public string WagonType;
        public int MaxDurability;
        public float BaseEfficientLoad;
        public float MaxLoad;
        public float BaseMoveSpeed;
        public int InventorySlotCount;
        public int MaxPullAnimals;
        public int MinRequireAnimals;
        public string[] EligibleAnimalTypes;
        public long BaseBuyPrice;
        public bool CanStack;
        public int MaxCount;
    }

    public sealed class SharedDraftAnimalDefinition
    {
        public string Id;
        public string DisplayName;
        public string AnimalType;
        public float FoodConsumptionPerSecond;
        public float BaseMoveSpeed;
        public float AdditionalEfficientLoad;
        public long BaseBuyPrice;
        public bool CanStack;
        public int MaxCount;
    }

    public sealed class SharedRouteDefinition
    {
        public string Id;
        public string DisplayName;
        public string FromTownId;
        public string ToTownId;
        public bool UnlockedByDefault;
        public float Distance;
        public float DefaultElapsedTime;
        public int BaseRequiredFoodQuantity;
        public int BaseRequiredMercenaryPower;
        public float BaseRiskLevel;
        public int MaxEventCount;
        public SharedRouteEventDefinition[] Events = new SharedRouteEventDefinition[0];
        public long BaseFoodCost;
        public long BaseMercenaryCost;
    }

    public sealed class SharedRouteEventDefinition
    {
        public string Id;
        public RouteEvent EventType;
        public string DisplayName;
        public string Description;
        public int BanditCombatPower;
        public float CargoLootRate;
        public float FodderLootRate;
        public RewardType RewardType;
        public long Reward;
        public long MinReward;
        public long MaxReward;
    }

    public sealed class SharedQuestDefinition
    {
        public string Id;
        public string DisplayName;
        /// <summary>Catalog description exposed so UI does not depend on the source ScriptableObject.</summary>
        public string Description;
        public QuestType Type;
        public QuestRepeatPolicy RepeatPolicy;
        public QuestPaymentPolicy PaymentPolicy;
        public int RegenerationSeconds;
        public string SubmissionTownId;
        public long RequiredTradingCurrency;
        public SharedQuestItemCost[] RequiredItems = new SharedQuestItemCost[0];
        public SharedQuestReward[] Rewards = new SharedQuestReward[0];
    }

    public sealed class SharedQuestItemCost
    {
        public string ItemId;
        public int Quantity;
    }

    public sealed class SharedQuestReward
    {
        public QuestRewardType RewardType;
        public string RewardId;
        public long Value;
        public float EncounterMultiplier;
    }
}
