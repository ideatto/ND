using System;
using ND.Framework;
using ND.UI.InGame.Warehouse;
using UnityEngine;

/// <summary>
/// Main UI의 건물 목록과 독립 Warehouse 팝업을 연결한다.
/// 건설 후 생성되는 '창고 Lv.N' 블록만 Warehouse 진입점으로 사용한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WarehouseMainUiEntry : MonoBehaviour
{
    [SerializeField] private BuildingListPanel buildingListPanel;
    [SerializeField] private WarehouseInventoryPopupController warehousePopup;

    private void Awake()
    {
        // 팝업은 Main UI와 함께 로드되지만 창고 블록을 누르기 전에는 표시하지 않는다.
        if (warehousePopup != null)
        {
            warehousePopup.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (buildingListPanel != null)
        {
            buildingListPanel.BuildingClicked += HandleBuildingClicked;
        }
    }

    private void OnDisable()
    {
        if (buildingListPanel != null)
        {
            buildingListPanel.BuildingClicked -= HandleBuildingClicked;
        }
    }

    private void HandleBuildingClicked(string buildingName)
    {
        if (!string.Equals(
                buildingName,
                WarehouseFunction.BuildingDisplayName,
                StringComparison.Ordinal))
        {
            return;
        }

        // TryOpen이 최신 SaveData의 창고 레벨과 현재 위치를 다시 검증한다.
        warehousePopup?.TryOpen();
    }
}
