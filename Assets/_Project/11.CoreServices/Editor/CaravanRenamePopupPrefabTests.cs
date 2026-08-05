#if UNITY_EDITOR
using ND.UI;
using NUnit.Framework;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class CaravanRenamePopupPrefabTests
{
    private const string PopupPath =
        "Assets/_Project/08.Prefabs/UI/Trade/CaravanRenamePopup.prefab";
    private const string MainUiPath =
        "Assets/_Project/08.Prefabs/UI/Maps/MainUICanvas.prefab";

    [Test]
    public void Popup_ReopenEnablesInputAndSubmitsNewCaravanIdentity()
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath);
        Assert.That(asset, Is.Not.Null);
        GameObject instance = Object.Instantiate(asset);
        try
        {
            instance.SetActive(true);
            CaravanRenamePopupController popup =
                instance.GetComponent<CaravanRenamePopupController>();
            TMP_InputField input = instance.GetComponentInChildren<TMP_InputField>(true);
            string submittedId = string.Empty;
            string submittedName = string.Empty;

            popup.Open("caravan-1", "첫 번째", (id, name) =>
            {
                submittedId = id;
                submittedName = name;
            });
            input.text = "변경 1";
            Confirm(popup);

            Assert.That(submittedId, Is.EqualTo("caravan-1"));
            Assert.That(submittedName, Is.EqualTo("변경 1"));
            Assert.That(input.interactable, Is.False);

            popup.Open("caravan-2", "두 번째", (id, name) =>
            {
                submittedId = id;
                submittedName = name;
            });

            Assert.That(input.interactable, Is.True);
            Assert.That(input.text, Is.EqualTo("두 번째"));
            input.text = "변경 2";
            Confirm(popup);
            Assert.That(submittedId, Is.EqualTo("caravan-2"));
            Assert.That(submittedName, Is.EqualTo("변경 2"));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void MainUi_ContainsInactivePopupAndSerializedRenameBinding()
    {
        GameObject mainUi = PrefabUtility.LoadPrefabContents(MainUiPath);
        try
        {
            CaravanRenamePopupController popup =
                mainUi.GetComponentInChildren<CaravanRenamePopupController>(true);
            CaravanOverviewPresenter presenter =
                mainUi.GetComponentInChildren<CaravanOverviewPresenter>(true);
            CaravanOverviewRenameBinding binding =
                presenter.GetComponent<CaravanOverviewRenameBinding>();

            Assert.That(popup, Is.Not.Null);
            Assert.That(popup.gameObject.activeSelf, Is.False);
            var popupSerialized = new SerializedObject(popup);
            Assert.That(popupSerialized.FindProperty("input").objectReferenceValue,
                Is.Not.Null);
            Assert.That(popupSerialized.FindProperty("errorText").objectReferenceValue,
                Is.Not.Null);
            Assert.That(popupSerialized.FindProperty("cancelButton").objectReferenceValue,
                Is.Not.Null);
            Assert.That(popupSerialized.FindProperty("confirmButton").objectReferenceValue,
                Is.Not.Null);
            Assert.That(binding, Is.Not.Null);
            var serialized = new SerializedObject(binding);
            Assert.That(serialized.FindProperty("presenter").objectReferenceValue,
                Is.SameAs(presenter));
            Assert.That(serialized.FindProperty("popup").objectReferenceValue,
                Is.SameAs(popup));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(mainUi);
        }
    }

    [TestCase(JourneyState.Prepare, "준비 중")]
    [TestCase(JourneyState.Traveling, "무역 중")]
    [TestCase(JourneyState.Selling, "판매 대기")]
    [TestCase(JourneyState.Settling, "정산 대기")]
    [TestCase(JourneyState.Completed, "무역 완료")]
    public void JourneyLabel_UsesSharedPresentationContract(
        JourneyState state,
        string expected)
    {
        Assert.That(CaravanJourneyStateLabel.Format(state), Is.EqualTo(expected));
    }

    private static void Confirm(CaravanRenamePopupController popup)
    {
        typeof(CaravanRenamePopupController)
            .GetMethod("Confirm", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(popup, null);
    }
}
#endif
