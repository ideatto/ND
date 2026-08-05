using System;
using System.Linq;
using ND.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.InGame.Warehouse
{
    /// <summary>목록 index가 아닌 영속 caravanId로 선택 의도를 전달한다.</summary>
    public sealed class WarehouseCaravanSlotView : MonoBehaviour
    {
        private Button button;
        private TMP_Text nameText;
        private TMP_Text stateText;
        private GameObject unavailableOverlay;
        private TMP_Text unavailableReason;
        private string caravanId = string.Empty;
        private Action<string> clicked;

        private void Awake() => Resolve();

        /// <summary>SaveData references are not retained; the view keeps display values and a persistent caravan-ID callback.</summary>
        public void Bind(
            ND.Framework.CaravanSaveData caravan,
            bool eligible,
            string reason,
            Action<string> onClicked,
            bool showUnavailableOverlay = false,
            string statusText = null)
        {
            Resolve();
            caravanId = caravan != null ? caravan.caravanId ?? string.Empty : string.Empty;
            clicked = onClicked;
            if (nameText != null)
                nameText.text = caravan != null && !string.IsNullOrEmpty(caravan.caravanId)
                    ? "Caravan " + (caravan.slotIndex + 1) : "Empty";
            if (stateText != null)
                stateText.text = caravan != null ? statusText ?? caravan.state.ToString() : string.Empty;
            // 404 is reserved for an unlocked slot with no Caravan data (or invalid slot data).
            // A real Caravan outside BaseCamp keeps its identity/location visible and only disables selection.
            if (unavailableOverlay != null) unavailableOverlay.SetActive(showUnavailableOverlay);
            if (unavailableReason != null) unavailableReason.text = reason ?? string.Empty;
            if (button != null)
            {
                button.interactable = eligible;
                button.onClick.RemoveListener(NotifyClicked);
                button.onClick.AddListener(NotifyClicked);
            }
        }

        private void NotifyClicked()
        {
            if (!string.IsNullOrEmpty(caravanId)) clicked?.Invoke(caravanId);
        }

        private void Resolve()
        {
            if (button != null) return;
            button = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            nameText = Find("CaravanName")?.GetComponent<TMP_Text>();
            stateText = Find("StateText")?.GetComponent<TMP_Text>();
            unavailableOverlay = Find("UnavailableOverlay")?.gameObject;
            unavailableReason = Find("UnavailableReason")?.GetComponent<TMP_Text>();
        }

        private Transform Find(string objectName) =>
            GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == objectName);
    }
}