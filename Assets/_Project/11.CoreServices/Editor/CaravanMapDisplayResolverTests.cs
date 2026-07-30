#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;

namespace ND.Framework.Editor
{
    public sealed class CaravanMapDisplayResolverTests
    {
        private const string OriginTownId = "origin";
        private const string DestinationTownId = "destination";
        private const string RouteId = "route";

        [Test]
        public void Preparing_UsesDurableCurrentTown()
        {
            TestContext context = CreateContext(TradeProgressState.Preparing);

            AssertResolved(context, 0f, CaravanMapDisplayMode.Town, OriginTownId);
        }

        [Test]
        public void Traveling_UsesRouteWithClampedProgress()
        {
            TestContext context = CreateContext(TradeProgressState.Traveling);

            CaravanMapDisplayState state = Resolve(context, 1.5f);

            Assert.That(state.Mode, Is.EqualTo(CaravanMapDisplayMode.Route));
            Assert.That(state.RouteId, Is.EqualTo(RouteId));
            Assert.That(state.Progress01, Is.EqualTo(1f));
        }

        [TestCase(JourneyResultGrade.Success)]
        [TestCase(JourneyResultGrade.PartialSuccess)]
        public void SuccessfulPending_UsesExactDestination(JourneyResultGrade grade)
        {
            TestContext context = CreatePendingContext(grade);

            AssertResolved(context, 1f, CaravanMapDisplayMode.Town, DestinationTownId);
        }

        [Test]
        public void FailedPending_UsesExactOrigin()
        {
            TestContext context = CreatePendingContext(JourneyResultGrade.Failed);

            AssertResolved(context, 1f, CaravanMapDisplayMode.Town, OriginTownId);
        }

        [Test]
        public void MissingPending_UsesSameCaravanCurrentTown()
        {
            TestContext context = CreateContext(TradeProgressState.SettlementPending);

            CaravanMapDisplayState state = Resolve(context, 1f);

            Assert.That(state.TownId, Is.EqualTo(OriginTownId));
            Assert.That(state.Issue, Is.EqualTo(CaravanMapDisplayIssue.MissingPendingSettlement));
        }

        [TestCase("other-caravan", "trade")]
        [TestCase("caravan", "other-trade")]
        public void MismatchedPending_IsNotUsed(string pendingCaravanId, string pendingTradeId)
        {
            TestContext context = CreateContext(TradeProgressState.SettlementPending);
            context.Save.pendingSettlements.Add(new PendingSettlementSaveData
            {
                hasResult = true,
                caravanId = pendingCaravanId,
                tradeId = pendingTradeId,
                routeId = RouteId,
                grade = JourneyResultGrade.Success
            });

            CaravanMapDisplayState state = Resolve(context, 1f);

            Assert.That(state.TownId, Is.EqualTo(OriginTownId));
            Assert.That(state.Issue, Is.EqualTo(CaravanMapDisplayIssue.MissingPendingSettlement));
        }

        [Test]
        public void TwoCaravans_ResolveTheirOwnPendingGrades()
        {
            TestContext first = CreatePendingContext(JourneyResultGrade.Success);
            var secondCaravan = new CaravanSaveData { caravanId = "caravan-2", currentTownId = "origin-2" };
            var secondProgress = new TradeProgressSaveData
            {
                caravanId = secondCaravan.caravanId,
                activeTradeId = "trade-2",
                activeRouteId = RouteId,
                state = TradeProgressState.SettlementPending
            };
            first.Save.caravans.Add(secondCaravan);
            first.Save.tradeProgressEntries.Add(secondProgress);
            first.Save.pendingSettlements.Add(new PendingSettlementSaveData
            {
                hasResult = true,
                caravanId = secondCaravan.caravanId,
                tradeId = secondProgress.activeTradeId,
                routeId = RouteId,
                grade = JourneyResultGrade.Failed
            });

            bool resolved = CaravanMapDisplayResolver.TryResolve(
                first.Save,
                first.Shared,
                secondCaravan,
                secondProgress,
                1f,
                out CaravanMapDisplayState secondState);

            Assert.That(resolved, Is.True);
            Assert.That(Resolve(first, 1f).TownId, Is.EqualTo(DestinationTownId));
            Assert.That(secondState.TownId, Is.EqualTo("origin-2"));
        }

        [Test]
        public void DurablePending_ResolvesWithoutRuntimeSettlementAuthority()
        {
            TestContext restored = CreatePendingContext(JourneyResultGrade.PartialSuccess);

            CaravanMapDisplayState state = Resolve(restored, 0f);

            Assert.That(state.TownId, Is.EqualTo(DestinationTownId));
        }

