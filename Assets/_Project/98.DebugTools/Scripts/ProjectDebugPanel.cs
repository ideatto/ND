/*
 * Technical Ownership
 * - Responsible Discipline: Development Tools
 *
 * Script Purpose
 * - Editor와 Development Build에서 Framework 공개 상태를 읽기 전용으로 표시한다.
 *
 * Main Features
 * - Monitoring is read-only; force-arrival and currency grants mutate only from explicit button clicks.
 * - F12로 패널을 열고 닫는다.
 * - Framework가 없거나 초기화 중이어도 N/A 상태로 안전하게 표시한다.
 * - CoreServices가 predefined assembly에 있으므로 공개 멤버를 런타임 리플렉션으로 조회한다.
 * - 선택 Caravan과 전체 Caravan, 거래 진행, 정산 대기 상태를 저장 데이터 변경 없이 표시한다.
 */
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    /// <remarks>
    /// Monitoring remains read-only. Explicit command buttons revalidate current Framework state before invoking one debug mutation command.
    /// </remarks>
    public sealed class ProjectDebugPanel : MonoBehaviour
    {
        private const string FrameworkRootTypeName = "ND.Framework.FrameworkRoot";
        private const float RefreshIntervalSeconds = 0.25f;
        private const int DebugWindowId = 9801;
        private const int StatusTab = 0;
        private const int HomeInventoryTab = 1;
        private const int ScreenRouterTab = 2;
        private const int TabCount = 3;
        private static readonly string[] PendingPayloadMemberNames =
            { "hasResult", "result", "settlementResult", "snapshot", "resultSnapshot" };

        [SerializeField, Tooltip("플레이 시작 시 디버그 패널을 펼친 상태로 표시할지 여부입니다.")]
        private bool visibleOnStart;

        private readonly StringBuilder textBuilder = new StringBuilder(4096);
        private readonly HomeInventoryItemDebugSection homeInventorySection = new HomeInventoryItemDebugSection();
        private readonly InGameScreenRouterDebugSection screenRouterSection = new InGameScreenRouterDebugSection();
        private readonly Vector2[] tabScrollPositions = new Vector2[TabCount];
        private Rect windowRect = new Rect(16f, 16f, 560f, 700f);
        private GUIStyle labelStyle;
        private Type frameworkRootType;
        private string snapshot = string.Empty;
        private string lastForceArrivalResult = "No command executed.";
        private string tradingCurrencyAmountInput = string.Empty;
        private string developmentCurrencyAmountInput = string.Empty;
        private string lastCurrencyResult = "No currency command executed.";
        private string wagonRepairMultiplierInput = "1";
        private string lastWagonRepairMultiplierResult = "No multiplier command executed.";
        private float nextRefreshTime;
        private bool isVisible;
        private int selectedTab;

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
            screenRouterSection.Tick();

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
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(selectedTab == StatusTab, "Status", GUI.skin.button))
            {
                selectedTab = StatusTab;
            }

            if (GUILayout.Toggle(selectedTab == HomeInventoryTab, "Home Inventory", GUI.skin.button))
            {
                if (selectedTab != HomeInventoryTab)
                {
                    homeInventorySection.Refresh();
                }

                selectedTab = HomeInventoryTab;
            }

            if (GUILayout.Toggle(selectedTab == ScreenRouterTab, "Screen Router", GUI.skin.button))
            {
                selectedTab = ScreenRouterTab;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            tabScrollPositions[selectedTab] = GUILayout.BeginScrollView(
                tabScrollPositions[selectedTab],
                false,
                true,
                GUILayout.ExpandHeight(true));

            if (selectedTab == HomeInventoryTab)
            {
                homeInventorySection.Draw();
            }
            else if (selectedTab == ScreenRouterTab)
            {
                screenRouterSection.Draw();
            }
            else
            {
                GUILayout.Label(snapshot, labelStyle);
                DrawForceArrivalControl();
                DrawCurrencyControls();
                DrawWagonRepairMultiplierControl();
            }

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 24f));
        }

        private void DrawForceArrivalControl()
        {
            var state = ResolveForceArrivalState();

            GUILayout.Space(8f);
            GUILayout.Label("[Selected Traveling Trade - Force Arrival]", labelStyle);
            GUILayout.Label($"Selected Caravan ID: {FormatIdentifier(state.SelectedCaravanId)}", labelStyle);
            GUILayout.Label($"Entry Caravan ID: {FormatIdentifier(state.EntryCaravanId)}", labelStyle);
            GUILayout.Label($"Active Trade ID: {FormatIdentifier(state.TradeId)}", labelStyle);
            GUILayout.Label($"State: {FormatIdentifier(state.State)}", labelStyle);
            GUILayout.Label($"Route ID: {FormatIdentifier(state.RouteId)}", labelStyle);
            GUILayout.Label($"Progress: {FormatProgress(state.Progress)}", labelStyle);
            GUILayout.Label($"Availability: {(state.CanExecute ? "Ready" : state.DisabledReason)}", labelStyle);
            GUILayout.Label("Debug mutation command.", labelStyle);
            GUILayout.Label("Forces only the exact selected Traveling trade to arrival.", labelStyle);
            GUILayout.Label("Does not sell cargo, claim rewards, or complete the trade.", labelStyle);
            GUILayout.Label("The command revalidates caravanId and tradeId at click time.", labelStyle);

            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && state.CanExecute;
            var clicked = GUILayout.Button("Force Selected Trade to Arrival");
            GUI.enabled = previousEnabled;

            if (clicked)
            {
                ExecuteForceArrival();
            }

            GUILayout.Label($"Last Result: {lastForceArrivalResult}", labelStyle);
        }

        private void DrawCurrencyControls()
        {
            var tradingState = ResolveCurrencyState("TryAddTradingCurrency", "tradingCurrency");
            var developmentState = ResolveCurrencyState("TryAddDevelopmentCurrency", "developmentCurrency");

            GUILayout.Space(8f);
            GUILayout.Label("[Currency Controls]", labelStyle);
            DrawCurrencyControl("Trading Currency", true, tradingState, ref tradingCurrencyAmountInput);
            GUILayout.Space(4f);
            DrawCurrencyControl("Development Currency", false, developmentState, ref developmentCurrencyAmountInput);
            GUILayout.Label($"Last Currency Result:\n{lastCurrencyResult}", labelStyle);
        }

        private void DrawCurrencyControl(
            string displayName,
            bool isTradingCurrency,
            CurrencyState state,
            ref string amountInput)
        {
            GUILayout.Label(displayName, labelStyle);
            GUILayout.Label($"Current: {FormatCurrency(state)}", labelStyle);
            GUILayout.Label($"Availability: {(state.CanExecute ? "Ready" : state.DisabledReason)}", labelStyle);

            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && state.CanExecute;
            GUILayout.BeginHorizontal();
            var add100 = GUILayout.Button("+100");
            var add1000 = GUILayout.Button("+1,000");
            var add10000 = GUILayout.Button("+10,000");
            GUILayout.EndHorizontal();
            GUI.enabled = previousEnabled;

            if (add100)
            {
                ExecuteCurrencyGrant(isTradingCurrency, 100L);
            }
            else if (add1000)
            {
                ExecuteCurrencyGrant(isTradingCurrency, 1000L);
            }
            else if (add10000)
            {
                ExecuteCurrencyGrant(isTradingCurrency, 10000L);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Custom:", labelStyle, GUILayout.Width(64f));
            amountInput = GUILayout.TextField(amountInput);
            GUI.enabled = previousEnabled && state.CanExecute;
            var addCustom = GUILayout.Button("Add", GUILayout.Width(64f));
            GUI.enabled = previousEnabled;
            GUILayout.EndHorizontal();

            if (addCustom)
            {
                ExecuteCustomCurrencyGrant(isTradingCurrency, amountInput);
            }
        }

        private void DrawWagonRepairMultiplierControl()
        {
            var state = ResolveWagonRepairMultiplierState();

            GUILayout.Space(8f);
            GUILayout.Label("[Wagon Repair Cost Multiplier]", labelStyle);
            GUILayout.Label(
                $"Current: {(state.HasCurrentValue ? state.CurrentValue.ToString("0.##", CultureInfo.InvariantCulture) + "x" : "N/A")}",
                labelStyle);
            GUILayout.Label($"Availability: {(state.CanExecute ? "Ready" : state.DisabledReason)}", labelStyle);
            GUILayout.Label("Session only. Actual repair calculation is not connected yet.", labelStyle);

            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && state.CanExecute;
            GUILayout.BeginHorizontal();
            var decrease = GUILayout.Button("-0.25x");
            var increase = GUILayout.Button("+0.25x");
            var reset = GUILayout.Button("Reset 1x");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Custom:", labelStyle, GUILayout.Width(64f));
            wagonRepairMultiplierInput = GUILayout.TextField(wagonRepairMultiplierInput);
            var apply = GUILayout.Button("Apply", GUILayout.Width(64f));
            GUILayout.EndHorizontal();
            GUI.enabled = previousEnabled;

            if (decrease)
            {
                ExecuteWagonRepairMultiplierCommand("DecreaseWagonRepairCostMultiplier");
            }
            else if (increase)
            {
                ExecuteWagonRepairMultiplierCommand("IncreaseWagonRepairCostMultiplier");
            }
            else if (reset)
            {
                ExecuteWagonRepairMultiplierCommand("ResetWagonRepairCostMultiplier");
            }
            else if (apply)
            {
                ExecuteWagonRepairMultiplierApply(wagonRepairMultiplierInput);
            }

            GUILayout.Label($"Last Multiplier Result: {lastWagonRepairMultiplierResult}", labelStyle);
        }

        private WagonRepairMultiplierState ResolveWagonRepairMultiplierState()
        {
            var state = new WagonRepairMultiplierState();

            try
            {
                state.Root = GetFrameworkRoot();
                if (state.Root == null)
                {
                    state.DisabledReason = "Framework is unavailable";
                    return state;
                }

                state.DebugCommands = GetMemberValue(state.Root, "DebugCommands");
                if (state.DebugCommands == null)
                {
                    state.DisabledReason = "DebugCommands is unavailable";
                    return state;
                }

                var type = state.DebugCommands.GetType();
                state.ValueProperty = type.GetProperty(
                    "WagonRepairCostMultiplier",
                    BindingFlags.Public | BindingFlags.Instance);
                state.SetMethod = type.GetMethod(
                    "TrySetWagonRepairCostMultiplier",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(double) },
                    null);

                if (state.ValueProperty == null || !state.ValueProperty.CanRead || state.SetMethod == null)
                {
                    state.DisabledReason = "Wagon repair multiplier API is unavailable";
                    return state;
                }

                var value = state.ValueProperty.GetValue(state.DebugCommands);
                if (!(value is double currentValue))
                {
                    state.DisabledReason = "Wagon repair multiplier value is unavailable";
                    return state;
                }

                state.CurrentValue = currentValue;
                state.HasCurrentValue = true;
                state.CanExecute = true;
                state.DisabledReason = string.Empty;
                return state;
            }
            catch (Exception exception)
            {
                state.DisabledReason = $"Wagon repair multiplier unavailable: {exception.GetType().Name}";
                return state;
            }
        }

        private void ExecuteWagonRepairMultiplierApply(string input)
        {
            if (!double.TryParse(
                    input?.Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var multiplier))
            {
                lastWagonRepairMultiplierResult = "Not applied. Enter a number from 0 to 1000.";
                return;
            }

            var state = ResolveWagonRepairMultiplierState();
            if (!state.CanExecute)
            {
                lastWagonRepairMultiplierResult = $"Not applied. {state.DisabledReason}.";
                return;
            }

            try
            {
                var applied = state.SetMethod.Invoke(state.DebugCommands, new object[] { multiplier });
                lastWagonRepairMultiplierResult = applied is bool succeeded && succeeded
                    ? $"Applied {multiplier:0.##}x."
                    : "Not applied. Enter a number from 0 to 1000.";
            }
            catch (Exception exception)
            {
                var cause = exception is TargetInvocationException invocation && invocation.InnerException != null
                    ? invocation.InnerException
                    : exception;
                lastWagonRepairMultiplierResult = $"Invocation failed: {cause.GetType().Name}: {cause.Message}";
            }
        }

        private void ExecuteWagonRepairMultiplierCommand(string methodName)
        {
            var state = ResolveWagonRepairMultiplierState();
            if (!state.CanExecute)
            {
                lastWagonRepairMultiplierResult = $"Not executed. {state.DisabledReason}.";
                return;
            }

            try
            {
                var method = state.DebugCommands.GetType().GetMethod(
                    methodName,
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);
                if (method == null)
                {
                    lastWagonRepairMultiplierResult = "Multiplier command API is unavailable.";
                    return;
                }

                var result = method.Invoke(state.DebugCommands, null);
                lastWagonRepairMultiplierResult = result is double multiplier
                    ? $"Applied {multiplier:0.##}x."
                    : "Multiplier command returned no value.";
            }
            catch (Exception exception)
            {
                var cause = exception is TargetInvocationException invocation && invocation.InnerException != null
                    ? invocation.InnerException
                    : exception;
                lastWagonRepairMultiplierResult = $"Invocation failed: {cause.GetType().Name}: {cause.Message}";
            }
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

        private CurrencyState ResolveCurrencyState(string methodName, string currencyMemberName)
        {
            var state = new CurrencyState();

            try
            {
                state.Root = GetFrameworkRoot();
                if (state.Root == null)
                {
                    state.DisabledReason = "Framework is unavailable";
                    return state;
                }

                state.DebugCommands = GetMemberValue(state.Root, "DebugCommands");
                if (state.DebugCommands == null)
                {
                    state.DisabledReason = "DebugCommands is unavailable";
                    return state;
                }

                state.Method = FindCurrencyMethod(state.DebugCommands.GetType(), methodName);
                if (state.Method == null)
                {
                    state.DisabledReason = "Currency API is unavailable";
                    return state;
                }

                var saveData = GetMemberValue(state.Root, "CurrentSaveData");
                if (saveData == null)
                {
                    state.DisabledReason = "Current SaveData is unavailable";
                    return state;
                }

                var player = GetMemberValue(saveData, "player");
                if (player == null)
                {
                    state.DisabledReason = "Player data is unavailable";
                    return state;
                }

                if (!TryGetMemberValue(player, currencyMemberName, out var value) || !(value is long currentValue))
                {
                    state.DisabledReason = "Currency value is unavailable";
                    return state;
                }

                state.CurrentValue = currentValue;
                state.HasCurrentValue = true;
                state.CanExecute = true;
                state.DisabledReason = string.Empty;
                return state;
            }
            catch (Exception exception)
            {
                state.DisabledReason = $"Currency status unavailable: {exception.GetType().Name}";
                return state;
            }
        }

        private static MethodInfo FindCurrencyMethod(Type debugCommandsType, string methodName)
        {
            if (debugCommandsType == null)
            {
                return null;
            }

            try
            {
                var methods = debugCommandsType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                for (var index = 0; index < methods.Length; index++)
                {
                    var method = methods[index];
                    if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length == 1
                        && parameters[0].ParameterType == typeof(long)
                        && string.Equals(method.ReturnType.FullName, "ND.Framework.SaveResult", StringComparison.Ordinal))
                    {
                        return method;
                    }
                }
            }
            catch (Exception exception) when (IsReflectionAccessException(exception))
            {
                return null;
            }

            return null;
        }

        private void ExecuteCustomCurrencyGrant(bool isTradingCurrency, string input)
        {
            var trimmedInput = input?.Trim();
            if (string.IsNullOrEmpty(trimmedInput))
            {
                lastCurrencyResult = "Currency grant not executed.\nReason: Enter a positive whole number.";
                return;
            }

            if (!long.TryParse(trimmedInput, NumberStyles.None, CultureInfo.InvariantCulture, out var amount))
            {
                lastCurrencyResult = ContainsOnlyAsciiDigits(trimmedInput)
                    ? "Currency grant not executed.\nReason: Amount is too large or is not a positive whole number."
                    : "Currency grant not executed.\nReason: Enter a positive whole number.";
                return;
            }

            if (amount <= 0L)
            {
                lastCurrencyResult = "Currency grant not executed.\nReason: Enter a positive whole number.";
                return;
            }

            ExecuteCurrencyGrant(isTradingCurrency, amount);
        }

        private static bool ContainsOnlyAsciiDigits(string value)
        {
            for (var index = 0; index < value.Length; index++)
            {
                if (value[index] < '0' || value[index] > '9')
                {
                    return false;
                }
            }

            return value.Length > 0;
        }

        private void ExecuteCurrencyGrant(bool isTradingCurrency, long amount)
        {
            var displayName = isTradingCurrency ? "Trading Currency" : "Development Currency";
            var methodName = isTradingCurrency ? "TryAddTradingCurrency" : "TryAddDevelopmentCurrency";
            var memberName = isTradingCurrency ? "tradingCurrency" : "developmentCurrency";
            var state = ResolveCurrencyState(methodName, memberName);

            if (amount <= 0L)
            {
                lastCurrencyResult = $"{displayName} grant not executed.\nRequested: {amount}\nReason: Enter a positive whole number.";
                return;
            }

            if (!state.CanExecute)
            {
                lastCurrencyResult =
                    $"{displayName} grant invocation failed.\nRequested: {amount:N0}\nReason: {state.DisabledReason}.";
                RefreshSnapshot();
                return;
            }

            try
            {
                var result = state.Method.Invoke(state.DebugCommands, new object[] { amount });
                var refreshedState = ResolveCurrencyState(methodName, memberName);
                lastCurrencyResult = FormatCurrencyResult(result, displayName, amount, refreshedState);
            }
            catch (TargetInvocationException exception)
            {
                var cause = exception.InnerException ?? exception;
                lastCurrencyResult =
                    $"{displayName} grant invocation failed.\nRequested: {amount:N0}\nReason: {cause.GetType().Name}: {cause.Message}";
            }
            catch (Exception exception) when (
                IsReflectionAccessException(exception)
                || exception is InvalidOperationException)
            {
                lastCurrencyResult =
                    $"{displayName} grant invocation failed.\nRequested: {amount:N0}\nReason: {exception.GetType().Name}: {exception.Message}";
            }

            RefreshSnapshot();
        }

        private static string FormatCurrencyResult(
            object result,
            string displayName,
            long requestedAmount,
            CurrencyState refreshedState)
        {
            if (result == null)
            {
                return $"{displayName} grant invocation failed.\nRequested: {requestedAmount:N0}\nReason: Command returned no result.";
            }

            if (!(GetMemberValue(result, "Succeeded") is bool succeeded))
            {
                return $"{displayName} grant invocation failed.\nRequested: {requestedAmount:N0}\nReason: Unexpected result shape.";
            }

            var builder = new StringBuilder(256);
            if (succeeded)
            {
                builder.AppendLine($"{displayName} grant succeeded.");
                builder.AppendLine($"Added: {requestedAmount:N0}");
                builder.Append($"Current: {(refreshedState.HasCurrentValue ? refreshedState.CurrentValue.ToString("N0", CultureInfo.InvariantCulture) : "N/A")}");
                return builder.ToString();
            }

            builder.AppendLine($"{displayName} grant failed.");
            builder.AppendLine($"Requested: {requestedAmount:N0}");
            AppendResultMember(builder, "Category", result, "FailedDataCategory");
            AppendResultMember(builder, "Reason", result, "FailureReason");
            AppendResultMember(builder, "Message", result, "Message");
            return builder.ToString().TrimEnd();
        }

        private static void AppendResultMember(
            StringBuilder builder,
            string label,
            object result,
            string memberName)
        {
            if (!TryGetMemberValue(result, memberName, out var value) || value == null)
            {
                return;
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.AppendLine($"{label}: {text}");
            }
        }

        private static string FormatCurrency(CurrencyState state)
        {
            return state.HasCurrentValue
                ? state.CurrentValue.ToString("N0", CultureInfo.InvariantCulture)
                : "N/A";
        }

        private ForceArrivalState ResolveForceArrivalState()
        {
            var state = new ForceArrivalState();

            try
            {
                state.Root = GetFrameworkRoot();
                if (state.Root == null)
                {
                    state.DisabledReason = "Framework is unavailable";
                    return state;
                }

                state.DebugCommands = GetMemberValue(state.Root, "DebugCommands");
                if (state.DebugCommands == null)
                {
                    state.DisabledReason = "DebugCommands is unavailable";
                    return state;
                }

                state.Method = FindForceArrivalMethod(state.DebugCommands.GetType());
                if (state.Method == null)
                {
                    state.DisabledReason = "Exact force-arrival API is unavailable";
                    return state;
                }

                var saveData = GetMemberValue(state.Root, "CurrentSaveData");
                if (saveData == null)
                {
                    state.DisabledReason = "Current SaveData is unavailable";
                    return state;
                }

                state.SelectedCaravanId = GetStringMember(saveData, "selectedCaravanId");
                if (string.IsNullOrWhiteSpace(state.SelectedCaravanId))
                {
                    state.DisabledReason = "No selected Caravan";
                    return state;
                }

                if (!TryGetMemberValue(saveData, "tradeProgressEntries", out var entriesValue))
                {
                    state.DisabledReason = "Selected Caravan progress not found (exact entry list unavailable)";
                    return state;
                }

                var entry = FindFirstByCaravanId(ReadCollection(entriesValue), state.SelectedCaravanId);
                if (entry == null)
                {
                    state.DisabledReason = "Selected Caravan progress not found";
                    return state;
                }

                state.EntryCaravanId = GetStringMember(entry, "caravanId");
                state.TradeId = GetStringMember(entry, "activeTradeId");
                state.State = Convert.ToString(GetMemberValue(entry, "state"));
                state.RouteId = GetStringMember(entry, "activeRouteId");
                state.Progress = GetMemberValue(entry, "progress01");

                if (string.IsNullOrWhiteSpace(state.EntryCaravanId)
                    || !IdEquals(state.EntryCaravanId, state.SelectedCaravanId))
                {
                    state.DisabledReason = "Selected entry identity mismatch";
                    return state;
                }

                if (string.IsNullOrWhiteSpace(state.TradeId))
                {
                    state.DisabledReason = "No active trade";
                    return state;
                }

                if (!string.Equals(state.State, "Traveling", StringComparison.Ordinal))
                {
                    state.DisabledReason = "Selected trade is not Traveling";
                    return state;
                }

                state.CanExecute = true;
                state.DisabledReason = string.Empty;
                return state;
            }
            catch (Exception exception)
            {
                state.CanExecute = false;
                state.DisabledReason = $"Force-arrival status unavailable: {exception.GetType().Name}";
                return state;
            }
        }

        private static MethodInfo FindForceArrivalMethod(Type debugCommandsType)
        {
            if (debugCommandsType == null)
            {
                return null;
            }

            try
            {
                var methods = debugCommandsType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                for (var index = 0; index < methods.Length; index++)
                {
                    var method = methods[index];
                    if (!string.Equals(method.Name, "TryForceCompleteTrade", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length == 2
                        && parameters[0].ParameterType == typeof(string)
                        && parameters[1].ParameterType == typeof(string))
                    {
                        return method;
                    }
                }
            }
            catch (Exception exception) when (IsReflectionAccessException(exception))
            {
                return null;
            }

            return null;
        }

        private void ExecuteForceArrival()
        {
            var state = ResolveForceArrivalState();
            if (!state.CanExecute)
            {
                lastForceArrivalResult = $"Force arrival not executed. Reason: {state.DisabledReason}.";
                RefreshSnapshot();
                return;
            }

            try
            {
                var result = state.Method.Invoke(
                    state.DebugCommands,
                    new object[] { state.EntryCaravanId, state.TradeId });
                lastForceArrivalResult = FormatForceArrivalResult(result, state.EntryCaravanId, state.TradeId);
            }
            catch (TargetInvocationException exception)
            {
                var cause = exception.InnerException ?? exception;
                lastForceArrivalResult =
                    $"Force arrival invocation failed. Reason: {cause.GetType().Name}: {cause.Message}";
            }
            catch (Exception exception) when (
                IsReflectionAccessException(exception)
                || exception is InvalidOperationException)
            {
                lastForceArrivalResult =
                    $"Force arrival invocation failed. Reason: {exception.GetType().Name}: {exception.Message}";
            }

            RefreshSnapshot();
        }

        private static string FormatForceArrivalResult(object result, string requestedCaravanId, string requestedTradeId)
        {
            if (result == null)
            {
                return "Force arrival invocation failed. Reason: command returned no result.";
            }

            var succeededValue = GetMemberValue(result, "Succeeded");
            if (!(succeededValue is bool succeeded))
            {
                return "Force arrival invocation failed. Reason: unexpected result shape.";
            }

            var caravanId = GetStringMember(result, "CaravanId");
            var tradeId = GetStringMember(result, "TradeId");
            caravanId = string.IsNullOrWhiteSpace(caravanId) ? requestedCaravanId : caravanId;
            tradeId = string.IsNullOrWhiteSpace(tradeId) ? requestedTradeId : tradeId;

            if (succeeded)
            {
                return $"Success - {FormatIdentifier(caravanId)} / {FormatIdentifier(tradeId)} is now SettlementPending.";
            }

            var builder = new StringBuilder(256);
            builder.Append($"Force arrival failed. Reason: {FormatValue(GetMemberValue(result, "FailureReason"))}");
            builder.Append($". Caravan: {FormatIdentifier(caravanId)}. Trade: {FormatIdentifier(tradeId)}");

            var saveResult = GetMemberValue(result, "SaveResult");
            if (saveResult != null)
            {
                builder.Append($". Save Succeeded: {FormatValue(GetMemberValue(saveResult, "Succeeded"))}");
                builder.Append($". Save Failure: {FormatValue(GetMemberValue(saveResult, "FailureReason"))}");
                var message = GetStringMember(saveResult, "Message");
                if (!string.IsNullOrWhiteSpace(message))
                {
                    builder.Append($". Message: {message}");
                }

                var failedDataCategory = GetMemberValue(saveResult, "FailedDataCategory");
                if (failedDataCategory != null)
                {
                    builder.Append($". Failed Data Category: {FormatValue(failedDataCategory)}");
                }
            }

            return builder.ToString();
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

        private sealed class ForceArrivalState
        {
            public object Root;
            public object DebugCommands;
            public MethodInfo Method;
            public string SelectedCaravanId;
            public string EntryCaravanId;
            public string TradeId;
            public string State;
            public string RouteId;
            public object Progress;
            public bool CanExecute;
            public string DisabledReason;
        }

        private sealed class CurrencyState
        {
            public object Root;
            public object DebugCommands;
            public MethodInfo Method;
            public long CurrentValue;
            public bool HasCurrentValue;
            public bool CanExecute;
            public string DisabledReason;
        }

        private sealed class WagonRepairMultiplierState
        {
            public object Root;
            public object DebugCommands;
            public PropertyInfo ValueProperty;
            public MethodInfo SetMethod;
            public double CurrentValue;
            public bool HasCurrentValue;
            public bool CanExecute;
            public string DisabledReason;
        }
    }
}
#endif
