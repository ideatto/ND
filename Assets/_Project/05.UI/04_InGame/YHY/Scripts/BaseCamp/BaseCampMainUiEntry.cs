using System;
using UnityEngine;

namespace ND.UI.InGame.BaseCamp
{
    [DisallowMultipleComponent]
    public sealed class BaseCampMainUiEntry : MonoBehaviour
    {
        [SerializeField] private BuildingListPanel buildingListPanel;
        [SerializeField] private BaseCampOverviewPopupController popup;
        [SerializeField] private NoticeUI noticeUI;

        private void Awake()
        {
            if (popup != null)
                popup.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (buildingListPanel != null)
                buildingListPanel.BuildingClicked += HandleBuildingClicked;
        }

        private void OnDisable()
        {
            if (buildingListPanel != null)
                buildingListPanel.BuildingClicked -= HandleBuildingClicked;
        }

        private void HandleBuildingClicked(string buildingName)
        {
            if (!string.Equals(
                    buildingName,
                    ND.Framework.BaseCampBuildingLevelPolicy.BaseCampDisplayName,
                    StringComparison.Ordinal))
                return;

            if (popup == null || !popup.Open())
            {
                noticeUI?.Show("건물 정보를 불러오지 못했습니다. 저장 데이터를 확인해 주세요.");
                Debug.LogError("BaseCamp overview could not open because its UI reference or building save data is invalid.", this);
            }
        }
    }
}
