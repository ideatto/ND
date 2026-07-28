#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ND.Framework
{
    /// <summary>
    /// Development-only overlay for observing and forcing route events per Caravan.
    /// Press F8 during Play Mode to show or hide the overlay.
    /// </summary>
    internal sealed class RouteEventDebugOverlay : MonoBehaviour
    {
        private const int WindowId = 0x52455654;
        private const float WindowWidth = 620f;
        private const float WindowHeight = 520f;

        private static RouteEventDebugOverlay instance;

        private readonly Dictionary<string, string> selectedCombatEventIds =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, RouteBalanceSnapshot> routeBalanceSnapshots =
            new Dictionary<string, RouteBalanceSnapshot>(StringComparer.Ordinal);
        private readonly Dictionary<string, CombatBalanceSnapshot> combatBalanceSnapshots =
            new Dictionary<string, CombatBalanceSnapshot>(StringComparer.Ordinal);

        private Rect windowRect = new Rect(16f, 48f, WindowWidth, WindowHeight);
        private Vector2 scrollPosition;
        private bool visible;
        private string lastResult = "No forced event has been executed.";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null)
            {
                return;
            }

            var host = new GameObject(nameof(RouteEventDebugOverlay));
            DontDestroyOnLoad(host);
            instance = host.AddComponent<RouteEventDebugOverlay>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            var togglePressed = false;
#if ENABLE_INPUT_SYSTEM
            togglePressed = Keyboard.current != null
                && Keyboard.current.f8Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            togglePressed = Input.GetKeyDown(KeyCode.F8);
#endif
            if (togglePressed)
            {
                visible = !visible;
            }
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            windowRect = GUI.Window(WindowId, windowRect, DrawWindow, "Route Event Debug (F8)");
            windowRect.x = Mathf.Clamp(windowRect.x, 0f, Mathf.Max(0f, Screen.width - windowRect.width));
            windowRect.y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, Screen.height - windowRect.height));
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();

            var root = FrameworkRoot.Instance;
            if (root == null)
            {
                DrawUnavailable("FrameworkRoot is not ready.");
                return;
            }

            var saveData = root.CurrentSaveData;
            var coordinator = root.TradeProgressCoordinator;
            var sharedData = root.SharedGameData;
            if (saveData == null || coordinator == null || sharedData == null || !sharedData.IsLoaded)
            {
                DrawUnavailable("SaveData, TradeProgressCoordinator, or SharedGameData is not ready.");
                return;
            }

            GUILayout.Label("Forced result: " + lastResult);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Runtime overrides affect future checks only.");
            if (GUILayout.Button("Restore all balancing values", GUILayout.Width(190f)))
            {
                RestoreAllBalanceValues(root.SharedGameData);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            var travelingCount = DrawTravelingCaravans(saveData, coordinator, sharedData);
            if (travelingCount == 0)
            {
                GUILayout.Label("No Traveling Caravan.");
            }
            GUILayout.EndScrollView();

            GUILayout.Space(4f);
            GUILayout.Label("Natural events are confirmed by the Console message:");
            GUILayout.TextField("Route event occurred after save.");
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, WindowWidth, 24f));
        }

        private int DrawTravelingCaravans(
            SaveData saveData,
            TradeProgressCoordinator coordinator,
            ISharedGameDataProvider sharedData)
        {
            var entries = saveData.tradeProgressEntries;
            if (entries == null)
            {
                return 0;
            }

            var travelingCount = 0;
            for (var index = 0; index < entries.Count; index++)
            {
                var progress = entries[index];
                if (progress == null || progress.state != TradeProgressState.Traveling)
                {
                    continue;
                }

                travelingCount++;
                DrawCaravan(progress, coordinator, sharedData);
            }

            return travelingCount;
        }

        private void DrawCaravan(
            TradeProgressSaveData progress,
            TradeProgressCoordinator coordinator,
            ISharedGameDataProvider sharedData)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"Caravan: {progress.caravanId}");
            GUILayout.Label($"Trade: {progress.activeTradeId}");
            GUILayout.Label($"Route: {progress.activeRouteId}");

            if (!coordinator.TryGetRuntimeCaravan(progress.caravanId, out var caravan)
                || caravan == null)
            {
                GUILayout.Label("Runtime Caravan: missing");
                GUILayout.EndVertical();
                return;
            }

            GUILayout.Label(
                $"Progress: {caravan.progress01:P1}  Distance: {caravan.currentDistanceKm:0.##} km");
            GUILayout.Label(
                $"Checks: {caravan.runEventChecksProcessed}  Events: {caravan.runEventsOccurred}  Battles: {caravan.runBattlesFought}");
            GUILayout.Label(
                $"Cargo lost: {caravan.runCargoLost}  Food lost: {caravan.runFoodLost:0.##}  Durability lost: {caravan.runDurabilityLost}");
            GUILayout.Label($"Fatal: {caravan.runFatalReason}");

            if (!sharedData.TryGetRoute(progress.activeRouteId, out var route) || route == null)
            {
                GUILayout.Label("Route data: missing");
                GUILayout.EndVertical();
                return;
            }

            CaptureRouteBalance(route);
            DrawRouteBalanceControls(route);

            var combatEvents = GetCombatEvents(route);
            if (combatEvents.Count == 0)
            {
                GUILayout.Label("Combat event: none on this route");
                GUILayout.EndVertical();
                return;
            }

            var selectedId = ResolveSelectedEventId(progress.caravanId, combatEvents);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Combat event", GUILayout.Width(90f));
            if (GUILayout.Button("<", GUILayout.Width(28f)))
            {
                selectedId = SelectAdjacent(combatEvents, selectedId, -1);
            }
            GUILayout.Label(FormatEvent(combatEvents, selectedId), GUILayout.MinWidth(300f));
            if (GUILayout.Button(">", GUILayout.Width(28f)))
            {
                selectedId = SelectAdjacent(combatEvents, selectedId, 1);
            }
            selectedCombatEventIds[progress.caravanId] = selectedId;
            GUILayout.EndHorizontal();

            var selectedEvent = FindEvent(combatEvents, selectedId);
            if (selectedEvent != null)
            {
                CaptureCombatBalance(route.Id, selectedEvent);
                DrawCombatBalanceControls(route.Id, selectedEvent);
            }

            GUI.enabled = caravan.runFatalReason == JourneyFailureReason.None;
            if (GUILayout.Button("Force selected Combat event and save"))
            {
                ForceEvent(coordinator, progress, selectedId);
            }
            GUI.enabled = true;
            GUILayout.EndVertical();
        }

        private void DrawRouteBalanceControls(SharedRouteDefinition route)
        {
            GUILayout.Space(3f);
            GUILayout.Label("Runtime route balancing");
            route.BaseRiskLevel = DrawFloatControl(
                "Risk per check",
                route.BaseRiskLevel,
                0f,
                1f,
                0.05f,
                "P1");
            route.MaxEventCount = Mathf.RoundToInt(DrawFloatControl(
                "Max checks",
                route.MaxEventCount,
                0f,
                30f,
                1f,
                "0"));

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Restore this route", GUILayout.Width(150f)))
            {
                RestoreRouteBalance(route);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawCombatBalanceControls(
            string routeId,
            SharedRouteEventDefinition routeEvent)
        {
            GUILayout.Label("Runtime Combat balancing");
            routeEvent.BanditCombatPower = Mathf.RoundToInt(DrawFloatControl(
                "Bandit power",
                routeEvent.BanditCombatPower,
                0f,
                1000f,
                10f,
                "0"));
            routeEvent.CargoLootRate = DrawFloatControl(
                "Cargo loot rate",
                routeEvent.CargoLootRate,
                0f,
                1f,
                0.05f,
                "P1");
            routeEvent.FodderLootRate = DrawFloatControl(
                "Fodder loot rate",
                routeEvent.FodderLootRate,
                0f,
                1f,
                0.05f,
                "P1");

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Restore this Combat event", GUILayout.Width(190f)))
            {
                RestoreCombatBalance(routeId, routeEvent);
            }
            GUILayout.EndHorizontal();
        }

        private static float DrawFloatControl(
            string label,
            float value,
            float minimum,
            float maximum,
            float step,
            string format)
        {
            value = Mathf.Clamp(value, minimum, maximum);
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110f));
            if (GUILayout.Button("-", GUILayout.Width(26f)))
            {
                value -= step;
            }
            value = GUILayout.HorizontalSlider(value, minimum, maximum, GUILayout.Width(260f));
            if (GUILayout.Button("+", GUILayout.Width(26f)))
            {
                value += step;
            }
            value = Mathf.Clamp(value, minimum, maximum);
            GUILayout.Label(value.ToString(format), GUILayout.Width(65f));
            GUILayout.EndHorizontal();
            return value;
        }

        private void ForceEvent(
            TradeProgressCoordinator coordinator,
            TradeProgressSaveData progress,
            string eventId)
        {
            var result = coordinator.TryProcessForcedRouteEvent(
                progress.caravanId,
                progress.activeTradeId,
                eventId);
            var saveSucceeded = result.SaveResult != null && result.SaveResult.Succeeded;
            lastResult = result.Succeeded
                ? $"SUCCESS | Caravan={result.CaravanId} | Event={result.EventId} | Save={saveSucceeded}"
                : $"FAILED | Caravan={result.CaravanId} | Event={result.EventId} | Reason={result.FailureReason} | Save={saveSucceeded}";

            if (result.Succeeded)
            {
                Debug.Log("[RouteEventDebugOverlay] " + lastResult);
            }
            else
            {
                Debug.LogWarning("[RouteEventDebugOverlay] " + lastResult);
            }
        }

        private static List<SharedRouteEventDefinition> GetCombatEvents(SharedRouteDefinition route)
        {
            var result = new List<SharedRouteEventDefinition>();
            if (route.Events == null)
            {
                return result;
            }

            for (var index = 0; index < route.Events.Length; index++)
            {
                var routeEvent = route.Events[index];
                if (routeEvent != null
                    && routeEvent.EventType == RouteEvent.Combat
                    && !string.IsNullOrWhiteSpace(routeEvent.Id))
                {
                    result.Add(routeEvent);
                }
            }

            return result;
        }

        private static SharedRouteEventDefinition FindEvent(
            List<SharedRouteEventDefinition> events,
            string eventId)
        {
            for (var index = 0; index < events.Count; index++)
            {
                if (string.Equals(events[index].Id, eventId, StringComparison.Ordinal))
                {
                    return events[index];
                }
            }

            return null;
        }

        private void CaptureRouteBalance(SharedRouteDefinition route)
        {
            if (route == null
                || string.IsNullOrWhiteSpace(route.Id)
                || routeBalanceSnapshots.ContainsKey(route.Id))
            {
                return;
            }

            routeBalanceSnapshots.Add(
                route.Id,
                new RouteBalanceSnapshot(route.BaseRiskLevel, route.MaxEventCount));
        }

        private void CaptureCombatBalance(
            string routeId,
            SharedRouteEventDefinition routeEvent)
        {
            var key = GetCombatSnapshotKey(routeId, routeEvent?.Id);
            if (key.Length == 0 || combatBalanceSnapshots.ContainsKey(key))
            {
                return;
            }

            combatBalanceSnapshots.Add(
                key,
                new CombatBalanceSnapshot(
                    routeEvent.BanditCombatPower,
                    routeEvent.CargoLootRate,
                    routeEvent.FodderLootRate));
        }

        private void RestoreRouteBalance(SharedRouteDefinition route)
        {
            if (route == null
                || !routeBalanceSnapshots.TryGetValue(route.Id, out var snapshot))
            {
                return;
            }

            route.BaseRiskLevel = snapshot.BaseRiskLevel;
            route.MaxEventCount = snapshot.MaxEventCount;
            if (route.Events == null)
            {
                return;
            }

            for (var index = 0; index < route.Events.Length; index++)
            {
                RestoreCombatBalance(route.Id, route.Events[index]);
            }
        }

        private void RestoreCombatBalance(
            string routeId,
            SharedRouteEventDefinition routeEvent)
        {
            if (routeEvent == null
                || !combatBalanceSnapshots.TryGetValue(
                    GetCombatSnapshotKey(routeId, routeEvent.Id),
                    out var snapshot))
            {
                return;
            }

            routeEvent.BanditCombatPower = snapshot.BanditCombatPower;
            routeEvent.CargoLootRate = snapshot.CargoLootRate;
            routeEvent.FodderLootRate = snapshot.FodderLootRate;
        }

        private void RestoreAllBalanceValues(ISharedGameDataProvider sharedData)
        {
            if (sharedData == null)
            {
                return;
            }

            foreach (var pair in routeBalanceSnapshots)
            {
                if (sharedData.TryGetRoute(pair.Key, out var route) && route != null)
                {
                    RestoreRouteBalance(route);
                }
            }

            lastResult = "All runtime balancing values were restored.";
        }

        private static string GetCombatSnapshotKey(string routeId, string eventId)
        {
            if (string.IsNullOrWhiteSpace(routeId) || string.IsNullOrWhiteSpace(eventId))
            {
                return string.Empty;
            }

            return routeId + "\n" + eventId;
        }

        private string ResolveSelectedEventId(
            string caravanId,
            List<SharedRouteEventDefinition> combatEvents)
        {
            if (selectedCombatEventIds.TryGetValue(caravanId, out var selectedId))
            {
                for (var index = 0; index < combatEvents.Count; index++)
                {
                    if (string.Equals(combatEvents[index].Id, selectedId, StringComparison.Ordinal))
                    {
                        return selectedId;
                    }
                }
            }

            return combatEvents[0].Id;
        }

        private static string SelectAdjacent(
            List<SharedRouteEventDefinition> events,
            string selectedId,
            int direction)
        {
            var selectedIndex = 0;
            for (var index = 0; index < events.Count; index++)
            {
                if (string.Equals(events[index].Id, selectedId, StringComparison.Ordinal))
                {
                    selectedIndex = index;
                    break;
                }
            }

            selectedIndex = (selectedIndex + direction + events.Count) % events.Count;
            return events[selectedIndex].Id;
        }

        private static string FormatEvent(
            List<SharedRouteEventDefinition> events,
            string selectedId)
        {
            for (var index = 0; index < events.Count; index++)
            {
                var routeEvent = events[index];
                if (string.Equals(routeEvent.Id, selectedId, StringComparison.Ordinal))
                {
                    return $"{routeEvent.Id} ({routeEvent.DisplayName}, Power {routeEvent.BanditCombatPower})";
                }
            }

            return selectedId;
        }

        private void DrawUnavailable(string message)
        {
            GUILayout.Label(message);
            GUILayout.Label("Enter Play Mode and wait for Framework initialization.");
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, WindowWidth, 24f));
        }

        private readonly struct RouteBalanceSnapshot
        {
            public RouteBalanceSnapshot(float baseRiskLevel, int maxEventCount)
            {
                BaseRiskLevel = baseRiskLevel;
                MaxEventCount = maxEventCount;
            }

            public float BaseRiskLevel { get; }
            public int MaxEventCount { get; }
        }

        private readonly struct CombatBalanceSnapshot
        {
            public CombatBalanceSnapshot(
                int banditCombatPower,
                float cargoLootRate,
                float fodderLootRate)
            {
                BanditCombatPower = banditCombatPower;
                CargoLootRate = cargoLootRate;
                FodderLootRate = fodderLootRate;
            }

            public int BanditCombatPower { get; }
            public float CargoLootRate { get; }
            public float FodderLootRate { get; }
        }
    }
}
#endif
