/*
 * Technical Ownership
 * - Responsible Discipline: Development Tools
 *
 * Script Purpose
 * - InGameScreenStateRouter의 현재 상태와 최근 전환 이력을 ProjectDebugPanel에 표시한다.
 * - 기존 InGameSceneRouterDebug의 Console logging 역할을 공용 F12 패널로 통합한다.
 *
 * Important Notes
 * - InGameScreenStateRouter는 predefined assembly에 있으므로 리플렉션으로 조회한다.
 * - 운영 Router의 상태를 변경하지 않는 읽기 전용 디버그 섹션이다.
 */
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ND.DebugTools
{
    internal sealed class InGameScreenRouterDebugSection
    {
        private const string FrameworkRootTypeName = "ND.Framework.FrameworkRoot";
        private const int MaxHistoryCount = 20;

        private readonly List<string> transitionHistory = new List<string>();
        private Type frameworkRootType;
        private bool hasObservedState;
        private string lastScreenState = "N/A";

        public void Tick()
        {
            string currentState = ReadCurrentScreenState();
            if (currentState == "N/A")
            {
                return;
            }

            if (!hasObservedState)
            {
                hasObservedState = true;
                lastScreenState = currentState;
                return;
            }

            if (string.Equals(lastScreenState, currentState, StringComparison.Ordinal))
            {
                return;
            }

            string previousState = lastScreenState;
            lastScreenState = currentState;
            string entry = $"{DateTime.Now:HH:mm:ss}  {previousState} -> {currentState}";
            transitionHistory.Insert(0, entry);
            if (transitionHistory.Count > MaxHistoryCount)
            {
                transitionHistory.RemoveAt(transitionHistory.Count - 1);
            }

            Debug.Log($"[InGame Route Debug] Current Screen State : {currentState}");
        }

        public void Draw()
        {
            object root = GetFrameworkRoot();
            object router = GetProperty(root, "InGameScreenRouter");
            object saveData = GetProperty(root, "CurrentSaveData");
            object tradeProgress = GetField(saveData, "tradeProgress");

            GUILayout.Label($"InGameScreenStateRouter: {(router != null ? "Ready" : "N/A")}");
            GUILayout.Label($"Current Screen State: {FormatValue(GetProperty(router, "CurrentScreenState"))}");
            GUILayout.Label($"Trade Progress State: {FormatValue(GetField(tradeProgress, "state"))}");
            GUILayout.Label($"Active Trade ID: {FormatValue(GetField(tradeProgress, "activeTradeId"))}");
            GUILayout.Label($"Active Route ID: {FormatValue(GetField(tradeProgress, "activeRouteId"))}");

            GUILayout.Space(8f);
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label($"Recent transitions ({transitionHistory.Count}/{MaxHistoryCount})");
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Clear history", GUILayout.Width(110f)))
                {
                    transitionHistory.Clear();
                }
            }

            if (transitionHistory.Count == 0)
            {
                GUILayout.Box("아직 감지된 화면 상태 전환이 없습니다.", GUILayout.Height(64f));
                return;
            }

            for (int index = 0; index < transitionHistory.Count; index++)
            {
                GUILayout.Label(transitionHistory[index]);
            }
        }

        private string ReadCurrentScreenState()
        {
            object root = GetFrameworkRoot();
            object router = GetProperty(root, "InGameScreenRouter");
            return FormatValue(GetProperty(router, "CurrentScreenState"));
        }

        private object GetFrameworkRoot()
        {
            frameworkRootType ??= FindType(FrameworkRootTypeName);
            return frameworkRootType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
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
    }
}
#endif
