using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.Quest
{
    /// <summary>
    /// Optional Button adapter. A town or base-house presenter may replace TownId
    /// at runtime, so the access-location decision remains outside Quest logic.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class TownQuestListOpenButton : MonoBehaviour
    {
        [SerializeField] private TownQuestPanelController panelController;
        [SerializeField] private string townId = string.Empty;

        private Button button;

        public string TownId
        {
            get => townId;
            set => townId = value ?? string.Empty;
        }

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (button == null) button = GetComponent<Button>();
            button.onClick.AddListener(Open);
        }

        private void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(Open);
        }

        public void Configure(
            TownQuestPanelController controller,
            string targetTownId)
        {
            panelController = controller;
            TownId = targetTownId;
        }

        public void Open()
        {
            if (panelController == null)
            {
                Debug.LogWarning(
                    "[Quest] Town quest panel controller is not connected.",
                    this);
                return;
            }

            if (!string.IsNullOrWhiteSpace(townId))
            {
                panelController.OpenTownQuestList(townId.Trim());
                return;
            }

            panelController.OpenCaravanSelection();
        }
    }
}
