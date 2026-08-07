using ND.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.InGame.TransportInventory
{
    public sealed class TransportInventoryPopupController : MonoBehaviour
    {
        private static readonly Color Selected = new Color32(214, 194, 166, 255);
        private static readonly Color Normal = new Color32(168, 163, 153, 255);

        private ITransportInventoryViewDataProvider provider;
        private TransportInventoryTab selectedTab = TransportInventoryTab.Wagon;
        [SerializeField] private TransportInventoryPanelView wagonPanel;
        [SerializeField] private TransportInventoryPanelView animalPanel;
        [SerializeField] private TransportInventoryTooltipView tooltip;
        [SerializeField] private Button wagonTab;
        [SerializeField] private Button animalTab;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backdropButton;
        [SerializeField] private TMP_Text wagonTabLabel;
        [SerializeField] private TMP_Text animalTabLabel;
        private bool subscribed;

        public void ConfigureReferences(
            TransportInventoryPanelView wagon,
            TransportInventoryPanelView animal,
            TransportInventoryTooltipView tooltipView,
            Button wagonTabButton,
            Button animalTabButton,
            Button close,
            Button backdrop,
            TMP_Text wagonLabel,
            TMP_Text animalLabel)
        {
            wagonPanel = wagon;
            animalPanel = animal;
            tooltip = tooltipView;
            wagonTab = wagonTabButton;
            animalTab = animalTabButton;
            closeButton = close;
            backdropButton = backdrop;
            wagonTabLabel = wagonLabel;
            animalTabLabel = animalLabel;
        }

        private void Awake()
        {
            wagonPanel?.SetTooltip(tooltip);
            animalPanel?.SetTooltip(tooltip);
            wagonTab?.onClick.AddListener(SelectWagonTab);
            animalTab?.onClick.AddListener(SelectAnimalTab);
            closeButton?.onClick.AddListener(Close);
            backdropButton?.onClick.AddListener(Close);
            SelectTab(TransportInventoryTab.Wagon, true);
        }

        private void OnDisable()
        {
            tooltip?.Hide();
            Unsubscribe();
        }

        private void OnDestroy()
        {
            wagonTab?.onClick.RemoveListener(SelectWagonTab);
            animalTab?.onClick.RemoveListener(SelectAnimalTab);
            closeButton?.onClick.RemoveListener(Close);
            backdropButton?.onClick.RemoveListener(Close);
            Unsubscribe();
        }

        public bool Open(ITransportInventoryViewDataProvider dataProvider)
        {
            if (dataProvider == null) return false;
            provider = dataProvider;
            TransportInventoryPopupViewData viewData = provider.GetViewData();
            if (viewData == null || !viewData.CanOpen) return false;

            gameObject.SetActive(true);
            Subscribe();
            selectedTab = TransportInventoryTab.Wagon;
            Render(viewData, true);
            return true;
        }

        public void Close()
        {
            tooltip?.Hide();
            Unsubscribe();
            gameObject.SetActive(false);
        }

        /// <summary>Called by building/scene composition code after any non-inventory dependency changes.</summary>
        public void Refresh()
        {
            if (!gameObject.activeInHierarchy || provider == null) return;
            TransportInventoryPopupViewData viewData = provider.GetViewData();
            if (viewData == null || !viewData.CanOpen) { Close(); return; }
            Render(viewData, false);
        }

        private void SelectWagonTab() => SelectTab(TransportInventoryTab.Wagon, true);
        private void SelectAnimalTab() => SelectTab(TransportInventoryTab.Animal, true);

        private void SelectTab(TransportInventoryTab tab, bool resetScroll)
        {
            selectedTab = tab;
            bool wagonSelected = tab == TransportInventoryTab.Wagon;
            if (wagonPanel != null) wagonPanel.gameObject.SetActive(wagonSelected);
            if (animalPanel != null) animalPanel.gameObject.SetActive(!wagonSelected);
            SetTabVisual(wagonTab, wagonTabLabel, wagonSelected);
            SetTabVisual(animalTab, animalTabLabel, !wagonSelected);
            tooltip?.Hide();
            if (resetScroll) (wagonSelected ? wagonPanel : animalPanel)?.ResetScrollPosition();
        }

        private void Render(TransportInventoryPopupViewData viewData, bool resetScroll)
        {
            wagonPanel?.Render(viewData.Wagon);
            animalPanel?.Render(viewData.Animal);
            SelectTab(selectedTab, resetScroll);
        }

        private void Subscribe()
        {
            if (subscribed) return;
            FrameworkEvents.TransportInventoryChanged += Refresh;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            FrameworkEvents.TransportInventoryChanged -= Refresh;
            subscribed = false;
        }

        private static void SetTabVisual(Button button, TMP_Text label, bool selected)
        {
            Image image = button != null ? button.GetComponent<Image>() : null;
            if (image != null) image.color = selected ? Selected : Normal;
            if (label != null) label.color = selected ? new Color32(31, 36, 33, 255) : new Color32(77, 74, 68, 255);
        }
    }
}
