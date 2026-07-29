using System;
using ND.Framework;
using UnityEngine;

/// <summary>
/// 기존 건물 선택 UI에서 전달받은 BuildData를 이용해
/// Detail Popup과 Confirm Popup의 화면 흐름을 연결한다.
/// 실제 건설 처리, 재료 차감 및 저장은 외부 실행 계층에 전달한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BuildingPopupRuntimeBinding : MonoBehaviour
{
    [Header("Presenter")]
    [SerializeField] private BuildingDetailPopupPresenter detailPresenter;
    [SerializeField] private BuildingConfirmPopupPresenter confirmPresenter;

    private readonly BuildingViewDataBuilder viewDataBuilder = new BuildingViewDataBuilder();

    private BuildData selectedBuildData;
    private int selectedBuildingCurrentLevel;

    /// <summary>최종 확인된 건물의 buildId를 실제 건설 실행 계층에 전달한다.</summary>
    public event Action<string> BuildConfirmed;

    private void OnEnable()
    {
        if(detailPresenter != null)
        {
            detailPresenter.BuildRequested += HandleBuildRequested;
            detailPresenter.CloseRequested += ClearSelection;
        }

        if(confirmPresenter != null)
        {
            confirmPresenter.ConfirmRequested += HandleConfirmRequested;
        }
    }

    private void OnDisable()
    {
        if(detailPresenter != null)
        {
            detailPresenter.BuildRequested -= HandleBuildRequested;
            detailPresenter.CloseRequested -= ClearSelection;
        }

        if(confirmPresenter != null)
        {
            confirmPresenter.ConfirmRequested -= HandleConfirmRequested;
        }
    }

    /// <summary>
    /// 기존 건물 선택 UI에서 선택한 BuildData를 전달받아 Detail Popup을 표시한다.
    /// </summary>
    public void OpenDetail(BuildData data)
    {
        if(data == null)
        {
            return;
        }

        selectedBuildData = data;
        selectedBuildingCurrentLevel = ResolveCurrentBuildingLevel(data);

        ShowSelectedBuildingDetail();
    }

    /// <summary>열려 있는 건설 Popup을 모두 닫고 현재 선택 상태를 초기화한다.</summary>
    public void CloseAll()
    {
        detailPresenter?.Hide();
        confirmPresenter?.Hide();

        ClearSelection();
    }

    // Detail은 뒤에 유지하고 Confirm만 위에 표시한다.
    private void HandleBuildRequested(string buildId)
    {
        if (!IsCurrentSelection(buildId))
        {
            return;
        }

        BuildingConfirmViewData viewData = viewDataBuilder.BuildConfirm
            (selectedBuildData, selectedBuildingCurrentLevel, PlayerMainManager.Instance, FrameworkRoot.Instance?.SharedGameData);

        confirmPresenter?.Show(viewData);
    }

    // 최종 확인이 끝나면 두 Popup을 닫고 실제 건설 요청을 외부로 전달한다.
    private void HandleConfirmRequested(string buildId)
    {
        if (!IsCurrentSelection(buildId))
        {
            return;
        }

        confirmPresenter?.Hide();
        detailPresenter?.Hide();

        BuildConfirmed?.Invoke(buildId);

        ClearSelection();
    }

    // 선택한 BuildData와 현재 레벨을 조합해 Detail 화면을 갱신한다.
    private void ShowSelectedBuildingDetail()
    {
        if(selectedBuildData == null)
        {
            return;
        }

        BuildingDetailViewData viewData = viewDataBuilder.BuildDetail
            (selectedBuildData, selectedBuildingCurrentLevel, PlayerMainManager.Instance, FrameworkRoot.Instance?.SharedGameData);

        confirmPresenter?.Hide();
        detailPresenter?.Show(viewData);
    }

    // 현재 Popup 흐름에서 선택한 건물과 같은 요청인지 확인한다.
    private bool IsCurrentSelection(string buildId)
    {
        return selectedBuildData != null && !string.IsNullOrWhiteSpace(buildId) && selectedBuildData.BuildId == buildId;
    }

    private static int ResolveCurrentBuildingLevel(BuildData data)
    {
        // TODO: 건물 진행 SaveData가 구현되면 data.BuildId로 현재 레벨을 조회한다.
        return 0;
    }

    // Popup 흐름이 끝날 때 선택한 건물과 레벨 상태를 함께 초기화한다.
    private void ClearSelection()
    {
        selectedBuildData = null;
        selectedBuildingCurrentLevel = 0;
    }

}
