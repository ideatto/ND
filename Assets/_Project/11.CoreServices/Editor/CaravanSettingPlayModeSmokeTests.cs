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

public sealed class CaravanSettingPlayModeSmokeTests
{
    private const string BootScenePath = "Assets/_Project/07.Scenes/01_Boot/Boot.unity";
    private const string TitleSceneName = "Title";
    private const string InGameSceneName = "InGame";
    private const int SceneLoadTimeoutFrames = 300;
    private SceneSetup[] originalSceneSetup;

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
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (Application.isPlaying)
            yield return new ExitPlayMode();

        if (originalSceneSetup != null && originalSceneSetup.Length > 0)
            EditorSceneManager.RestoreSceneManagerSetup(originalSceneSetup);
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

        List<TestCaravanSettingService> services =
            FindComponentsInScene<TestCaravanSettingService>(inGame);
        List<CaravanOverviewEditBinding> bindings =
            FindComponentsInScene<CaravanOverviewEditBinding>(inGame);
        List<TradePrepareUIManager> tradePrepareManagers =
            FindComponentsInScene<TradePrepareUIManager>(inGame);
        List<CaravanSlotView> slotViews = FindComponentsInScene<CaravanSlotView>(inGame);

        Assert.That(services.Count, Is.EqualTo(1));
        Assert.That(bindings.Count, Is.EqualTo(1));
        Assert.That(tradePrepareManagers.Count, Is.EqualTo(1));
        Assert.That(services[0].isActiveAndEnabled, Is.True);
        Assert.That(bindings[0].isActiveAndEnabled, Is.True);
        Assert.That(services[0].GetOptions(), Is.Not.Null);

        var settingCommand = new RecordingSettingCommand();
        var cargoCommand = new RecordingCargoCommand();
        bindings[0].SetSettingServices(services[0], settingCommand);
        bindings[0].SetLoadSettingServices(services[0], cargoCommand);

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
