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

    private readonly BuildingViewDataBuilder viewDataBuilder =
        new BuildingViewDataBuilder();

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
        if (detailPresenter != null)
        {
            detailPresenter.BuildRequested -= HandleBuildRequested;
            detailPresenter.CloseRequested -= ClearSelection;
        }

        if (confirmPresenter != null)
        {
            confirmPresenter.ConfirmRequested -= HandleConfirmRequested;
        }

        // Binding이 다시 활성화됐을 때 이전 선택과 Popup 상태가 남지 않게 정리한다.
        CloseAll();
    }

    /// <summary>
    /// 기존 건물 선택 UI에서 선택한 BuildData와 현재 레벨을 전달받아
    /// Detail Popup을 표시한다.
    /// </summary>
    public void OpenDetail(BuildData data, int currentLevel)
    {
        if (data == null)
        {
            return;
        }

        selectedBuildData = data;
        selectedBuildingCurrentLevel = Mathf.Max(0, currentLevel);

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

        BuildingConfirmViewData viewData =
            viewDataBuilder.BuildConfirm(
                selectedBuildData,
                selectedBuildingCurrentLevel,
                PlayerMainManager.Instance,
                FrameworkRoot.Instance?.SharedGameData);

        confirmPresenter?.Show(viewData);
    }

    // 최종 확인이 끝나면 Popup 상태를 정리하고 실제 건설 요청을 외부로 전달한다.
    private void HandleConfirmRequested(string buildId)
    {
        if (!IsCurrentSelection(buildId))
        {
            return;
        }

        confirmPresenter?.Hide();
        detailPresenter?.Hide();

        // 구독자가 이벤트 처리 중 새 Detail을 열어도 그 선택을 지우지 않도록
        // 현재 선택을 먼저 정리한 뒤 외부 실행 계층에 요청을 전달한다.
        ClearSelection();
        BuildConfirmed?.Invoke(buildId);
    }

    // 선택한 BuildData와 현재 레벨을 조합해 Detail 화면을 갱신한다.
    private void ShowSelectedBuildingDetail()
    {
        if (selectedBuildData == null)
        {
            return;
        }

        BuildingDetailViewData viewData =
            viewDataBuilder.BuildDetail(
                selectedBuildData,
                selectedBuildingCurrentLevel,
                PlayerMainManager.Instance,
                FrameworkRoot.Instance?.SharedGameData);

        confirmPresenter?.Hide();
        detailPresenter?.Show(viewData);
    }

    // 현재 Popup 흐름에서 선택한 건물과 같은 요청인지 확인한다.
    private bool IsCurrentSelection(string buildId)
    {
        return selectedBuildData != null
            && !string.IsNullOrWhiteSpace(buildId)
            && selectedBuildData.BuildId == buildId;
    }

    // Popup 흐름이 끝날 때 선택한 건물과 레벨 상태를 함께 초기화한다.
    private void ClearSelection()
    {
        selectedBuildData = null;
        selectedBuildingCurrentLevel = 0;
    }

}
