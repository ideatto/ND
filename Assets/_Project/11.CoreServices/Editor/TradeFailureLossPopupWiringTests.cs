#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ND.Framework.Editor.Tests
{
    public sealed class TradeFailureLossPopupWiringTests
    {
        private const string PopupPath =
            "Assets/_Project/08.Prefabs/UI/Trade/TradeFailureLossPopup.prefab";
        private const string InGameScenePath =
            "Assets/_Project/07.Scenes/04_InGame/InGame.unity";

        [Test]
        public void PopupPrefab_HasRequiredSerializedReferencesAndStartsInactive()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PopupPath);
            try
            {
                ReusableMessagePopup popup = root.GetComponent<ReusableMessagePopup>();
                Assert.That(popup, Is.Not.Null);
                Assert.That(root.activeSelf, Is.False);

                var serialized = new SerializedObject(popup);
                Assert.That(serialized.FindProperty("messageText").objectReferenceValue,
                    Is.Not.Null);
                Assert.That(serialized.FindProperty("confirmButton").objectReferenceValue,
                    Is.Not.Null);
                Assert.That(serialized.FindProperty("confirmButtonText").objectReferenceValue,
                    Is.Not.Null);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        [Explicit("Run after assembling the placed Popup reference in the target InGame scene.")]
        public void InGameScene_SettlementAdaptersReferencePlacedPopupInstance()
        {
            Scene scene = EditorSceneManager.OpenScene(InGameScenePath, OpenSceneMode.Additive);
            try
            {
                int adapterCount = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    SettlementUiDataAdapter[] adapters =
                        root.GetComponentsInChildren<SettlementUiDataAdapter>(true);
                    foreach (SettlementUiDataAdapter adapter in adapters)
                    {
                        adapterCount++;
                        var serialized = new SerializedObject(adapter);
                        var popup = serialized.FindProperty("failureLossPopup")
                            .objectReferenceValue as ReusableMessagePopup;
                        Assert.That(popup, Is.Not.Null, adapter.name);
                        Assert.That(popup.gameObject.scene, Is.EqualTo(scene), adapter.name);
                        Assert.That(popup.gameObject.activeSelf, Is.False, adapter.name);
                    }
                }

                Assert.That(adapterCount, Is.GreaterThan(0));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
#endif
