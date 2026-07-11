/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - CoreServices debug 기능을 service 형태로 묶어 scene controller와 debug bridge에 제공한다.
 * - time scale 변경, 무역 즉시 완료 요청, load completed 강제 발행, M2 월드 Force* API를 한 진입점으로 모은다.
 *
 * Main Features
 * - GameTimeService time scale 변경을 위임한다.
 * - CompleteTradeRequested 이벤트를 발행해 coordinator가 active trade를 즉시 완료하게 한다.
 * - LoadCompleted 이벤트를 강제로 발행한다.
 * - SharedGameData 로드 상태와 요약을 로그로 출력한다.
 * - ForceSeason / ForceDisaster로 WorldSaveData를 갱신하고 즉시 저장한다.
 * - ForceRouteEvent로 Traveling trade에 route event 1회 주입 hook을 남기고 이벤트를 발행한다.
 *
 * Usage for Team Members
 * - FrameworkRoot.DebugCommands를 통해 호출한다.
 * - runtime game flow 검증과 개발 편의 기능에 한정해 사용한다.
 *
 * Main Public APIs
 * - SetTimeScale(...): debug time scale을 적용한다.
 * - SetInGameTimeMultiplier(...): gameplay 인게임 시간 배율을 적용한다.
 * - PauseGameTime() / ResumeGameTime(): 인게임 시간 진행을 정지/재개한다.
 * - CompleteTradeImmediately(): active trade 즉시 완료 이벤트를 발행한다.
 * - ForceLoadCompleted(): 현재 저장 데이터로 load completed 이벤트를 발행한다.
 * - LogSharedGameDataSummary(): 공용 데이터 로드 요약을 출력한다.
 * - ForceSeason(...): WorldSaveData.currentSeasonId를 강제 변경하고 저장한다.
 * - ForceDisaster(...): WorldSaveData.currentDisasterId를 강제 변경하고 저장한다.
 * - ForceRouteEvent(...): Traveling trade에 route event 1회 주입 hook을 등록한다.
 * - TryConsumeForcedRouteEvent(...): 등록된 route event hook을 1회 소모한다.
 *
 * Important Notes
 * - 실제 상태 변경은 각 이벤트 구독자 또는 GameTimeService가 수행한다.
 * - ForceRouteEvent는 Core 로드/약탈 적용 API가 준비되기 전까지 Framework stub hook이다.
 * - Related Documentation: Docs/Personal_Documents/CSU/2026-07-11-framework-m2-planning-handoff.md
 */
namespace ND.Framework
{
    /// <summary>
    /// Framework debug 동작을 호출하기 위한 command service이다.
    /// </summary>
    public sealed class FrameworkDebugCommands
    {
        private readonly GameTimeService gameTimeService;

        private string pendingForcedRouteEventId = string.Empty;
        private string pendingForcedRouteEventTradeId = string.Empty;

        /// <summary>
        /// debug command service를 생성한다.
        /// </summary>
        /// <param name="gameTimeService">time scale 변경을 수행할 GameTimeService.</param>
        public FrameworkDebugCommands(GameTimeService gameTimeService)
        {
            this.gameTimeService = gameTimeService;
        }

        /// <summary>
        /// Unity time scale을 변경한다.
        /// </summary>
        /// <param name="scale">적용할 time scale.</param>
        public void SetTimeScale(float scale)
        {
            // time scale 보정과 Unity 적용은 GameTimeService에 위임한다.
            gameTimeService.SetTimeScale(scale);
        }

        /// <summary>
        /// gameplay 인게임 시간 배율을 변경한다.
        /// </summary>
        /// <param name="multiplier">현실 1초당 인게임 N초.</param>
        /// <returns>Editor 또는 Development Build에서 적용되면 true.</returns>
        public bool SetInGameTimeMultiplier(float multiplier)
        {
            return gameTimeService.TrySetInGameTimeMultiplier(multiplier);
        }

        /// <summary>
        /// config 기본값으로 인게임 시간 배율을 되돌린다.
        /// </summary>
        public void ResetInGameTimeMultiplier()
        {
            gameTimeService.ResetInGameTimeMultiplier();
        }

