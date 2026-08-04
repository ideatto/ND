#if UNITY_EDITOR
using System.Collections;
using ND.Framework;
using ND.UI.RescueLoan;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using System.Collections.Generic;

namespace ND.UI.RescueLoanEditor
{
    public sealed class RescueLoanPanelPlayModeTests
    {
        private SceneSetup[] originalSceneSetup;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                    Assert.Ignore("PlayMode test does not replace an open dirty Scene.");
            }

            originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.OpenScene(
                RescueLoanPanelPrefabGenerator.InGameScenePath,
                OpenSceneMode.Single);
            yield return new EnterPlayMode();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            if (originalSceneSetup != null && originalSceneSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(originalSceneSetup);
        }

        [UnityTest]
        public IEnumerator InGameScene_ContainsHiddenFrameworkConnectedPanel()
        {
            yield return null;
            yield return null;
            yield return null;

            RescueLoanPanelVisibility[] panels = Object.FindObjectsByType<RescueLoanPanelVisibility>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(panels, Has.Length.EqualTo(1));
            Assert.That(panels[0].GetComponentInParent<Canvas>(true)?.name, Is.EqualTo("MainUICanvas"));
            Assert.That(panels[0].IsVisible, Is.False);
            Assert.That(FrameworkRoot.Instance, Is.Not.Null);

            Transform launcher = FindSceneTransform("LoanLauncherButton");
            Assert.That(launcher, Is.Not.Null);
            Assert.That(launcher.parent.name, Is.EqualTo("RescueLoanPanel"));
            Transform navigationGroup = FindSceneTransform("NavigationButtonGroup");
            Assert.That(navigationGroup, Is.Not.Null);
            Assert.That(navigationGroup.childCount, Is.EqualTo(5));

            FrameworkRoot.Instance.CurrentSaveData.player.tradingCurrency = 602L;
            FrameworkRoot.Instance.CurrentSaveData.rescueLoan.isActive = false;
            RescueLoanPanelView view = panels[0].GetComponent<RescueLoanPanelView>();
            FrameworkEvents.RaiseTradingCurrencyChanged(602L);
            yield return null;
            Assert.That(view.IsLauncherVisible, Is.True);
            Assert.That(view.LauncherDisplay, Is.EqualTo("구조 대출"));

            Button launcherButton = launcher.GetComponent<Button>();
            Assert.That(launcherButton, Is.Not.Null);
            Assert.That(launcherButton.interactable, Is.True);
            Assert.That(IsLauncherRaycastReachable(launcherButton, out string raycastTarget), Is.True,
                $"Top EventSystem raycast target was {raycastTarget}.");

            launcherButton.onClick.Invoke();
            yield return null;
            Assert.That(panels[0].IsVisible, Is.True);
            CanvasGroup modalGroup = panels[0].GetComponentInChildren<CanvasGroup>(true);
            Assert.That(modalGroup, Is.Not.Null);
            Assert.That(modalGroup.alpha, Is.EqualTo(1f));
            Assert.That(modalGroup.interactable, Is.True);
            Assert.That(modalGroup.blocksRaycasts, Is.True);
            Assert.That(panels[0].GetComponent<RescueLoanPanelView>(), Is.Not.Null);
            Assert.That(panels[0].GetComponent<RescueLoanPanelPresenter>(), Is.Not.Null);
        }

        private static bool IsLauncherRaycastReachable(Button launcherButton, out string topTarget)
        {
            Canvas canvas = launcherButton.GetComponentInParent<Canvas>();
            if (canvas == null || EventSystem.current == null)
            {
                topTarget = "<missing canvas or EventSystem>";
                return false;
            }

            RectTransform rect = (RectTransform)launcherButton.transform;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center));
            PointerEventData eventData = new PointerEventData(EventSystem.current) { position = screenPoint };
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            if (results.Count == 0)
            {
                topTarget = "<none>";
                return false;
            }

            Transform hit = results[0].gameObject.transform;
            topTarget = results[0].gameObject.name;
            return hit == launcherButton.transform || hit.IsChildOf(launcherButton.transform);
        }

        private static Transform FindSceneTransform(string objectName)
        {
            foreach (Transform candidate in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (candidate.name == objectName) return candidate;
            }

            return null;
        }

    }
}
#endif
