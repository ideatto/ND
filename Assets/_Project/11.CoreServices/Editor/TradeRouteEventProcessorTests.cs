#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ND.Framework.Editor
{
    public sealed class TradeRouteEventProcessorTests
    {
        [Test]
        public void Process_SplitAndSingleDistanceProduceSameCursorAndEvents()
        {
            var route = CreateLuckyRoute();
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
            var route = CreateLuckyRoute();

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
                caravan, CreateLuckyRoute(), "trade-c", "lucky");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(caravan.runEventChecksProcessed, Is.EqualTo(3));
            Assert.That(caravan.runEventsOccurred, Is.EqualTo(1));
        }

        [Test]
        public void Process_CombatSafePassReportsVictory()
        {
            var caravan = CreateTravelingCaravan();
            caravan.progress01 = 0.1f;
            caravan.baseSafetyChancePercent = 100f;

            var result = TradeRouteEventProcessor.Process(
                caravan, CreateCombatRoute(), "trade-victory", 10f, 1f);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Occurrences, Has.Count.EqualTo(1));
            Assert.That(result.Occurrences[0].CombatVictory, Is.True);
            Assert.That(result.Occurrences[0].IsFatal, Is.False);
        }

        [Test]
        public void Process_CombatLossReportsDefeatWithoutRequiringFatalState()
        {
            var caravan = CreateTravelingCaravan();
            caravan.progress01 = 0.1f;
            caravan.baseSafetyChancePercent = 0f;

            var result = TradeRouteEventProcessor.Process(
                caravan, CreateCombatRoute(), "trade-defeat", 10f, 1f);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Occurrences, Has.Count.EqualTo(1));
            Assert.That(result.Occurrences[0].CombatVictory, Is.False);
            Assert.That(result.Occurrences[0].IsFatal, Is.False);
            Assert.That(caravan.runFatalReason, Is.EqualTo(JourneyFailureReason.None));
        }

        [Test]
        public void Process_ZeroBanditMultiplierSuppressesCombatOnly()
        {
            var combatCaravan = CreateTravelingCaravan();
            combatCaravan.progress01 = 0.1f;
            var combat = TradeRouteEventProcessor.Process(
                combatCaravan,
                CreateCombatRoute(),
                "trade-suppressed",
                10f,
                1f,
                0f);

            var luckyCaravan = CreateTravelingCaravan();
            luckyCaravan.progress01 = 0.1f;
            var lucky = TradeRouteEventProcessor.Process(
                luckyCaravan,
                CreateLuckyRoute(),
                "trade-lucky",
                10f,
                1f,
                0f);

            Assert.That(combat.Succeeded, Is.True);
            Assert.That(combat.EventsOccurred, Is.Zero);
            Assert.That(lucky.Succeeded, Is.True);
            Assert.That(lucky.EventsOccurred, Is.EqualTo(1));
        }

        [Test]
        public void QuestBanditModifier_AppliesToBothConnectedRouteDirectionsUntilExpiry()
        {
            var now = new System.DateTime(
                2026, 7, 30, 12, 0, 0, System.DateTimeKind.Utc);
            var world = new WorldSaveData();
            world.townRouteBanditModifiers.Add(
                new TownRouteBanditModifierSaveData
                {
                    sourceQuestId = "quest-a",
                    townId = "town-a",
                    encounterMultiplier = 0.5f,
                    expiresUtcTicks = now.AddHours(1).Ticks
                });
            var outbound = CreateCombatRoute();
            outbound.FromTownId = "town-a";
            outbound.ToTownId = "town-b";
            var inbound = CreateCombatRoute();
            inbound.FromTownId = "town-b";
            inbound.ToTownId = "town-a";

            Assert.That(
                QuestRuntimeService.ResolveBanditEncounterMultiplier(
                    world, outbound, now),
                Is.EqualTo(0.5f));
            Assert.That(
                QuestRuntimeService.ResolveBanditEncounterMultiplier(
                    world, inbound, now),
                Is.EqualTo(0.5f));
            Assert.That(
                QuestRuntimeService.ResolveBanditEncounterMultiplier(
                    world, outbound, now.AddHours(2)),
                Is.EqualTo(1f));
        }

        [Test]
        public void ProcessForced_CombatReportsActualOutcome()
        {
            var caravan = CreateTravelingCaravan();
            caravan.baseSafetyChancePercent = 0f;

            var result = TradeRouteEventProcessor.ProcessForced(
                caravan, CreateCombatRoute(), "trade-forced-defeat", "combat");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Occurrences.Count, Is.EqualTo(1));
            Assert.That(result.Occurrences[0].CombatVictory, Is.False);
        }

        [Test]
        public void RouteData_NormalizePreservesConfiguredChanceInsteadOfCombatPower()
        {
            var route = ScriptableObject.CreateInstance<global::RouteData>();
            try
            {
                var serialized = new SerializedObject(route);
                serialized.FindProperty("baseRiskLevel").floatValue = 0.35f;
                var events = serialized.FindProperty("routeEvents");
                events.arraySize = 1;
                var routeEvent = events.GetArrayElementAtIndex(0);
                routeEvent.FindPropertyRelative("eventType").enumValueIndex =
                    (int)global::RouteEvent.Combat;
                routeEvent.FindPropertyRelative("eventValue").intValue = 90;
                routeEvent.FindPropertyRelative("banditCombatPower").intValue = 120;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                MethodInfo normalize = typeof(global::RouteData).GetMethod(
                    "NormalizeRouteEvents",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(normalize, Is.Not.Null);
                normalize.Invoke(route, null);

                Assert.That(route.BaseRiskLevel, Is.EqualTo(0.35f).Within(0.0001f));
                Assert.That(route.RouteEvents[0].BanditCombatPower, Is.EqualTo(120));
            }
            finally
            {
                Object.DestroyImmediate(route);
            }
        }

        [Test]
        public void RouteData_BaseRiskLevelClampsToProbabilityRange()
        {
            var route = ScriptableObject.CreateInstance<global::RouteData>();
            try
            {
                var serialized = new SerializedObject(route);
                serialized.FindProperty("baseRiskLevel").floatValue = 2f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(route.BaseRiskLevel, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(route);
            }
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

        private static SharedRouteDefinition CreateLuckyRoute()
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
                        Id = "lucky",
                        EventType = RouteEvent.Lucky
                    }
                }
            };
        }

        private static SharedRouteDefinition CreateCombatRoute()
        {
            return new SharedRouteDefinition
            {
                Id = "combat-route",
                Distance = 100f,
                MaxEventCount = 10,
                BaseRiskLevel = 1f,
                Events = new[]
                {
                    new SharedRouteEventDefinition
                    {
                        Id = "combat",
                        EventType = RouteEvent.Combat,
                        BanditCombatPower = 100,
                        CargoLootRate = 0.25f,
                        FodderLootRate = 0.25f
                    }
                }
            };
        }
    }
}
#endif
