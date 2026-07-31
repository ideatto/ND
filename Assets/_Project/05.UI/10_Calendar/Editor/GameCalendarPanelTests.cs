#if UNITY_EDITOR
using System;
using System.Reflection;
using ND.Framework;
using ND.UI.Calendar;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FrameworkSaveData = ND.Framework.SaveData;
using Object = UnityEngine.Object;

namespace ND.UI.CalendarEditor
{
    public sealed class GameCalendarPanelTests
    {
        [Test]
        public void Prefab_ContainsCompleteTwelveMonthDisplay()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameCalendarPanelPrefabGenerator.PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            GameCalendarPanelView view = prefab.GetComponent<GameCalendarPanelView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(prefab.GetComponent<GameCalendarPanelPresenter>(), Is.Not.Null);
            Assert.That(view.MonthSlots, Has.Length.EqualTo(12));
            for (int i = 0; i < 12; i++) Assert.That(view.MonthSlots[i].Month, Is.EqualTo(i + 1));
        }

        [Test]
        public void Refresh_UpdatesFullDisplayAndClearsPreviousHighlight()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameCalendarPanelPrefabGenerator.PrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                GameCalendarPanelView view = instance.GetComponent<GameCalendarPanelView>();
                view.Refresh(Snapshot(1, 3, GameSeason.Spring, string.Empty));
                Assert.That(view.YearMonthDisplay, Is.EqualTo("1년 3월"));
                Assert.That(view.SeasonDisplay, Is.EqualTo("봄"));
                Assert.That(view.IsDisasterVisible, Is.False);
                AssertOnlyMonth(view, 3);

                view.Refresh(Snapshot(2, 6, GameSeason.Summer, "flood"));
                Assert.That(view.YearMonthDisplay, Is.EqualTo("2년 6월"));
                Assert.That(view.SeasonDisplay, Is.EqualTo("여름"));
                Assert.That(view.DisasterDisplay, Is.EqualTo("홍수"));
                Assert.That(view.IsDisasterVisible, Is.True);
                AssertOnlyMonth(view, 6);

