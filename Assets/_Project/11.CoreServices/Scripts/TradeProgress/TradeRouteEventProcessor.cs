/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - 거리 구간별 route event를 호출 시점과 무관하게 결정적으로 판정하고 Caravan runtime에 적용한다.
 *
 * Main Features
 * - trade ID와 check index의 stable hash로 발생 여부와 이벤트 선택을 분리한다.
 * - 처리한 check cursor와 실제 발생 횟수를 runtime caravan에 기록한다.
 * - 강제 이벤트는 자동 check cursor를 소비하지 않는다.
 *
 * Important Notes
 * - Events 배열 순서가 바뀌면 같은 trade/check의 선택 결과도 바뀔 수 있다.
 * - 저장, registry 조회, rollback, framework event 발행은 coordinator 책임이다.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Framework
{
    public sealed class RouteEventOccurrence
    {
        public RouteEventOccurrence(int checkIndex, string eventId, bool isFatal)
        {
            CheckIndex = checkIndex;
            EventId = eventId ?? string.Empty;
            IsFatal = isFatal;
        }

        public int CheckIndex { get; }
        public string EventId { get; }
        public bool IsFatal { get; }
    }

    public sealed class RouteEventProcessResult
    {
        private RouteEventProcessResult(
            bool succeeded,
            bool changed,
            int checksProcessed,
            int eventsOccurred,
            bool becameFatal,
            string fatalReason,
            string failureReason,
            IReadOnlyList<RouteEventOccurrence> occurrences)
        {
            Succeeded = succeeded;
            Changed = changed;
            ChecksProcessed = checksProcessed;
            EventsOccurred = eventsOccurred;
            BecameFatal = becameFatal;
            FatalReason = fatalReason ?? string.Empty;
            FailureReason = failureReason ?? string.Empty;
            Occurrences = occurrences ?? Array.Empty<RouteEventOccurrence>();
        }

        public bool Succeeded { get; }
        public bool Changed { get; }
        public int ChecksProcessed { get; }
        public int EventsOccurred { get; }
        public bool BecameFatal { get; }
        public string FatalReason { get; }
        public string FailureReason { get; }
        public IReadOnlyList<RouteEventOccurrence> Occurrences { get; }

        public static RouteEventProcessResult Failure(string reason)
            => new RouteEventProcessResult(false, false, 0, 0, false, string.Empty, reason, null);

        public static RouteEventProcessResult Success(
            bool changed,
            int checksProcessed,
            int eventsOccurred,
            bool becameFatal,
            string fatalReason,
            IReadOnlyList<RouteEventOccurrence> occurrences)
            => new RouteEventProcessResult(
                true, changed, checksProcessed, eventsOccurred, becameFatal,
                fatalReason, string.Empty, occurrences);
    }

    public static class TradeRouteEventProcessor
    {
        public static RouteEventProcessResult Process(
            CaravanData caravan,
            SharedRouteDefinition route,
            string tradeId,
            float eventIntervalKm,
            float eventChancePerCheck)
        {
            var validationFailure = ValidateCommon(caravan, route, tradeId);
            if (validationFailure != null) return validationFailure;
            if (eventIntervalKm <= 0f || float.IsNaN(eventIntervalKm) || float.IsInfinity(eventIntervalKm))
                return RouteEventProcessResult.Failure("Event interval must be finite and greater than zero.");
            if (caravan.progress01 < 0f || caravan.currentDistanceKm < 0f)
                return RouteEventProcessResult.Failure("Current travel distance is negative.");
            if (caravan.runEventChecksProcessed < 0)
                return RouteEventProcessResult.Failure("Processed event check count is negative.");
            var tableFailure = ValidateEventTable(route.Events);
            if (tableFailure != null) return RouteEventProcessResult.Failure(tableFailure);

            var traveledDistanceKm = caravan.progress01 * caravan.currentDistanceKm;
            var completedCheckCount = Mathf.FloorToInt(traveledDistanceKm / eventIntervalKm);
            var chance = Mathf.Clamp01(
                float.IsNaN(eventChancePerCheck) ? 0f : eventChancePerCheck);
            var checksProcessed = 0;
            var eventsOccurred = 0;
            var occurrences = new List<RouteEventOccurrence>();
            var wasFatal = IsFatal(caravan);

            for (var checkIndex = caravan.runEventChecksProcessed;
                 checkIndex < completedCheckCount;
                 checkIndex++)
            {
                caravan.runEventChecksProcessed = checkIndex + 1;
                checksProcessed++;

                if (ToUnitFloat(StableHash(tradeId, checkIndex, "occur")) >= chance)
                    continue;

                var eventIndex = (int)(StableHash(tradeId, checkIndex, "select") % (uint)route.Events.Length);
                var routeEvent = route.Events[eventIndex];
                if (!TryApply(caravan, routeEvent, StableHash(tradeId, checkIndex, "apply"), out var failureReason))
                    return RouteEventProcessResult.Failure(failureReason);

                caravan.runEventsOccurred++;
                eventsOccurred++;
                var isFatal = IsFatal(caravan);
                occurrences.Add(new RouteEventOccurrence(checkIndex, routeEvent.Id, isFatal));
                if (isFatal) break;
            }

            return RouteEventProcessResult.Success(
                checksProcessed > 0 || eventsOccurred > 0,
                checksProcessed,
                eventsOccurred,
                !wasFatal && IsFatal(caravan),
                caravan.runFatalReason.ToString(),
                occurrences);
        }

        public static RouteEventProcessResult ProcessForced(
            CaravanData caravan,
            SharedRouteDefinition route,
            string tradeId,
            string eventId)
        {
            var validationFailure = ValidateCommon(caravan, route, tradeId);
            if (validationFailure != null) return validationFailure;
            if (string.IsNullOrWhiteSpace(eventId))
                return RouteEventProcessResult.Failure("Event ID is empty.");

            SharedRouteEventDefinition selected = null;
            for (var index = 0; index < route.Events.Length; index++)
            {
                var candidate = route.Events[index];
                if (candidate != null && string.Equals(candidate.Id, eventId.Trim(), StringComparison.Ordinal))
                {
                    selected = candidate;
                    break;
                }
            }
            if (selected == null) return RouteEventProcessResult.Failure("Event was not found on the route.");
            var selectedFailure = ValidateEvent(selected);
            if (selectedFailure != null) return RouteEventProcessResult.Failure(selectedFailure);

            var wasFatal = IsFatal(caravan);
            if (!TryApply(caravan, selected, StableHash(tradeId, -1, selected.Id), out var failureReason))
                return RouteEventProcessResult.Failure(failureReason);

            caravan.runEventsOccurred++;
            var isFatal = IsFatal(caravan);
            return RouteEventProcessResult.Success(
                true,
                0,
                1,
                !wasFatal && isFatal,
                caravan.runFatalReason.ToString(),
                new[] { new RouteEventOccurrence(-1, selected.Id, isFatal) });
        }

        private static RouteEventProcessResult ValidateCommon(
            CaravanData caravan,
            SharedRouteDefinition route,
            string tradeId)
        {
            if (caravan == null) return RouteEventProcessResult.Failure("Caravan is missing.");
            if (route == null) return RouteEventProcessResult.Failure("Route is missing.");
            if (string.IsNullOrWhiteSpace(tradeId)) return RouteEventProcessResult.Failure("Trade ID is empty.");
            if (route.Events == null || route.Events.Length == 0)
                return RouteEventProcessResult.Failure("Route event table is empty.");
            if (IsFatal(caravan)) return RouteEventProcessResult.Failure("Caravan is already fatal.");
            return null;
        }

        private static bool TryApply(
            CaravanData caravan,
            SharedRouteEventDefinition routeEvent,
            uint seed,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (routeEvent == null || string.IsNullOrWhiteSpace(routeEvent.Id))
            {
                failureReason = "Route event definition or ID is missing.";
                return false;
            }

            switch (routeEvent.EventType)
            {
                case RouteEvent.Combat:
                    var combat = JourneyRunner.ResolveBanditRaid(
                        caravan,
                        routeEvent.BanditCombatPower,
                        routeEvent.CargoLootRate,
                        routeEvent.FodderLootRate,
                        unchecked((int)seed));
                    if (!combat.processed)
                    {
                        failureReason = "Combat event application was rejected.";
                        return false;
                    }
                    return true;

                case RouteEvent.Lucky:
                case RouteEvent.Weather:
                    // 현재 Core에는 Lucky/Weather runtime 효과 API가 없다.
                    // 정의된 event 발생 자체만 기록하고 임의의 cargo/재화 효과를 만들지 않는다.
                    return true;

                default:
                    failureReason = $"Unsupported route event type: {routeEvent.EventType}.";
                    return false;
            }
        }

        private static string ValidateEventTable(SharedRouteEventDefinition[] events)
        {
            for (var index = 0; index < events.Length; index++)
            {
                var failure = ValidateEvent(events[index]);
                if (failure != null) return $"Route event at index {index} is invalid. {failure}";
            }
            return null;
        }

        private static string ValidateEvent(SharedRouteEventDefinition routeEvent)
        {
            if (routeEvent == null || string.IsNullOrWhiteSpace(routeEvent.Id))
                return "Route event definition or ID is missing.";
            if (routeEvent.EventType != RouteEvent.Combat
                && routeEvent.EventType != RouteEvent.Lucky
                && routeEvent.EventType != RouteEvent.Weather)
                return $"Unsupported route event type: {routeEvent.EventType}.";
            return null;
        }

        private static bool IsFatal(CaravanData caravan)
            => caravan.runFatalReason != JourneyFailureReason.None;

        private static uint StableHash(string tradeId, int checkIndex, string purpose)
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            var hash = offset;
            Append(ref hash, tradeId, prime);
            Append(ref hash, "|", prime);
            Append(ref hash, checkIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), prime);
            Append(ref hash, "|", prime);
            Append(ref hash, purpose, prime);
            return hash;
        }

        private static void Append(ref uint hash, string value, uint prime)
        {
            if (value == null) return;
            for (var index = 0; index < value.Length; index++)
            {
                hash ^= value[index];
                hash *= prime;
            }
        }

        private static float ToUnitFloat(uint value)
            => (float)(value / 4294967296d);
    }
}
