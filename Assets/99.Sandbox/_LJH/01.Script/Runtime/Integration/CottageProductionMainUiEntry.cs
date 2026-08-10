using System;
using ND.Framework;
using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// 공용 건물 목록과 오두막 Popup 사이의 Scene 연결부다.
/// BuildingListPanel이 특정 건물 기능에 의존하지 않도록 클릭 분기와 Badge 계산을 여기서 담당한다.
/// </summary>
public sealed class CottageProductionMainUiEntry : MonoBehaviour
{
    [SerializeField] private BuildingListPanel buildingListPanel;
    [SerializeField] private CottageProductionPopupPresenter popup;
    [SerializeField] private Sprite productionReadyIcon;

    public void Configure(
        BuildingListPanel listPanel,
        CottageProductionPopupPresenter presenter,
        Sprite readyIcon)
    {
        buildingListPanel = listPanel;
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
        FrameworkEvents.CottageProductionChanged += RefreshProductionBadge;
        FrameworkEvents.LoadCompleted += HandleLoadCompleted;
        RefreshProductionBadge();
    }

    private void OnDisable()
    {
        if (buildingListPanel != null)
            buildingListPanel.BuildingClicked -= HandleBuildingClicked;
        FrameworkEvents.CottageProductionChanged -= RefreshProductionBadge;
        FrameworkEvents.LoadCompleted -= HandleLoadCompleted;
    }

    private void HandleBuildingClicked(string buildingName)
    {
        CottageProductionData config =
            FrameworkRoot.Instance?.CottageProduction?.Config;
        if (config == null || !string.Equals(
                buildingName, config.BuildingDisplayName, StringComparison.Ordinal))
            return;
        if (popup == null || !popup.TryOpen())
            Debug.LogWarning("오두막 생산 정보를 표시할 수 없습니다.", this);
    }

    /// <summary>
    /// 공용 건물 목록은 오두막 데이터를 모른다. 이 기능 연결부가 ViewData를 해석한 뒤
    /// 범용 Badge 계약으로 "양쪽 보관함이 모두 가득 참" 상태만 전달한다.
    /// </summary>
    private void RefreshProductionBadge()
    {
        CottageProductionData config =
            FrameworkRoot.Instance?.CottageProduction?.Config;
        if (buildingListPanel == null || config == null) return;
        bool isFull = CottageProductionViewDataBuilder.TryBuild(
            FrameworkRoot.Instance,
            out CottageProductionViewData data)
            && data.IsAllStorageFull;
        buildingListPanel.SetBuildingBadge(
            config.BuildingDisplayName,
            productionReadyIcon,
            isFull);
    }

    private void HandleLoadCompleted(ND.Framework.SaveData _) =>
        RefreshProductionBadge();
}
