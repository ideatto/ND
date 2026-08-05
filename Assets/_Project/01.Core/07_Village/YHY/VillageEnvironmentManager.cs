// =============================================================================
// VillageEnvironmentManager — 환경 아이템 건설/삭제/저장 허브
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 마을 '환경 아이템'(오크나무·벤치·울타리 등)의 건설·삭제·저장을 담당한다.
//        건물(재현님 트랜잭션: 아이템 비용 + 종류당 1개 + 레벨업)과 달리 환경은
//        <b>거래재화(Gold=tradingCurrency) 비용 + 다중 설치 + 레벨 없음 + 삭제 가능</b>이라
//        건물 경로에 끼우지 않고 이 경량 경로로 분리한다.
//
// [흐름]
//   · 건설: 건물추가 팝업의 BuildConfirmed(buildId) → 환경 항목이면 여기서 처리
//           → Gold 차감 → 인스턴스 생성(Registry) → 격자 배치(Controller) → villageEnvironments append → 저장
//   · 이동/회전: BuildingPlacementController가 PlacedEnvironment를 감지해 UpdatePlacement 호출
//   · 삭제: 편집모드에서 선택 → Controller가 DeleteEnvironment 호출(환불 없음)
//   · 복원: BuildingPlacementController.RestoreAndRegisterExistingBuildings가 저장 목록으로 재생성
//
// [주의] 건물 팝업 이벤트는 재현님 핸들러도 함께 구독하므로, 그쪽은 환경 buildId를 early-return으로 건너뛴다.
// =============================================================================

using UnityEngine;
using ND.Framework;

/// <summary>환경 아이템 건설(재화 차감)·삭제·저장 담당. UI/Controller가 싱글톤으로 접근.</summary>
public sealed class VillageEnvironmentManager : MonoBehaviour
{
    public static VillageEnvironmentManager Instance { get; private set; }

