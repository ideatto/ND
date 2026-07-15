/*
 * Technical Ownership
 * - Responsible Area: UI / World Map
 *
 * Script Purpose
 * - 루트 표시 상태(unlock / completed / active)를 routeId 기준으로 해석한다.
 * - 1차에서는 SaveData의 unlocked/completed 목록과 active route만 사용한다.
 *
 * Main Features
 * - GetDisplayState(routeId)로 표시용 플래그를 반환한다.
 * - 이후 RouteRuntimeState(blocked, multiplier)를 같은 해석 지점에 확장할 수 있다.
 *
 * Important Notes
 * - 게임플레이 거리·시간·위험 계산을 수행하지 않는다.
 * - Related Documentation: Docs/Guide/Framework_World_Map_API_Guide.md
 */
using System.Collections.Generic;
using FrameworkWorldSaveData = ND.Framework.WorldSaveData;

namespace ND.UI.WorldMap
{
    /// <summary>
    /// 월드맵 루트 한 줄의 표시 상태이다.
    /// </summary>
    public readonly struct RouteMapDisplayState
    {
        public bool IsUnlocked { get; }
        public bool IsCompleted { get; }
        public bool IsActive { get; }

        public RouteMapDisplayState(bool isUnlocked, bool isCompleted, bool isActive)
        {
            IsUnlocked = isUnlocked;
            IsCompleted = isCompleted;
            IsActive = isActive;
        }
    }

    /// <summary>
    /// Save unlock 목록과 active route를 루트 표시 상태로 변환한다.
    /// </summary>
    public sealed class RouteMapPresentationResolver
    {
        private readonly HashSet<string> unlockedRouteIds = new HashSet<string>();
        private readonly HashSet<string> completedRouteIds = new HashSet<string>();
        private string activeRouteId = string.Empty;

        /// <summary>
        /// Framework WorldSaveData unlock/completed 목록과 현재 active route를 반영한다.
        /// </summary>
        /// <param name="world">월드 저장 데이터. null이면 목록을 비운다.</param>
        /// <param name="activeRouteIdValue">현재 진행 중인 route ID.</param>
        public void Refresh(FrameworkWorldSaveData world, string activeRouteIdValue)
        {
            unlockedRouteIds.Clear();
            completedRouteIds.Clear();
            activeRouteId = activeRouteIdValue ?? string.Empty;

            if (world?.unlockedRouteIds != null)
            {
                for (var i = 0; i < world.unlockedRouteIds.Count; i++)
                {
                    var id = world.unlockedRouteIds[i];
                    if (!string.IsNullOrEmpty(id))
                    {
                        unlockedRouteIds.Add(id);
                    }
                }
            }

            if (world?.completedRouteIds != null)
            {
                for (var i = 0; i < world.completedRouteIds.Count; i++)
                {
                    var id = world.completedRouteIds[i];
                    if (!string.IsNullOrEmpty(id))
                    {
                        completedRouteIds.Add(id);
                    }
                }
            }
        }

        /// <summary>
        /// routeId에 대한 표시 상태를 반환한다.
        /// </summary>
        /// <remarks>
        /// unlocked 목록에 없어도 SharedRouteDefinition.UnlockedByDefault는 Presenter가 별도로 반영할 수 있다.
        /// 추후 isBlocked / dangerMultiplier / speedMultiplier는 이 메서드 결과에 필드를 확장하는 방식으로 추가한다.
        /// </remarks>
        public RouteMapDisplayState GetDisplayState(string routeId)
        {
            if (string.IsNullOrEmpty(routeId))
            {
                return new RouteMapDisplayState(false, false, false);
            }

            var unlocked = unlockedRouteIds.Contains(routeId);
            var completed = completedRouteIds.Contains(routeId);
            var active = string.Equals(activeRouteId, routeId);
            return new RouteMapDisplayState(unlocked, completed, active);
        }
    }
}