        private static TestContext CreatePendingContext(JourneyResultGrade grade)
        {
            TestContext context = CreateContext(TradeProgressState.SettlementPending);
            context.Save.pendingSettlements.Add(new PendingSettlementSaveData
            {
                hasResult = true,
                caravanId = context.Caravan.caravanId,
                tradeId = context.Progress.activeTradeId,
                routeId = RouteId,
                grade = grade
            });
            context.Save.tradePreparationCommits.Add(new TradePreparationCommitSaveData
            {
                hasCommit = true,
                caravanId = context.Caravan.caravanId,
                tradeId = context.Progress.activeTradeId,
                routeId = RouteId,
                currentTownId = OriginTownId,
                destinationTownId = DestinationTownId
            });
            return context;
        }

        private static TestContext CreateContext(TradeProgressState state)
        {
            var save = new SaveData();
            CaravanSaveData caravan = save.caravans[0];
            caravan.caravanId = "caravan";
            caravan.currentTownId = OriginTownId;
            var progress = new TradeProgressSaveData
            {
                caravanId = caravan.caravanId,
                activeTradeId = "trade",
                activeRouteId = RouteId,
                state = state
            };
            save.tradeProgressEntries.Add(progress);
            return new TestContext(save, caravan, progress, new FakeSharedGameDataProvider());
        }

        private static CaravanMapDisplayState Resolve(TestContext context, float progress01)
        {
            bool resolved = CaravanMapDisplayResolver.TryResolve(
                context.Save,
                context.Shared,
                context.Caravan,
                context.Progress,
                progress01,
                out CaravanMapDisplayState state);
            Assert.That(resolved, Is.True);
            return state;
        }

        private static void AssertResolved(
            TestContext context,
            float progress01,
            CaravanMapDisplayMode mode,
            string townId)
        {
            CaravanMapDisplayState state = Resolve(context, progress01);
            Assert.That(state.Mode, Is.EqualTo(mode));
            Assert.That(state.TownId, Is.EqualTo(townId));
        }

        private sealed class TestContext
        {
            public SaveData Save { get; }
            public CaravanSaveData Caravan { get; }
            public TradeProgressSaveData Progress { get; }
            public ISharedGameDataProvider Shared { get; }

            public TestContext(
                SaveData save,
                CaravanSaveData caravan,
                TradeProgressSaveData progress,
                ISharedGameDataProvider shared)
            {
                Save = save;
                Caravan = caravan;
                Progress = progress;
                Shared = shared;
            }
        }

        private sealed class FakeSharedGameDataProvider : ISharedGameDataProvider
        {
            private static readonly IReadOnlyList<string> EmptyIds = new string[0];

            public bool IsLoaded => true;
            public string Summary => string.Empty;
            public int TownCount => 2;
            public int MarketCount => 0;
            public int TradeItemCount => 0;
            public int WagonCount => 0;
            public int DraftAnimalCount => 0;
            public int RouteCount => 1;
            public IReadOnlyList<string> TownIds => EmptyIds;
            public IReadOnlyList<string> MarketIds => EmptyIds;
            public IReadOnlyList<string> TradeItemIds => EmptyIds;
            public IReadOnlyList<string> WagonIds => EmptyIds;
            public IReadOnlyList<string> DraftAnimalIds => EmptyIds;
            public IReadOnlyList<string> RouteIds => EmptyIds;

            public bool TryGetTown(string id, out SharedTownDefinition town)
            {
                town = null;
                return false;
            }

            public bool TryGetMarket(string id, out SharedMarketDefinition market)
            {
                market = null;
                return false;
            }

            public bool TryGetTradeItem(string id, out SharedTradeItemDefinition tradeItem)
            {
                tradeItem = null;
                return false;
            }

            public bool TryGetWagon(string id, out SharedWagonDefinition wagon)
            {
                wagon = null;
                return false;
            }

            public bool TryGetDraftAnimal(string id, out SharedDraftAnimalDefinition draftAnimal)
            {
                draftAnimal = null;
                return false;
            }

            public bool TryGetRoute(string id, out SharedRouteDefinition route)
            {
                route = id == RouteId
                    ? new SharedRouteDefinition
                    {
                        Id = RouteId,
                        FromTownId = OriginTownId,
                        ToTownId = DestinationTownId
                    }
                    : null;
                return route != null;
            }
        }
    }
}
#endif
