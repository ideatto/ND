using System;
using ND.Framework;
using UnityEngine;

/// <summary>
/// 기존 공용 건물 목록과 빵집 Popup을 연결한다. 건물 클릭 분기와 생산 완료 Badge만 담당하며,
/// 생산 계산이나 SaveData 변경은 BakeryProductionService에 위임한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BakeryProductionMainUiEntry : MonoBehaviour
{
    [SerializeField] private BuildingListPanel buildingListPanel;
    [SerializeField] private BakeryProductionPopupPresenter popup;
    [SerializeField] private Sprite productionReadyIcon;

    public void Configure(
        BuildingListPanel list,
        BakeryProductionPopupPresenter presenter,
        Sprite readyIcon)
    {
        buildingListPanel = list;
        popup = presenter;
        productionReadyIcon = readyIcon;
    }

    private void Awake()
    {
        if (popup != null) popup.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (buildingListPanel != null)
            buildingListPanel.BuildingClicked += HandleBuildingClicked;
        FrameworkEvents.BakeryProductionChanged += RefreshBadge;
        FrameworkEvents.LoadCompleted += HandleLoad;
        RefreshBadge();
    }

    private void OnDisable()
    {
        if (buildingListPanel != null)
            buildingListPanel.BuildingClicked -= HandleBuildingClicked;
        FrameworkEvents.BakeryProductionChanged -= RefreshBadge;
        FrameworkEvents.LoadCompleted -= HandleLoad;
    }

    private void HandleBuildingClicked(string buildingName)
    {
        BakeryProductionData config = FrameworkRoot.Instance?.BakeryProduction?.Config;
        if (config == null || !string.Equals(
                buildingName, config.BuildingDisplayName, StringComparison.Ordinal))
            return;
        if (popup == null || !popup.TryOpen())
            Debug.LogWarning("빵집 생산 정보를 표시할 수 없습니다.", this);
    }

    private void RefreshBadge()
    {
        BakeryProductionData config = FrameworkRoot.Instance?.BakeryProduction?.Config;
        if (buildingListPanel == null || config == null) return;
        bool isFull = BakeryProductionViewDataBuilder.TryBuild(
            FrameworkRoot.Instance,
            out BakeryProductionViewData data) && data.IsFull;
        buildingListPanel.SetBuildingBadge(
            config.BuildingDisplayName,
            productionReadyIcon,
            isFull);
    }

    private void HandleLoad(ND.Framework.SaveData _) => RefreshBadge();
}
