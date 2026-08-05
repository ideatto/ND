using System;
using UnityEngine;

namespace ND.Framework
{
    public enum CaravanMapDisplayMode
    {
        Route,
        Town
    }

    public enum CaravanMapDisplayIssue
    {
        None,
        InvalidIdentity,
        MissingRoute,
        MissingPendingSettlement,
        AmbiguousPendingSettlement,
        InvalidSettlementGrade,
        MissingPreparationCommit,
        AmbiguousPreparationCommit,
        UnresolvedDestination
    }

    /// <summary>캐러밴 지도 표시가 경로 진행 위치인지 정확한 마을 위치인지 나타내는 읽기 전용 결과이다.</summary>
    public readonly struct CaravanMapDisplayState
    {
        public string CaravanId { get; }
        public string TradeId { get; }
        public CaravanMapDisplayMode Mode { get; }
        public string RouteId { get; }
        public float Progress01 { get; }
        public string TownId { get; }
        public CaravanMapDisplayIssue Issue { get; }

        private CaravanMapDisplayState(
            string caravanId,
            string tradeId,
            CaravanMapDisplayMode mode,
            string routeId,
            float progress01,
            string townId,
            CaravanMapDisplayIssue issue)
        {
            CaravanId = caravanId ?? string.Empty;
            TradeId = tradeId ?? string.Empty;
            Mode = mode;
            RouteId = routeId ?? string.Empty;
            Progress01 = Mathf.Clamp01(progress01);
            TownId = townId ?? string.Empty;
            Issue = issue;
        }

        public static CaravanMapDisplayState OnRoute(
            string caravanId,
            string tradeId,
            string routeId,
            float progress01)
        {
            return new CaravanMapDisplayState(
                caravanId, tradeId, CaravanMapDisplayMode.Route, routeId,
                progress01, string.Empty, CaravanMapDisplayIssue.None);
        }

        public static CaravanMapDisplayState AtTown(
            string caravanId,
            string tradeId,
            string townId,
            CaravanMapDisplayIssue issue = CaravanMapDisplayIssue.None)
        {
            return new CaravanMapDisplayState(
                caravanId, tradeId, CaravanMapDisplayMode.Town, string.Empty,
                0f, townId, issue);
        }
    }

