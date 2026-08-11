using System.Linq;
using ND.UI.InGame.BaseCamp;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ND.Framework.Editor.Tests
{
    public sealed class BaseCampOverviewUiContractTests
    {
        private const string PopupPath =
            "Assets/_Project/08.Prefabs/UI/Building/BaseCampOverviewPopup.prefab";
        private const string MainUiPath =
            "Assets/_Project/08.Prefabs/MainUICanvas.prefab";
        private const string BuildUiScenePath =
            "Assets/_Project/07.Scenes/04_InGame/Build UI.unity";
        private const string InGameScenePath =
            "Assets/_Project/07.Scenes/04_InGame/InGame.unity";

        [Test]
        public void PopupPrefab_HasAllStaticReferences()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath);
            Assert.That(prefab, Is.Not.Null);

            BaseCampOverviewPopupController controller =
                prefab.GetComponent<BaseCampOverviewPopupController>();
            Assert.That(controller, Is.Not.Null);

            SerializedObject serialized = new SerializedObject(controller);
            Assert.That(serialized.FindProperty("baseCampLevelText").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("levelLimitText").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("unlockGuideText").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("backdropButton").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("closeButton").objectReferenceValue, Is.Not.Null);

            SerializedProperty rows = serialized.FindProperty("buildingRows");
            Assert.That(rows.arraySize, Is.EqualTo(6));
            for (int i = 0; i < rows.arraySize; i++)
            {
                SerializedProperty row = rows.GetArrayElementAtIndex(i);
                Assert.That(row.FindPropertyRelative("buildingDisplayName").stringValue, Is.Not.Empty);
                Assert.That(row.FindPropertyRelative("label").objectReferenceValue, Is.Not.Null);
            }
        }

        [Test]
        [Explicit("Run after BaseCamp UI is assembled into the target branch MainUICanvas prefab.")]
        public void MainUiPrefab_HasOneWiredEntryAndPopup()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainUiPath);
            Assert.That(prefab, Is.Not.Null);

            BaseCampMainUiEntry[] entries =
                prefab.GetComponentsInChildren<BaseCampMainUiEntry>(true);
            BaseCampOverviewPopupController[] popups =
                prefab.GetComponentsInChildren<BaseCampOverviewPopupController>(true);
            Assert.That(entries, Has.Length.EqualTo(1));
            Assert.That(popups, Has.Length.EqualTo(1));

            SerializedObject serialized = new SerializedObject(entries[0]);
            Assert.That(serialized.FindProperty("buildingListPanel").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("popup").objectReferenceValue, Is.SameAs(popups[0]));
            Assert.That(serialized.FindProperty("noticeUI").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void BuildUiScene_DependsOnPopupPrefab()
        {
            string[] dependencies = AssetDatabase.GetDependencies(BuildUiScenePath, true);
            Assert.That(dependencies.Contains(PopupPath), Is.True);
        }

        [Test]
        [Explicit("Run after BaseCamp UI is assembled into the target branch InGame scene.")]
        public void InGameScene_InheritsBaseCampUiThroughMainUiPrefab()
        {
            string[] dependencies = AssetDatabase.GetDependencies(InGameScenePath, true);
            Assert.That(dependencies.Contains(MainUiPath), Is.True);
            Assert.That(dependencies.Contains(PopupPath), Is.True);
        }
    }
}
