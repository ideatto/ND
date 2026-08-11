using System.Linq;
using ND.UI.CargoSell;
using ND.UI.Market;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ND.Framework.Editor
{
    public sealed class CargoSellPopupPrefabTests
    {
        private const string PrefabPath =
            "Assets/_Project/08.Prefabs/UI/Trade/CargoSellPopup.prefab";
        private const string MainUiPrefabPath =
            "Assets/_Project/08.Prefabs/MainUICanvas.prefab";

        [Test]
        public void Prefab_OpenBindsExactCaravanAndTradeWithoutSceneConnection()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                CargoSellPopupController controller =
                    instance.GetComponent<CargoSellPopupController>();
                Assert.That(controller, Is.Not.Null);

                bool opened = controller.Open(new CargoSellViewData
                {
                    caravanId = "popup-caravan",
                    tradeId = "popup-trade",
                    caravanDisplayName = "Caravan 1",
                    destinationTownName = "River Town",
                    canConfirm = true,
                    cargoItems = new[]
                    {
                        new CargoSellCargoItemViewData
                        {
                            itemId = "grain",
                            purchaseUnitPrice = 10,
                            displayName = "Grain",
                            cargoQuantity = 3,
                            sellUnitPrice = 7
                        }
                    }
                });

                Assert.That(opened, Is.True);
                Assert.That(controller.CaravanId, Is.EqualTo("popup-caravan"));
                Assert.That(controller.TradeId, Is.EqualTo("popup-trade"));
                Assert.That(controller.HasDraft, Is.False);
                Assert.That(instance.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void EmptyDraft_ConfirmRequestsSettlementAndLocksDuplicateInput()
        {
            GameObject instance = InstantiatePopup(out CargoSellPopupController controller);
            try
            {
                int requests = 0;
                int submittedLines = -1;
                controller.ConfirmRequested += pending =>
                {
                    requests++;
                    submittedLines = pending.Count;
                };
                Assert.That(controller.Open(CreateViewData()), Is.True);

                Find(instance, "ConfirmSaleButton").GetComponent<Button>().onClick.Invoke();
                Find(instance, "ConfirmSaleButton").GetComponent<Button>().onClick.Invoke();

                Assert.That(requests, Is.EqualTo(1));
                Assert.That(submittedLines, Is.Zero);
                Assert.That(controller.IsSubmitting, Is.True);

                controller.SetSubmissionResult(false, "SAVE_FAILED");
                Assert.That(controller.IsSubmitting, Is.False);
                Assert.That(instance.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void Backdrop_DiscardsDraftAndClosesWithoutConfirming()
        {
            GameObject instance = InstantiatePopup(out CargoSellPopupController controller);
            try
            {
                int closes = 0;
                int confirms = 0;
                controller.CloseRequested += () => closes++;
                controller.ConfirmRequested += _ => confirms++;
                CargoSellViewData data = CreateViewData();
                data.pendingItems = new[]
                {
                    new CargoSellPendingSaleRowViewData
                    {
                        ItemId = "grain",
                        PurchaseUnitPrice = 10,
                        DisplayName = "Grain",
                        Quantity = 2,
                        SellUnitPrice = 7
                    }
                };
                Assert.That(controller.Open(data), Is.True);
                Assert.That(controller.HasDraft, Is.True);

                Find(instance, "Backdrop").GetComponent<Button>().onClick.Invoke();

                Assert.That(closes, Is.EqualTo(1));
                Assert.That(confirms, Is.Zero);
                Assert.That(controller.HasDraft, Is.False);
                Assert.That(instance.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void MainUi_ContainsInactiveCargoSellPopupConnectedToArrivalController()
        {
            GameObject mainUi = AssetDatabase.LoadAssetAtPath<GameObject>(MainUiPrefabPath);
            Assert.That(mainUi, Is.Not.Null);

            CargoSellPopupController[] popups =
                mainUi.GetComponentsInChildren<CargoSellPopupController>(true);
            CaravanArrivalSaleController[] arrivals =
                mainUi.GetComponentsInChildren<CaravanArrivalSaleController>(true);

            Assert.That(popups.Length, Is.EqualTo(1));
            Assert.That(arrivals.Length, Is.EqualTo(1));
            Assert.That(popups[0].gameObject.activeSelf, Is.False);

            var serialized = new SerializedObject(arrivals[0]);
            SerializedProperty property = serialized.FindProperty("cargoSellPopup");
            Assert.That(property, Is.Not.Null);
            Assert.That(property.objectReferenceValue, Is.SameAs(popups[0]));
        }

        private static GameObject InstantiatePopup(out CargoSellPopupController controller)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = Object.Instantiate(prefab);
            controller = instance.GetComponent<CargoSellPopupController>();
            Assert.That(controller, Is.Not.Null);
            return instance;
        }

        private static CargoSellViewData CreateViewData()
        {
            return new CargoSellViewData
            {
                caravanId = "popup-caravan",
                tradeId = "popup-trade",
                caravanDisplayName = "Caravan 1",
                destinationTownName = "River Town",
                canConfirm = true,
                cargoItems = new[]
                {
                    new CargoSellCargoItemViewData
                    {
                        itemId = "grain",
                        purchaseUnitPrice = 10,
                        displayName = "Grain",
                        cargoQuantity = 3,
                        sellUnitPrice = 7
                    }
                }
            };
        }

        private static Transform Find(GameObject root, string objectName)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .First(child => child.name == objectName);
        }
    }
}
