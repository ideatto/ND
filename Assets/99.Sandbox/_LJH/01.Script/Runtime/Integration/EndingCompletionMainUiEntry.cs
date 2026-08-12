using ND.Framework;
using UnityEngine;

/// <summary>
/// 엔딩 건물의 건설 완료 이벤트와 좌하단 건물 블록 클릭을 엔딩 팝업에 연결한다.
/// 표시 이름은 직렬화된 BuildData에서 읽고, 별도 엔딩 SaveData는 만들지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EndingCompletionMainUiEntry : MonoBehaviour
{
    [SerializeField] private BuildingListPanel buildingListPanel;
    [SerializeField] private EndingCompletionPopupPresenter popup;
    [SerializeField] private BuildData endingBuildingData;

    public void Configure(
        BuildingListPanel listPanel,
        EndingCompletionPopupPresenter popupPresenter,
        BuildData buildData)
    {
        buildingListPanel = listPanel;
        popup = popupPresenter;
        endingBuildingData = buildData;
    }

    private void Awake()
    {
        if (popup != null)
            popup.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        FrameworkEvents.VillageBuildingCommitted += HandleVillageBuildingCommitted;
        if (buildingListPanel != null)
            buildingListPanel.BuildingClicked += HandleBuildingClicked;
    }

    private void OnDisable()
    {
        FrameworkEvents.VillageBuildingCommitted -= HandleVillageBuildingCommitted;
        if (buildingListPanel != null)
            buildingListPanel.BuildingClicked -= HandleBuildingClicked;
    }

    private void HandleVillageBuildingCommitted(string buildingName, int targetLevel)
    {
        if (!IsEndingBuilding(buildingName) || targetLevel < 1)
            return;

        popup?.Open(BuildCurrentViewData());
    }

    private void HandleBuildingClicked(string buildingName)
    {
        if (!IsEndingBuilding(buildingName))
            return;

        popup?.Open(BuildCurrentViewData());
    }

    private EndingCompletionViewData BuildCurrentViewData()
    {
        return EndingCompletionViewData.Build(
            FrameworkRoot.Instance?.CurrentSaveData,
            endingBuildingData != null ? endingBuildingData.DisplayName : string.Empty);
    }

    private bool IsEndingBuilding(string buildingName)
    {
        return EndingCompletionViewData.IsEndingBuildingName(
            buildingName,
            endingBuildingData != null ? endingBuildingData.DisplayName : string.Empty);
    }
}