    /// <summary>저장된 무역 소유 identity와 정산 등급을 지도 표시 의미로 변환한다.</summary>
    public static class CaravanMapDisplayResolver
    {
        /// <summary>
        /// 전달받은 캐러밴과 그 캐러밴의 정확한 진행 항목만 사용하며 저장 데이터는 변경하지 않는다.
        /// </summary>
        public static bool TryResolve(
            SaveData saveData,
            ISharedGameDataProvider sharedGameData,
            CaravanSaveData caravan,
            TradeProgressSaveData progress,
            float travelingProgress01,
            out CaravanMapDisplayState state)
        {
            state = default;
            if (caravan == null || string.IsNullOrEmpty(caravan.caravanId))
                return false;

            if (progress == null
                || progress.state == TradeProgressState.None
                || progress.state == TradeProgressState.Preparing
                || progress.state == TradeProgressState.Completed
                || progress.state == TradeProgressState.Failed)
            {
                return TryUseCurrentTown(caravan, string.Empty, CaravanMapDisplayIssue.None, out state);
            }

            if (!string.Equals(progress.caravanId, caravan.caravanId, StringComparison.Ordinal))
            {
                return TryUseCurrentTown(
                    caravan, progress.activeTradeId, CaravanMapDisplayIssue.InvalidIdentity, out state);
            }

            if (progress.state == TradeProgressState.Traveling)
            {
                if (string.IsNullOrEmpty(progress.activeRouteId)
                    || sharedGameData == null
                    || !sharedGameData.TryGetRoute(progress.activeRouteId, out _))
                {
                    return TryUseCurrentTown(
                        caravan, progress.activeTradeId, CaravanMapDisplayIssue.MissingRoute, out state);
                }

                state = CaravanMapDisplayState.OnRoute(
                    caravan.caravanId,
                    progress.activeTradeId,
                    progress.activeRouteId,
                    travelingProgress01);
                return true;
            }

            // [Selling] 목적지 마을에 도착해 판매 중인 상태다. currentTownId는 아직 출발지(거점)이므로
            // 그걸 쓰면 마커가 거점으로 튄다. 정산(SettlementPending)과 동일하게 commit/route로 목적지 마을을 구해 표시한다.
            if (progress.state == TradeProgressState.Selling)
            {
                if (string.IsNullOrEmpty(progress.activeTradeId))
                    return TryUseCurrentTown(
                        caravan, progress.activeTradeId, CaravanMapDisplayIssue.InvalidIdentity, out state);

                TradePreparationCommitSaveData sellingCommit =
                    FindCommit(saveData, caravan.caravanId, progress.activeTradeId, out bool sellingCommitAmbiguous);

                string sellingDestination = sellingCommit != null ? sellingCommit.destinationTownId : string.Empty;
                if (string.IsNullOrEmpty(sellingDestination))
                {
                    string sellingRouteId = sellingCommit != null && !string.IsNullOrEmpty(sellingCommit.routeId)
                        ? sellingCommit.routeId
                        : progress.activeRouteId;
                    string sellingOrigin = sellingCommit != null && !string.IsNullOrEmpty(sellingCommit.currentTownId)
                        ? sellingCommit.currentTownId
                        : caravan.currentTownId;
                    sellingDestination = ResolveOppositeTown(sharedGameData, sellingRouteId, sellingOrigin);
                }

                if (!string.IsNullOrEmpty(sellingDestination))
                    return TryUseTown(
                        caravan.caravanId, progress.activeTradeId, sellingDestination,
                        sellingCommitAmbiguous ? CaravanMapDisplayIssue.AmbiguousPreparationCommit : CaravanMapDisplayIssue.None,
                        out state);

                // 목적지를 못 구하면 최소한 경로 끝(진행 100%)에 표시해 거점으로 튀지 않게 한다.
                if (!string.IsNullOrEmpty(progress.activeRouteId)
                    && sharedGameData != null
                    && sharedGameData.TryGetRoute(progress.activeRouteId, out _))
                {
                    state = CaravanMapDisplayState.OnRoute(
                        caravan.caravanId, progress.activeTradeId, progress.activeRouteId, 1f);
                    return true;
                }

                return TryUseCurrentTown(
                    caravan, progress.activeTradeId, CaravanMapDisplayIssue.UnresolvedDestination, out state);
            }

            if (progress.state != TradeProgressState.SettlementPending
                || string.IsNullOrEmpty(progress.activeTradeId))
            {
                return TryUseCurrentTown(
                    caravan, progress.activeTradeId, CaravanMapDisplayIssue.InvalidIdentity, out state);
            }

            PendingSettlementSaveData pending = FindPending(
                saveData, caravan.caravanId, progress.activeTradeId, out bool pendingAmbiguous);
            if (pending == null || !pending.hasResult)
            {
                return TryUseCurrentTown(
                    caravan,
                    progress.activeTradeId,
                    pendingAmbiguous
                        ? CaravanMapDisplayIssue.AmbiguousPendingSettlement
                        : CaravanMapDisplayIssue.MissingPendingSettlement,
                    out state);
            }

            TradePreparationCommitSaveData commit = FindCommit(
                saveData, caravan.caravanId, progress.activeTradeId, out bool commitAmbiguous);
            if (commitAmbiguous)
            {
                return TryUseCurrentTown(
                    caravan,
                    progress.activeTradeId,
                    CaravanMapDisplayIssue.AmbiguousPreparationCommit,
                    out state);
            }

            if (pending.grade == JourneyResultGrade.Failed)
            {
                string originTownId = commit != null && !string.IsNullOrEmpty(commit.currentTownId)
                    ? commit.currentTownId
                    : caravan.currentTownId;
                return TryUseTown(
                    caravan.caravanId,
                    progress.activeTradeId,
                    originTownId,
                    commit == null
                        ? CaravanMapDisplayIssue.MissingPreparationCommit
                        : CaravanMapDisplayIssue.None,
                    out state);
            }

            if (pending.grade != JourneyResultGrade.Success
                && pending.grade != JourneyResultGrade.PartialSuccess)
            {
                return TryUseCurrentTown(
                    caravan,
                    progress.activeTradeId,
                    CaravanMapDisplayIssue.InvalidSettlementGrade,
                    out state);
            }

            string destinationTownId = commit != null ? commit.destinationTownId : string.Empty;
            if (string.IsNullOrEmpty(destinationTownId))
            {
                string routeId = commit != null && !string.IsNullOrEmpty(commit.routeId)
                    ? commit.routeId
                    : pending.routeId;
                string originTownId = commit != null && !string.IsNullOrEmpty(commit.currentTownId)
                    ? commit.currentTownId
                    : caravan.currentTownId;
                destinationTownId = ResolveOppositeTown(sharedGameData, routeId, originTownId);
            }

            if (!string.IsNullOrEmpty(destinationTownId))
            {
                return TryUseTown(
                    caravan.caravanId,
                    progress.activeTradeId,
                    destinationTownId,
                    CaravanMapDisplayIssue.None,
                    out state);
            }

            return TryUseCurrentTown(
                caravan,
                progress.activeTradeId,
                commit == null
                    ? CaravanMapDisplayIssue.MissingPreparationCommit
                    : CaravanMapDisplayIssue.UnresolvedDestination,
                out state);
        }

