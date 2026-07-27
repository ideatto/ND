#if UNITY_EDITOR
using NUnit.Framework;

namespace ND.Framework.Editor
{
    public sealed class TradeRouteEventProcessorTests
    {
        [Test]
        public void Process_SplitAndSingleDistanceProduceSameCursorAndEvents()
        {
            var route = CreateWeatherRoute();
            var split = CreateTravelingCaravan();
            split.progress01 = 0.2f;
            var first = TradeRouteEventProcessor.Process(split, route, "trade-a", 10f, 1f);
            split.progress01 = 0.5f;
            var second = TradeRouteEventProcessor.Process(split, route, "trade-a", 10f, 1f);

            var single = CreateTravelingCaravan();
            single.progress01 = 0.5f;
            var whole = TradeRouteEventProcessor.Process(single, route, "trade-a", 10f, 1f);

            Assert.That(first.Succeeded && second.Succeeded && whole.Succeeded, Is.True);
            Assert.That(split.runEventChecksProcessed, Is.EqualTo(single.runEventChecksProcessed));
            Assert.That(split.runEventsOccurred, Is.EqualTo(single.runEventsOccurred));
            Assert.That(first.EventsOccurred + second.EventsOccurred, Is.EqualTo(whole.EventsOccurred));
        }

        [Test]
        public void Process_DoesNotRepeatCompletedChecks()
        {
            var caravan = CreateTravelingCaravan();
            caravan.progress01 = 0.5f;
            var route = CreateWeatherRoute();

            var first = TradeRouteEventProcessor.Process(caravan, route, "trade-b", 10f, 1f);
            var second = TradeRouteEventProcessor.Process(caravan, route, "trade-b", 10f, 1f);

            Assert.That(first.ChecksProcessed, Is.EqualTo(5));
            Assert.That(second.ChecksProcessed, Is.Zero);
            Assert.That(second.EventsOccurred, Is.Zero);
        }

        [Test]
        public void ProcessForced_DoesNotConsumeAutomaticCursor()
        {
            var caravan = CreateTravelingCaravan();
            caravan.runEventChecksProcessed = 3;

            var result = TradeRouteEventProcessor.ProcessForced(
                caravan, CreateWeatherRoute(), "trade-c", "weather");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(caravan.runEventChecksProcessed, Is.EqualTo(3));
            Assert.That(caravan.runEventsOccurred, Is.EqualTo(1));
        }

        private static CaravanData CreateTravelingCaravan()
        {
            return new CaravanData
            {
                caravanId = "caravan",
                state = JourneyState.Traveling,
                currentDistanceKm = 100f,
                progress01 = 0f
            };
        }

        private static SharedRouteDefinition CreateWeatherRoute()
        {
            return new SharedRouteDefinition
            {
                Id = "route",
                Distance = 100f,
                MaxEventCount = 10,
                BaseRiskLevel = 1f,
                Events = new[]
                {
                    new SharedRouteEventDefinition
                    {
                        Id = "weather",
                        EventType = RouteEvent.Weather
                    }
                }
            };
        }
    }
}
#endif
