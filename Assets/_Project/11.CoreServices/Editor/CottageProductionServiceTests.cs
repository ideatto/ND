using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ND.Framework;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CottageProductionServiceTests
{
    private sealed class FakeTime : IGameTimeProvider
    {
        public DateTime CurrentUtc { get; set; }
    }

    private CottageProductionData config;
    private FakeTime time;
    private ND.Framework.SaveData save;
    private CottageProductionService service;

    [SetUp]
    public void SetUp()
    {
        config = Resources.Load<CottageProductionData>(
            CottageProductionData.ResourceName);
        Assert.That(config, Is.Not.Null);
        Assert.That(config.Validate(out string error), Is.True, error);
        time = new FakeTime
        {
            CurrentUtc = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc)
        };
        save = new ND.Framework.SaveData();
        save.player.cottageProduction = new CottageProductionSaveData();
        service = new CottageProductionService(config, time, () => save, null);
    }

    [Test]
    public void FirstLevelBuildStagesInitialSupplyOnce()
    {
        Assert.That(service.TryStageBuildingLevelChange(
            save, 0, 1, out string error), Is.True, error);
        CottageProductionSaveData state = save.player.cottageProduction;
        Assert.That(state.initialized, Is.True);
        Assert.That(state.initialSupplyGranted, Is.True);
        Assert.That(state.storedWagonCount, Is.EqualTo(1));
        Assert.That(state.storedDraftAnimalCount, Is.EqualTo(2));
        Assert.That(state.nextWagonProductionUtcTicks, Is.Zero);
        Assert.That(state.nextDraftAnimalProductionUtcTicks, Is.Zero);

        Assert.That(service.TryStageBuildingLevelChange(
            save, 0, 1, out error), Is.True, error);
        Assert.That(state.storedWagonCount, Is.EqualTo(1));
        Assert.That(state.storedDraftAnimalCount, Is.EqualTo(2));
    }

    [Test]
    public void OfflineRestoreFillsNewCapacityAndPausesAtFull()
    {
        AddCottage(level: 2);
        CottageProductionSaveData state = save.player.cottageProduction;
        state.initialized = true;
        state.initialSupplyGranted = true;
        state.storedWagonContentId = config.WagonContentId;
        state.storedDraftAnimalContentId = config.DraftAnimalContentId;
        state.nextWagonProductionUtcTicks =
            time.CurrentUtc.AddSeconds(config.WagonProductionIntervalSeconds).Ticks;
        state.nextDraftAnimalProductionUtcTicks =
            time.CurrentUtc.AddSeconds(config.DraftAnimalProductionIntervalSeconds).Ticks;

        DateTime evaluation = time.CurrentUtc.AddHours(2);
        var context = new OfflineRestoreContext(
            time.CurrentUtc,
            evaluation,
            evaluation,
            evaluation - time.CurrentUtc,
            false,
            false);
        CottageProductionRestoreResult result =
            service.RestoreOffline(save, context);

        Assert.That(result.Changed, Is.True);
        Assert.That(state.storedWagonCount, Is.EqualTo(2));
        Assert.That(state.storedDraftAnimalCount, Is.EqualTo(4));
        Assert.That(state.nextWagonProductionUtcTicks, Is.Zero);
        Assert.That(state.nextDraftAnimalProductionUtcTicks, Is.Zero);
    }

    [Test]
    public void ClockRollbackDoesNotMutateProduction()
    {
        AddCottage(level: 1);
        CottageProductionSaveData state = save.player.cottageProduction;
        state.initialized = true;
        state.initialSupplyGranted = true;
        state.nextWagonProductionUtcTicks = time.CurrentUtc.AddMinutes(30).Ticks;
        string before = JsonUtility.ToJson(state);
        var context = new OfflineRestoreContext(
            time.CurrentUtc,
            time.CurrentUtc.AddMinutes(-1),
            time.CurrentUtc.AddMinutes(-1),
            TimeSpan.Zero,
            true,
            false);

        Assert.That(service.RestoreOffline(save, context).Changed, Is.False);
        Assert.That(JsonUtility.ToJson(state), Is.EqualTo(before));
    }

    [Test]
    public void LevelZeroClearsProductsButKeepsOneTimeGrantFlag()
    {
        CottageProductionSaveData state = save.player.cottageProduction;
        state.initialized = true;
        state.initialSupplyGranted = true;
        state.storedWagonCount = 1;
        state.storedDraftAnimalCount = 2;
        state.storedWagonContentId = config.WagonContentId;
        state.storedDraftAnimalContentId = config.DraftAnimalContentId;
        var context = new OfflineRestoreContext(
            time.CurrentUtc,
            time.CurrentUtc,
            time.CurrentUtc,
            TimeSpan.Zero,
            false,
            false);

        Assert.That(service.RestoreOffline(save, context).Changed, Is.True);
        Assert.That(state.initialized, Is.False);
        Assert.That(state.initialSupplyGranted, Is.True);
        Assert.That(state.storedWagonCount, Is.Zero);
        Assert.That(state.storedDraftAnimalCount, Is.Zero);
    }

    [Test]
    public void CottageBuildAssetContainsExactlyLevelsOneToThree()
    {
        BuildData build = AssetDatabase.LoadAssetAtPath<BuildData>(
            "Assets/_Project/02.Data/01_ScriptableObjects/Build/Build_Cottage.asset");
        Assert.That(build, Is.Not.Null);
        DataPerLevel[] levels = build.DataPerLevels;
        Assert.That(levels.Length, Is.EqualTo(3));
        CollectionAssert.AreEquivalent(new[] { 1, 2, 3 }, levels.Select(x => x.level));
        Assert.That(levels.All(x => x != null && x.buildPrefab != null), Is.True);
    }

    [Test]
    public void CottagePopupPrefabContainsFixedAndFullyWiredContent()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/08.Prefabs/UI/Cottage/CottageProductionPopup.prefab");
        Assert.That(prefab, Is.Not.Null);

        var view = prefab.GetComponent<CottageProductionPopupView>();
        var presenter = prefab.GetComponent<CottageProductionPopupPresenter>();
        Assert.That(view, Is.Not.Null);
        Assert.That(presenter, Is.Not.Null);

        AssertObjectReferenceAssigned(view, "backdropButton");
        AssertObjectReferenceAssigned(view, "closeButton");
        AssertObjectReferenceAssigned(view, "wagonIcon");
        AssertObjectReferenceAssigned(view, "draftAnimalIcon");
        AssertObjectReferenceAssigned(view, "wagonReceiveButton");
        AssertObjectReferenceAssigned(view, "draftAnimalReceiveButton");
        AssertObjectReferenceAssigned(view, "receiveAllButton");
        AssertObjectReferenceAssigned(presenter, "view");
    }

    [Test]
    public void CatalogValidationAcceptsConfiguredProductionIds()
    {
        var catalog = CreateTransportCatalog();
        Assert.That(service.ValidateCatalog(catalog, out string error), Is.True, error);
    }

    [Test]
    public void CollectionSaveFailureRestoresSaveAndDerivedInventoryIndex()
    {
        if (PlayerMainManager.Instance != null)
            UnityEngine.Object.DestroyImmediate(PlayerMainManager.Instance.gameObject);
        var playerObject = new GameObject("CottageCollectionRollbackTest");
        try
        {
            PlayerMainManager player = playerObject.AddComponent<PlayerMainManager>();
            player.SetTransportCatalogForTests(CreateTransportCatalog());
            AddCottage(level: 1);
            save.player.villageBuildings.Add(new ND.Framework.VillageBuildingSaveData
            {
                displayName = TransportInventoryFunction.BuildingDisplayName,
                level = 1
            });
            save.player.cottageProduction.initialized = true;
            save.player.cottageProduction.initialSupplyGranted = true;
            save.player.cottageProduction.storedWagonCount = 1;
            save.player.cottageProduction.storedWagonContentId = config.WagonContentId;
            typeof(PlayerMainManager).GetField(
                    "fallbackSave", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(player, save);
            var failingService = new CottageProductionService(
                config, time, () => save, new FailingSaveService());

            CottageCollectionResult result = failingService.Collect(
                CottageCollectionTarget.Wagon, player);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(CottageCollectionFailureReason.SaveFailed));
            Assert.That(save.player.cottageProduction.storedWagonCount, Is.EqualTo(1));
            Assert.That(save.player.wagonInventory, Is.Empty);
            var index = typeof(PlayerMainManager).GetField(
                "wagonsByInstanceId", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(player) as System.Collections.IDictionary;
            Assert.That(index, Is.Not.Null);
            Assert.That(index.Count, Is.Zero);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(playerObject);
        }
    }

    private void AddCottage(int level)
    {
        save.player.villageBuildings.Add(new ND.Framework.VillageBuildingSaveData
        {
            displayName = config.BuildingDisplayName,
            level = level
        });
    }

    private static void AssertObjectReferenceAssigned(UnityEngine.Object target, string propertyName)
    {
        var serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        Assert.That(property, Is.Not.Null, propertyName);
        Assert.That(property.objectReferenceValue, Is.Not.Null, propertyName);
    }

    private static ISharedGameDataProvider CreateTransportCatalog()
    {
        return new SharedGameDataView(
            new Dictionary<string, SharedTownDefinition>(),
            new Dictionary<string, SharedMarketDefinition>(),
            new Dictionary<string, SharedTradeItemDefinition>(),
            new Dictionary<string, SharedWagonDefinition>
            {
                ["Wagon_M"] = new SharedWagonDefinition
                    { Id = "Wagon_M", MaxDurability = 100 }
            },
            new Dictionary<string, SharedDraftAnimalDefinition>
            {
                ["Horse"] = new SharedDraftAnimalDefinition { Id = "Horse" }
            },
            new Dictionary<string, SharedRouteDefinition>());
    }

    private sealed class FailingSaveService : ISaveService
    {
        public bool HasSaveData() => true;
        public ND.Framework.SaveData CreateNewGameData() => new ND.Framework.SaveData();
        public ND.Framework.SaveData Load() => new ND.Framework.SaveData();
        public SaveResult Save(ND.Framework.SaveData data) =>
            SaveResult.Failure(SaveFailureReason.WriteFailed, "Expected failure.");
        public void ResetSaveData() { }
    }
}