                view.Refresh(Snapshot(2, 13, GameSeason.Winter, "custom"));
                Assert.That(view.DisasterDisplay, Is.EqualTo("custom"));
                AssertOnlyMonth(view, 0);
            }
            finally
            {
                DestroyPanel(instance);
            }
        }

        [Test]
        public void HideUntilInitialized_HidesContent()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameCalendarPanelPrefabGenerator.PrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                GameCalendarPanelView view = instance.GetComponent<GameCalendarPanelView>();
                view.HideUntilInitialized();
                Assert.That(view.IsContentVisible, Is.False);
            }
            finally
            {
                DestroyPanel(instance);
            }
        }

        [TestCase(GameSeason.Spring, "봄")]
        [TestCase(GameSeason.Summer, "여름")]
        [TestCase(GameSeason.Autumn, "가을")]
        [TestCase(GameSeason.Winter, "겨울")]
        public void SeasonLabel_UsesKoreanPresentation(GameSeason season, string expected)
        {
            Assert.That(GameCalendarPanelView.GetSeasonLabel(season), Is.EqualTo(expected));
        }

        [TestCase("", "")]
        [TestCase("flood", "홍수")]
        [TestCase("drought", "가뭄")]
        [TestCase("custom", "custom")]
        public void DisasterLabel_UsesKnownLabelsAndRawFallback(string id, string expected)
        {
            Assert.That(GameCalendarPanelView.GetDisasterLabel(id), Is.EqualTo(expected));
        }

        [Test]
        public void Presenter_EnabledBeforeInitialization_RefreshesImmediatelyWhenSessionStarts()
        {
            FrameworkRoot root = CreateRoot();
            GameObject instance = InstantiatePanel();
            try
            {
                GameCalendarPanelView view = instance.GetComponent<GameCalendarPanelView>();
                Assert.That(root.GameCalendar.HasCurrent, Is.False);
                Assert.That(view.IsContentVisible, Is.False);

                Assert.That(root.GameCalendar.BeginOnlineSession(CalendarData(0L), DateTime.UtcNow), Is.True);

                Assert.That(view.IsContentVisible, Is.True);
                Assert.That(view.YearMonthDisplay, Does.Contain("1").And.Contain("3"));
                AssertOnlyMonth(view, 3);
            }
            finally
            {
                DestroyPanel(instance);
                DestroyRoot(root);
            }
        }

        [Test]
        public void Presenter_AlreadyInitializedOnEnable_DisplaysCurrentWithoutEventHistory()
        {
            FrameworkRoot root = CreateRoot();
            Assert.That(root.GameCalendar.BeginOnlineSession(CalendarData(0L), DateTime.UtcNow), Is.True);
            GameObject instance = InstantiatePanel();
            try
            {
                GameCalendarPanelView view = instance.GetComponent<GameCalendarPanelView>();
                Assert.That(view.IsContentVisible, Is.True);
                AssertOnlyMonth(view, 3);
            }
            finally
            {
                DestroyPanel(instance);
                DestroyRoot(root);
            }
        }

        [Test]
        public void Presenter_DisabledIgnoresEvents_AndReenableDisplaysLatestCurrent()
        {
            FrameworkRoot root = CreateRoot();
            Assert.That(root.GameCalendar.BeginOnlineSession(CalendarData(0L), DateTime.UtcNow), Is.True);
            GameObject instance = InstantiatePanel();
            try
            {
                GameCalendarPanelView view = instance.GetComponent<GameCalendarPanelView>();
                DisablePanel(instance);
                view.HideUntilInitialized();
                Assert.That(root.GameCalendar.BeginOnlineSession(CalendarData(90L), DateTime.UtcNow), Is.True);
                FrameworkEvents.RaiseCalendarInitialized(root.GameCalendar.Current);
                Assert.That(view.IsContentVisible, Is.False);

                EnablePanel(instance);
                Assert.That(view.IsContentVisible, Is.True);
                AssertOnlyMonth(view, 6);
            }
            finally
            {
                DestroyPanel(instance);
                DestroyRoot(root);
            }
        }

        [Test]
        public void Presenter_RepeatedEnableDisable_KeepsSingleInitializationSubscription()
        {
            FrameworkRoot root = CreateRoot();
            GameObject instance = InstantiatePanel();
            try
            {
                GameCalendarPanelPresenter presenter = instance.GetComponent<GameCalendarPanelPresenter>();
                for (int i = 0; i < 3; i++)
                {
                    DisablePanel(instance);
                    Assert.That(InitializationHandlerCount(presenter), Is.Zero);
                    EnablePanel(instance);
                }

                Assert.That(InitializationHandlerCount(presenter), Is.EqualTo(1));
                Assert.That(root.GameCalendar.BeginOnlineSession(CalendarData(0L), DateTime.UtcNow), Is.True);
                AssertOnlyMonth(instance.GetComponent<GameCalendarPanelView>(), 3);
            }
            finally
            {
                DestroyPanel(instance);
                DestroyRoot(root);
            }
        }

        [Test]
        public void Presenter_CalendarRestored_StillRefreshesFinalSnapshot()
        {
            FrameworkRoot root = CreateRoot();
            GameObject instance = InstantiatePanel();
            try
            {
                var service = new GameCalendarService(new TestTimeProvider());
                FrameworkSaveData data = CalendarData(0L);
                var now = DateTime.UtcNow;
                var context = new OfflineRestoreContext(now, now, now, TimeSpan.Zero, false, false);
                CalendarRestoreResult result = service.RestoreOffline(data, context);
                Assert.That(result, Is.Not.Null);

                FrameworkEvents.RaiseCalendarRestored(result);

                GameCalendarPanelView view = instance.GetComponent<GameCalendarPanelView>();
                Assert.That(view.IsContentVisible, Is.True);
                AssertOnlyMonth(view, 3);
            }
            finally
            {
                DestroyPanel(instance);
                DestroyRoot(root);
            }
        }

        [Test]
        public void Presenter_FailedInitialization_EmitsNoReadyEventAndRemainsHidden()
        {
            FrameworkRoot root = CreateRoot();
            GameObject instance = InstantiatePanel();
            int calls = 0;
            Action<GameCalendarSnapshot> handler = _ => calls++;
            FrameworkEvents.CalendarInitialized += handler;
            try
            {
                Assert.That(root.GameCalendar.BeginOnlineSession(new FrameworkSaveData(), DateTime.UtcNow), Is.False);
                Assert.That(calls, Is.Zero);
                Assert.That(instance.GetComponent<GameCalendarPanelView>().IsContentVisible, Is.False);
            }
            finally
            {
                FrameworkEvents.CalendarInitialized -= handler;
                DestroyPanel(instance);
                DestroyRoot(root);
            }
        }

        private static FrameworkRoot CreateRoot()
        {
            var rootObject = new GameObject("GameCalendarPanelTests.FrameworkRoot");
            FrameworkRoot root = rootObject.AddComponent<FrameworkRoot>();
            SetBackingField(root, "GameCalendar", new GameCalendarService(new TestTimeProvider()));
            SetBackingField(null, "Instance", root);
            Assert.That(FrameworkRoot.Instance, Is.SameAs(root));
            return root;
        }

        private static void DestroyRoot(FrameworkRoot root)
        {
            SetBackingField(null, "Instance", null);
            Object.DestroyImmediate(root.gameObject);
        }

        private static void SetBackingField(object target, string propertyName, object value)
        {
            FieldInfo field = typeof(FrameworkRoot).GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static GameObject InstantiatePanel()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameCalendarPanelPrefabGenerator.PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = Object.Instantiate(prefab);
            InvokePresenterLifecycle(instance, "OnEnable");
            return instance;
        }

        private static void DisablePanel(GameObject instance)
        {
            InvokePresenterLifecycle(instance, "OnDisable");
            instance.SetActive(false);
        }

        private static void EnablePanel(GameObject instance)
        {
            instance.SetActive(true);
            InvokePresenterLifecycle(instance, "OnEnable");
        }

        private static void DestroyPanel(GameObject instance)
        {
            InvokePresenterLifecycle(instance, "OnDisable");
            Object.DestroyImmediate(instance);
        }

        private static void InvokePresenterLifecycle(GameObject instance, string methodName)
        {
            GameCalendarPanelPresenter presenter = instance.GetComponent<GameCalendarPanelPresenter>();
            MethodInfo method = typeof(GameCalendarPanelPresenter).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(method, Is.Not.Null);
            method.Invoke(presenter, null);
        }

        private static FrameworkSaveData CalendarData(long totalElapsedDays)
        {
            var data = new FrameworkSaveData();
            data.world.calendar = new GameCalendarSaveData
            {
                totalElapsedDays = totalElapsedDays,
                dayAnchorUtcTicks = DateTime.UtcNow.Ticks
            };
            return data;
        }

        private static int InitializationHandlerCount(GameCalendarPanelPresenter presenter)
        {
            FieldInfo field = typeof(FrameworkEvents).GetField(
                "CalendarInitialized",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var handlers = field.GetValue(null) as Delegate;
            int count = 0;
            if (handlers == null)
            {
                return count;
            }

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                if (ReferenceEquals(handler.Target, presenter))
                {
                    count++;
                }
            }

            return count;
        }

        private sealed class TestTimeProvider : IGameTimeProvider
        {
            public DateTime CurrentUtc => DateTime.UtcNow;
        }

        private static GameCalendarSnapshot Snapshot(
            int year, int month, GameSeason season, string disasterId)
        {
            return new GameCalendarSnapshot(
                new GameCalendarDate(0L, year, month, 1, 0L, season),
                disasterId);
        }

        private static void AssertOnlyMonth(GameCalendarPanelView view, int selectedMonth)
        {
            foreach (GameCalendarMonthSlotView slot in view.MonthSlots)
            {
                Assert.That(slot.IsCurrent, Is.EqualTo(slot.Month == selectedMonth));
            }
        }
    }
}
#endif
