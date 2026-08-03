using System.Collections.Generic;
using NUnit.Framework;

namespace ND.Framework.EditorTests
{
    public sealed class CaravanSlotValidationTests
    {
        [Test]
        public void Validate_OutOfRangeCaravan_IsNotResolvable()
        {
            var caravan = new CaravanSaveData { caravanId = "outside", slotIndex = 4 };

            CaravanSlotValidationResult result = CaravanSlotValidation.Validate(
                new List<CaravanSaveData> { caravan });

            Assert.That(result.HasInvalidEntries, Is.True);
            Assert.That(result.TryGetCaravan(caravan.caravanId, out _), Is.False);
        }

        [Test]
        public void Validate_DuplicateSlot_RejectsEveryOccupant()
        {
            var first = new CaravanSaveData { caravanId = "first", slotIndex = 2 };
            var second = new CaravanSaveData { caravanId = "second", slotIndex = 2 };

            CaravanSlotValidationResult result = CaravanSlotValidation.Validate(
                new List<CaravanSaveData> { first, second });

            Assert.That(result.IsConflicted(2), Is.True);
            Assert.That(result.GetCaravanAt(2), Is.Null);
            Assert.That(result.TryGetCaravan(first.caravanId, out _), Is.False);
            Assert.That(result.TryGetCaravan(second.caravanId, out _), Is.False);
        }

        [Test]
        public void Validate_UniqueSlots_ResolveByCaravanId()
        {
            var first = new CaravanSaveData { caravanId = "first", slotIndex = 0 };
            var second = new CaravanSaveData { caravanId = "second", slotIndex = 3 };

            CaravanSlotValidationResult result = CaravanSlotValidation.Validate(
                new List<CaravanSaveData> { first, second });

            Assert.That(result.TryGetCaravan(second.caravanId, out CaravanSaveData selected), Is.True);
            Assert.That(selected, Is.SameAs(second));
        }

        [Test]
        public void Transfer_DuplicateSlot_IsRejectedBeforeSaveOrMutation()
        {
            SaveData save = CreateSaveWithHomeItem();
            save.caravans.Add(CreateCaravan("first", 1));
            save.caravans.Add(CreateCaravan("second", 1));
            var saveService = new CountingSaveService();
            var request = new WarehouseTransferRequest(
                "first",
                WarehouseFunction.BaseTownId,
                "Logs",
                10L,
                1,
                WarehouseTransferDirection.HomeToCargo,
                10,
                10,
                100f);

            bool succeeded = WarehouseInventoryTransferService.TryTransfer(
                save, saveService, request, out WarehouseTransferFailure failure);

            Assert.That(succeeded, Is.False);
            Assert.That(failure, Is.EqualTo(WarehouseTransferFailure.InvalidCaravan));
            Assert.That(saveService.SaveCalls, Is.Zero);
            Assert.That(save.player.homeInventory[0].quantity, Is.EqualTo(2));
            Assert.That(save.caravans[0].cargo, Is.Empty);
            Assert.That(save.caravans[1].cargo, Is.Empty);
        }

        private static SaveData CreateSaveWithHomeItem()
        {
            var save = new SaveData();
            save.caravans.Clear();
            save.player.currentTownId = WarehouseFunction.BaseTownId;
            save.player.homeInventory = new List<CargoEntrySaveData>
            {
                new CargoEntrySaveData
                {
                    quantity = 2,
                    item = new TradeItemSaveData
                    {
                        itemId = "Logs",
                        itemName = "Logs",
                        purchaseUnitPrice = 10L,
                        maxCount = 99,
                        weight = 1f
                    }
                }
            };
            return save;
        }

        private static CaravanSaveData CreateCaravan(string id, int slotIndex) =>
            new CaravanSaveData
            {
                caravanId = id,
                slotIndex = slotIndex,
                currentTownId = WarehouseFunction.BaseTownId,
                state = JourneyState.Prepare,
                wagon = new WagonSaveData { inventorySlotCount = 10, maxLoad = 100f }
            };

        private sealed class CountingSaveService : ISaveService
        {
            public int SaveCalls { get; private set; }
            public bool HasSaveData() => true;
            public SaveData CreateNewGameData() => new SaveData();
            public SaveData Load() => new SaveData();
            public SaveResult Save(SaveData data)
            {
                SaveCalls++;
                return SaveResult.Success();
            }
            public void ResetSaveData() { }
        }
    }
}
