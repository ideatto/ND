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
        private const int CalendarTab = 3;
        private const int TabCount = 4;
        private static readonly string[] PendingPayloadMemberNames =
            { "hasResult", "result", "settlementResult", "snapshot", "resultSnapshot" };

        [SerializeField, Tooltip("플레이 시작 시 디버그 패널을 펼친 상태로 표시할지 여부입니다.")]
        private bool visibleOnStart;

        private readonly StringBuilder textBuilder = new StringBuilder(4096);
        private readonly HomeInventoryItemDebugSection homeInventorySection = new HomeInventoryItemDebugSection();
        private readonly InGameScreenRouterDebugSection screenRouterSection = new InGameScreenRouterDebugSection();
        private readonly Vector2[] tabScrollPositions = new Vector2[TabCount];
        private Rect windowRect = new Rect(16f, 16f, 600f, 700f);
        private GUIStyle labelStyle;
        private Type frameworkRootType;
        private string snapshot = string.Empty;
        private string lastForceArrivalResult = "실행한 명령이 없습니다.";
        private string tradingCurrencyAmountInput = string.Empty;
        private string developmentCurrencyAmountInput = string.Empty;
        private string lastCurrencyResult = "실행한 재화 명령이 없습니다.";
        private string wagonRepairMultiplierInput = "1";
        private string lastWagonRepairMultiplierResult = "실행한 배율 명령이 없습니다.";
        private string calendarTargetMonthInput = string.Empty;
        private string calendarOfflineHoursInput = string.Empty;
        private string lastCalendarResult = "실행한 달력 명령이 없습니다.";
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
            windowRect = GUI.Window(DebugWindowId, windowRect, DrawWindow, "프로젝트 디버그 패널 (F12)");
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(selectedTab == StatusTab, "상태", GUI.skin.button))
            {
                selectedTab = StatusTab;
            }

            if (GUILayout.Toggle(selectedTab == HomeInventoryTab, "거점 인벤토리", GUI.skin.button))
            {
                if (selectedTab != HomeInventoryTab)
                {
                    homeInventorySection.Refresh();
                }

                selectedTab = HomeInventoryTab;
            }

            if (GUILayout.Toggle(selectedTab == ScreenRouterTab, "화면 전환", GUI.skin.button))
            {
                selectedTab = ScreenRouterTab;
            }

            if (GUILayout.Toggle(selectedTab == CalendarTab, "달력", GUI.skin.button))
            {
                selectedTab = CalendarTab;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            tabScrollPositions[selectedTab] = GUILayout.BeginScrollView(
                tabScrollPositions[selectedTab],
                false,
                true,
                GUILayout.ExpandHeight(true));

            if (selectedTab == CalendarTab)
            {
                DrawCalendarTab();
            }
            else if (selectedTab == HomeInventoryTab)
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
            GUILayout.Label("[선택한 이동 중 무역 - 즉시 도착 처리]", labelStyle);
            GUILayout.Label($"선택한 Caravan ID: {FormatIdentifier(state.SelectedCaravanId)}", labelStyle);
            GUILayout.Label($"진행 항목 Caravan ID: {FormatIdentifier(state.EntryCaravanId)}", labelStyle);
            GUILayout.Label($"진행 중인 무역 ID: {FormatIdentifier(state.TradeId)}", labelStyle);
            GUILayout.Label($"상태: {FormatIdentifier(state.State)}", labelStyle);
            GUILayout.Label($"경로 ID: {FormatIdentifier(state.RouteId)}", labelStyle);
            GUILayout.Label($"진행률: {FormatProgress(state.Progress)}", labelStyle);
            GUILayout.Label($"사용 가능 여부: {(state.CanExecute ? "사용 가능" : state.DisabledReason)}", labelStyle);
            GUILayout.Label("선택한 Traveling 무역만 도착 상태로 변경하는 디버그 명령입니다.", labelStyle);
            GUILayout.Label("화물 판매, 보상 수령, 무역 완료는 수행하지 않습니다.", labelStyle);
            GUILayout.Label("실행 시 caravanId와 tradeId를 다시 검증합니다.", labelStyle);

            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && state.CanExecute;
            var clicked = GUILayout.Button("선택한 무역 즉시 도착 처리");
            GUI.enabled = previousEnabled;

            if (clicked)
            {
                ExecuteForceArrival();
            }

            GUILayout.Label($"최근 실행 결과: {lastForceArrivalResult}", labelStyle);
        }

        private void DrawCurrencyControls()
        {
            var tradingState = ResolveCurrencyState("TryAddTradingCurrency", "tradingCurrency");
            var developmentState = ResolveCurrencyState("TryAddDevelopmentCurrency", "developmentCurrency");

            GUILayout.Space(8f);
            GUILayout.Label("[재화 조정]", labelStyle);
            DrawCurrencyControl("거래 재화", true, tradingState, ref tradingCurrencyAmountInput);
            GUILayout.Space(4f);
            DrawCurrencyControl("발전 재화", false, developmentState, ref developmentCurrencyAmountInput);
            GUILayout.Label($"최근 재화 실행 결과:\n{lastCurrencyResult}", labelStyle);
        }

        private void DrawCurrencyControl(
            string displayName,
            bool isTradingCurrency,
            CurrencyState state,
            ref string amountInput)
        {
            GUILayout.Label(displayName, labelStyle);
            GUILayout.Label($"현재 값: {FormatCurrency(state)}", labelStyle);
            GUILayout.Label($"사용 가능 여부: {(state.CanExecute ? "사용 가능" : state.DisabledReason)}", labelStyle);

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
            GUILayout.Label("직접 입력:", labelStyle, GUILayout.Width(72f));
            amountInput = GUILayout.TextField(amountInput);
            GUI.enabled = previousEnabled && state.CanExecute;
            var addCustom = GUILayout.Button("추가", GUILayout.Width(64f));
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
            GUILayout.Label("[마차 수리 비용 배율]", labelStyle);
            GUILayout.Label(
                $"현재 값: {(state.HasCurrentValue ? state.CurrentValue.ToString("0.##", CultureInfo.InvariantCulture) + "x" : "N/A")}",
                labelStyle);
            GUILayout.Label($"사용 가능 여부: {(state.CanExecute ? "사용 가능" : state.DisabledReason)}", labelStyle);
            GUILayout.Label("세션에만 적용됩니다. 실제 수리 계산에는 아직 연결되지 않았습니다.", labelStyle);

            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && state.CanExecute;
            GUILayout.BeginHorizontal();
            var decrease = GUILayout.Button("-0.25x");
            var increase = GUILayout.Button("+0.25x");
            var reset = GUILayout.Button("1x 초기화");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("직접 입력:", labelStyle, GUILayout.Width(72f));
            wagonRepairMultiplierInput = GUILayout.TextField(wagonRepairMultiplierInput);
            var apply = GUILayout.Button("적용", GUILayout.Width(64f));
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

            GUILayout.Label($"최근 배율 실행 결과: {lastWagonRepairMultiplierResult}", labelStyle);
        }

        private WagonRepairMultiplierState ResolveWagonRepairMultiplierState()
        {
            var state = new WagonRepairMultiplierState();

            try
            {
                state.Root = GetFrameworkRoot();
                if (state.Root == null)
                {
                    state.DisabledReason = "Framework를 사용할 수 없음";
                    return state;
                }

                state.DebugCommands = GetMemberValue(state.Root, "DebugCommands");
                if (state.DebugCommands == null)
                {
                    state.DisabledReason = "DebugCommands를 사용할 수 없음";
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
                    state.DisabledReason = "마차 수리 비용 배율 API를 사용할 수 없음";
                    return state;
                }

                var value = state.ValueProperty.GetValue(state.DebugCommands);
                if (!(value is double currentValue))
                {
                    state.DisabledReason = "마차 수리 비용 배율 값을 사용할 수 없음";
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
                state.DisabledReason = $"마차 수리 비용 배율을 사용할 수 없음: {exception.GetType().Name}";
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
                lastWagonRepairMultiplierResult = "적용하지 않았습니다. 0부터 1000 사이의 숫자를 입력하세요.";
                return;
            }

            var state = ResolveWagonRepairMultiplierState();
            if (!state.CanExecute)
            {
                lastWagonRepairMultiplierResult = $"적용하지 않았습니다. {state.DisabledReason}.";
                return;
            }

            try
            {
                var applied = state.SetMethod.Invoke(state.DebugCommands, new object[] { multiplier });
                lastWagonRepairMultiplierResult = applied is bool succeeded && succeeded
                    ? $"{multiplier:0.##}x를 적용했습니다."
                    : "적용하지 않았습니다. 0부터 1000 사이의 숫자를 입력하세요.";
            }
            catch (Exception exception)
            {
                var cause = exception is TargetInvocationException invocation && invocation.InnerException != null
                    ? invocation.InnerException
                    : exception;
                lastWagonRepairMultiplierResult = $"명령 실행 실패: {cause.GetType().Name}: {cause.Message}";
            }
        }

        private void ExecuteWagonRepairMultiplierCommand(string methodName)
        {
            var state = ResolveWagonRepairMultiplierState();
            if (!state.CanExecute)
            {
                lastWagonRepairMultiplierResult = $"실행하지 않았습니다. {state.DisabledReason}.";
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
                    lastWagonRepairMultiplierResult = "배율 명령 API를 사용할 수 없습니다.";
                    return;
                }

                var result = method.Invoke(state.DebugCommands, null);
                lastWagonRepairMultiplierResult = result is double multiplier
                    ? $"{multiplier:0.##}x를 적용했습니다."
                    : "배율 명령이 값을 반환하지 않았습니다.";
            }
            catch (Exception exception)
            {
                var cause = exception is TargetInvocationException invocation && invocation.InnerException != null
                    ? invocation.InnerException
                    : exception;
                lastWagonRepairMultiplierResult = $"명령 실행 실패: {cause.GetType().Name}: {cause.Message}";
            }
        }

        private void DrawCalendarTab()
        {
            var state = ResolveCalendarState();

            GUILayout.Label("[달력 상태]", labelStyle);
            GUILayout.Label($"초기화 상태: {(state.HasCurrent ? "완료" : "사용 불가")}", labelStyle);
            if (state.HasCurrent)
            {
                GUILayout.Label($"현재 날짜: {FormatCalendarDate(state.Snapshot)}", labelStyle);
                GUILayout.Label($"현재 계절: {FormatLocalizedCalendarValue(GetStringMember(state.Snapshot, "SeasonId"), true)}", labelStyle);
                GUILayout.Label($"현재 재난: {FormatLocalizedCalendarValue(GetStringMember(state.Snapshot, "ActiveDisasterId"), false)}", labelStyle);
                GUILayout.Label($"누적 경과 일수: {FormatValue(GetMemberValue(state.Snapshot, "TotalElapsedDays"))}일", labelStyle);
            }
            else
            {
                GUILayout.Label("달력 서비스를 사용할 수 없습니다.", labelStyle);
            }
            GUILayout.Label($"달력 디버그 배속: {(state.HasDebugScale ? state.DebugScale.ToString("0.##", CultureInfo.InvariantCulture) + "x" : "N/A")}", labelStyle);

            GUILayout.Space(8f);
            GUILayout.Label("[달력 배속]", labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("0x 정지")) SetCalendarDebugScale(0f);
            if (GUILayout.Button("1x 기본")) SetCalendarDebugScale(1f);
            if (GUILayout.Button("2x")) SetCalendarDebugScale(2f);
            if (GUILayout.Button("4x")) SetCalendarDebugScale(4f);
            GUILayout.EndHorizontal();
            GUILayout.Label("배속은 온라인 달력 진행에만 적용되며 세션에 저장되지 않습니다.", labelStyle);

            GUILayout.Space(8f);
            GUILayout.Label("[날짜 진행]", labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1일 진행")) ExecuteCalendarAdvance("AdvanceOneGameDay", "달력을 1일 진행했습니다.");
            if (GUILayout.Button("30일 진행")) ExecuteCalendarAdvance("AdvanceOneGameMonth", "달력을 30일 진행했습니다.");
            GUILayout.EndHorizontal();
            GUILayout.Label("게임의 한 달은 30일이며 현재 날짜를 기준으로 진행합니다.", labelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("목표 월", labelStyle, GUILayout.Width(64f));
            calendarTargetMonthInput = GUILayout.TextField(calendarTargetMonthInput, GUILayout.Width(90f));
            if (GUILayout.Button("다음 해당 월까지 진행")) ExecuteAdvanceToMonth();
            GUILayout.EndHorizontal();
            GUILayout.Label("현재 날짜보다 앞으로 진행하여 다음에 도달하는 해당 월로 이동합니다.", labelStyle);

            GUILayout.Space(8f);
            GUILayout.Label("[달력 오프라인 시뮬레이션]", labelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("현실 경과 시간", labelStyle, GUILayout.Width(100f));
            calendarOfflineHoursInput = GUILayout.TextField(calendarOfflineHoursInput, GUILayout.Width(100f));
            GUILayout.Label("시간", labelStyle, GUILayout.Width(36f));
            GUILayout.EndHorizontal();
            if (GUILayout.Button("달력 오프라인 시간 적용")) ExecuteCalendarOfflineSimulation();
            GUILayout.Label("이 기능은 달력만 진행합니다. 무역 진행, 무역 도착 시간, Unity 배속에는 영향을 주지 않습니다.", labelStyle);
            GUILayout.Label("Framework 정책에 따라 최대 72시간까지만 반영합니다.", labelStyle);

            GUILayout.Space(8f);
            GUILayout.Label("[진단]", labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("새로고침")) RefreshSnapshot();
            if (GUILayout.Button("현재 달력 상태 로그")) ExecuteCalendarLog("LogCalendarState", false);
            if (GUILayout.Button("최근 복구 타임라인 로그")) ExecuteCalendarLog("LogCalendarRestoreTimeline", true);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("[최근 실행 결과]", labelStyle);
            GUILayout.Label(lastCalendarResult, labelStyle);
        }

        private CalendarState ResolveCalendarState()
        {
            var state = new CalendarState();
            try
            {
                state.Root = GetFrameworkRoot();
                if (state.Root == null) return state;
                state.DebugCommands = GetMemberValue(state.Root, "DebugCommands");
                state.Calendar = GetMemberValue(state.Root, "GameCalendar");
                if (state.Calendar == null) return state;
                state.HasCurrent = ToBool(GetMemberValue(state.Calendar, "HasCurrent"));
                if (state.HasCurrent) state.Snapshot = GetMemberValue(state.Calendar, "Current");
                var scale = GetMemberValue(state.Calendar, "DebugScale");
                if (scale is float floatScale)
                {
                    state.DebugScale = floatScale;
                    state.HasDebugScale = true;
                }
            }
            catch (Exception exception)
            {
                state.Error = exception.GetType().Name;
            }
            return state;
        }

        private void SetCalendarDebugScale(float scale)
        {
            var state = ResolveCalendarState();
            var method = FindInstanceMethod(state.DebugCommands, "TrySetCalendarDebugScale", typeof(float));
            if (method == null)
            {
                lastCalendarResult = "달력 배속 API를 사용할 수 없습니다.";
                return;
            }
            try
            {
                var result = method.Invoke(state.DebugCommands, new object[] { scale });
                lastCalendarResult = result is bool succeeded && succeeded
                    ? $"달력 배속을 {scale:0.##}x로 변경했습니다."
                    : "달력 배속 변경에 실패했습니다.";
            }
            catch (Exception exception)
            {
                lastCalendarResult = $"달력 배속 명령 실행에 실패했습니다. {GetInvocationCause(exception)}";
            }
            RefreshSnapshot();
        }

        private void ExecuteCalendarAdvance(string methodName, string successAction)
        {
            var state = ResolveCalendarState();
            var method = FindInstanceMethod(state.DebugCommands, methodName);
            if (method == null)
            {
                lastCalendarResult = "달력 진행 API를 사용할 수 없습니다.";
                return;
            }
            try
            {
                lastCalendarResult = FormatCalendarAdvanceResult(method.Invoke(state.DebugCommands, null), successAction);
            }
            catch (Exception exception)
            {
                lastCalendarResult = $"달력 진행 명령 실행에 실패했습니다. {GetInvocationCause(exception)}";
            }
            RefreshSnapshot();
        }

        private void ExecuteAdvanceToMonth()
        {
            var input = calendarTargetMonthInput?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                lastCalendarResult = "목표 월을 입력하세요.";
                return;
            }
            if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var month))
            {
                lastCalendarResult = "목표 월은 숫자로 입력해야 합니다.";
                return;
            }
            if (month < 1 || month > 12)
            {
                lastCalendarResult = "목표 월은 1부터 12 사이여야 합니다.";
                return;
            }
            var state = ResolveCalendarState();
            var method = FindInstanceMethod(state.DebugCommands, "AdvanceToMonth", typeof(int));
            if (method == null)
            {
                lastCalendarResult = "목표 월 진행 API를 사용할 수 없습니다.";
                return;
            }
            try
            {
                lastCalendarResult = FormatCalendarAdvanceResult(
                    method.Invoke(state.DebugCommands, new object[] { month }),
                    $"다음 {month}월까지 달력을 진행했습니다.");
            }
            catch (Exception exception)
            {
                lastCalendarResult = $"목표 월 진행 명령 실행에 실패했습니다. {GetInvocationCause(exception)}";
            }
            RefreshSnapshot();
        }

        private void ExecuteCalendarOfflineSimulation()
        {
            var input = calendarOfflineHoursInput?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                lastCalendarResult = "경과 시간을 입력하세요.";
                return;
            }
            if (!double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var hours))
            {
                lastCalendarResult = "경과 시간은 숫자로 입력해야 합니다.";
                return;
            }
            if (double.IsNaN(hours) || double.IsInfinity(hours) || hours <= 0d)
            {
                lastCalendarResult = "경과 시간은 0보다 커야 합니다.";
                return;
            }
            var seconds = hours * 3600d;
            if (double.IsInfinity(seconds) || double.IsNaN(seconds))
            {
                lastCalendarResult = "경과 시간이 너무 큽니다.";
                return;
            }
            var state = ResolveCalendarState();
            var method = FindInstanceMethod(state.DebugCommands, "SimulateCalendarOffline", typeof(double));
            if (method == null)
            {
                lastCalendarResult = "달력 오프라인 시뮬레이션 API를 사용할 수 없습니다.";
                return;
            }
            try
            {
                var result = method.Invoke(state.DebugCommands, new object[] { seconds });
                lastCalendarResult = result == null
                    ? "달력 오프라인 시뮬레이션을 실행하지 못했습니다. 입력값과 달력 초기화 상태를 확인하세요."
                    : $"달력 오프라인 경과 시간을 적용했습니다.\n요청: {hours:0.##}시간\n현재 날짜: {FormatCalendarDate(GetMemberValue(result, "Current"))}";
            }
            catch (Exception exception)
            {
                lastCalendarResult = $"달력 오프라인 시뮬레이션 실행에 실패했습니다. {GetInvocationCause(exception)}";
            }
            RefreshSnapshot();
        }

        private void ExecuteCalendarLog(string methodName, bool isTimeline)
        {
            var state = ResolveCalendarState();
            var method = FindCalendarLogMethod(state.DebugCommands, methodName, isTimeline);
            if (method == null)
            {
                lastCalendarResult = "달력 로그 명령을 사용할 수 없습니다.";
                return;
            }
            try
            {
                method.Invoke(state.DebugCommands, isTimeline ? new object[] { null } : null);
                lastCalendarResult = isTimeline
                    ? "복구 타임라인 로그 출력을 요청했습니다. 세부 결과는 Console에서 확인하세요."
                    : "현재 달력 상태를 Console에 출력했습니다.";
            }
            catch (Exception exception)
            {
                lastCalendarResult = $"달력 로그 명령 실행에 실패했습니다. {GetInvocationCause(exception)}";
            }
        }

        private static string FormatCalendarAdvanceResult(object result, string successAction)
        {
            if (result == null || !(GetMemberValue(result, "Changed") is bool changed))
                return "달력 진행 결과를 확인할 수 없습니다.";
            var saveResult = GetMemberValue(result, "SaveResult");
            if (saveResult != null && GetMemberValue(saveResult, "Succeeded") is bool saved && !saved)
            {
                var builder = new StringBuilder("달력 진행 저장에 실패했습니다.\n변경 사항은 이전 상태로 복구되었습니다.");
                AppendResultMember(builder, "실패 분류", saveResult, "FailedDataCategory");
                AppendResultMember(builder, "실패 코드", saveResult, "FailureReason");
                return builder.ToString();
            }
            if (!changed || saveResult == null)
                return "달력 상태가 변경되지 않았습니다. 달력이 아직 초기화되지 않았는지 확인하세요.";
            if (!(GetMemberValue(saveResult, "Succeeded") is bool succeeded) || !succeeded)
                return "달력 진행 저장 결과를 확인할 수 없습니다.";
            return $"{successAction}\n현재 날짜: {FormatCalendarDate(GetMemberValue(result, "Current"))}";
        }

        private static string FormatCalendarDate(object snapshotValue)
        {
            if (snapshotValue == null) return "N/A";
            return $"{FormatValue(GetMemberValue(snapshotValue, "Year"))}년 {FormatValue(GetMemberValue(snapshotValue, "Month"))}월 {FormatValue(GetMemberValue(snapshotValue, "Day"))}일";
        }

        private static string FormatLocalizedCalendarValue(string rawValue, bool season)
        {
            if (string.IsNullOrWhiteSpace(rawValue)) return "없음";
            string localized = null;
            if (season)
            {
                if (rawValue == "winter") localized = "겨울";
                else if (rawValue == "spring") localized = "봄";
                else if (rawValue == "summer") localized = "여름";
                else if (rawValue == "autumn") localized = "가을";
            }
            else if (rawValue == "flood") localized = "홍수";
            return localized == null ? rawValue : $"{localized} ({rawValue})";
        }

        private static MethodInfo FindInstanceMethod(object target, string methodName, params Type[] parameterTypes)
        {
            if (target == null) return null;
            try
            {
                return target.GetType().GetMethod(
                    methodName, BindingFlags.Public | BindingFlags.Instance, null, parameterTypes, null);
            }
            catch (Exception exception) when (IsReflectionAccessException(exception))
            {
                return null;
            }
        }

        private static MethodInfo FindCalendarLogMethod(object target, string methodName, bool acceptsOptionalResult)
        {
            if (target == null) return null;
            try
            {
                var methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
                for (var index = 0; index < methods.Length; index++)
                {
                    if (!string.Equals(methods[index].Name, methodName, StringComparison.Ordinal)) continue;
                    var parameters = methods[index].GetParameters();
                    if ((!acceptsOptionalResult && parameters.Length == 0)
                        || (acceptsOptionalResult && parameters.Length == 1 && parameters[0].IsOptional))
                        return methods[index];
                }
            }
            catch (Exception exception) when (IsReflectionAccessException(exception))
            {
                return null;
            }
            return null;
        }

        private static string GetInvocationCause(Exception exception)
        {
            var cause = exception is TargetInvocationException invocation && invocation.InnerException != null
                ? invocation.InnerException
                : exception;
            return $"{cause.GetType().Name}: {cause.Message}";
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            RefreshSnapshot();
        }

        private void RefreshSnapshot()
        {
            nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;
            textBuilder.Clear();
            textBuilder.AppendLine("[Framework 상태]");
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

                textBuilder.AppendLine($"FrameworkRoot 초기화: {FormatBool(root != null && coordinator != null && router != null)}");
                textBuilder.AppendLine($"저장 데이터: {FormatBool(saveData != null)}");
                textBuilder.AppendLine($"화면: {FormatValue(GetMemberValue(router, "CurrentScreenState"))}");
                textBuilder.AppendLine($"거래 재화: {FormatValue(GetMemberValue(player, "tradingCurrency"))}");
                textBuilder.AppendLine($"발전 재화: {FormatValue(GetMemberValue(player, "developmentCurrency"))}");
                textBuilder.AppendLine($"공용 게임 데이터 로드: {FormatBool(ToBool(GetMemberValue(sharedData, "IsLoaded")))}");
                textBuilder.AppendLine($"  마을: {FormatValue(GetMemberValue(sharedData, "TownCount"))}");
                textBuilder.AppendLine($"  시장: {FormatValue(GetMemberValue(sharedData, "MarketCount"))}");
                textBuilder.AppendLine($"  거래 아이템: {FormatValue(GetMemberValue(sharedData, "TradeItemCount"))}");
                textBuilder.AppendLine($"  마차: {FormatValue(GetMemberValue(sharedData, "WagonCount"))}");
                textBuilder.AppendLine($"  역축: {FormatValue(GetMemberValue(sharedData, "DraftAnimalCount"))}");
                textBuilder.AppendLine($"  경로: {FormatValue(GetMemberValue(sharedData, "RouteCount"))}");

                AppendSelectedCaravan(saveData, selectedCaravanId, caravans, tradeEntries, pendingEntries);
                AppendAllCaravans(selectedCaravanId, caravans, tradeEntries, pendingEntries);
                AppendUnmatchedTrades(caravans, tradeEntries);
                AppendPendingSettlements(caravans, tradeEntries, pendingEntries);
            }
            catch (Exception exception)
            {
                textBuilder.AppendLine($"상태 조회 실패: {exception.GetType().Name}");
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
                    state.DisabledReason = "Framework를 사용할 수 없음";
                    return state;
                }

                state.DebugCommands = GetMemberValue(state.Root, "DebugCommands");
                if (state.DebugCommands == null)
                {
                    state.DisabledReason = "DebugCommands를 사용할 수 없음";
                    return state;
                }

                state.Method = FindCurrencyMethod(state.DebugCommands.GetType(), methodName);
                if (state.Method == null)
                {
                    state.DisabledReason = "재화 API를 사용할 수 없음";
                    return state;
                }

                var saveData = GetMemberValue(state.Root, "CurrentSaveData");
                if (saveData == null)
                {
                    state.DisabledReason = "현재 SaveData를 사용할 수 없음";
                    return state;
                }

                var player = GetMemberValue(saveData, "player");
                if (player == null)
                {
                    state.DisabledReason = "플레이어 데이터를 사용할 수 없음";
                    return state;
                }

                if (!TryGetMemberValue(player, currencyMemberName, out var value) || !(value is long currentValue))
                {
                    state.DisabledReason = "재화 값을 사용할 수 없음";
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
                state.DisabledReason = $"재화 상태를 사용할 수 없음: {exception.GetType().Name}";
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
                lastCurrencyResult = "재화 추가를 실행하지 않았습니다.\n사유: 양의 정수를 입력하세요.";
                return;
            }

            if (!long.TryParse(trimmedInput, NumberStyles.None, CultureInfo.InvariantCulture, out var amount))
            {
                lastCurrencyResult = ContainsOnlyAsciiDigits(trimmedInput)
                    ? "재화 추가를 실행하지 않았습니다.\n사유: 값이 너무 크거나 양의 정수가 아닙니다."
                    : "재화 추가를 실행하지 않았습니다.\n사유: 양의 정수를 입력하세요.";
                return;
            }

            if (amount <= 0L)
            {
                lastCurrencyResult = "재화 추가를 실행하지 않았습니다.\n사유: 양의 정수를 입력하세요.";
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
            var displayName = isTradingCurrency ? "거래 재화" : "발전 재화";
            var methodName = isTradingCurrency ? "TryAddTradingCurrency" : "TryAddDevelopmentCurrency";
            var memberName = isTradingCurrency ? "tradingCurrency" : "developmentCurrency";
            var state = ResolveCurrencyState(methodName, memberName);

            if (amount <= 0L)
            {
                lastCurrencyResult = $"{displayName} 추가를 실행하지 않았습니다.\n요청값: {amount}\n사유: 양의 정수를 입력하세요.";
                return;
            }

            if (!state.CanExecute)
            {
                lastCurrencyResult =
                    $"{displayName} 추가 명령 실행에 실패했습니다.\n요청값: {amount:N0}\n사유: {state.DisabledReason}.";
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
                    $"{displayName} 추가 명령 실행에 실패했습니다.\n요청값: {amount:N0}\n사유: {cause.GetType().Name}: {cause.Message}";
            }
            catch (Exception exception) when (
                IsReflectionAccessException(exception)
                || exception is InvalidOperationException)
            {
                lastCurrencyResult =
                    $"{displayName} 추가 명령 실행에 실패했습니다.\n요청값: {amount:N0}\n사유: {exception.GetType().Name}: {exception.Message}";
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
                return $"{displayName} 추가 명령 실행에 실패했습니다.\n요청값: {requestedAmount:N0}\n사유: 명령 결과가 없습니다.";
            }

            if (!(GetMemberValue(result, "Succeeded") is bool succeeded))
            {
                return $"{displayName} 추가 명령 실행에 실패했습니다.\n요청값: {requestedAmount:N0}\n사유: 예상하지 못한 결과 형식입니다.";
            }

            var builder = new StringBuilder(256);
            if (succeeded)
            {
                builder.AppendLine($"{displayName}를 추가했습니다.");
                builder.AppendLine($"추가량: {requestedAmount:N0}");
                builder.Append($"현재 값: {(refreshedState.HasCurrentValue ? refreshedState.CurrentValue.ToString("N0", CultureInfo.InvariantCulture) : "N/A")}");
                return builder.ToString();
            }

            builder.AppendLine($"{displayName} 추가에 실패했습니다.");
            builder.AppendLine($"요청값: {requestedAmount:N0}");
            AppendResultMember(builder, "실패 분류", result, "FailedDataCategory");
            AppendResultMember(builder, "실패 코드", result, "FailureReason");
            AppendResultMember(builder, "메시지", result, "Message");
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
                    state.DisabledReason = "Framework를 사용할 수 없음";
                    return state;
                }

                state.DebugCommands = GetMemberValue(state.Root, "DebugCommands");
                if (state.DebugCommands == null)
                {
                    state.DisabledReason = "DebugCommands를 사용할 수 없음";
                    return state;
                }

                state.Method = FindForceArrivalMethod(state.DebugCommands.GetType());
                if (state.Method == null)
                {
                    state.DisabledReason = "정확한 즉시 도착 API를 사용할 수 없음";
                    return state;
                }

                var saveData = GetMemberValue(state.Root, "CurrentSaveData");
                if (saveData == null)
                {
                    state.DisabledReason = "현재 SaveData를 사용할 수 없음";
                    return state;
                }

                state.SelectedCaravanId = GetStringMember(saveData, "selectedCaravanId");
                if (string.IsNullOrWhiteSpace(state.SelectedCaravanId))
                {
                    state.DisabledReason = "선택한 Caravan 없음";
                    return state;
                }

                if (!TryGetMemberValue(saveData, "tradeProgressEntries", out var entriesValue))
                {
                    state.DisabledReason = "선택한 Caravan 진행 정보를 찾지 못함 (정확한 항목 목록 사용 불가)";
                    return state;
                }

                var entry = FindFirstByCaravanId(ReadCollection(entriesValue), state.SelectedCaravanId);
                if (entry == null)
                {
                    state.DisabledReason = "선택한 Caravan 진행 정보를 찾지 못함";
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
                    state.DisabledReason = "선택 항목 식별자가 일치하지 않음";
                    return state;
                }

                if (string.IsNullOrWhiteSpace(state.TradeId))
                {
                    state.DisabledReason = "진행 중인 무역 없음";
                    return state;
                }

                if (!string.Equals(state.State, "Traveling", StringComparison.Ordinal))
                {
                    state.DisabledReason = "선택한 무역이 Traveling 상태가 아님";
                    return state;
                }

                state.CanExecute = true;
                state.DisabledReason = string.Empty;
                return state;
            }
            catch (Exception exception)
            {
                state.CanExecute = false;
                state.DisabledReason = $"즉시 도착 상태를 사용할 수 없음: {exception.GetType().Name}";
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
                lastForceArrivalResult = $"즉시 도착 처리를 실행하지 않았습니다. 사유: {state.DisabledReason}.";
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
                    $"즉시 도착 명령 실행에 실패했습니다. 사유: {cause.GetType().Name}: {cause.Message}";
            }
            catch (Exception exception) when (
                IsReflectionAccessException(exception)
                || exception is InvalidOperationException)
            {
                lastForceArrivalResult =
                    $"즉시 도착 명령 실행에 실패했습니다. 사유: {exception.GetType().Name}: {exception.Message}";
            }

            RefreshSnapshot();
        }

        private static string FormatForceArrivalResult(object result, string requestedCaravanId, string requestedTradeId)
        {
            if (result == null)
            {
                return "즉시 도착 명령 실행에 실패했습니다. 사유: 명령 결과가 없습니다.";
            }

            var succeededValue = GetMemberValue(result, "Succeeded");
            if (!(succeededValue is bool succeeded))
            {
                return "즉시 도착 명령 실행에 실패했습니다. 사유: 예상하지 못한 결과 형식입니다.";
            }

            var caravanId = GetStringMember(result, "CaravanId");
            var tradeId = GetStringMember(result, "TradeId");
            caravanId = string.IsNullOrWhiteSpace(caravanId) ? requestedCaravanId : caravanId;
            tradeId = string.IsNullOrWhiteSpace(tradeId) ? requestedTradeId : tradeId;

            if (succeeded)
            {
                return $"성공: {FormatIdentifier(caravanId)} / {FormatIdentifier(tradeId)}가 SettlementPending 상태가 되었습니다.";
            }

            var builder = new StringBuilder(256);
            builder.Append($"즉시 도착 처리 실패. 사유: {FormatValue(GetMemberValue(result, "FailureReason"))}");
            builder.Append($". Caravan: {FormatIdentifier(caravanId)}. 무역: {FormatIdentifier(tradeId)}");

            var saveResult = GetMemberValue(result, "SaveResult");
            if (saveResult != null)
            {
                builder.Append($". 저장 성공: {FormatValue(GetMemberValue(saveResult, "Succeeded"))}");
                builder.Append($". 저장 실패 코드: {FormatValue(GetMemberValue(saveResult, "FailureReason"))}");
                var message = GetStringMember(saveResult, "Message");
                if (!string.IsNullOrWhiteSpace(message))
                {
                    builder.Append($". 메시지: {message}");
                }

                var failedDataCategory = GetMemberValue(saveResult, "FailedDataCategory");
                if (failedDataCategory != null)
                {
                    builder.Append($". 실패 데이터 분류: {FormatValue(failedDataCategory)}");
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
            textBuilder.AppendLine("[선택한 Caravan]");
            textBuilder.AppendLine($"선택한 Caravan ID: {FormatIdentifier(selectedCaravanId)}");

            var caravan = GetMemberValue(saveData, "caravan");
            if (caravan == null)
            {
                caravan = FindFirstByCaravanId(caravans, selectedCaravanId);
            }

            if (!string.IsNullOrEmpty(selectedCaravanId) && caravan == null)
            {
                textBuilder.AppendLine("선택한 Caravan 항목: 없음");
            }

            textBuilder.AppendLine($"Caravan ID: {FormatValue(GetMemberValue(caravan, "caravanId"))}");
            textBuilder.AppendLine($"슬롯 인덱스: {FormatValue(GetMemberValue(caravan, "slotIndex"))}");
            textBuilder.AppendLine($"이동 상태: {FormatValue(GetMemberValue(caravan, "state"))}");
            textBuilder.AppendLine($"Caravan 진행률: {FormatProgress(GetMemberValue(caravan, "progress01"))}");

            var trade = GetMemberValue(saveData, "tradeProgress");
            if (trade == null)
            {
                trade = FindFirstByCaravanId(tradeEntries, selectedCaravanId);
            }

            if (trade == null)
            {
                textBuilder.AppendLine("무역 진행 상태: 없음");
                textBuilder.AppendLine("진행 중인 무역 ID: N/A");
                textBuilder.AppendLine("진행 중인 경로 ID: N/A");
                textBuilder.AppendLine("무역 시작 UTC: N/A");
                textBuilder.AppendLine("예상 무역 종료 UTC: N/A");
                textBuilder.AppendLine("정산 대기: 아니요");
                return;
            }

            textBuilder.AppendLine($"무역 진행 상태: {FormatValue(GetMemberValue(trade, "state"))}");
            textBuilder.AppendLine($"진행 중인 무역 ID: {FormatValue(GetMemberValue(trade, "activeTradeId"))}");
            textBuilder.AppendLine($"진행 중인 경로 ID: {FormatValue(GetMemberValue(trade, "activeRouteId"))}");
            textBuilder.AppendLine($"무역 시작 UTC: {FormatUtcTicks(GetMemberValue(trade, "tradeStartUtcTick"))}");
            textBuilder.AppendLine($"예상 무역 종료 UTC: {FormatUtcTicks(GetMemberValue(trade, "expectedTradeEndUtcTick"))}");
            textBuilder.AppendLine($"정산 대기: {FormatBool(HasMatchingPending(trade, pendingEntries))}");
        }

        private void AppendAllCaravans(
            string selectedCaravanId,
            List<object> caravans,
            List<object> tradeEntries,
            List<object> pendingEntries)
        {
            textBuilder.AppendLine();
            textBuilder.AppendLine($"[전체 Caravans] 개수: {caravans.Count}");
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
                textBuilder.AppendLine($"  슬롯: {FormatValue(GetMemberValue(caravan, "slotIndex"))}");
                textBuilder.AppendLine($"  이동 상태: {FormatValue(GetMemberValue(caravan, "state"))}");
                textBuilder.AppendLine($"  진행률: {FormatProgress(GetMemberValue(caravan, "progress01"))}");

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
                    textBuilder.AppendLine("  무역: 없음");
                    textBuilder.AppendLine("  정산 대기: 아니요");
                }

                textBuilder.AppendLine("  런타임: 지연 상태 (저장 데이터 표시)");
            }
        }

        private void AppendTradeLine(object trade, List<object> pendingEntries, int matchIndex)
        {
                var duplicateMarker = matchIndex > 0 ? $" [중복 일치 {matchIndex + 1}]" : string.Empty;
            textBuilder.AppendLine(
                $"  무역{duplicateMarker}: {FormatValue(GetMemberValue(trade, "activeTradeId"))} / {FormatValue(GetMemberValue(trade, "state"))}");
            textBuilder.AppendLine($"  경로: {FormatValue(GetMemberValue(trade, "activeRouteId"))}");
            textBuilder.AppendLine($"  시작 UTC: {FormatUtcTicks(GetMemberValue(trade, "tradeStartUtcTick"))}");
            textBuilder.AppendLine($"  종료 UTC: {FormatUtcTicks(GetMemberValue(trade, "expectedTradeEndUtcTick"))}");
            textBuilder.AppendLine($"  정산 대기: {FormatBool(HasMatchingPending(trade, pendingEntries))}");
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
                    textBuilder.AppendLine("[일치하지 않는 무역 항목]");
                }

                textBuilder.AppendLine(
                    $"- [{index}] Caravan: {FormatIdentifier(caravanId)}, 무역: {FormatValue(GetMemberValue(trade, "activeTradeId"))} (Caravan 없음)");
                unmatchedCount++;
            }

            if (unmatchedCount > 0)
            {
                textBuilder.AppendLine($"개수: {unmatchedCount}");
            }
        }

        private void AppendPendingSettlements(
            List<object> caravans,
            List<object> tradeEntries,
            List<object> pendingEntries)
        {
            textBuilder.AppendLine();
            textBuilder.AppendLine($"[정산 대기] 개수: {pendingEntries.Count}");
            for (var index = 0; index < pendingEntries.Count; index++)
            {
                var pending = pendingEntries[index];
                if (pending == null)
                {
                    textBuilder.AppendLine($"- [{index}] <null 항목> (참조 없음)");
                    continue;
                }

                var caravanId = GetStringMember(pending, "caravanId");
                var tradeId = GetStringMember(pending, "tradeId");
                var caravanFound = ContainsCaravanId(caravans, caravanId);
                var tradeFound = ContainsTradeIdentity(tradeEntries, caravanId, tradeId);
                var referenceState = caravanFound && tradeFound
                    ? string.Empty
                    : $" ({(caravanFound ? string.Empty : "Caravan 없음")}{(!caravanFound && !tradeFound ? ", " : string.Empty)}{(tradeFound ? string.Empty : "무역 없음")})";

                textBuilder.AppendLine($"- [{index}] {FormatIdentifier(caravanId)} + {FormatIdentifier(tradeId)}{referenceState}");
                textBuilder.AppendLine($"  결과/스냅샷: {FormatPendingPayloadState(pending)}");
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
                    return hasResult ? "사용 가능" : "없음";
                }

                if (value != null)
                {
                    return "사용 가능";
                }
            }

            return foundMember ? "없음" : "알 수 없음";
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
            return value ? "예" : "아니요";
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
                return "유효하지 않음";
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

        private sealed class CalendarState
        {
            public object Root;
            public object DebugCommands;
            public object Calendar;
            public object Snapshot;
            public float DebugScale;
            public bool HasCurrent;
            public bool HasDebugScale;
            public string Error;
        }
    }
}
#endif
