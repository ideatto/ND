using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using FrameworkCaravanSaveData = ND.Framework.CaravanSaveData;
using FrameworkSaveData = ND.Framework.SaveData;
using FrameworkTradeProgressSaveData = ND.Framework.TradeProgressSaveData;
using ND.UI.InGame.TransportInventory;
using TMPro;

public sealed class CaravanSettingPlayModeSmokeTests
{
    private const string BootScenePath = "Assets/_Project/07.Scenes/01_Boot/Boot.unity";
    private const string TitleSceneName = "Title";
    private const string InGameSceneName = "InGame";
    private const int SceneLoadTimeoutFrames = 300;
    private SceneSetup[] originalSceneSetup;
    private string originalRuntimeSaveJson;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            if (SceneManager.GetSceneAt(index).isDirty)
                Assert.Ignore("PlayMode smoke test does not replace an open dirty Scene.");
        }

        originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
        yield return new EnterPlayMode();
        InstallDeterministicSaveFixture();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (Application.isPlaying)
        {
            RestoreRuntimeSave();
            yield return new ExitPlayMode();
        }

        if (originalSceneSetup != null && originalSceneSetup.Length > 0)
            EditorSceneManager.RestoreSceneManagerSetup(originalSceneSetup);
    }

    private void InstallDeterministicSaveFixture()
    {
        ND.Framework.FrameworkRoot root = ND.Framework.FrameworkRoot.Instance;
        Assert.That(root, Is.Not.Null, "FrameworkRoot was not created before the smoke fixture setup.");
        Assert.That(root.CurrentSaveData, Is.Not.Null, "FrameworkRoot has no runtime SaveData to isolate.");

        originalRuntimeSaveJson = JsonUtility.ToJson(root.CurrentSaveData);
        var fixture = new FrameworkSaveData();
        fixture.caravans.Clear();
        fixture.tradeProgressEntries.Clear();
        fixture.pendingSettlements.Clear();
        fixture.caravanActivityLogs.Clear();
        fixture.player.currentTownId = ND.Framework.CaravanManagementService.InitialCaravanTownId;
        fixture.world.unlockedCaravanSlotIndices.Clear();
        fixture.world.unlockedCaravanSlotIndices.Add(0);
        fixture.player.villageBuildings.Clear();
        fixture.player.villageBuildings.Add(new ND.Framework.VillageBuildingSaveData
        {
            displayName = ND.Framework.TransportInventoryFunction.BuildingDisplayName,
            level = 1
        });
        fixture.player.wagonInventory.Clear();
        fixture.player.draftAnimalInventory.Clear();

        const string caravanId = "caravan-setting-ui-smoke";
        fixture.caravans.Add(new FrameworkCaravanSaveData
        {
            caravanId = caravanId,
            slotIndex = 0,
            currentTownId = ND.Framework.CaravanManagementService.InitialCaravanTownId,
            state = JourneyState.Prepare
        });
        fixture.tradeProgressEntries.Add(new FrameworkTradeProgressSaveData
        {
            caravanId = caravanId,
            state = ND.Framework.TradeProgressState.None
        });
        fixture.selectedCaravanId = caravanId;

        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fixture), root.CurrentSaveData);
        root.InGameScreenRouter.RefreshFromSaveData(root.CurrentSaveData, true);
    }

    private void RestoreRuntimeSave()
    {
        ND.Framework.FrameworkRoot root = ND.Framework.FrameworkRoot.Instance;
        if (root == null || root.CurrentSaveData == null || string.IsNullOrEmpty(originalRuntimeSaveJson))
            return;

        JsonUtility.FromJsonOverwrite(originalRuntimeSaveJson, root.CurrentSaveData);
        root.InGameScreenRouter.RefreshFromSaveData(root.CurrentSaveData, true);
        originalRuntimeSaveJson = null;
    }

    [UnityTest]
    public IEnumerator BootToInGame_InitializesCaravanSettingConnectionWithoutErrors()
    {
        int waitFrames = 0;
        while (!SceneManager.GetSceneByName(TitleSceneName).isLoaded
            && waitFrames < SceneLoadTimeoutFrames)
        {
            waitFrames++;
            yield return null;
        }
        Assert.That(
            SceneManager.GetSceneByName(TitleSceneName).isLoaded,
            Is.True,
            "Boot did not finish its initial Title scene transition.");

        AsyncOperation load = SceneManager.LoadSceneAsync(InGameSceneName, LoadSceneMode.Single);
        Assert.That(load, Is.Not.Null);
        while (!load.isDone)
            yield return null;

        // Allow Awake, OnEnable, Start, and one subsequent UI binding update to complete.
        for (int frame = 0; frame < 3; frame++)
            yield return null;

        Scene inGame = SceneManager.GetSceneByName(InGameSceneName);
        Assert.That(inGame.IsValid(), Is.True);
        Assert.That(inGame.isLoaded, Is.True);
        ND.Framework.FrameworkRoot root = ND.Framework.FrameworkRoot.Instance;
        Assert.That(root, Is.Not.Null);

        List<TestCaravanSettingService> temporaryServices =
            FindComponentsInScene<TestCaravanSettingService>(inGame);
        List<CaravanSettingRuntimeBridge> bridges =
            FindComponentsInScene<CaravanSettingRuntimeBridge>(inGame);
        List<CaravanOverviewEditBinding> bindings =
            FindComponentsInScene<CaravanOverviewEditBinding>(inGame);
        List<TradePrepareUIManager> tradePrepareManagers =
            FindComponentsInScene<TradePrepareUIManager>(inGame);
        List<CaravanSlotView> slotViews = FindComponentsInScene<CaravanSlotView>(inGame);
        List<TransportInventoryMainUiEntry> inventoryEntries =
            FindComponentsInScene<TransportInventoryMainUiEntry>(inGame);
        List<TransportInventoryPopupController> inventoryPopups =
            FindComponentsInScene<TransportInventoryPopupController>(inGame);
        List<TransportInventoryRewardDebugButton> rewardButtons =
            FindComponentsInScene<TransportInventoryRewardDebugButton>(inGame);

        Assert.That(temporaryServices, Is.Empty);
        Assert.That(bridges.Count, Is.EqualTo(1));
        Assert.That(bindings.Count, Is.EqualTo(1));
        Assert.That(tradePrepareManagers.Count, Is.EqualTo(1));
        Assert.That(inventoryEntries.Count, Is.EqualTo(1));
        Assert.That(inventoryPopups.Count, Is.EqualTo(1));
        Assert.That(rewardButtons.Count, Is.EqualTo(1));
        Assert.That(bridges[0].isActiveAndEnabled, Is.True);
        Assert.That(bindings[0].isActiveAndEnabled, Is.True);
        Assert.That(bridges[0].GetOptions(), Is.Not.Null);
        Assert.That(inventoryPopups[0].gameObject.activeSelf, Is.False);

        Assert.That(rewardButtons[0].TryGrantRewards(PlayerMainManager.Instance), Is.True);
        Assert.That(root.CurrentSaveData.player.wagonInventory.Count, Is.EqualTo(2));
        Assert.That(root.CurrentSaveData.player.draftAnimalInventory.Count, Is.EqualTo(2));

        BuildingListPanel buildingList = FindComponentsInScene<BuildingListPanel>(inGame)[0];
        buildingList.Rebuild();
        yield return null;
        Button farmButton = FindButtonByPrefix(
            buildingList,
            ND.Framework.TransportInventoryFunction.BuildingDisplayName);
        Assert.That(farmButton, Is.Not.Null, "Farm building row was not created.");
        farmButton.onClick.Invoke();
        yield return null;
        Assert.That(inventoryPopups[0].gameObject.activeInHierarchy, Is.True);
        inventoryPopups[0].Close();

        var settingCommand = new RecordingSettingCommand();
        var cargoCommand = new RecordingCargoCommand();
        bindings[0].SetSettingServices(bridges[0], settingCommand);
        bindings[0].SetLoadSettingServices(bridges[0], cargoCommand);

        CaravanSlotView editableSlot = FindEditableSlot(slotViews);
        Assert.That(editableSlot, Is.Not.Null, "No occupied Caravan slot exposes Setting and Cargo actions.");

        Button settingButton = GetButton(editableSlot, "settingButton");
        Button cargoButton = GetButton(editableSlot, "cargoButton");
        Assert.That(settingButton, Is.Not.Null);
        Assert.That(cargoButton, Is.Not.Null);

        settingButton.onClick.Invoke();
        yield return null;

        string selectedCaravanId = bindings[0].CurrentEditCaravanId;
        Assert.That(selectedCaravanId, Is.Not.Empty);
        Assert.That(bindings[0].CurrentEditTarget, Is.EqualTo(CaravanOverviewEditTarget.Setting));
        Assert.That(tradePrepareManagers[0].IsDetachedCaravanEditOpen, Is.True);
        Assert.That(tradePrepareManagers[0].IsDetachedCaravanCargoEditOpen, Is.False);
        Assert.That(tradePrepareManagers[0].ActiveCaravanEditId, Is.EqualTo(selectedCaravanId));

        Button settingConfirmButton = GetPrivateField<Button>(tradePrepareManagers[0], "animalNext");
        Assert.That(settingConfirmButton, Is.Not.Null);
        Assert.That(settingConfirmButton.interactable, Is.True);
        settingConfirmButton.onClick.Invoke();
        yield return null;

        Assert.That(
            settingCommand.ExecutionCount,
            Is.EqualTo(1),
            "Setting Confirm did not reach the isolated command.");
        Assert.That(settingCommand.LastDraft, Is.Not.Null);
        Assert.That(settingCommand.LastDraft.caravanId, Is.EqualTo(selectedCaravanId));
        Assert.That(bindings[0].CurrentEditCaravanId, Is.Empty);
        Assert.That(bindings[0].CurrentEditTarget, Is.EqualTo(CaravanOverviewEditTarget.None));
        Assert.That(tradePrepareManagers[0].IsDetachedCaravanEditOpen, Is.False);

        cargoButton.onClick.Invoke();
        yield return null;

        Assert.That(bindings[0].CurrentEditCaravanId, Is.EqualTo(selectedCaravanId));
        Assert.That(bindings[0].CurrentEditTarget, Is.EqualTo(CaravanOverviewEditTarget.Cargo));
        Assert.That(tradePrepareManagers[0].IsDetachedCaravanEditOpen, Is.True);
        Assert.That(tradePrepareManagers[0].IsDetachedCaravanCargoEditOpen, Is.True);
        Assert.That(tradePrepareManagers[0].ActiveCaravanEditId, Is.EqualTo(selectedCaravanId));

        CargoLoadingPanelController cargoPanel =
            GetPrivateField<CargoLoadingPanelController>(tradePrepareManagers[0], "cargoPanel");
        Assert.That(cargoPanel, Is.Not.Null);
        cargoPanel.CanCommitCargoTransaction = () => true;
        Button cargoConfirmButton = GetPrivateField<Button>(cargoPanel, "nextButton");
        Assert.That(cargoConfirmButton, Is.Not.Null);
        Assert.That(cargoConfirmButton.interactable, Is.True);
        cargoConfirmButton.onClick.Invoke();
        yield return null;

        Assert.That(
            cargoCommand.ExecutionCount,
            Is.EqualTo(1),
            "Cargo Confirm did not reach the isolated command.");
        Assert.That(cargoCommand.LastDraft, Is.Not.Null);
        Assert.That(cargoCommand.LastDraft.caravanId, Is.EqualTo(selectedCaravanId));
        Assert.That(bindings[0].CurrentEditCaravanId, Is.Empty);
        Assert.That(bindings[0].CurrentEditTarget, Is.EqualTo(CaravanOverviewEditTarget.None));
        Assert.That(tradePrepareManagers[0].IsDetachedCaravanEditOpen, Is.False);
    }

    private static Button FindButtonByPrefix(Component root, string prefix)
    {
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null && label.text.StartsWith(prefix))
                return button;
        }

        return null;
    }

    private static List<T> FindComponentsInScene<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
            result.AddRange(roots[index].GetComponentsInChildren<T>(true));
        return result;
    }

    private static CaravanSlotView FindEditableSlot(IEnumerable<CaravanSlotView> slotViews)
    {
        foreach (CaravanSlotView slotView in slotViews)
        {
            Button settingButton = GetButton(slotView, "settingButton");
            Button cargoButton = GetButton(slotView, "cargoButton");
            if (settingButton != null && settingButton.interactable
                && cargoButton != null && cargoButton.interactable)
            {
                return slotView;
            }
        }

        return null;
    }

    private static Button GetButton(CaravanSlotView slotView, string fieldName)
    {
        return GetPrivateField<Button>(slotView, fieldName);
    }

    private static T GetPrivateField<T>(object target, string fieldName) where T : class
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(target) as T;
    }

    private sealed class RecordingSettingCommand : ICaravanSettingCommand
    {
        public int ExecutionCount { get; private set; }
        public CaravanSettingDraft LastDraft { get; private set; }

        public CaravanSettingCommandResult Execute(CaravanSettingDraft draft)
        {
            ExecutionCount++;
            LastDraft = draft?.CreateSnapshot();
            return CaravanSettingCommandResult.Success();
        }
    }

    private sealed class RecordingCargoCommand : ICaravanLoadSettingCommand
    {
        public int ExecutionCount { get; private set; }
        public CaravanLoadSettingDraft LastDraft { get; private set; }

        public CaravanLoadSettingCommandResult Execute(CaravanLoadSettingDraft draft)
        {
            ExecutionCount++;
            LastDraft = draft?.CreateSnapshot();
            return CaravanLoadSettingCommandResult.Success();
        }
    }
}