        /// <summary>
        /// 인게임 시간 진행을 일시정지한다.
        /// </summary>
        public void PauseGameTime()
        {
            gameTimeService.PauseGameTime();
        }

        /// <summary>
        /// 인게임 시간 진행을 재개한다.
        /// </summary>
        public void ResumeGameTime()
        {
            gameTimeService.ResumeGameTime();
        }

        /// <summary>
        /// 현재 active trade를 즉시 완료하도록 요청한다.
        /// </summary>
        public void CompleteTradeImmediately()
        {
            // coordinator가 이벤트를 받아 실제 progress와 settlement 생성을 처리한다.
            FrameworkEvents.RaiseCompleteTradeRequested();
        }

        /// <summary>
        /// 현재 저장 데이터로 LoadCompleted 이벤트를 강제로 발행한다.
        /// </summary>
        public void ForceLoadCompleted()
        {
            // debug UI 갱신을 위해 현재 root의 SaveData 참조를 그대로 전달한다.
            FrameworkEvents.RaiseLoadCompleted(FrameworkRoot.Instance.CurrentSaveData);
        }

        /// <summary>
        /// 현재 FrameworkRoot가 보유한 공용 데이터 요약을 로그로 출력한다.
        /// </summary>
        public void LogSharedGameDataSummary()
        {
            var provider = FrameworkRoot.Instance != null ? FrameworkRoot.Instance.SharedGameData : null;
            if (provider == null)
            {
                FrameworkLog.Warning("Shared game data summary is unavailable because provider is null.");
                return;
            }

            FrameworkLog.Info($"Shared game data summary: {provider.Summary}");
        }

        /// <summary>
        /// WorldSaveData의 현재 계절 ID를 강제 변경하고 즉시 저장한다.
        /// </summary>
        /// <param name="seasonId">적용할 계절 ID. 공백이면 실패한다.</param>
        /// <returns>
        /// WorldSaveData 갱신과 SaveService.Save가 모두 완료되면 true를 반환한다.
        /// FrameworkRoot/SaveData가 없거나 seasonId가 비어 있으면 false를 반환하며 저장 데이터는 변경되지 않는다.
        /// </returns>
        public bool ForceSeason(string seasonId)
        {
            if (string.IsNullOrWhiteSpace(seasonId))
            {
                FrameworkLog.Warning("ForceSeason skipped because seasonId is empty.");
                return false;
            }

            if (!TryGetWritableWorld(out var root, out var world, out var tradeId))
            {
                return false;
            }

            var normalizedSeasonId = seasonId.Trim();
            world.currentSeasonId = normalizedSeasonId;
            root.SaveService.Save(root.CurrentSaveData);

            FrameworkLog.Info(
                $"ForceSeason applied. TradeId: {tradeId}, SeasonId: {normalizedSeasonId}");
            return true;
        }

        /// <summary>
        /// WorldSaveData의 현재 재난 ID를 강제 변경하고 즉시 저장한다.
        /// </summary>
        /// <param name="disasterId">적용할 재난 ID. null은 빈 문자열로 정규화되어 재난 없음을 의미한다.</param>
        /// <returns>
        /// WorldSaveData 갱신과 SaveService.Save가 모두 완료되면 true를 반환한다.
        /// FrameworkRoot/SaveData가 없으면 false를 반환하며 저장 데이터는 변경되지 않는다.
        /// </returns>
        public bool ForceDisaster(string disasterId)
        {
            if (!TryGetWritableWorld(out var root, out var world, out var tradeId))
            {
                return false;
            }

            var normalizedDisasterId = disasterId == null ? string.Empty : disasterId.Trim();
            world.currentDisasterId = normalizedDisasterId;
            root.SaveService.Save(root.CurrentSaveData);

            FrameworkLog.Info(
                $"ForceDisaster applied. TradeId: {tradeId}, DisasterId: '{normalizedDisasterId}'");
            return true;
        }

