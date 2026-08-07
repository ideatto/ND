using System.Collections.Generic;
using NUnit.Framework;

namespace ND.Framework.Editor.Tests
{
    public sealed class FailedTradeTransportLossTests
    {
        [Test]
        public void Apply_RemovesOnlyEquippedTransportAndClearsCaravanLoad()
        {
            var save = new SaveData();
            save.player.wagonInventory.Add(new OwnedWagonSaveData { instanceId = "lost-wagon" });
            save.player.wagonInventory.Add(new OwnedWagonSaveData { instanceId = "spare-wagon" });
            save.player.draftAnimalInventory.Add(new OwnedDraftAnimalSaveData { instanceId = "lost-animal-a" });
            save.player.draftAnimalInventory.Add(new OwnedDraftAnimalSaveData { instanceId = "lost-animal-b" });
            save.player.draftAnimalInventory.Add(new OwnedDraftAnimalSaveData { instanceId = "spare-animal" });

            var caravan = new CaravanData
            {
                wagon = new imsiWagonData { instanceId = "lost-wagon", wagonName = "Wagon_M" },
                currentDurability = 0,
                runFatalReason = JourneyFailureReason.WagonBroken,
                runWagonDestroyed = true,
                foodAmount = 12,
                animals = new List<imsiAnimalData>
                {
                    new imsiAnimalData { instanceId = "lost-animal-a" },
                    new imsiAnimalData { instanceId = "lost-animal-b" }
                },
                cargo = new List<CargoEntry>
                {
                    new CargoEntry { item = new imsiTradeItemData { itemName = "Wheat" }, quantity = 3 }
                }
            };

            FailedTradeTransportLoss.Apply(save, caravan);

            Assert.That(save.player.wagonInventory.ConvertAll(value => value.instanceId),
                Is.EqualTo(new[] { "spare-wagon" }));
            Assert.That(save.player.draftAnimalInventory.ConvertAll(value => value.instanceId),
                Is.EqualTo(new[] { "spare-animal" }));
            Assert.That(caravan.wagon, Is.Null);
            Assert.That(caravan.animals, Is.Empty);
            Assert.That(caravan.cargo, Is.Empty);
            Assert.That(caravan.foodAmount, Is.Zero);
            Assert.That(caravan.currentDurability, Is.Zero);
            Assert.That(caravan.runFatalReason, Is.EqualTo(JourneyFailureReason.None));
            Assert.That(caravan.runWagonDestroyed, Is.False);

            var savedCaravan = new CaravanSaveData
            {
                wagon = new WagonSaveData
                {
                    instanceId = "lost-wagon",
                    contentId = "Wagon_M",
                    wagonName = "Wagon_M"
                }
            };
            CaravanSaveDataMapper.CopyToSave(caravan, savedCaravan);
            Assert.That(savedCaravan.wagon.instanceId, Is.Empty);
            Assert.That(savedCaravan.wagon.contentId, Is.Empty);
            Assert.That(savedCaravan.wagon.wagonName, Is.Empty);
        }

        [Test]
        public void Apply_RepeatedCall_IsIdempotent()
        {
            var save = new SaveData();
            save.player.wagonInventory.Add(new OwnedWagonSaveData { instanceId = "spare-wagon" });
            save.player.draftAnimalInventory.Add(new OwnedDraftAnimalSaveData { instanceId = "spare-animal" });
            var caravan = new CaravanData();

            FailedTradeTransportLoss.Apply(save, caravan);
            FailedTradeTransportLoss.Apply(save, caravan);

            Assert.That(save.player.wagonInventory, Has.Count.EqualTo(1));
            Assert.That(save.player.draftAnimalInventory, Has.Count.EqualTo(1));
        }

        [Test]
        public void Apply_UsesClaimSnapshotIdsWhenRuntimeTransportIdsAreMissing()
        {
            var save = new SaveData();
            save.player.wagonInventory.Add(new OwnedWagonSaveData { instanceId = "lost-wagon" });
            save.player.wagonInventory.Add(new OwnedWagonSaveData { instanceId = "spare-wagon" });
            save.player.draftAnimalInventory.Add(new OwnedDraftAnimalSaveData { instanceId = "lost-animal" });
            save.player.draftAnimalInventory.Add(new OwnedDraftAnimalSaveData { instanceId = "spare-animal" });

            var caravan = new CaravanData
            {
                wagon = new imsiWagonData { instanceId = string.Empty, wagonName = "Wagon_S" },
                animals = new List<imsiAnimalData>
                {
                    new imsiAnimalData { instanceId = string.Empty }
                }
            };

            FailedTradeTransportLoss.Apply(
                save,
                caravan,
                "lost-wagon",
                new[] { "lost-animal" });

            Assert.That(save.player.wagonInventory.ConvertAll(value => value.instanceId),
                Is.EqualTo(new[] { "spare-wagon" }));
            Assert.That(save.player.draftAnimalInventory.ConvertAll(value => value.instanceId),
                Is.EqualTo(new[] { "spare-animal" }));
        }
    }
}
