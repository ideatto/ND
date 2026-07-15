/*
 * Technical Ownership
 * - Responsible Area: UI / World Map
 *
 * Script Purpose
 * - 월드맵 씬의 마을/루트 뷰와 Framework 진행 스냅샷을 연결한다.
 * - 캐러밴 위치, 활성 루트 하이라이트, 진행률(%) 표시를 갱신한다.
 *
 * Main Features
 * - townId / routeId 룩업 구축 및 중복·고아 ID 검증.
 * - TradeProgressCoordinator.TryGetMapProgress로 진행률을 읽는다.
 * - SharedGameData + WorldSaveData unlock으로 표시 상태를 갱신한다.
 * - TownClicked / RouteClicked 이벤트를 노출한다(무역 준비 UI는 후속 연결).
 *
 * Important Notes
 * - 무역 출발·정산·Save 쓰기를 직접 수행하지 않는다.
 * - Sandbox 전역 SaveData와 구분하기 위해 Framework 타입 alias를 사용한다.
 * - Shared가 미로드이거나 해당 ID가 없으면 마을/루트 모두 표시한다(WorldMapTest 등).
 * - Related Documentation: Docs/Guide/Framework_World_Map_API_Guide.md
 */
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using ND.Framework;
using FrameworkSaveData = ND.Framework.SaveData;
using FrameworkTradeProgressState = ND.Framework.TradeProgressState;

namespace ND.UI.WorldMap
{
    /// <summary>
    /// 월드맵 표시와 Framework 읽기 API를 연결하는 Presenter이다.
    /// </summary>
    public sealed class WorldMapPresenter : MonoBehaviour
    {
        [Header("Scene Bindings")]
        [SerializeField] private TownWorldView[] townViews = Array.Empty<TownWorldView>();
        [SerializeField] private RouteVisual[] routeVisuals = Array.Empty<RouteVisual>();
        [SerializeField] private CaravanMapMarker caravanMarker;
        [SerializeField] private TMP_Text progressPercentLabel;
        [SerializeField] private TMP_Text riskLabel;

        [Header("Behaviour")]
        [SerializeField] private bool autoCollectChildren = true;
        [SerializeField] private bool refreshEveryFrameWhileTraveling = true;

        private readonly Dictionary<string, TownWorldView> townsById = new Dictionary<string, TownWorldView>();
        private readonly Dictionary<string, RouteVisual> routesById = new Dictionary<string, RouteVisual>();
        private readonly RouteMapPresentationResolver presentationResolver = new RouteMapPresentationResolver();

        private string selectedTownId = string.Empty;
        private bool lookupsBuilt;

        /// <summary>
        /// 마을이 클릭되었을 때 발생한다. 인자는 townId이다.
        /// </summary>
        /// <remarks>
        /// 무역 준비 UI 연결은 구독 측에서 수행한다. Presenter는 선택 표시만 갱신한다.
        /// </remarks>
        public event Action<string> TownClicked;

        /// <summary>
        /// 루트가 선택되었을 때 발생한다. 인자는 routeId이다.
        /// </summary>
        public event Action<string> RouteClicked;

        private void OnEnable()
        {
            FrameworkEvents.InGameScreenChanged += HandleScreenChanged;
            FrameworkEvents.LoadCompleted += HandleLoadCompleted;
            BuildLookups();
            SubscribeTownClicks(true);
            RefreshAll();
        }

        private void OnDisable()
        {
            FrameworkEvents.InGameScreenChanged -= HandleScreenChanged;
            FrameworkEvents.LoadCompleted -= HandleLoadCompleted;
            SubscribeTownClicks(false);
        }

        private void Update()
        {
            if (!refreshEveryFrameWhileTraveling)
            {
                return;
            }

            var root = FrameworkRoot.Instance;
            if (root?.TradeProgressCoordinator == null)
            {
                return;
            }

            if (!root.TradeProgressCoordinator.TryGetMapProgress(out var snapshot))
            {
                return;
            }

            if (snapshot.State == FrameworkTradeProgressState.Traveling)
            {
                ApplyProgress(snapshot);
            }
        }

        /// <summary>
        /// 룩업과 표시 상태를 즉시 다시 구성한다.
        /// </summary>
        public void RefreshAll()
        {
            BuildLookups();
            RefreshPresentationFromSave();
            RefreshProgressFromCoordinator();
        }

        /// <summary>
        /// 빌더가 씬 참조를 주입할 때 사용한다.
        /// </summary>
        public void Configure(
            TownWorldView[] towns,
            RouteVisual[] routes,
            CaravanMapMarker marker,
            TMP_Text progressLabel,
            TMP_Text riskText)
        {
            townViews = towns ?? Array.Empty<TownWorldView>();
            routeVisuals = routes ?? Array.Empty<RouteVisual>();
            caravanMarker = marker;
            progressPercentLabel = progressLabel;
            riskLabel = riskText;
            lookupsBuilt = false;
            BuildLookups();
        }