    [Tooltip("건물추가 팝업 바인딩(BuildConfirmed 구독). 비면 런타임 탐색.")]
    [SerializeField] private BuildingPopupRuntimeBinding popupBinding;
    [Tooltip("배치 컨트롤러(격자 배치/복원). 비면 런타임 탐색.")]
    [SerializeField] private BuildingPlacementController placementController;
    [Tooltip("재화 부족 등 안내 표시용. 비면 런타임 탐색.")]
    [SerializeField] private NoticeUI noticeUI;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        // 씬 로드 순서에 무관하게 바인딩을 확보한 뒤 건설 확정 이벤트를 구독한다.
        if (popupBinding == null) popupBinding = FindAnyObjectByType<BuildingPopupRuntimeBinding>();
        if (popupBinding != null) popupBinding.BuildConfirmed += HandleBuildConfirmed;
    }

    private void OnDisable()
    {
        if (popupBinding != null) popupBinding.BuildConfirmed -= HandleBuildConfirmed;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>이 buildId가 환경 아이템이면 여기서 건설을 처리한다(건물이면 무시 → 재현님 핸들러 담당).</summary>
    private void HandleBuildConfirmed(string buildId)
    {
        VillageBuildingRegistry registry = VillageBuildingRegistry.Instance;
        if (registry == null) return;

        // 환경 항목이 아니면(=건물) 조용히 무시한다.
        if (!registry.TryGetCatalogEnvironmentEntry(buildId, out _, out _, out long cost))
            return;

        BuildingPlacementController controller = ResolveController();
        PlayerMainManager player = PlayerMainManager.Instance;
        FrameworkRoot root = FrameworkRoot.Instance;

        if (controller == null || player == null ||
            root == null || root.CurrentSaveData == null || root.CurrentSaveData.player == null || root.SaveService == null)
        {
            ShowNotice("환경 아이템을 설치할 준비가 되지 않았습니다.",
                "Environment build blocked: controller/player/save unavailable.");
            return;
        }

        // 1) 재화 검사 + 차감(부족하면 차감 없이 중단).
        if (!player.SpendGold(cost))
        {
            ShowNotice("거래재화가 부족해 설치할 수 없습니다.",
                $"Environment build blocked: insufficient gold. need={cost}, have={player.Gold}");
            return;
        }

        // 2) 인스턴스 생성(고유 ID 부여).
        string instanceId = System.Guid.NewGuid().ToString("N");
        GameObject go = registry.BuildEnvironmentInstance(buildId, instanceId, Vector3.zero);
        if (go == null)
        {
            player.AddGold(cost);   // 생성 실패 → 차감 되돌림
            ShowNotice("환경 아이템 생성에 실패했습니다.",
                $"Environment build failed: instance not created. envId={buildId}");
            return;
        }

        // 3) 격자에 빈 칸으로 배치(컨트롤러가 셀을 결정).
        PlaceableBuilding placeable = go.GetComponent<PlaceableBuilding>();
        if (placeable == null || !controller.RegisterNewEnvironment(placeable, out int cx, out int cz, out int yawStep))
        {
            player.AddGold(cost);
            Destroy(go);
            ShowNotice("빈 자리가 없어 환경 아이템을 설치하지 못했습니다.",
                $"Environment build failed: no free cell. envId={buildId}");
            return;
        }

        // 4) 저장 목록에 append.
        if (root.CurrentSaveData.player.villageEnvironments == null)
            root.CurrentSaveData.player.villageEnvironments =
                new System.Collections.Generic.List<VillageEnvironmentSaveData>();

        var entry = new VillageEnvironmentSaveData
        {
            instanceId = instanceId,
            envId = buildId,
            gridCellX = cx,
            gridCellZ = cz,
            yawStep = yawStep
        };
        root.CurrentSaveData.player.villageEnvironments.Add(entry);

        // 5) 저장. 실패하면 전부 되돌린다(재화·인스턴스·목록).
        SaveResult result = root.SaveService.Save(root.CurrentSaveData);
        if (result == null || !result.Succeeded)
        {
            root.CurrentSaveData.player.villageEnvironments.Remove(entry);
            controller.UnregisterEnvironment(placeable);
            Destroy(go);
            player.AddGold(cost);
            ShowNotice("저장에 실패해 설치를 취소했습니다.",
                $"Environment build save failed: {(result != null ? result.Message : "NULL_RESULT")}");
            return;
        }

        // 6) 신축 성공 → 편집모드로 전환하고 방금 설치한 것을 선택(위치는 이미 화면 중앙).
        controller.EnterEditModeAndSelect(placeable.transform);
    }

    /// <summary>
    /// 이동/회전 확정 시 해당 인스턴스의 저장 배치(셀·회전)를 갱신하고 저장한다.
    /// BuildingPlacementController가 PlacedEnvironment를 감지했을 때 호출한다.
    /// </summary>
    public bool UpdatePlacement(string instanceId, int cx, int cz, int yawStep)
    {
        FrameworkRoot root = FrameworkRoot.Instance;
        var list = root != null && root.CurrentSaveData != null && root.CurrentSaveData.player != null
            ? root.CurrentSaveData.player.villageEnvironments
            : null;
        if (list == null || root.SaveService == null || string.IsNullOrEmpty(instanceId)) return false;

        VillageEnvironmentSaveData target = null;
        foreach (VillageEnvironmentSaveData e in list)
            if (e != null && e.instanceId == instanceId) { target = e; break; }
        if (target == null) return false;

        int beforeX = target.gridCellX, beforeZ = target.gridCellZ, beforeYaw = target.yawStep;
        target.gridCellX = cx;
        target.gridCellZ = cz;
        target.yawStep = yawStep;

        SaveResult result = root.SaveService.Save(root.CurrentSaveData);
        if (result == null || !result.Succeeded)
        {
            // 저장 실패 → 값 복원(씬은 호출자가 롤백)
            target.gridCellX = beforeX;
            target.gridCellZ = beforeZ;
            target.yawStep = beforeYaw;
            return false;
        }
        return true;
    }

    /// <summary>
    /// 환경 아이템 인스턴스를 삭제한다(환불 없음). 저장 목록에서 제거 후 저장한다.
    /// 씬 인스턴스 파괴와 격자 해제는 호출자(BuildingPlacementController)가 수행한다.
    /// </summary>
    public bool RemoveEnvironmentSave(string instanceId)
    {
        FrameworkRoot root = FrameworkRoot.Instance;
        var list = root != null && root.CurrentSaveData != null && root.CurrentSaveData.player != null
            ? root.CurrentSaveData.player.villageEnvironments
            : null;
        if (list == null || root.SaveService == null || string.IsNullOrEmpty(instanceId)) return false;

        int removed = list.RemoveAll(e => e != null && e.instanceId == instanceId);
        if (removed <= 0) return false;

        SaveResult result = root.SaveService.Save(root.CurrentSaveData);
        return result != null && result.Succeeded;
    }

    private BuildingPlacementController ResolveController()
    {
        if (placementController == null)
            placementController = FindAnyObjectByType<BuildingPlacementController>();
        return placementController;
    }

    private void ShowNotice(string userMessage, string diagnosticMessage)
    {
        if (noticeUI == null) noticeUI = FindFirstObjectByType<NoticeUI>(FindObjectsInactive.Include);
        noticeUI?.Show(userMessage);
        Debug.LogWarning(diagnosticMessage, this);
    }
}
