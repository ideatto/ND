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
 */
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
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

        [SerializeField, Tooltip("플레이 시작 시 디버그 패널을 펼친 상태로 표시할지 여부입니다.")]
        private bool visibleOnStart;

        private readonly StringBuilder textBuilder = new StringBuilder(1024);
        private Rect windowRect = new Rect(16f, 16f, 470f, 510f);
        private GUIStyle labelStyle;
        private Type frameworkRootType;
        private string snapshot = string.Empty;
        private float nextRefreshTime;
        private bool isVisible;

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
            GUILayout.Label(snapshot, labelStyle);
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
            textBuilder.AppendLine($"Current Scene: {SceneManager.GetActiveScene().name}");

            try
            {
                var root = GetFrameworkRoot();
                var saveData = GetProperty(root, "CurrentSaveData");
                var coordinator = GetProperty(root, "TradeProgressCoordinator");
                var router = GetProperty(root, "InGameScreenRouter");
                var sharedData = GetProperty(root, "SharedGameData");
                var tradeProgress = GetField(saveData, "tradeProgress");
                var caravan = GetField(saveData, "caravan");
                var player = GetField(saveData, "player");

                textBuilder.AppendLine($"FrameworkRoot Initialized: {FormatBool(root != null && coordinator != null && router != null)}");
                textBuilder.AppendLine($"CurrentSaveData Exists: {FormatBool(saveData != null)}");
                textBuilder.AppendLine($"TradeProgressState: {FormatValue(GetField(tradeProgress, "state"))}");
                textBuilder.AppendLine($"InGameScreenState: {FormatValue(GetProperty(router, "CurrentScreenState"))}");
                textBuilder.AppendLine($"Active Trade ID: {FormatValue(GetField(tradeProgress, "activeTradeId"))}");
                textBuilder.AppendLine($"Active Route ID: {FormatValue(GetField(tradeProgress, "activeRouteId"))}");
                textBuilder.AppendLine($"Trade Start (UTC): {FormatUtcTicks(GetField(tradeProgress, "tradeStartUtcTick"))}");
                textBuilder.AppendLine($"Trade End (UTC): {FormatUtcTicks(GetField(tradeProgress, "expectedTradeEndUtcTick"))}");
                textBuilder.AppendLine($"Caravan Progress: {FormatProgress(GetField(caravan, "progress01"))}");
                textBuilder.AppendLine($"Trading Currency: {FormatValue(GetField(player, "tradingCurrency"))}");
                textBuilder.AppendLine($"Development Currency: {FormatValue(GetField(player, "developmentCurrency"))}");
                textBuilder.AppendLine($"SharedGameData Loaded: {FormatBool(ToBool(GetProperty(sharedData, "IsLoaded")))}");
                textBuilder.AppendLine($"  Towns: {FormatValue(GetProperty(sharedData, "TownCount"))}");
                textBuilder.AppendLine($"  Markets: {FormatValue(GetProperty(sharedData, "MarketCount"))}");
                textBuilder.AppendLine($"  Trade Items: {FormatValue(GetProperty(sharedData, "TradeItemCount"))}");
                textBuilder.AppendLine($"  Wagons: {FormatValue(GetProperty(sharedData, "WagonCount"))}");
                textBuilder.AppendLine($"  Draft Animals: {FormatValue(GetProperty(sharedData, "DraftAnimalCount"))}");
                textBuilder.AppendLine($"  Routes: {FormatValue(GetProperty(sharedData, "RouteCount"))}");
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
            return frameworkRootType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
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

        private static object GetProperty(object target, string name)
        {
            return target?.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(target);
        }

        private static object GetField(object target, string name)
        {
            return target?.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(target);
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
                return "Invalid ticks";
            }
        }
    }
}
#endif