        private void HandleScreenChanged(InGameScreenState _)
        {
            RefreshAll();
        }

        private void HandleLoadCompleted(FrameworkSaveData _)
        {
            RefreshAll();
        }

        private void BuildLookups()
        {
            if (autoCollectChildren)
            {
                townViews = GetComponentsInChildren<TownWorldView>(true);
                routeVisuals = GetComponentsInChildren<RouteVisual>(true);
                if (caravanMarker == null)
                {
                    caravanMarker = GetComponentInChildren<CaravanMapMarker>(true);
                }
            }

            townsById.Clear();
            routesById.Clear();

            if (townViews != null)
            {
                for (var i = 0; i < townViews.Length; i++)
                {
                    var view = townViews[i];
                    if (view == null || string.IsNullOrEmpty(view.TownId))
                    {
                        continue;
                    }

                    if (townsById.ContainsKey(view.TownId))
                    {
                        Debug.LogError($"[WorldMap] Duplicate townId '{view.TownId}' on '{view.name}'.", view);
                        continue;
                    }

                    townsById.Add(view.TownId, view);
                }
            }

            if (routeVisuals != null)
            {
                for (var i = 0; i < routeVisuals.Length; i++)
                {
                    var visual = routeVisuals[i];
                    if (visual == null || string.IsNullOrEmpty(visual.RouteId))
                    {
                        continue;
                    }

                    if (routesById.ContainsKey(visual.RouteId))
                    {
                        Debug.LogError($"[WorldMap] Duplicate routeId '{visual.RouteId}' on '{visual.name}'.", visual);
                        continue;
                    }

                    routesById.Add(visual.RouteId, visual);
                    visual.RebuildLineIfNeeded(force: true);
                }
            }

            ValidateAgainstSharedData();
            lookupsBuilt = true;
        }

        private void ValidateAgainstSharedData()
        {
            var shared = FrameworkRoot.Instance != null ? FrameworkRoot.Instance.SharedGameData : null;
            if (shared == null || !shared.IsLoaded)
            {
                return;
            }

            foreach (var pair in routesById)
            {
                SharedRouteDefinition route;
                if (!shared.TryGetRoute(pair.Key, out route))
                {
                    Debug.LogWarning(
                        $"[WorldMap] RouteVisual routeId '{pair.Key}' has no matching SharedRouteDefinition.",
                        pair.Value);
                }
            }
        }

        private void SubscribeTownClicks(bool subscribe)
        {
            if (townViews == null)
            {
                return;
            }

            for (var i = 0; i < townViews.Length; i++)
            {
                var view = townViews[i];
                if (view == null)
                {
                    continue;
                }

                view.TownClicked -= HandleTownClicked;
                if (subscribe)
                {
                    view.TownClicked += HandleTownClicked;
                }
            }
        }

        private void HandleTownClicked(string townId)
        {
            selectedTownId = townId ?? string.Empty;
            RefreshTownVisualStates();
            TownClicked?.Invoke(selectedTownId);
        }

        private void RefreshPresentationFromSave()
        {
            var root = FrameworkRoot.Instance;
            FrameworkSaveData save = root != null ? root.CurrentSaveData : null;
            var activeRouteId = string.Empty;
            if (root?.TradeProgressCoordinator != null
                && root.TradeProgressCoordinator.TryGetMapProgress(out var snapshot))
            {
                activeRouteId = snapshot.ActiveRouteId;
            }
            else if (save?.tradeProgress != null)
            {
                activeRouteId = save.tradeProgress.activeRouteId;
            }

            presentationResolver.Refresh(save?.world, activeRouteId);
            RefreshTownVisualStates();
            RefreshRouteVisualStates(activeRouteId);
        }

        private void RefreshTownVisualStates()
        {
            var root = FrameworkRoot.Instance;
            FrameworkSaveData save = root != null ? root.CurrentSaveData : null;
            var shared = root != null ? root.SharedGameData : null;

            foreach (var pair in townsById)
            {
                var unlocked = IsTownUnlocked(pair.Key, save, shared);
                var selected = string.Equals(selectedTownId, pair.Key);
                pair.Value.SetPresentationState(unlocked, selected);
            }
        }

        private void RefreshRouteVisualStates(string activeRouteId)
        {
            var root = FrameworkRoot.Instance;
            var shared = root != null ? root.SharedGameData : null;

            foreach (var pair in routesById)
            {
                var display = presentationResolver.GetDisplayState(pair.Key);
                var isActive = display.IsActive || string.Equals(activeRouteId, pair.Key);
                // Shared 미로드 시에도 마을과 같이 표시한다. 잠금은 Shared 로드 후에만 적용한다.
                var unlocked = IsRouteUnlocked(pair.Key, display, shared) || isActive;
                pair.Value.SetActiveVisual(isActive);
                pair.Value.gameObject.SetActive(unlocked);
            }
        }

