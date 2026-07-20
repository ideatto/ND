using System;
using System.Collections.Generic;
using ND.Economy;

namespace ND.Framework
{
    /// <summary>
    /// 저장된 Caravan cargo와 상품 카탈로그를 순수 건설 비용 계산 입력으로 변환한다.
    /// SaveData와 원본 컬렉션은 변경하지 않는다.
    /// </summary>
    public static class CaravanBuildingCostInputFactory
    {
        public static bool TryCreate(
            SaveData saveData,
            IEnumerable<TradeItemData> tradeItemCatalog,
            BuildingCostDefinition definition,
            int currentLevel,
            out BuildingCostInput input)
        {
            input = null;
            if (saveData?.caravan?.cargo == null || definition == null || tradeItemCatalog == null)
            {
                return false;
            }

            Dictionary<string, TradeItemData> catalogById;
            if (!TryBuildCatalog(tradeItemCatalog, out catalogById))
            {
                return false;
            }

            var cargoSnapshot = new List<InventoryItemAmount>(saveData.caravan.cargo.Count);
            for (int i = 0; i < saveData.caravan.cargo.Count; i++)
            {
                CargoEntrySaveData cargo = saveData.caravan.cargo[i];
                string itemId = cargo?.item?.itemId;
                TradeItemData itemData;
                if (cargo == null || cargo.item == null || string.IsNullOrWhiteSpace(itemId)
                    || cargo.quantity < 0 || !catalogById.TryGetValue(itemId, out itemData))
                {
                    return false;
                }

                cargoSnapshot.Add(new InventoryItemAmount
                {
                    ItemId = itemId,
                    Category = itemData.Category,
                    Quantity = cargo.quantity
                });
            }

            input = new BuildingCostInput
            {
                DisplayName = definition.DisplayName ?? string.Empty,
                CurrentLevel = currentLevel,
                MaxLevel = definition.MaxLevel,
                LevelCosts = definition.LevelCosts != null
                    ? new List<BuildingLevelCost>(definition.LevelCosts)
                    : null,
                CaravanCargo = cargoSnapshot
            };
            return true;
        }

        private static bool TryBuildCatalog(
            IEnumerable<TradeItemData> source,
            out Dictionary<string, TradeItemData> catalogById)
        {
            catalogById = new Dictionary<string, TradeItemData>(StringComparer.Ordinal);
            foreach (TradeItemData item in source)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ItemId)
                    || catalogById.ContainsKey(item.ItemId))
                {
                    return false;
                }

                catalogById.Add(item.ItemId, item);
            }

            return true;
        }
    }
}
