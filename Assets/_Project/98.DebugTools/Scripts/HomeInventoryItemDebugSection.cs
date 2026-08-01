/*
 * Technical Ownership
 * - Responsible Discipline: Development Tools
 *
 * Script Purpose
 * - ProjectDebugPanel 안에서 TradeItemData 기반 상품을 선택하고
 *   PlayerMainManager의 거점 인벤토리에 원하는 수량만큼 추가한다.
 *
 * Important Notes
 * - CoreServices와 PlayerMainManager는 predefined assembly에 있으므로 리플렉션으로 연결한다.
 * - ProjectDebugPanel이 소유하는 F12 창 안에서만 그려지는 비 MonoBehaviour 보조 객체이다.
 */
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ND.DebugTools
{
    internal sealed class HomeInventoryItemDebugSection
    {
        private const string FrameworkRootTypeName = "ND.Framework.FrameworkRoot";
        private const string PlayerMainManagerTypeName = "PlayerMainManager";
        private const string TradeItemSaveDataTypeName = "ND.Framework.TradeItemSaveData";
        private const int MinQuantity = 1;
        private const int MaxQuantity = 999999;

        private readonly List<string> itemIds = new List<string>();
        private Type frameworkRootType;
        private Type playerMainManagerType;
        private Type tradeItemSaveDataType;
        private object cachedSharedData;
        private Vector2 itemScroll;
        private int selectedIndex = -1;
        private int quantity = 1;
        private string quantityText = "1";
        private string statusMessage = string.Empty;

        public void Draw()
        {
            object root = GetFrameworkRoot();
            object sharedData = GetProperty(root, "SharedGameData");
            object player = GetPlayerMainManager();

            if (!ReferenceEquals(sharedData, cachedSharedData))
            {
                RefreshCatalog(sharedData);
            }

            GUILayout.Label($"PlayerMainManager: {(player != null ? "사용 가능" : "N/A")}");
            GUILayout.Label($"거래 아이템 목록: {(sharedData != null ? itemIds.Count.ToString() : "N/A")}");

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("목록 새로고침", GUILayout.Height(28f)))
                {
                    RefreshCatalog(sharedData);
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label("TradeItemData → 거점 인벤토리");
            }

            GUILayout.Space(6f);
            DrawItemSelection(sharedData, player);
            GUILayout.Space(8f);
            DrawQuantityControls();
            GUILayout.Space(8f);
            DrawAddButton(sharedData, player);

            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Space(6f);
                GUILayout.Label(statusMessage);
            }
        }

        public void Refresh()
        {
            object root = GetFrameworkRoot();
            RefreshCatalog(GetProperty(root, "SharedGameData"));
        }

        private void DrawItemSelection(object sharedData, object player)
        {
            GUILayout.Label("거래 아이템 선택");

            if (itemIds.Count == 0)
            {
                GUILayout.Box("SharedGameData에 로드된 TradeItemData가 없습니다.", GUILayout.Height(80f));
                return;
            }

            itemScroll = GUILayout.BeginScrollView(itemScroll, GUI.skin.box, GUILayout.Height(230f));
            for (int index = 0; index < itemIds.Count; index++)
            {
                string itemId = itemIds[index];
                object definition = GetTradeItemDefinition(sharedData, itemId);
                string displayName = GetStringField(definition, "DisplayName", itemId);
                GUIStyle style = index == selectedIndex ? GUI.skin.box : GUI.skin.button;

                if (GUILayout.Button($"{displayName}  [{itemId}]", style, GUILayout.Height(30f)))
                {
                    selectedIndex = index;
                    statusMessage = string.Empty;
                }
            }
            GUILayout.EndScrollView();

            object selected = GetSelectedDefinition(sharedData);
            if (selected == null)
            {
                return;
            }

            string selectedId = GetStringField(selected, "Id", string.Empty);
            string selectedName = GetStringField(selected, "DisplayName", selectedId);
            int ownedQuantity = GetOwnedQuantity(player, selectedId);
            float weight = GetFieldValue(selected, "Weight", 0f);
            int maxCount = GetFieldValue(selected, "MaxCount", 1);

            GUILayout.Label($"선택: {selectedName} ({selectedId})");
            GUILayout.Label($"보유 수량: {ownedQuantity} / 무게: {weight:0.##} / 최대 묶음: {maxCount}");
        }

        private void DrawQuantityControls()
        {
            GUILayout.Label("수량");
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("-10", GUILayout.Width(58f)))
                {
                    SetQuantity(quantity - 10);
                }

                if (GUILayout.Button("-1", GUILayout.Width(58f)))
                {
                    SetQuantity(quantity - 1);
                }

                string editedText = GUILayout.TextField(quantityText, GUILayout.Width(120f));
                if (!string.Equals(editedText, quantityText, StringComparison.Ordinal))
                {
                    quantityText = editedText;
                    if (int.TryParse(quantityText, out int parsed))
                    {
                        quantity = Mathf.Clamp(parsed, MinQuantity, MaxQuantity);
                    }
                }

                if (GUILayout.Button("+1", GUILayout.Width(58f)))
                {
                    SetQuantity(quantity + 1);
                }

                if (GUILayout.Button("+10", GUILayout.Width(58f)))
                {
                    SetQuantity(quantity + 10);
                }
            }
        }

        private void DrawAddButton(object sharedData, object player)
        {
            object selected = GetSelectedDefinition(sharedData);
            GUI.enabled = selected != null && player != null;

            if (GUILayout.Button("거점 인벤토리에 추가", GUILayout.Height(42f)))
            {
                AddSelectedItem(player, selected);
            }

            GUI.enabled = true;
        }

        private void AddSelectedItem(object player, object definition)
        {
            if (player == null || definition == null)
            {
                statusMessage = "실패: PlayerMainManager 또는 선택 아이템이 준비되지 않았습니다.";
                return;
            }

            string itemId = GetStringField(definition, "Id", string.Empty);
            if (string.IsNullOrEmpty(itemId))
            {
                statusMessage = "실패: 선택한 아이템 ID가 비어 있습니다.";
                return;
            }

            NormalizeQuantityText();

            tradeItemSaveDataType ??= FindType(TradeItemSaveDataTypeName);
            if (tradeItemSaveDataType == null)
            {
                statusMessage = "실패: TradeItemSaveData 타입을 찾지 못했습니다.";
                return;
            }

            object saveItem = Activator.CreateInstance(tradeItemSaveDataType);
            SetField(saveItem, "itemId", itemId);
            SetField(saveItem, "itemName", GetStringField(definition, "DisplayName", string.Empty));
            SetField(saveItem, "weight", Mathf.Max(0f, GetFieldValue(definition, "Weight", 0f)));
            SetField(saveItem, "basePrice", Math.Max(0L, GetFieldValue(definition, "BaseBuyPrice", 0L)));
            SetField(saveItem, "maxCount", Mathf.Max(1, GetFieldValue(definition, "MaxCount", 1)));

            MethodInfo addItem = player.GetType().GetMethod(
                "AddItem",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { tradeItemSaveDataType, typeof(int) },
                null);

            if (addItem == null)
            {
                statusMessage = "실패: PlayerMainManager.AddItem API를 찾지 못했습니다.";
                return;
            }

            addItem.Invoke(player, new[] { saveItem, (object)quantity });
            string itemName = GetStringField(definition, "DisplayName", itemId);
            statusMessage =
                $"{itemName} ({itemId}) {quantity}개를 추가했습니다. 보유 수량: {GetOwnedQuantity(player, itemId)}";
        }

        private void RefreshCatalog(object sharedData)
        {
            string previousId = selectedIndex >= 0 && selectedIndex < itemIds.Count
                ? itemIds[selectedIndex]
                : string.Empty;

            cachedSharedData = sharedData;
            itemIds.Clear();

            if (sharedData == null || !GetPropertyValue(sharedData, "IsLoaded", false))
            {
                selectedIndex = -1;
                statusMessage = "SharedGameData가 로드되지 않았습니다.";
                return;
            }

            object idsValue = GetProperty(sharedData, "TradeItemIds");
            if (idsValue is IEnumerable ids)
            {
                foreach (object value in ids)
                {
                    string itemId = value as string;
                    if (!string.IsNullOrEmpty(itemId))
                    {
                        itemIds.Add(itemId);
                    }
                }
            }

            itemIds.Sort(StringComparer.Ordinal);
            selectedIndex = !string.IsNullOrEmpty(previousId)
                ? itemIds.IndexOf(previousId)
                : (itemIds.Count > 0 ? 0 : -1);

            if (selectedIndex < 0 && itemIds.Count > 0)
            {
                selectedIndex = 0;
            }

            statusMessage = itemIds.Count > 0
                ? $"TradeItemData {itemIds.Count}개를 불러왔습니다."
                : "불러온 TradeItemData가 없습니다.";
        }

        private object GetSelectedDefinition(object sharedData)
        {
            return selectedIndex >= 0 && selectedIndex < itemIds.Count
                ? GetTradeItemDefinition(sharedData, itemIds[selectedIndex])
                : null;
        }

        private static object GetTradeItemDefinition(object sharedData, string itemId)
        {
            if (sharedData == null || string.IsNullOrEmpty(itemId))
            {
                return null;
            }

            MethodInfo method = sharedData.GetType().GetMethod("TryGetTradeItem", BindingFlags.Public | BindingFlags.Instance);
            if (method == null)
            {
                return null;
            }

            object[] arguments = { itemId, null };
            return method.Invoke(sharedData, arguments) is bool found && found ? arguments[1] : null;
        }

        private object GetFrameworkRoot()
        {
            frameworkRootType ??= FindType(FrameworkRootTypeName);
            return frameworkRootType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        }

        private object GetPlayerMainManager()
        {
            playerMainManagerType ??= FindType(PlayerMainManagerTypeName);
            return playerMainManagerType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        }

        private static int GetOwnedQuantity(object player, string itemId)
        {
            if (player == null || string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            MethodInfo method = player.GetType().GetMethod(
                "GetItemCount",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(string) },
                null);
            return method?.Invoke(player, new object[] { itemId }) is int count ? count : 0;
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

        private static T GetPropertyValue<T>(object target, string name, T fallback)
        {
            object value = GetProperty(target, name);
            return value is T typed ? typed : fallback;
        }

        private static T GetFieldValue<T>(object target, string name, T fallback)
        {
            object value = target?.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(target);
            return value is T typed ? typed : fallback;
        }

        private static string GetStringField(object target, string name, string fallback)
        {
            string value = GetFieldValue(target, name, string.Empty);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static void SetField(object target, string name, object value)
        {
            target?.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance)?.SetValue(target, value);
        }

        private void SetQuantity(int value)
        {
            quantity = Mathf.Clamp(value, MinQuantity, MaxQuantity);
            quantityText = quantity.ToString();
        }

        private void NormalizeQuantityText()
        {
            if (!int.TryParse(quantityText, out int parsed))
            {
                parsed = quantity;
            }

            SetQuantity(parsed);
        }
    }
}
#endif
