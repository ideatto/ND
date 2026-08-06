using System.Linq;
using ND.Framework.CargoLoading;
using ND.UI.CargoSell;
using ND.UI.Market;
using NUnit.Framework;

namespace ND.Framework.Editor
{
    public sealed class CargoSellDraftTests
    {
        [Test]
        public void SameItemDifferentPurchasePrices_RemainIndependent()
        {
            var draft = new CargoSellDraft();
            CargoSellCargoItemViewData cheap = Cargo("grain", 10, 4);
            CargoSellCargoItemViewData expensive = Cargo("grain", 20, 6);

            Assert.That(draft.SetQuantity(cheap, 2), Is.True);
            Assert.That(draft.SetQuantity(expensive, 5), Is.True);

            CargoSellPendingSaleRowViewData[] snapshot = draft.Snapshot();
            Assert.That(snapshot.Length, Is.EqualTo(2));
            Assert.That(snapshot.Single(item => item.PurchaseUnitPrice == 10).Quantity, Is.EqualTo(2));
            Assert.That(snapshot.Single(item => item.PurchaseUnitPrice == 20).Quantity, Is.EqualTo(5));
        }

        [Test]
        public void Remove_ChangesOnlyExactPurchasePriceGroup()
        {
            var draft = new CargoSellDraft();
            draft.SetQuantity(Cargo("grain", 10, 4), 2);
            draft.SetQuantity(Cargo("grain", 20, 6), 5);

            Assert.That(draft.Remove("grain", 10), Is.True);

            CargoSellPendingSaleRowViewData remaining = draft.Snapshot().Single();
            Assert.That(remaining.PurchaseUnitPrice, Is.EqualTo(20));
            Assert.That(remaining.Quantity, Is.EqualTo(5));
        }

        [Test]
        public void SetQuantity_RejectsMoreThanExactGroupAvailability()
        {
            var draft = new CargoSellDraft();
            CargoSellCargoItemViewData cargo = Cargo("grain", 10, 4);

            Assert.That(draft.SetQuantity(cargo, 5), Is.False);
            Assert.That(draft.IsEmpty, Is.True);
        }

        [Test]
        public void Restore_DropsMissingGroupsAndClampsStaleQuantity()
        {
            var draft = new CargoSellDraft();
            var pending = new[]
            {
                Pending("grain", 10, 9),
                Pending("grain", 20, 2)
            };

            draft.Restore(pending, new[] { Cargo("grain", 10, 4) });

            CargoSellPendingSaleRowViewData restored = draft.Snapshot().Single();
            Assert.That(restored.PurchaseUnitPrice, Is.EqualTo(10));
            Assert.That(restored.Quantity, Is.EqualTo(4));
        }

        [Test]
        public void Snapshot_DoesNotExposeMutableDraftEntries()
        {
            var draft = new CargoSellDraft();
            draft.SetQuantity(Cargo("grain", 10, 4), 2);

            CargoSellPendingSaleRowViewData[] first = draft.Snapshot();
            first[0].Quantity = 99;

            Assert.That(draft.Snapshot()[0].Quantity, Is.EqualTo(2));
        }

        [Test]
        public void TransactionBuilder_AggregatesItemLineButPreservesPriceGroups()
        {
            var pending = new[]
            {
                Pending("grain", 10, 2),
                Pending("grain", 20, 3),
                Pending("salt", 4, 1)
            };

            MarketTransactionLine grain = CargoSellMarketTransactionBuilder.Build(pending)
                .Single(line => line.ItemId == "grain");

            Assert.That(grain.SellQuantity, Is.EqualTo(5));
            Assert.That(grain.SalePriceGroups.Count, Is.EqualTo(2));
            Assert.That(grain.SalePriceGroups.Single(group => group.PurchaseUnitPrice == 10).Quantity,
                Is.EqualTo(2));
            Assert.That(grain.SalePriceGroups.Single(group => group.PurchaseUnitPrice == 20).Quantity,
                Is.EqualTo(3));
        }

        private static CargoSellCargoItemViewData Cargo(
            string itemId,
            long purchasePrice,
            int quantity)
        {
            return new CargoSellCargoItemViewData
            {
                itemId = itemId,
                purchaseUnitPrice = purchasePrice,
                displayName = itemId,
                cargoQuantity = quantity,
                sellUnitPrice = 7
            };
        }

        private static CargoSellPendingSaleRowViewData Pending(
            string itemId,
            long purchasePrice,
            int quantity)
        {
            return new CargoSellPendingSaleRowViewData
            {
                ItemId = itemId,
                PurchaseUnitPrice = purchasePrice,
                DisplayName = itemId,
                Quantity = quantity,
                SellUnitPrice = 7
            };
        }
    }
}