        private static bool IsTownUnlocked(string townId, FrameworkSaveData save, ISharedGameDataProvider shared)
        {
            if (save?.world?.unlockedTownIds != null)
            {
                for (var i = 0; i < save.world.unlockedTownIds.Count; i++)
                {
                    if (string.Equals(save.world.unlockedTownIds[i], townId))
                    {
                        return true;
                    }
                }
            }

            SharedTownDefinition town;
            if (shared != null && shared.TryGetTown(townId, out town))
            {
                return town.UnlockedByDefault;
            }

            return true;
        }

        /// <summary>
        /// 루트 해금 여부를 Save·Shared 기준으로 판정한다.
        /// </summary>
        /// <remarks>
        /// Shared가 없거나 해당 routeId가 Shared에 없으면 true를 반환한다.
        /// WorldMapTest처럼 Shared를 로드하지 않는 환경에서 루트가 전부 숨겨지는 것을 막기 위함이며,
        /// 마을 <see cref="IsTownUnlocked"/> 폴백과 동일하다.
        /// Shared가 로드된 뒤에는 UnlockedByDefault=false인 루트는 Save 해금·완료·활성 상태가 아니면 숨긴다.
        /// </remarks>
        /// <returns>
        /// Save unlocked/completed이거나 Shared UnlockedByDefault이면 true.
        /// Shared 미로드 또는 Shared에 route가 없으면 true.
        /// Shared에 등록되고 UnlockedByDefault=false이며 Save에도 없으면 false.
        /// </returns>
        private static bool IsRouteUnlocked(
            string routeId,
            RouteMapDisplayState display,
            ISharedGameDataProvider shared)
        {
            if (display.IsUnlocked || display.IsCompleted)
            {
                return true;
            }

            SharedRouteDefinition route;
            if (shared != null && shared.TryGetRoute(routeId, out route))
            {
                return route.UnlockedByDefault;
            }

            return true;
        }

        private void RefreshProgressFromCoordinator()
        {
            var root = FrameworkRoot.Instance;
            if (root?.TradeProgressCoordinator == null
                || !root.TradeProgressCoordinator.TryGetMapProgress(out var snapshot))
            {
                ClearProgressVisuals();
                return;
            }

            ApplyProgress(snapshot);
        }

        private void ApplyProgress(TradeMapProgressSnapshot snapshot)
        {
            if (!lookupsBuilt)
            {
                BuildLookups();
            }

            RouteVisual route = null;
            if (!string.IsNullOrEmpty(snapshot.ActiveRouteId))
            {
                routesById.TryGetValue(snapshot.ActiveRouteId, out route);
            }

            if (caravanMarker != null)
            {
                caravanMarker.SetRoute(route);
                if (route != null)
                {
                    caravanMarker.SetProgress(snapshot.Progress01);
                }
            }

            if (progressPercentLabel != null)
            {
                progressPercentLabel.text = $"Progress {snapshot.ProgressPercent:0.#}%";
            }

            UpdateRiskLabel(snapshot.ActiveRouteId);

            foreach (var pair in routesById)
            {
                pair.Value.SetActiveVisual(string.Equals(pair.Key, snapshot.ActiveRouteId));
            }
        }

        private void ClearProgressVisuals()
        {
            if (caravanMarker != null)
            {
                caravanMarker.SetRoute(null);
            }

            if (progressPercentLabel != null)
            {
                progressPercentLabel.text = "Progress --";
            }

            if (riskLabel != null)
            {
                riskLabel.text = "Risk --";
            }
        }

        private void UpdateRiskLabel(string routeId)
        {
            if (riskLabel == null)
            {
                return;
            }

            var shared = FrameworkRoot.Instance != null ? FrameworkRoot.Instance.SharedGameData : null;
            SharedRouteDefinition route;
            if (string.IsNullOrEmpty(routeId) || shared == null || !shared.TryGetRoute(routeId, out route))
            {
                riskLabel.text = "Risk --";
                return;
            }

            riskLabel.text = $"Risk {route.BaseRiskLevel:0.##}";
        }

        /// <summary>
        /// 외부에서 루트 선택을 알릴 때 사용한다.
        /// </summary>
        public void NotifyRouteClicked(string routeId)
        {
            if (string.IsNullOrEmpty(routeId))
            {
                return;
            }

            UpdateRiskLabel(routeId);
            RouteClicked?.Invoke(routeId);
        }
    }
}
