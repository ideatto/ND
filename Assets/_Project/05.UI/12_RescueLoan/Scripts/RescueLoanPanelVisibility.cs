using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.RescueLoan
{
    public sealed class RescueLoanPanelVisibility : MonoBehaviour
    {
        [SerializeField] private GameObject modalObject;
        [SerializeField] private PanelOpener panelOpener;
        [SerializeField] private Button launcherButton;
        [SerializeField] private Button closeButton;

        public bool IsVisible => modalObject != null && modalObject.activeSelf;

        private void Awake()
        {
            launcherButton?.onClick.AddListener(Show);
            closeButton?.onClick.AddListener(Hide);
            Hide();
        }

        private void OnDestroy()
        {
            launcherButton?.onClick.RemoveListener(Show);
            closeButton?.onClick.RemoveListener(Hide);
        }

        public void Show()
        {
            if (panelOpener != null) panelOpener.Open();
            else if (modalObject != null) modalObject.SetActive(true);
        }

        public void Hide()
        {
            if (panelOpener != null) panelOpener.ClosePanel();
            else if (modalObject != null) modalObject.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            if (visible) Show();
            else Hide();
        }
    }
}
