using System;
using System.Linq;
using System.Reflection;
using ND.Framework.CargoLoading;
using ND.UI.Market;
using NUnit.Framework;
using UnityEngine;

namespace ND.Framework.Editor
{
    public sealed class CargoSellMarketTransactionTests
    {
        [Test]
        public void ExactPriceGroupSale_PreservesUnselectedGroup()
        {
            TradeItemData item = CreateItem("grain");
            try
            {
                var save = new SaveData();
                save.player.tradingCurrency = 100;
                save.caravan.cargo.Add(Cargo("grain", 10, 3));
                save.caravan.cargo.Add(Cargo("grain", 20, 4));

                Assert.That(MarketInventoryMutationSession.TryOpen(
                    save,
                    new MemorySaveService(),
                    new FixedTimeProvider(),
                    "cargo-sell-market",
                    new[] { item },
                    1,
                    1,
                    1d,
                    7,
                    out MarketInventoryMutationSession session,
                    out string error), Is.True, error);

                MarketTransactionResult result = MarketTransactionCommand.Execute(
                    session,
                    new[]
                    {
                        new MarketTransactionLine
                        {
                            ItemId = "grain",
                            SellQuantity = 2,
                            SalePriceGroups =
                            {
                                new MarketSalePriceGroup { PurchaseUnitPrice = 20, Quantity = 2 }
                            }
                        }
                    },
                    100f);

                Assert.That(result.Success, Is.True, result.ErrorCode);
                Assert.That(GroupQuantity(save, 10), Is.EqualTo(3));
                Assert.That(GroupQuantity(save, 20), Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void ExactPriceGroupSale_RejectsStaleGroupWithoutMutation()
        {
            TradeItemData item = CreateItem("grain");
            try
            {
                var save = new SaveData();
                save.player.tradingCurrency = 100;
                save.caravan.cargo.Add(Cargo("grain", 10, 3));
                var saveService = new MemorySaveService();

                Assert.That(MarketInventoryMutationSession.TryOpen(
                    save, saveService, new FixedTimeProvider(), "cargo-sell-stale-market",
                    new[] { item }, 1, 1, 1d, 7,
                    out MarketInventoryMutationSession session, out string error), Is.True, error);
                int saveCallsBeforeCommit = saveService.SaveCalls;

                MarketTransactionResult result = MarketTransactionCommand.Execute(
                    session,
                    new[]
                    {
                        new MarketTransactionLine
                        {
                            ItemId = "grain",
                            SellQuantity = 1,
                            SalePriceGroups =
                            {
                                new MarketSalePriceGroup { PurchaseUnitPrice = 20, Quantity = 1 }
                            }
                        }
                    },
                    100f);

                Assert.That(result.Success, Is.False);
                Assert.That(result.ErrorCode, Is.EqualTo(MarketInventoryMutationSession.ErrorInsufficientCargo));
                Assert.That(GroupQuantity(save, 10), Is.EqualTo(3));
                Assert.That(save.player.tradingCurrency, Is.EqualTo(100));
                Assert.That(saveService.SaveCalls, Is.EqualTo(saveCallsBeforeCommit));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(item);
            }
        }

        private static CargoEntrySaveData Cargo(string itemId, long purchasePrice, int quantity)
        {
            return new CargoEntrySaveData
            {
                quantity = quantity,
                item = new TradeItemSaveData
                {
                    itemId = itemId,
                    itemName = itemId,
                    purchaseUnitPrice = purchasePrice,
                    basePrice = 10,
                    weight = 1f,
                    maxCount = 10
                }
            };
        }

        private static int GroupQuantity(SaveData save, long purchasePrice) =>
            save.caravan.cargo
                .Where(entry => entry?.item != null
                    && entry.item.itemId == "grain"
                    && entry.item.purchaseUnitPrice == purchasePrice)
                .Sum(entry => entry.quantity);

        private static TradeItemData CreateItem(string itemId)
        {
            TradeItemData item = ScriptableObject.CreateInstance<TradeItemData>();
            SetField(item, "itemId", itemId);
            SetField(item, "displayName", itemId);
            SetField(item, "baseBuyPrice", 10L);
            SetField(item, "baseSellPrice", 7L);
            SetField(item, "weight", 1f);
            SetField(item, "maxCount", 10);
            return item;
        }

        private static void SetField(object target, string name, object value) =>
            typeof(TradeItemData).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);

        private sealed class FixedTimeProvider : IGameTimeProvider
        {
            public DateTime CurrentUtc => DateTime.UnixEpoch;
        }

        private sealed class MemorySaveService : ISaveService
        {
            public int SaveCalls { get; private set; }
            public bool HasSaveData() => false;
            public SaveData CreateNewGameData() => new SaveData();
            public SaveData Load() => null;
            public SaveResult Save(SaveData data)
            {
                SaveCalls++;
                return SaveResult.Success();
            }
            public void ResetSaveData() { }
        }
    }
}
