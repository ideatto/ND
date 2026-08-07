using ND.Framework;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class CaravanActivityLogDetailPanelTests
{
    private const string DetailPanelPath =
        "Assets/_Project/05.UI/09_QoL/CaravanActivityLog/Prefabs/CaravanActivityLogDetailPanel.prefab";

    [Test]
    public void DetailPrefab_ProvidesEmptyContentAndBackButton()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DetailPanelPath);

        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<CaravanActivityLogDetailPanel>(), Is.Not.Null);
        Assert.That(prefab.transform.Find("ContentRoot").childCount, Is.Zero);
        Assert.That(
            prefab.transform.Find("BackButton").GetComponent<Button>(),
            Is.Not.Null);
    }

    [Test]
    public void DetailBackButton_RaisesCloseRequest()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DetailPanelPath);
        var instance = Object.Instantiate(prefab);
        try
        {
            var detail = instance.GetComponent<CaravanActivityLogDetailPanel>();
            var entry = new CaravanActivityLogEntrySaveData
            {
                caravanId = "caravan-test",
                eventType = CaravanActivityLogType.Arrival
            };
            var closed = false;
            detail.CloseRequested += () => closed = true;

            detail.Open(entry);
            detail.transform.Find("BackButton").GetComponent<Button>().onClick.Invoke();

            Assert.That(detail.CurrentEntry, Is.SameAs(entry));
            Assert.That(closed, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }
}
