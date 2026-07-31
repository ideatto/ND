#if UNITY_EDITOR
using ND.Framework;
using ND.UI.Calendar;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
                Object.DestroyImmediate(instance);
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
                Object.DestroyImmediate(instance);
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
