#if UNITY_EDITOR
using ND.UI.RescueLoan;
using ND.Framework;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ND.UI.RescueLoanEditor
{
    public sealed class RescueLoanPanelTests
    {
        [OneTimeSetUp]
        public void EnsurePrefabExists()
        {
            RescueLoanPanelPrefabGenerator.Build();
        }

        [Test]
        public void Prefab_ContainsViewPresenterAndCommands()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RescueLoanPanelPrefabGenerator.PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<RescueLoanPanelView>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<RescueLoanPanelPresenter>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<RescueLoanPanelVisibility>(), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Button>(true), Has.Length.EqualTo(3));
        }

        [Test]
        public void Visibility_StartsHiddenAndSupportsExplicitToggle()
        {
            GameObject instance = InstantiatePanel();
            try
            {
                RescueLoanPanelVisibility visibility = instance.GetComponent<RescueLoanPanelVisibility>();
                Assert.That(visibility.IsVisible, Is.False);
                visibility.Show();
                Assert.That(visibility.IsVisible, Is.True);
                visibility.Hide();
                Assert.That(visibility.IsVisible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void Refresh_EligibleStateShowsShortfallAndEnablesIssueOnly()
        {
            GameObject instance = InstantiatePanel();
            try
            {
                RescueLoanPanelView view = instance.GetComponent<RescueLoanPanelView>();
                view.Refresh(new RescueLoanPanelSnapshot
                {
                    IsAvailable = true,
                    CanOfferLoan = true,
                    NeedsRecovery = true,
                    TradeMoney = 250L,
                    MinimumTradeCost = 1000L,
                    Shortfall = 750L
                });

                Assert.That(view.StatusDisplay, Does.Contain("대출 가능").And.Contain("750"));
                Assert.That(view.TradeMoneyDisplay, Does.Contain("250"));
                Assert.That(view.LauncherDisplay, Is.EqualTo("구조 대출"));
                Assert.That(view.IsLauncherVisible, Is.True);
                Assert.That(view.IsActionInteractable, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void Refresh_ActiveLoanShowsBalanceAndEnablesRepaymentOnly()
        {
            GameObject instance = InstantiatePanel();
            try
            {
                RescueLoanPanelView view = instance.GetComponent<RescueLoanPanelView>();
                view.Refresh(new RescueLoanPanelSnapshot
                {
                    IsAvailable = true,
                    IsActive = true,
                    IsRestrictedPreparation = true,
                    TradeMoney = 1400L,
                    MinimumTradeCost = 1000L,
                    OriginalPrincipal = 900L,
                    RemainingPrincipal = 600L
                });

                Assert.That(view.StatusDisplay, Is.EqualTo("활성 대출 상환 가능"));
                Assert.That(view.RemainingPrincipalDisplay, Does.Contain("600"));
                Assert.That(view.LauncherDisplay, Is.EqualTo("대출 상환"));
                Assert.That(view.IsLauncherVisible, Is.True);
                Assert.That(view.IsActionInteractable, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [TestCase("1", 1L)]
        [TestCase("2500", 2500L)]
        public void TryParseRepayment_AcceptsPositiveInteger(string value, long expected)
        {
            Assert.That(RescueLoanPanelView.TryParseRepayment(value, out long amount), Is.True);
            Assert.That(amount, Is.EqualTo(expected));
        }

        [TestCase("")]
        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("1.5")]
        [TestCase("abc")]
        public void TryParseRepayment_RejectsInvalidAmount(string value)
        {
            Assert.That(RescueLoanPanelView.TryParseRepayment(value, out _), Is.False);
        }

        [Test]
        public void RepaymentFailureMessage_ExplainsMinimumBalanceRestriction()
        {
            SaveResult result = SaveResult.Failure(
                SaveFailureReason.InvalidData,
                "Rescue loan repayment was rejected: RepaymentWouldTriggerRecovery.",
                "rescueLoan");

            Assert.That(
                RescueLoanPanelPresenter.RepaymentFailureMessage(result),
                Is.EqualTo("상환 후 보유 금액이 대출금보다 작을 수 없습니다."));
        }

        private static GameObject InstantiatePanel()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RescueLoanPanelPrefabGenerator.PrefabPath);
            Assert.That(prefab, Is.Not.Null, "Build the rescue loan panel prefab before running tests.");
            return Object.Instantiate(prefab);
        }
    }
}
#endif
