using ND.Framework;
using NUnit.Framework;

public sealed class CaravanCargoDraftServiceTests
{
    [Test]
    public void Drafts_AreIsolatedByCaravanId()
    {
        var service = new CaravanCargoDraftService();
        Assert.That(service.Set(
            "caravan-a",
            "apple:1;",
            new[] { new CaravanCargoDraftItem("apple", 2) }), Is.True);
        Assert.That(service.Set(
            "caravan-b",
            "cloth:1;",
            new[] { new CaravanCargoDraftItem("cloth", 3) }), Is.True);

        Assert.That(service.TryGet("caravan-a", out CaravanCargoDraftSnapshot first), Is.True);
        Assert.That(service.TryGet("caravan-b", out CaravanCargoDraftSnapshot second), Is.True);
        Assert.That(first.Items[0].ItemId, Is.EqualTo("apple"));
        Assert.That(first.Items[0].Quantity, Is.EqualTo(2));
        Assert.That(second.Items[0].ItemId, Is.EqualTo("cloth"));
        Assert.That(second.Items[0].Quantity, Is.EqualTo(3));
    }

    [Test]
    public void BaselineMismatch_InvalidatesOnlySelectedCaravanDraft()
    {
        var service = new CaravanCargoDraftService();
        service.Set(
            "caravan-a",
            "apple:1;",
            new[] { new CaravanCargoDraftItem("apple", 2) });
        service.Set(
            "caravan-b",
            "cloth:1;",
            new[] { new CaravanCargoDraftItem("cloth", 3) });

        bool compatible = service.TryGetCompatible(
            "caravan-a",
            "apple:4;",
            out CaravanCargoDraftSnapshot invalidated);

        Assert.That(compatible, Is.False);
        Assert.That(invalidated, Is.Null);
        Assert.That(service.TryGet("caravan-a", out _), Is.False);
        Assert.That(service.TryGet("caravan-b", out _), Is.True);
    }

    [Test]
    public void ReturnedSnapshot_CannotMutateStoredDraft()
    {
        var service = new CaravanCargoDraftService();
        service.Set(
            "caravan-a",
            string.Empty,
            new[] { new CaravanCargoDraftItem("apple", 2) });
        service.TryGet("caravan-a", out CaravanCargoDraftSnapshot first);

        CaravanCargoDraftItem[] exposed = first.Items as CaravanCargoDraftItem[];
        Assert.That(exposed, Is.Not.Null);
        exposed[0] = new CaravanCargoDraftItem("cloth", 99);

        Assert.That(service.TryGet("caravan-a", out CaravanCargoDraftSnapshot retained), Is.True);
        Assert.That(retained.Items[0].ItemId, Is.EqualTo("apple"));
        Assert.That(retained.Items[0].Quantity, Is.EqualTo(2));
    }

    [Test]
    public void InvalidDraft_DoesNotReplaceExistingDraft()
    {
        var service = new CaravanCargoDraftService();
        service.Set(
            "caravan-a",
            string.Empty,
            new[] { new CaravanCargoDraftItem("apple", 2) });

        bool stored = service.Set(
            "caravan-a",
            string.Empty,
            new[]
            {
                new CaravanCargoDraftItem("cloth", 1),
                new CaravanCargoDraftItem("cloth", 2)
            });

        Assert.That(stored, Is.False);
        Assert.That(service.TryGet("caravan-a", out CaravanCargoDraftSnapshot retained), Is.True);
        Assert.That(retained.Items[0].ItemId, Is.EqualTo("apple"));
    }
}
