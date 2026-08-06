using System.Collections.Generic;
using ND.Framework;
using NUnit.Framework;
using FrameworkSaveData = ND.Framework.SaveData;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;

public sealed class CaravanMarketCatalogServiceTests
{
    [Test]
    public void Resolve_UsesCaravanTownInsteadOfPlayerTown()
    {
        FrameworkSaveData save = CreateSave();
        save.player.currentTownId = "player-town";
        ISharedGameDataProvider data = CreateSharedData();
        var service = new CaravanMarketCatalogService();

        bool resolved = service.TryResolve(
            save,
            data,
            "caravan-a",
            out CaravanMarketCatalogSnapshot catalog);

        Assert.That(resolved, Is.True);
        Assert.That(catalog.TownId, Is.EqualTo("caravan-town"));
        Assert.That(catalog.MarketId, Is.EqualTo("caravan-market"));
    }

    [Test]
    public void Resolve_OverlaysAuthoritativeStockAndPrice()
    {
        FrameworkSaveData save = CreateSave();
        ISharedGameDataProvider data = CreateSharedData();
        var service = new CaravanMarketCatalogService();

        Assert.That(service.TryResolve(
            save,
            data,
            "caravan-a",
            out CaravanMarketCatalogSnapshot catalog), Is.True);
        Assert.That(catalog.TryGetItem("apple", out CaravanMarketCatalogItem apple), Is.True);
        Assert.That(apple.Stock, Is.EqualTo(7));
        Assert.That(apple.BuyUnitPrice, Is.EqualTo(135));
        Assert.That(apple.Definition.Weight, Is.EqualTo(2.5f));
    }

    [Test]
    public void Resolve_MergesRegularAndSpecialtyIdsWithoutDuplicates()
    {
        FrameworkSaveData save = CreateSave();
        ISharedGameDataProvider data = CreateSharedData();
        var service = new CaravanMarketCatalogService();

        service.TryResolve(save, data, "caravan-a", out CaravanMarketCatalogSnapshot catalog);

        Assert.That(catalog.Items.Count, Is.EqualTo(2));
        Assert.That(catalog.TryGetItem("apple", out _), Is.True);
        Assert.That(catalog.TryGetItem("cloth", out _), Is.True);
    }

    [Test]
    public void Resolve_ExcludesLockedSpecialtyFromCaravanTownCatalog()
    {
        FrameworkSaveData save = CreateSave();
        save.world.unlockedTownSpecialties.Clear();
        ISharedGameDataProvider data = CreateSharedData();
        var service = new CaravanMarketCatalogService();

        service.TryResolve(save, data, "caravan-a", out CaravanMarketCatalogSnapshot catalog);

        Assert.That(catalog.TryGetItem("apple", out _), Is.False);
        Assert.That(catalog.TryGetItem("cloth", out _), Is.True);
    }

    [Test]
    public void Resolve_DoesNotUseAnotherTownsSpecialtyUnlock()
    {
        FrameworkSaveData save = CreateSave();
        save.world.unlockedTownSpecialties[0].townId = "player-town";
        ISharedGameDataProvider data = CreateSharedData();
        var service = new CaravanMarketCatalogService();

        service.TryResolve(save, data, "caravan-a", out CaravanMarketCatalogSnapshot catalog);

        Assert.That(catalog.TryGetItem("apple", out _), Is.False);
    }

    [Test]
    public void Resolve_MissingInventoryFailsClosedWithZeroStock()
    {
        FrameworkSaveData save = CreateSave();
        save.world.marketInventories.Clear();
        ISharedGameDataProvider data = CreateSharedData();
        var service = new CaravanMarketCatalogService();

        service.TryResolve(save, data, "caravan-a", out CaravanMarketCatalogSnapshot catalog);

        Assert.That(catalog.TryGetItem("apple", out CaravanMarketCatalogItem apple), Is.True);
        Assert.That(apple.Stock, Is.Zero);
        Assert.That(apple.BuyUnitPrice, Is.EqualTo(100));
    }

    private static FrameworkSaveData CreateSave()
    {
        var save = new FrameworkSaveData();
        save.caravans.Add(new FrameworkCaravanSaveData
        {
            caravanId = "caravan-a",
            currentTownId = "caravan-town"
        });
        save.world.marketInventories.Add(new MarketInventorySaveData
        {
            marketId = "caravan-market",
            stocks = new List<MarketStockSaveData>
            {
                new MarketStockSaveData
                {
                    itemId = "apple",
                    quantity = 7,
                    unitPrice = 135
                }
            }
        });
        save.world.unlockedTownSpecialties.Add(new TownSpecialtyUnlockSaveData
        {
            townId = "caravan-town",
            itemId = "apple"
        });
        return save;
    }

    private static ISharedGameDataProvider CreateSharedData()
    {
        return new SharedGameDataView(
            new Dictionary<string, SharedTownDefinition>
            {
                ["caravan-town"] = new SharedTownDefinition
                {
                    Id = "caravan-town",
                    MarketId = "caravan-market"
                },
                ["player-town"] = new SharedTownDefinition
                {
                    Id = "player-town",
                    MarketId = "player-market"
                }
            },
            new Dictionary<string, SharedMarketDefinition>
            {
                ["caravan-market"] = new SharedMarketDefinition
                {
                    Id = "caravan-market",
                    TradeItemIds = new[] { "cloth" },
                    LocalSpecialtyItemIds = new[] { "apple" }
                }
            },
            new Dictionary<string, SharedTradeItemDefinition>
            {
                ["apple"] = new SharedTradeItemDefinition
                {
                    Id = "apple",
                    BaseBuyPrice = 100,
                    Weight = 2.5f
                },
                ["cloth"] = new SharedTradeItemDefinition
                {
                    Id = "cloth",
                    BaseBuyPrice = 80,
                    Weight = 1f
                }
            },
            new Dictionary<string, SharedWagonDefinition>(),
            new Dictionary<string, SharedDraftAnimalDefinition>(),
            new Dictionary<string, SharedRouteDefinition>());
    }
}
