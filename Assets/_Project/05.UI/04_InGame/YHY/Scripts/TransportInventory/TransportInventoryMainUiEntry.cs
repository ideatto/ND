using System;
using ND.Framework;
using UnityEngine;

namespace ND.UI.InGame.TransportInventory
{
    [DisallowMultipleComponent]
    public sealed class TransportInventoryMainUiEntry : MonoBehaviour
    {
        [SerializeField] private BuildingListPanel buildingListPanel;
        [SerializeField] private TransportInventoryPopupController popup;
        private bool openWhenReady;

        private void Awake()
        {
            if (popup != null) popup.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (buildingListPanel != null)
                buildingListPanel.BuildingClicked += HandleBuildingClicked;
            FrameworkEvents.SharedGameDataLoaded += HandleFrameworkReady;
            FrameworkEvents.LoadCompleted += HandleFrameworkReady;
        }

        private void OnDisable()
        {
            if (buildingListPanel != null)
                buildingListPanel.BuildingClicked -= HandleBuildingClicked;
            FrameworkEvents.SharedGameDataLoaded -= HandleFrameworkReady;
            FrameworkEvents.LoadCompleted -= HandleFrameworkReady;
            openWhenReady = false;
        }

        private void HandleBuildingClicked(string buildingName)
        {
            if (!string.Equals(buildingName, TransportInventoryFunction.BuildingDisplayName,
                    StringComparison.Ordinal))
                return;

            openWhenReady = true;
            TryOpen();
        }

        private void HandleFrameworkReady(ISharedGameDataProvider _) => TryOpen();
        private void HandleFrameworkReady(ND.Framework.SaveData _) => TryOpen();

        private void TryOpen()
        {
            FrameworkRoot root = FrameworkRoot.Instance;
            if (!openWhenReady || root == null || popup == null || root.CurrentSaveData == null
                || root.SharedGameData == null || !root.SharedGameData.IsLoaded)
                return;

            openWhenReady = false;
            popup.Open(new TransportInventoryDataProvider(
                () => FrameworkRoot.Instance?.CurrentSaveData,
                () => FrameworkRoot.Instance?.SharedGameData));
        }
    }
}