        private static PendingSettlementSaveData FindPending(
            SaveData saveData,
            string caravanId,
            string tradeId,
            out bool ambiguous)
        {
            ambiguous = false;
            PendingSettlementSaveData match = null;
            if (saveData?.pendingSettlements == null)
                return null;

            for (int i = 0; i < saveData.pendingSettlements.Count; i++)
            {
                PendingSettlementSaveData candidate = saveData.pendingSettlements[i];
                if (candidate == null
                    || !string.Equals(candidate.caravanId, caravanId, StringComparison.Ordinal)
                    || !string.Equals(candidate.tradeId, tradeId, StringComparison.Ordinal))
                    continue;

                if (match != null)
                {
                    ambiguous = true;
                    return null;
                }

                match = candidate;
            }

            return match;
        }

        private static TradePreparationCommitSaveData FindCommit(
            SaveData saveData,
            string caravanId,
            string tradeId,
            out bool ambiguous)
        {
            ambiguous = false;
            TradePreparationCommitSaveData match = null;
            if (saveData?.tradePreparationCommits == null)
                return null;

            for (int i = 0; i < saveData.tradePreparationCommits.Count; i++)
            {
                TradePreparationCommitSaveData candidate = saveData.tradePreparationCommits[i];
                if (candidate == null
                    || !candidate.hasCommit
                    || !string.Equals(candidate.caravanId, caravanId, StringComparison.Ordinal)
                    || !string.Equals(candidate.tradeId, tradeId, StringComparison.Ordinal))
                    continue;

                if (match != null)
                {
                    ambiguous = true;
                    return null;
                }

                match = candidate;
            }

            return match;
        }

        private static string ResolveOppositeTown(
            ISharedGameDataProvider sharedGameData,
            string routeId,
            string originTownId)
        {
            if (sharedGameData == null
                || string.IsNullOrEmpty(routeId)
                || string.IsNullOrEmpty(originTownId)
                || !sharedGameData.TryGetRoute(routeId, out SharedRouteDefinition route))
                return string.Empty;

            if (string.Equals(route.FromTownId, originTownId, StringComparison.Ordinal))
                return route.ToTownId;
            if (string.Equals(route.ToTownId, originTownId, StringComparison.Ordinal))
                return route.FromTownId;
            return string.Empty;
        }

        private static bool TryUseCurrentTown(
            CaravanSaveData caravan,
            string tradeId,
            CaravanMapDisplayIssue issue,
            out CaravanMapDisplayState state)
        {
            return TryUseTown(caravan.caravanId, tradeId, caravan.currentTownId, issue, out state);
        }

        private static bool TryUseTown(
            string caravanId,
            string tradeId,
            string townId,
            CaravanMapDisplayIssue issue,
            out CaravanMapDisplayState state)
        {
            state = default;
            if (string.IsNullOrEmpty(townId))
                return false;

            state = CaravanMapDisplayState.AtTown(caravanId, tradeId, townId, issue);
            return true;
        }
    }
}
