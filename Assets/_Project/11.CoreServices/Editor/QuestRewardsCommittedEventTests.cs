using System.Collections.Generic;
using NUnit.Framework;

namespace ND.Framework.Tests
{
    public sealed class QuestRewardsCommittedEventTests
    {
        [Test]
        public void Payload_CopiesInputLists()
        {
            var towns = new List<string> { "town-new" };
            var routes = new List<string> { "route-new" };
            var specialties = new List<string> { "item-new" };

            var committed = new QuestRewardsCommittedEvent(
                "quest-a", towns, routes, specialties);
            towns[0] = "changed";
            routes.Clear();
            specialties.Add("changed");

            Assert.That(committed.QuestId, Is.EqualTo("quest-a"));
            Assert.That(committed.UnlockedTownIds,
                Is.EqualTo(new[] { "town-new" }));
            Assert.That(committed.UnlockedRouteIds,
                Is.EqualTo(new[] { "route-new" }));
            Assert.That(committed.UnlockedSpecialtyItemIds,
                Is.EqualTo(new[] { "item-new" }));
        }

        [Test]
        public void RaiseQuestRewardsCommitted_PublishesSameCommittedSnapshotOnce()
        {
            QuestRewardsCommittedEvent received = null;
            int count = 0;
            System.Action<QuestRewardsCommittedEvent> handler = value =>
            {
                received = value;
                count++;
            };

            FrameworkEvents.QuestRewardsCommitted += handler;
            try
            {
                var committed = new QuestRewardsCommittedEvent(
                    "quest-a",
                    new[] { "town-new" },
                    new[] { "route-new" },
                    new[] { "item-new" });

                FrameworkEvents.RaiseQuestRewardsCommitted(committed);

                Assert.That(count, Is.EqualTo(1));
                Assert.That(received, Is.SameAs(committed));
            }
            finally
            {
                FrameworkEvents.QuestRewardsCommitted -= handler;
            }
        }
    }
}
