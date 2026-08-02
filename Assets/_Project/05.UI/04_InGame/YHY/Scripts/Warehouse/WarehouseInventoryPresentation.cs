using System;
using System.Collections.Generic;
using System.Linq;
using ND.Framework;
using UnityEngine;

namespace ND.UI.InGame.Warehouse
{
    /// <summary>Mutually exclusive modal state; tooltip is allowed only while this is None.</summary>
    public enum WarehouseSelectionPanel { None, PriceGroup, Quantity, Busy }

    /// <summary>Render-only data for one physical stack slot.</summary>
    public sealed class WarehouseInventorySlotViewData
    {
        public string ItemId;
        public string DisplayName;
        public Sprite Icon;
        public int TotalQuantity;
        public int StackIndex;
        public int StackQuantity;
    }

    /// <summary>Catalog-backed tooltip data; it never exposes mutable SaveData references.</summary>
    public sealed class WarehouseItemTooltipViewData
    {
        public string ItemId;
        public string DisplayName;
        public string Description;
        public long BaseBuyPrice;
        public Sprite Icon;
        public bool IsCatalogMissing;
    }

    /// <summary>Selected item summary and its derived purchase-price rows.</summary>
    public sealed class WarehousePriceGroupModalViewData
    {
        public string ItemId;
        public string DisplayName;
        public Sprite Icon;
        public int TotalQuantity;
        public IReadOnlyList<WarehousePriceGroup> Groups;
    }

    /// <summary>
    /// Converts current inventory snapshots into render-only models. It deliberately performs
    /// no caching so each refresh reflects the latest successful SaveData state.
    /// </summary>
    public static class WarehouseInventoryViewDataBuilder
    {
        public static IReadOnlyList<WarehouseInventorySlotViewData> BuildSlots(
            IEnumerable<CargoEntrySaveData> entries, ISharedGameDataProvider catalog)
        {
            if (entries == null) return Array.Empty<WarehouseInventorySlotViewData>();
            var result = new List<WarehouseInventorySlotViewData>();
            foreach (var group in entries
                .Where(e => e?.item != null && e.quantity > 0 && !string.IsNullOrWhiteSpace(e.item.itemId))
                .GroupBy(e => e.item.itemId, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                int total = group.Sum(e => e.quantity);
                SharedTradeItemDefinition definition = null;
                catalog?.TryGetTradeItem(group.Key, out definition);
                TradeItemSaveData saved = group.First().item;
                // Purchase-price groups share physical slots, so split only the itemId total.
                int maxStack = Math.Max(1, definition?.MaxCount ?? saved.maxCount);
                int remaining = total;
                int stackIndex = 0;
                while (remaining > 0)
                {
                    int stackQuantity = Math.Min(maxStack, remaining);
                    result.Add(new WarehouseInventorySlotViewData
                    {
                        ItemId = group.Key,
                        DisplayName = definition?.DisplayName ?? saved.itemName ?? group.Key,
                        Icon = definition?.Icon,
                        TotalQuantity = total,
                        StackIndex = stackIndex++,
                        StackQuantity = stackQuantity
                    });
                    remaining -= stackQuantity;
                }
            }
            return result;
        }

        public static WarehouseItemTooltipViewData BuildTooltip(
            string itemId, ISharedGameDataProvider catalog)
        {
            if (catalog != null && catalog.TryGetTradeItem(itemId, out SharedTradeItemDefinition item))
            {
                return new WarehouseItemTooltipViewData
                {
                    ItemId = itemId, DisplayName = item.DisplayName, Description = item.Description,
                    BaseBuyPrice = Math.Max(0L, item.BaseBuyPrice), Icon = item.Icon
                };
            }
            return new WarehouseItemTooltipViewData
            {
                ItemId = itemId ?? string.Empty, DisplayName = "알 수 없는 아이템",
                Description = "카탈로그에서 아이템 정보를 찾을 수 없습니다.", IsCatalogMissing = true
            };
        }

        public static WarehousePriceGroupModalViewData BuildPriceGroups(
            IEnumerable<CargoEntrySaveData> source, string itemId, ISharedGameDataProvider catalog)
        {
            IReadOnlyList<WarehousePriceGroup> groups = WarehousePriceGroupResolver.Resolve(source, itemId);
            SharedTradeItemDefinition definition = null;
            catalog?.TryGetTradeItem(itemId, out definition);
            return new WarehousePriceGroupModalViewData
            {
                ItemId = itemId ?? string.Empty,
                DisplayName = definition?.DisplayName ?? itemId ?? string.Empty,
                Icon = definition?.Icon,
                TotalQuantity = groups.Sum(g => g.Quantity),
                Groups = groups
            };
        }
    }

    /// <summary>
    /// View가 보낸 선택 의도를 SaveData 스냅샷 기반 ViewData/전송 요청으로 바꾼다.
    /// View 또는 Resolver가 SaveData 목록을 직접 수정하지 않도록 경계를 유지한다.
    /// </summary>
    public sealed class WarehouseInventoryPopupPresenter
    {
        public WarehouseSelectionPanel Panel { get; private set; }
        public WarehousePriceGroup SelectedGroup { get; private set; }

public WarehousePriceGroupModalViewData SelectItem(
            IEnumerable<CargoEntrySaveData> source,
            string itemId,
            ISharedGameDataProvider catalog)
        {
            if (Panel == WarehouseSelectionPanel.Busy)
                return null;

            // A previous selection must never leak into a newly selected item.
            SelectedGroup = default;
            WarehousePriceGroupModalViewData data =
                WarehouseInventoryViewDataBuilder.BuildPriceGroups(source, itemId, catalog);
            Panel = data.Groups.Count > 1
                ? WarehouseSelectionPanel.PriceGroup
                : data.Groups.Count == 1
                    ? WarehouseSelectionPanel.Quantity
                    : WarehouseSelectionPanel.None;
            if (data.Groups.Count == 1)
                SelectedGroup = data.Groups[0];
            return data;
        }

        public void SelectPriceGroup(WarehousePriceGroup group)
        {
            if (Panel != WarehouseSelectionPanel.PriceGroup) return;
            SelectedGroup = group;
            Panel = WarehouseSelectionPanel.Quantity;
        }

        public void BeginTransfer() => Panel = WarehouseSelectionPanel.Busy;
public void EndTransfer()
        {
            SelectedGroup = default;
            Panel = WarehouseSelectionPanel.None;
        }
public void CancelSelection()
        {
            if (Panel == WarehouseSelectionPanel.Busy)
                return;

            SelectedGroup = default;
            Panel = WarehouseSelectionPanel.None;
        }
    }
}