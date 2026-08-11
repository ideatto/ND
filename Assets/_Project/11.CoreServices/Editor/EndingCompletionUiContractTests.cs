using System.Collections.Generic;
using ND.Framework;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class EndingCompletionUiContractTests
{
    private const string PrefabPath =
        "Assets/_Project/08.Prefabs/UI/Building/EndingCompletionPopup.prefab";
    private const string BuildDataPath =
        "Assets/_Project/02.Data/01_ScriptableObjects/Build/Build_EndingItem.asset";

    [Test]
    public void ViewData_UsesSavedEndingBuildingLevelAsAuthority()
    {
        var save = new ND.Framework.SaveData();
        save.player.villageBuildings = new List<VillageBuildingSaveData>();
        BuildData buildData = AssetDatabase.LoadAssetAtPath<BuildData>(BuildDataPath);
        Assert.That(buildData, Is.Not.Null);
        Assert.That(EndingCompletionViewData.Build(save, buildData.DisplayName).IsCompleted, Is.False);

        save.player.villageBuildings.Add(new VillageBuildingSaveData
        {
            displayName = buildData.DisplayName,
            level = 1
        });

        Assert.That(EndingCompletionViewData.Build(save, buildData.DisplayName).IsCompleted, Is.True);

        save.player.villageBuildings[0].displayName =
            EndingCompletionViewData.LegacyEndingBuildingDisplayName;
        Assert.That(EndingCompletionViewData.Build(save, buildData.DisplayName).IsCompleted, Is.True);
    }

    [Test]
    public void PopupPrefab_IsStaticAndHasRequiredReferences()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.transform.Find("Backdrop"), Is.Not.Null);
        Assert.That(prefab.transform.Find("Panel/Header/TitleText"), Is.Not.Null);
        Assert.That(prefab.transform.Find("Panel/MessageText"), Is.Not.Null);
        Assert.That(prefab.transform.Find("Panel/EndGameButton"), Is.Not.Null);
        Assert.That(prefab.transform.Find("Panel/ContinueButton"), Is.Not.Null);

        EndingCompletionPopupView view = prefab.GetComponent<EndingCompletionPopupView>();
        EndingCompletionPopupPresenter presenter =
            prefab.GetComponent<EndingCompletionPopupPresenter>();
        Assert.That(view, Is.Not.Null);
        Assert.That(presenter, Is.Not.Null);

        var viewSerialized = new SerializedObject(view);
        var presenterSerialized = new SerializedObject(presenter);
        Assert.That(viewSerialized.FindProperty("backdropButton").objectReferenceValue, Is.Not.Null);
        Assert.That(viewSerialized.FindProperty("messageText").objectReferenceValue, Is.Not.Null);
        Assert.That(viewSerialized.FindProperty("endGameButton").objectReferenceValue, Is.Not.Null);
        Assert.That(viewSerialized.FindProperty("continueButton").objectReferenceValue, Is.Not.Null);
        Assert.That(presenterSerialized.FindProperty("view").objectReferenceValue, Is.Not.Null);
    }
}
