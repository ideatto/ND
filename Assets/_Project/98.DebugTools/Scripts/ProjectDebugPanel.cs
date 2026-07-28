/*
 * Technical Ownership
 * - Responsible Discipline: Development Tools
 *
 * Script Purpose
 * - Editor와 Development Build에서 Framework 공개 상태를 읽기 전용으로 표시한다.
 *
 * Main Features
 * - F12로 패널을 열고 닫는다.
 * - Framework가 없거나 초기화 중이어도 N/A 상태로 안전하게 표시한다.
 * - CoreServices가 predefined assembly에 있으므로 공개 멤버를 런타임 리플렉션으로 조회한다.
 * - 선택 Caravan과 전체 Caravan, 거래 진행, 정산 대기 상태를 저장 데이터 변경 없이 표시한다.
 */
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ND.DebugTools
{
    /// <summary>
    /// 프로젝트 Framework 상태를 변경하지 않고 조회하는 개발 빌드 전용 패널이다.
    /// </summary>
    public sealed class ProjectDebugPanel : MonoBehaviour
    {
        private const string FrameworkRootTypeName = "ND.Framework.FrameworkRoot";
        private const float RefreshIntervalSeconds = 0.25f;
        private const int DebugWindowId = 9801;
        private static readonly string[] PendingPayloadMemberNames =
            { "hasResult", "result", "settlementResult", "snapshot", "resultSnapshot" };

        [SerializeField, Tooltip("플레이 시작 시 디버그 패널을 펼친 상태로 표시할지 여부입니다.")]
        private bool visibleOnStart;

        private readonly StringBuilder textBuilder = new StringBuilder(4096);
        private Rect windowRect = new Rect(16f, 16f, 560f, 700f);
        private GUIStyle labelStyle;
        private Type frameworkRootType;
        private string snapshot = string.Empty;
        private float nextRefreshTime;
        private bool isVisible;
        private Vector2 scrollPosition;

        private void Awake()
        {
            isVisible = visibleOnStart;
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            RefreshSnapshot();
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        private void Update()
        {
            if (Keyboard.current?.f12Key.wasPressedThisFrame == true)
            {
                isVisible = !isVisible;
                if (isVisible)
                {
                    RefreshSnapshot();
                }
            }

            if (isVisible && Time.unscaledTime >= nextRefreshTime)
            {
                RefreshSnapshot();
            }
        }

        private void OnGUI()
        {
            if (!isVisible)
            {
                return;
            }

            labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                richText = false,
                wordWrap = false
            };
            windowRect = GUI.Window(DebugWindowId, windowRect, DrawWindow, "Project Debug (F12)");
        }

        private void DrawWindow(int windowId)
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.Label(snapshot, labelStyle);
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 24f));
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            RefreshSnapshot();
        }

        private void RefreshSnapshot()
        {
            nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;
            textBuilder.Clear();
            textBuilder.AppendLine("[Framework]");
            textBuilder.AppendLine($"Scene: {SceneManager.GetActiveScene().name}");

            try
            {
                var root = GetFrameworkRoot();
                var saveData = GetMemberValue(root, "CurrentSaveData");
                var coordinator = GetMemberValue(root, "TradeProgressCoordinator");
                var router = GetMemberValue(root, "InGameScreenRouter");
                var sharedData = GetMemberValue(root, "SharedGameData");
                var player = GetMemberValue(saveData, "player");
                var caravans = ReadCollection(GetMemberValue(saveData, "caravans"));
                var tradeEntries = ReadCollection(GetMemberValue(saveData, "tradeProgressEntries"));
                var pendingEntries = ReadCollection(GetMemberValue(saveData, "pendingSettlements"));
                var selectedCaravanId = GetStringMember(saveData, "selectedCaravanId");

                textBuilder.AppendLine($"FrameworkRoot Initialized: {FormatBool(root != null && coordinator != null && router != null)}");
                textBuilder.AppendLine($"SaveData: {FormatBool(saveData != null)}");
                textBuilder.AppendLine($"Screen: {FormatValue(GetMemberValue(router, "CurrentScreenState"))}");
                textBuilder.AppendLine($"Trading Currency: {FormatValue(GetMemberValue(player, "tradingCurrency"))}");
                textBuilder.AppendLine($"Development Currency: {FormatValue(GetMemberValue(player, "developmentCurrency"))}");
                textBuilder.AppendLine($"SharedGameData Loaded: {FormatBool(ToBool(GetMemberValue(sharedData, "IsLoaded")))}");
                textBuilder.AppendLine($"  Towns: {FormatValue(GetMemberValue(sharedData, "TownCount"))}");
                textBuilder.AppendLine($"  Markets: {FormatValue(GetMemberValue(sharedData, "MarketCount"))}");
                textBuilder.AppendLine($"  Trade Items: {FormatValue(GetMemberValue(sharedData, "TradeItemCount"))}");
                textBuilder.AppendLine($"  Wagons: {FormatValue(GetMemberValue(sharedData, "WagonCount"))}");
                textBuilder.AppendLine($"  Draft Animals: {FormatValue(GetMemberValue(sharedData, "DraftAnimalCount"))}");
                textBuilder.AppendLine($"  Routes: {FormatValue(GetMemberValue(sharedData, "RouteCount"))}");

                AppendSelectedCaravan(saveData, selectedCaravanId, caravans, tradeEntries, pendingEntries);
                AppendAllCaravans(selectedCaravanId, caravans, tradeEntries, pendingEntries);
                AppendUnmatchedTrades(caravans, tradeEntries);
                AppendPendingSettlements(caravans, tradeEntries, pendingEntries);
            }
            catch (Exception exception)
            {
                textBuilder.AppendLine($"Status read failed: {exception.GetType().Name}");
            }

            snapshot = textBuilder.ToString();
        }

        private object GetFrameworkRoot()
        {
            frameworkRootType ??= FindType(FrameworkRootTypeName);
            return GetStaticMemberValue(frameworkRootType, "Instance");
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static object GetStaticMemberValue(Type type, string memberName)
        {
            if (type == null || string.IsNullOrEmpty(memberName))
            {
                return null;
            }

            try
            {
                var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Static);
                if (field != null)
                {
                    return field.GetValue(null);
                }

                var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Static);
                return property != null && property.CanRead && property.GetIndexParameters().Length == 0
                    ? property.GetValue(null)
                    : null;
            }
            catch (Exception exception) when (IsReflectionAccessException(exception))
            {
                return null;
            }
        }

        private static object GetMemberValue(object target, string memberName)
        {
            return TryGetMemberValue(target, memberName, out var value) ? value : null;
        }

        private static bool TryGetMemberValue(object target, string memberName, out object value)
        {
            value = null;
            if (target == null || string.IsNullOrEmpty(memberName))
            {
                return false;
            }

            try
            {
                var type = target.GetType();
                var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    value = field.GetValue(target);
                    return true;
                }

                var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null || !property.CanRead || property.GetIndexParameters().Length != 0)
                {
                    return false;
                }

                value = property.GetValue(target);
                return true;
            }
            catch (Exception exception) when (IsReflectionAccessException(exception))
            {
                value = null;
                return false;
            }
        }

        private static bool IsReflectionAccessException(Exception exception)
        {
            return exception is AmbiguousMatchException
                || exception is ArgumentException
                || exception is MethodAccessException
                || exception is TargetException
                || exception is TargetInvocationException;
        }

        private void AppendSelectedCaravan(
            object saveData,
            string selectedCaravanId,
            List<object> caravans,
            List<object> tradeEntries,
            List<object> pendingEntries)
        {
            textBuilder.AppendLine();
            textBuilder.AppendLine("[Selected Caravan]");
            textBuilder.AppendLine($"Selected Caravan ID: {FormatIdentifier(selectedCaravanId)}");

            var caravan = GetMemberValue(saveData, "caravan");
            if (caravan == null)
            {
                caravan = FindFirstByCaravanId(caravans, selectedCaravanId);
            }

            if (!string.IsNullOrEmpty(selectedCaravanId) && caravan == null)
            {
                textBuilder.AppendLine("Selected Caravan Entry: Missing");
            }

            textBuilder.AppendLine($"Caravan ID: {FormatValue(GetMemberValue(caravan, "caravanId"))}");
            textBuilder.AppendLine($"Slot Index: {FormatValue(GetMemberValue(caravan, "slotIndex"))}");
            textBuilder.AppendLine($"Journey State: {FormatValue(GetMemberValue(caravan, "state"))}");
            textBuilder.AppendLine($"Caravan Progress: {FormatProgress(GetMemberValue(caravan, "progress01"))}");

            var trade = GetMemberValue(saveData, "tradeProgress");
            if (trade == null)
            {
                trade = FindFirstByCaravanId(tradeEntries, selectedCaravanId);
            }

            if (trade == null)
            {
                textBuilder.AppendLine("Trade Progress State: None");
                textBuilder.AppendLine("Active Trade ID: N/A");
                textBuilder.AppendLine("Active Route ID: N/A");
                textBuilder.AppendLine("Trade Start UTC: N/A");
                textBuilder.AppendLine("Expected Trade End UTC: N/A");
                textBuilder.AppendLine("Pending Settlement: No");
                return;
            }

            textBuilder.AppendLine($"Trade Progress State: {FormatValue(GetMemberValue(trade, "state"))}");
            textBuilder.AppendLine($"Active Trade ID: {FormatValue(GetMemberValue(trade, "activeTradeId"))}");
            textBuilder.AppendLine($"Active Route ID: {FormatValue(GetMemberValue(trade, "activeRouteId"))}");
            textBuilder.AppendLine($"Trade Start UTC: {FormatUtcTicks(GetMemberValue(trade, "tradeStartUtcTick"))}");
            textBuilder.AppendLine($"Expected Trade End UTC: {FormatUtcTicks(GetMemberValue(trade, "expectedTradeEndUtcTick"))}");
            textBuilder.AppendLine($"Pending Settlement: {FormatBool(HasMatchingPending(trade, pendingEntries))}");
        }

        private void AppendAllCaravans(
            string selectedCaravanId,
            List<object> caravans,
            List<object> tradeEntries,
            List<object> pendingEntries)
        {
            textBuilder.AppendLine();
            textBuilder.AppendLine($"[All Caravans] Count: {caravans.Count}");
            for (var caravanIndex = 0; caravanIndex < caravans.Count; caravanIndex++)
            {
                var caravan = caravans[caravanIndex];
                if (caravan == null)
                {
                    textBuilder.AppendLine($"  [{caravanIndex}] <null entry>");
                    continue;
                }

                var caravanId = GetStringMember(caravan, "caravanId");
                var selectedMarker = IdEquals(caravanId, selectedCaravanId) ? "*" : " ";
                textBuilder.AppendLine($"{selectedMarker} [{caravanIndex}] {FormatIdentifier(caravanId)}");
                textBuilder.AppendLine($"  Slot: {FormatValue(GetMemberValue(caravan, "slotIndex"))}");
                textBuilder.AppendLine($"  Journey: {FormatValue(GetMemberValue(caravan, "state"))}");
                textBuilder.AppendLine($"  Progress: {FormatProgress(GetMemberValue(caravan, "progress01"))}");

                var matchCount = 0;
                for (var tradeIndex = 0; tradeIndex < tradeEntries.Count; tradeIndex++)
                {
                    var trade = tradeEntries[tradeIndex];
                    if (trade == null || !IdEquals(caravanId, GetStringMember(trade, "caravanId")))
                    {
                        continue;
                    }

                    AppendTradeLine(trade, pendingEntries, matchCount);
                    matchCount++;
                }

                if (matchCount == 0)
                {
                    textBuilder.AppendLine("  Trade: None");
                    textBuilder.AppendLine("  Pending: No");
                }

                textBuilder.AppendLine("  Runtime: Deferred (saved data shown)");
            }
        }

        private void AppendTradeLine(object trade, List<object> pendingEntries, int matchIndex)
        {
            var duplicateMarker = matchIndex > 0 ? $" [duplicate match {matchIndex + 1}]" : string.Empty;
            textBuilder.AppendLine(
                $"  Trade{duplicateMarker}: {FormatValue(GetMemberValue(trade, "activeTradeId"))} / {FormatValue(GetMemberValue(trade, "state"))}");
            textBuilder.AppendLine($"  Route: {FormatValue(GetMemberValue(trade, "activeRouteId"))}");
            textBuilder.AppendLine($"  Start UTC: {FormatUtcTicks(GetMemberValue(trade, "tradeStartUtcTick"))}");
            textBuilder.AppendLine($"  End UTC: {FormatUtcTicks(GetMemberValue(trade, "expectedTradeEndUtcTick"))}");
            textBuilder.AppendLine($"  Pending: {FormatBool(HasMatchingPending(trade, pendingEntries))}");
        }

        private void AppendUnmatchedTrades(List<object> caravans, List<object> tradeEntries)
        {
            var unmatchedCount = 0;
            for (var index = 0; index < tradeEntries.Count; index++)
            {
                var trade = tradeEntries[index];
                var caravanId = GetStringMember(trade, "caravanId");
                if (trade != null && ContainsCaravanId(caravans, caravanId))
                {
                    continue;
                }

                if (unmatchedCount == 0)
                {
                    textBuilder.AppendLine();
                    textBuilder.AppendLine("[Unmatched Trade Entries]");
                }

                textBuilder.AppendLine(
                    $"- [{index}] Caravan: {FormatIdentifier(caravanId)}, Trade: {FormatValue(GetMemberValue(trade, "activeTradeId"))} (Caravan Missing)");
                unmatchedCount++;
            }

            if (unmatchedCount > 0)
            {
                textBuilder.AppendLine($"Count: {unmatchedCount}");
            }
        }

        private void AppendPendingSettlements(
            List<object> caravans,
            List<object> tradeEntries,
            List<object> pendingEntries)
        {
            textBuilder.AppendLine();
            textBuilder.AppendLine($"[Pending Settlements] Count: {pendingEntries.Count}");
            for (var index = 0; index < pendingEntries.Count; index++)
            {
                var pending = pendingEntries[index];
                if (pending == null)
                {
                    textBuilder.AppendLine($"- [{index}] <null entry> (Missing References)");
                    continue;
                }

                var caravanId = GetStringMember(pending, "caravanId");
                var tradeId = GetStringMember(pending, "tradeId");
                var caravanFound = ContainsCaravanId(caravans, caravanId);
                var tradeFound = ContainsTradeIdentity(tradeEntries, caravanId, tradeId);
                var referenceState = caravanFound && tradeFound
                    ? string.Empty
                    : $" ({(caravanFound ? string.Empty : "Caravan Missing")}{(!caravanFound && !tradeFound ? ", " : string.Empty)}{(tradeFound ? string.Empty : "Trade Missing")})";

                textBuilder.AppendLine($"- [{index}] {FormatIdentifier(caravanId)} + {FormatIdentifier(tradeId)}{referenceState}");
                textBuilder.AppendLine($"  Result/Snapshot: {FormatPendingPayloadState(pending)}");
            }
        }

        private static List<object> ReadCollection(object value)
        {
            var entries = new List<object>();
            if (!(value is IEnumerable enumerable) || value is string)
            {
                return entries;
            }

            try
            {
                foreach (var entry in enumerable)
                {
                    entries.Add(entry);
                }
            }
            catch (Exception exception) when (
                exception is InvalidOperationException
                || exception is NotSupportedException
                || exception is TargetInvocationException)
            {
                // A malformed runtime collection must not break the rest of the panel.
            }

            return entries;
        }

        private static object FindFirstByCaravanId(List<object> entries, string caravanId)
        {
            if (string.IsNullOrEmpty(caravanId))
            {
                return null;
            }

            for (var index = 0; index < entries.Count; index++)
            {
                if (IdEquals(GetStringMember(entries[index], "caravanId"), caravanId))
                {
                    return entries[index];
                }
            }

            return null;
        }

        private static bool ContainsCaravanId(List<object> caravans, string caravanId)
        {
            if (string.IsNullOrEmpty(caravanId))
            {
                return false;
            }

            for (var index = 0; index < caravans.Count; index++)
            {
                if (IdEquals(GetStringMember(caravans[index], "caravanId"), caravanId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsTradeIdentity(List<object> trades, string caravanId, string tradeId)
        {
            if (string.IsNullOrEmpty(caravanId) || string.IsNullOrEmpty(tradeId))
            {
                return false;
            }

            for (var index = 0; index < trades.Count; index++)
            {
                var trade = trades[index];
                if (IdEquals(GetStringMember(trade, "caravanId"), caravanId)
                    && IdEquals(GetStringMember(trade, "activeTradeId"), tradeId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasMatchingPending(object trade, List<object> pendingEntries)
        {
            var caravanId = GetStringMember(trade, "caravanId");
            var tradeId = GetStringMember(trade, "activeTradeId");
            if (string.IsNullOrEmpty(caravanId) || string.IsNullOrEmpty(tradeId))
            {
                return false;
            }

            for (var index = 0; index < pendingEntries.Count; index++)
            {
                var pending = pendingEntries[index];
                if (IdEquals(GetStringMember(pending, "caravanId"), caravanId)
                    && IdEquals(GetStringMember(pending, "tradeId"), tradeId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// PendingSettlementSaveData의 결과 존재 여부를 Available/Missing/Unknown으로 표시한다.
        /// </summary>
        /// <remarks>
        /// 현재 저장 DTO는 단일 result 객체 대신 public bool hasResult와 평탄화된 결과 필드를 사용한다.
        /// hasResult를 우선 조회하고, 레거시 후보 멤버(result 등)가 있으면 null 여부로 판정한다.
        /// </remarks>
        private static string FormatPendingPayloadState(object pending)
        {
            var foundMember = false;
            for (var index = 0; index < PendingPayloadMemberNames.Length; index++)
            {
                if (!TryGetMemberValue(pending, PendingPayloadMemberNames[index], out var value))
                {
                    continue;
                }

                foundMember = true;
                if (value is bool hasResult)
                {
                    return hasResult ? "Available" : "Missing";
                }

                if (value != null)
                {
                    return "Available";
                }
            }

            return foundMember ? "Missing" : "Unknown";
        }

        private static string GetStringMember(object target, string memberName)
        {
            var value = GetMemberValue(target, memberName);
            return value?.ToString();
        }

        private static bool IdEquals(string left, string right)
        {
            return !string.IsNullOrEmpty(left)
                && !string.IsNullOrEmpty(right)
                && string.Equals(left, right, StringComparison.Ordinal);
        }

        private static string FormatIdentifier(string value)
        {
            return string.IsNullOrEmpty(value) ? "N/A" : value;
        }

        private static string FormatValue(object value)
        {
            return value == null || string.IsNullOrEmpty(value.ToString()) ? "N/A" : value.ToString();
        }

        private static string FormatBool(bool value)
        {
            return value ? "Yes" : "No";
        }

        private static bool ToBool(object value)
        {
            return value is bool boolValue && boolValue;
        }

        private static string FormatProgress(object value)
        {
            if (value is float progress)
            {
                return $"{Mathf.Clamp01(progress):P1}";
            }

            if (value is double doubleProgress)
            {
                return $"{Mathf.Clamp01((float)doubleProgress):P1}";
            }

            return "N/A";
        }

        private static string FormatUtcTicks(object value)
        {
            if (!(value is long ticks) || ticks <= 0L)
            {
                return "N/A";
            }

            try
            {
                return new DateTime(ticks, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
            }
            catch (ArgumentOutOfRangeException)
            {
                return "Invalid";
            }
        }
    }
}
#endif
