using System.Collections.Generic;
using ND.Framework;
using ND.UI.InGame.TransportInventory;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class TransportInventoryPresentationTests
{
    [Test]
    public void Build_UsesFarmLevelCapacity_AndBuildsFixedMaximumSlots()
    {
        ND.Framework.PlayerSaveData player = PlayerWithFarm(2);

        TransportInventoryPopupViewData view = TransportInventoryViewDataBuilder.Build(player, Catalog());

        Assert.That(view.CanOpen, Is.True);
        Assert.That(view.FarmLevel, Is.EqualTo(2));
        Assert.That(view.Wagon.AvailableSlots, Is.EqualTo(20));
        Assert.That(view.Animal.AvailableSlots, Is.EqualTo(40));
        Assert.That(view.Wagon.Slots, Has.Count.EqualTo(50));
        Assert.That(view.Animal.Slots, Has.Count.EqualTo(100));
        Assert.That(view.Wagon.NextRequiredFarmLevel, Is.EqualTo(3));
    }

    [Test]
    public void Build_MapsTooltipAndDurability_WithoutExposingMutableSaveEntry()
    {
        ND.Framework.PlayerSaveData player = PlayerWithFarm(1);
        player.wagonInventory.Add(new OwnedWagonSaveData
        {
            instanceId = "wagon-instance",
            contentId = "Wagon_M",
            currentDurability = 60
        });

        TransportInventorySlotViewData slot =
            TransportInventoryViewDataBuilder.Build(player, Catalog()).Wagon.Slots[0];

        Assert.That(slot.InstanceId, Is.EqualTo("wagon-instance"));
        Assert.That(slot.ContentId, Is.EqualTo("Wagon_M"));
        Assert.That(slot.DisplayName, Is.EqualTo("테스트 마차"));
        Assert.That(slot.Description, Is.EqualTo("마차 설명"));
        Assert.That(slot.BaseBuyPrice, Is.EqualTo(1200));
        Assert.That(slot.HasDurability, Is.True);
        Assert.That(slot.DurabilityPercent, Is.EqualTo(60));
    }

    [Test]
    public void AnimalSlot_NeverExposesDurability()
    {
        ND.Framework.PlayerSaveData player = PlayerWithFarm(1);
        player.draftAnimalInventory.Add(new OwnedDraftAnimalSaveData
        {
            instanceId = "animal-instance",
            contentId = "Horse"
        });

        TransportInventorySlotViewData slot =
            TransportInventoryViewDataBuilder.Build(player, Catalog()).Animal.Slots[0];

        Assert.That(slot.IsOccupied, Is.True);
        Assert.That(slot.HasDurability, Is.False);
        Assert.That(slot.DisplayName, Is.EqualTo("테스트 말"));
    }

    [Test]
    public void OverflowOwnedItems_RemainInViewData_ButStayBehindUnlockedBoundary()
    {
        ND.Framework.PlayerSaveData player = PlayerWithFarm(1);
        for (int index = 0; index < 12; index++)
        {
            player.wagonInventory.Add(new OwnedWagonSaveData
            {
                instanceId = "wagon-" + index,
                contentId = "Wagon_M",
                currentDurability = 100
            });
        }

        TransportInventoryPanelViewData panel =
            TransportInventoryViewDataBuilder.Build(player, Catalog()).Wagon;

        Assert.That(panel.OverflowCount, Is.EqualTo(2));
        Assert.That(panel.Slots[11].IsOccupied, Is.True);
        Assert.That(panel.Slots[11].IsOverflow, Is.True);
        Assert.That(panel.LockedStartIndex, Is.EqualTo(10));
    }

    [Test]
    public void PopupSlotPool_GrowsOnlyWhenRequired_AndReusesCreatedSlots()
    {
        const string path = "Assets/_Project/08.Prefabs/UI/TransportInventory/TransportInventoryPopup.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<TransportInventorySlotView>(true), Has.Length.EqualTo(30));

        GameObject instance = Object.Instantiate(prefab);
        try
        {
            TransportInventoryPanelView[] panels =
                instance.GetComponentsInChildren<TransportInventoryPanelView>(true);
            TransportInventoryPanelView wagon = System.Array.Find(panels, panel => panel.name == "WagonPanel");
            TransportInventoryPanelView animal = System.Array.Find(panels, panel => panel.name == "DraftAnimalPanel");
            Assert.That(wagon, Is.Not.Null);
            Assert.That(animal, Is.Not.Null);

            wagon.Render(TransportInventoryViewDataBuilder.Build(PlayerWithFarm(2), Catalog()).Wagon);
            Assert.That(wagon.GetComponentsInChildren<TransportInventorySlotView>(true), Has.Length.EqualTo(20));

            wagon.Render(TransportInventoryViewDataBuilder.Build(PlayerWithFarm(2), Catalog()).Wagon);
            Assert.That(wagon.GetComponentsInChildren<TransportInventorySlotView>(true), Has.Length.EqualTo(20));

            wagon.Render(TransportInventoryViewDataBuilder.Build(PlayerWithFarm(1), Catalog()).Wagon);
            Assert.That(wagon.GetComponentsInChildren<TransportInventorySlotView>(true), Has.Length.EqualTo(20));
            Assert.That(wagon.GetComponentsInChildren<TransportInventorySlotView>(false), Has.Length.EqualTo(10));

            animal.Render(TransportInventoryViewDataBuilder.Build(PlayerWithFarm(2), Catalog()).Animal);
            Assert.That(animal.GetComponentsInChildren<TransportInventorySlotView>(true), Has.Length.EqualTo(40));
            animal.Render(TransportInventoryViewDataBuilder.Build(PlayerWithFarm(2), Catalog()).Animal);
            Assert.That(animal.GetComponentsInChildren<TransportInventorySlotView>(true), Has.Length.EqualTo(40));
            animal.Render(TransportInventoryViewDataBuilder.Build(PlayerWithFarm(1), Catalog()).Animal);
            Assert.That(animal.GetComponentsInChildren<TransportInventorySlotView>(true), Has.Length.EqualTo(40));
            Assert.That(animal.GetComponentsInChildren<TransportInventorySlotView>(false), Has.Length.EqualTo(20));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void MissingCatalog_KeepsOwnedInstanceVisibleAsWarningData()
    {
        ND.Framework.PlayerSaveData player = PlayerWithFarm(1);
        player.wagonInventory.Add(new OwnedWagonSaveData
        {
            instanceId = "legacy-instance",
            contentId = "Missing",
            currentDurability = 1
        });

        TransportInventorySlotViewData slot =
            TransportInventoryViewDataBuilder.Build(player, Catalog()).Wagon.Slots[0];

        Assert.That(slot.IsOccupied, Is.True);
        Assert.That(slot.IsCatalogMissing, Is.True);
        Assert.That(slot.InstanceId, Is.EqualTo("legacy-instance"));
    }

    [Test]
    public void AssignedTransport_RemainsInSlot_AndNamesItsCaravan()
    {
        var save = new ND.Framework.SaveData { player = PlayerWithFarm(1) };
        save.player.wagonInventory.Add(new OwnedWagonSaveData
        {
            instanceId = "wagon-instance",
            contentId = "Wagon_M",
            currentDurability = 60
        });
        save.caravans.Clear();
        save.caravans.Add(new ND.Framework.CaravanSaveData
        {
            caravanId = "caravan-a",
            displayName = "북부 교역대",
            wagon = new WagonSaveData { instanceId = "wagon-instance", contentId = "Wagon_M" }
        });

        TransportInventorySlotViewData slot = TransportInventoryViewDataBuilder.Build(save, Catalog()).Wagon.Slots[0];

        Assert.That(slot.IsOccupied, Is.True);
        Assert.That(slot.IsAssigned, Is.True);
        Assert.That(slot.AssignedCaravanId, Is.EqualTo("caravan-a"));
        Assert.That(slot.AssignmentText, Is.EqualTo("북부 교역대 장착 중"));
    }

    private static ND.Framework.PlayerSaveData PlayerWithFarm(int level)
    {
        var player = new ND.Framework.PlayerSaveData();
        player.villageBuildings.Add(new VillageBuildingSaveData
        {
            displayName = TransportInventoryFunction.BuildingDisplayName,
            level = level
        });
        return player;
    }

    private static ISharedGameDataProvider Catalog()
    {
        return new SharedGameDataView(
            new Dictionary<string, SharedTownDefinition>(),
            new Dictionary<string, SharedMarketDefinition>(),
            new Dictionary<string, SharedTradeItemDefinition>(),
            new Dictionary<string, SharedWagonDefinition>
            {
                ["Wagon_M"] = new SharedWagonDefinition
                {
                    Id = "Wagon_M",
                    DisplayName = "테스트 마차",
                    Description = "마차 설명",
                    BaseBuyPrice = 1200,
                    MaxDurability = 100
                }
            },
            new Dictionary<string, SharedDraftAnimalDefinition>
            {
                ["Horse"] = new SharedDraftAnimalDefinition
                {
                    Id = "Horse",
                    DisplayName = "테스트 말",
                    Description = "동물 설명",
                    BaseBuyPrice = 500
                }
            },
            new Dictionary<string, SharedRouteDefinition>());
    }
}
