using ND.Framework;
using NUnit.Framework;

public sealed class CaravanCompositionDraftServiceTests
{
    [Test]
    public void Drafts_AreIsolatedByCaravanId()
    {
        OwnedTransportInventoryService inventory = CreateInventory();
        var service = new CaravanCompositionDraftService(inventory);

        Assert.That(
            service.TrySet("caravan-a", "wagon-a", new[] { "animal-a" }),
            Is.EqualTo(CaravanCompositionDraftFailure.None));
        Assert.That(
            service.TrySet("caravan-b", "wagon-b", new[] { "animal-b" }),
            Is.EqualTo(CaravanCompositionDraftFailure.None));

        CaravanCompositionSnapshot first = service.GetOrCreate("caravan-a", null, null);
        CaravanCompositionSnapshot second = service.GetOrCreate("caravan-b", null, null);
        Assert.That(first.WagonInstanceId, Is.EqualTo("wagon-a"));
        Assert.That(first.AnimalInstanceIds, Is.EqualTo(new[] { "animal-a" }));
        Assert.That(second.WagonInstanceId, Is.EqualTo("wagon-b"));
        Assert.That(second.AnimalInstanceIds, Is.EqualTo(new[] { "animal-b" }));
    }

    [Test]
    public void SameTransportInstance_CannotBeUsedByTwoDrafts()
    {
        OwnedTransportInventoryService inventory = CreateInventory();
        var service = new CaravanCompositionDraftService(inventory);
        Assert.That(
            service.TrySet("caravan-a", "wagon-a", new[] { "animal-a" }),
            Is.EqualTo(CaravanCompositionDraftFailure.None));

        CaravanCompositionDraftFailure result = service.TrySet(
            "caravan-b",
            "wagon-a",
            new[] { "animal-b" });

        Assert.That(result, Is.EqualTo(CaravanCompositionDraftFailure.AssetAlreadyInUse));
    }

    [Test]
    public void FailedReplacement_DoesNotMutateExistingDraft()
    {
        OwnedTransportInventoryService inventory = CreateInventory();
        var service = new CaravanCompositionDraftService(inventory);
        Assert.That(
            service.TrySet("caravan-a", "wagon-a", new[] { "animal-a" }),
            Is.EqualTo(CaravanCompositionDraftFailure.None));

        CaravanCompositionDraftFailure result = service.TrySet(
            "caravan-a",
            "missing-wagon",
            new[] { "animal-b" });

        Assert.That(result, Is.EqualTo(CaravanCompositionDraftFailure.WagonNotOwned));
        CaravanCompositionSnapshot retained = service.GetOrCreate("caravan-a", null, null);
        Assert.That(retained.WagonInstanceId, Is.EqualTo("wagon-a"));
        Assert.That(retained.AnimalInstanceIds, Is.EqualTo(new[] { "animal-a" }));
    }

    [Test]
    public void MixedAnimalContentTypes_AreRejectedBeforeDraftMutation()
    {
        OwnedTransportInventoryService inventory = CreateInventory();
        inventory.RegisterAnimal(new OwnedDraftAnimalInstance("animal-c", "Donkey"));
        var service = new CaravanCompositionDraftService(inventory);

        CaravanCompositionDraftFailure result = service.TrySet(
            "caravan-a",
            "wagon-a",
            new[] { "animal-a", "animal-c" });

        Assert.That(result, Is.EqualTo(CaravanCompositionDraftFailure.MixedAnimalType));
    }

    [Test]
    public void SameAnimalContentType_WithDifferentInstances_IsAllowed()
    {
        OwnedTransportInventoryService inventory = CreateInventory();
        var service = new CaravanCompositionDraftService(inventory);

        CaravanCompositionDraftFailure result = service.TrySet(
            "caravan-a",
            "wagon-a",
            new[] { "animal-a", "animal-b" });

        Assert.That(result, Is.EqualTo(CaravanCompositionDraftFailure.None));
    }

    [Test]
    public void WalkingComposition_RejectsSelectedAnimals()
    {
        OwnedTransportInventoryService inventory = CreateInventory();
        var service = new CaravanCompositionDraftService(inventory);

        CaravanCompositionDraftFailure result = service.TrySet(
            "caravan-a",
            string.Empty,
            new[] { "animal-a" });

        Assert.That(result, Is.EqualTo(CaravanCompositionDraftFailure.InvalidComposition));
    }

    private static OwnedTransportInventoryService CreateInventory()
    {
        var inventory = new OwnedTransportInventoryService();
        inventory.RegisterWagon(new OwnedWagonInstance("wagon-a", "Wagon_M", 30f, 5, 1, 2));
        inventory.RegisterWagon(new OwnedWagonInstance("wagon-b", "Wagon_M", 30f, 5, 1, 2));
        inventory.RegisterAnimal(new OwnedDraftAnimalInstance("animal-a", "Horse"));
        inventory.RegisterAnimal(new OwnedDraftAnimalInstance("animal-b", "Horse"));
        return inventory;
    }
}