        /// <summary>
        /// Traveling 상태의 active trade에 route event 1회 주입 hook을 등록한다.
        /// </summary>
        /// <param name="eventId">주입할 route event ID.</param>
        /// <returns>
        /// Traveling trade에 hook이 등록되고 RouteEventForced 이벤트가 발행되면 true를 반환한다.
        /// eventId가 비어 있거나 active trade가 Traveling이 아니면 false를 반환한다.
        /// Core 로드/약탈 적용은 이 메서드가 수행하지 않으며, 구독자 또는 TryConsumeForcedRouteEvent가 후속 처리한다.
        /// </returns>
        public bool ForceRouteEvent(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                FrameworkLog.Warning("ForceRouteEvent skipped because eventId is empty.");
                return false;
            }

            if (!TryGetActiveTravelingTrade(out var tradeId, out var routeId))
            {
                return false;
            }

            var normalizedEventId = eventId.Trim();
            pendingForcedRouteEventId = normalizedEventId;
            pendingForcedRouteEventTradeId = tradeId;

            FrameworkEvents.RaiseRouteEventForced(tradeId, normalizedEventId);
            FrameworkLog.Info(
                $"ForceRouteEvent inject hook registered. TradeId: {tradeId}, RouteId: {routeId}, EventId: {normalizedEventId}");
            return true;
        }

        /// <summary>
        /// ForceRouteEvent로 등록된 pending route event hook을 1회 소모한다.
        /// </summary>
        /// <param name="tradeId">소모 대상 trade ID. null 또는 공백이면 pending trade ID와 무관하게 소모한다.</param>
        /// <param name="eventId">소모된 route event ID.</param>
        /// <returns>
        /// pending hook이 있고 tradeId 조건이 일치하면 true를 반환하고 pending을 비운다.
        /// pending이 없거나 tradeId가 일치하지 않으면 false를 반환한다.
        /// </returns>
        public bool TryConsumeForcedRouteEvent(string tradeId, out string eventId)
        {
            eventId = string.Empty;
            if (string.IsNullOrEmpty(pendingForcedRouteEventId))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(tradeId)
                && pendingForcedRouteEventTradeId != tradeId.Trim())
            {
                return false;
            }

            eventId = pendingForcedRouteEventId;
            pendingForcedRouteEventId = string.Empty;
            pendingForcedRouteEventTradeId = string.Empty;
            return true;
        }

        private static bool TryGetWritableWorld(
            out FrameworkRoot root,
            out WorldSaveData world,
            out string tradeId)
        {
            root = FrameworkRoot.Instance;
            world = null;
            tradeId = "(none)";

            if (root == null || root.CurrentSaveData == null || root.SaveService == null)
            {
                FrameworkLog.Warning("World force command skipped because FrameworkRoot save services are not ready.");
                return false;
            }

            if (root.CurrentSaveData.world == null)
            {
                root.CurrentSaveData.world = new WorldSaveData();
            }

            world = root.CurrentSaveData.world;
            if (root.CurrentSaveData.tradeProgress != null
                && !string.IsNullOrEmpty(root.CurrentSaveData.tradeProgress.activeTradeId))
            {
                tradeId = root.CurrentSaveData.tradeProgress.activeTradeId;
            }

            return true;
        }

        private static bool TryGetActiveTravelingTrade(out string tradeId, out string routeId)
        {
            tradeId = string.Empty;
            routeId = string.Empty;

            var root = FrameworkRoot.Instance;
            if (root == null || root.CurrentSaveData == null || root.CurrentSaveData.tradeProgress == null)
            {
                FrameworkLog.Warning("ForceRouteEvent skipped because FrameworkRoot save data is not ready.");
                return false;
            }

            var progress = root.CurrentSaveData.tradeProgress;
            if (progress.state != TradeProgressState.Traveling)
            {
                FrameworkLog.Warning(
                    $"ForceRouteEvent skipped because trade state is {progress.state}. Traveling is required.");
                return false;
            }

            if (string.IsNullOrEmpty(progress.activeTradeId))
            {
                FrameworkLog.Warning("ForceRouteEvent skipped because activeTradeId is empty.");
                return false;
            }

            tradeId = progress.activeTradeId;
            routeId = progress.activeRouteId ?? string.Empty;
            return true;
        }
    }
}
