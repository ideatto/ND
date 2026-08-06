// =============================================================================
// VillageBuildingActionRouter — 마을 건물 클릭 → 건물별 액션(패널 열기) 매핑
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 일반(비편집) 모드에서 마을 건물을 클릭하면, BuildingPlacementController가
//        그 건물 종류(카탈로그 displayName)로 이벤트를 쏜다. 이 라우터가 그걸 받아
//        건물 종류 → 액션(UnityEvent) 매핑으로 처리한다.
//        예: "창고" → WarehouseInventoryPopupController.TryOpen()
//
// [확장] 새 건물을 클릭 반응시키려면 코드 수정 없이 Inspector의 actions 목록에
//        {displayName, onClicked} 항목을 추가하고 UnityEvent만 연결하면 된다.
//
// [부착] 씬의 UI/매니저 오브젝트에 붙이고, controller(비면 런타임 탐색)와 매핑을 연결한다.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>마을 건물 클릭(일반 모드) → 건물 종류별 액션(패널 열기 등)을 실행한다.</summary>
public class VillageBuildingActionRouter : MonoBehaviour
{
    /// <summary>건물 종류(displayName) 하나에 대응하는 클릭 액션.</summary>
    [Serializable]
    public class BuildingAction
    {
        [Tooltip("건물 종류(카탈로그 displayName). 예: 창고")]
        public string displayName;

        [Tooltip("이 건물을 일반 모드에서 클릭했을 때 실행할 동작(예: 인벤토리 패널 열기).")]
        public UnityEvent onClicked;
    }

    [Tooltip("건물 클릭 이벤트를 발생시키는 배치 컨트롤러. 비면 런타임 탐색.")]
    [SerializeField] private BuildingPlacementController controller;

    [Tooltip("건물 종류 → 클릭 액션 매핑. 항목을 추가해 새 건물 반응을 코드 없이 확장한다.")]
    [SerializeField] private List<BuildingAction> actions = new List<BuildingAction>();

    private void OnEnable()
    {
        if (controller == null) controller = FindAnyObjectByType<BuildingPlacementController>();
        if (controller != null) controller.BuildingClickedInView += HandleBuildingClicked;
    }

    private void OnDisable()
    {
        if (controller != null) controller.BuildingClickedInView -= HandleBuildingClicked;
    }

    /// <summary>클릭된 건물 종류에 매핑된 액션을 실행한다(매핑 없으면 무시).</summary>
    private void HandleBuildingClicked(string displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return;

        for (int i = 0; i < actions.Count; i++)
        {
            BuildingAction action = actions[i];
            if (action != null && string.Equals(action.displayName, displayName, StringComparison.Ordinal))
            {
                action.onClicked?.Invoke();
                return;
            }
        }
    }
}
