using System;
using System.Collections.Generic;
using ND.Economy;
using ND.Framework;
using UnityEngine;

/// <summary>
/// 건설 Confirm 요청을 기존 Economy 건물 업그레이드 트랜잭션에 연결한다.
/// BuildData를 비용의 단일 원본으로 사용하며, 저장 성공 후에만 씬과 목록 UI를 갱신한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BuildingConstructionRuntimeHandler : MonoBehaviour, IBuildingUpgradeTransactionPort
{
    [SerializeField] private BuildingPopupRuntimeBinding popupBinding;
    [SerializeField] private BuildingListPanel buildingListPanel;
    [SerializeField] private NoticeUI noticeUI;

    // Confirm 연속 입력이 같은 건설 요청을 중복 실행하지 못하도록 동기 실행 구간을 보호한다.
    private bool isProcessing;

    // Command 실행 중 TransactionPort 메서드들이 동일한 저장 대상과 건물을 사용하도록 보관한다.
    // 프로젝트에 전역 SaveData가 별도로 존재하므로 실제 저장 타입을 풀네임으로 명시한다.
    private ND.Framework.SaveData processingSaveData;
    private string processingDisplayName = string.Empty;

    private void OnEnable()
    {
        if(popupBinding != null)
        {
            popupBinding.BuildConfirmed += HandleBuildConfirmed;
        }
    }

    private void OnDisable()
    {
        if(popupBinding != null)
        {
            popupBinding.BuildConfirmed -= HandleBuildConfirmed;
        }
    }

    private void HandleBuildConfirmed(string buildId)
    {
        if (isProcessing)
        {
            return;
        }

        // Registry는 additive Village Scene에서 관리되므로 Prefab에 Scene 참조를 중복 연결하지 않고
        // 기존 싱글톤을 통해 현재 활성 Registry를 사용한다.
        VillageBuildingRegistry registry = VillageBuildingRegistry.Instance;

        // [환경 아이템 분기] 환경 아이템은 거래재화 비용 + 다중 설치 + 삭제 파이프라인을 별도로 쓰므로
        // 이 건물(아이템 비용 + 레벨업) 트랜잭션에서 건너뛴다. VillageEnvironmentManager가 처리한다.
        if (registry != null &&
            registry.TryGetCatalogEnvironmentEntry(buildId, out _, out _, out _))
        {
            return;
        }

        if(registry == null ||
           !registry.TryGetCatalogEntry(
               buildId,
               out BuildData buildData,
               out string displayName))
        {
            ShowFailure(
                "건물 정보를 찾을 수 없어 건설을 진행하지 못했습니다.",
                $"Building construction was blocked because buildId '{buildId}' was not found.");
            return;
        }

        FrameworkRoot root = FrameworkRoot.Instance;

        if(root == null || root.CurrentSaveData == null || root.CurrentSaveData.player == null || root.SaveService == null)
        {
            ShowFailure(
                "저장 데이터가 아직 준비되지 않아 건설을 진행할 수 없습니다.",
                "Building construction was blocked because save services are unavailable.");
            return;
        }

        isProcessing = true;

        try
        {
            ND.Framework.SaveData saveData = root.CurrentSaveData;

            if(!TryGetCurrentLevel(saveData, displayName, out int currentLevel) ||
               !TryBuildDefinition(buildData, out BuildingUpgradeDefinition definition))
            {
                ShowFailure(
                    "건설 단계 정보가 올바르지 않아 요청을 처리하지 못했습니다.",
                    $"Building construction input could not be created for '{buildId}'.");
                return;
            }

            int targetLevel = currentLevel + 1;
            if (!BaseCampBuildingLevelPolicy.CanAdvance(
                    buildId,
                    targetLevel,
                    saveData.player.villageBuildings,
                    out int baseCampLevel))
            {
                int requiredBaseCampLevel =
                    BaseCampBuildingLevelPolicy.GetRequiredBaseCampLevel(buildId, targetLevel);
                ShowFailure(
                    $"베이스 캠프 Lv.{requiredBaseCampLevel}이 필요합니다. 현재 Lv.{baseCampLevel}입니다.",
                    $"Building level gate blocked '{buildId}' Lv.{targetLevel}; "
                    + $"required BaseCamp Lv.{requiredBaseCampLevel}, current Lv.{baseCampLevel}.");
                return;
            }

            PlayerMainManager player = PlayerMainManager.Instance;

            if(player == null)
            {
                ShowFailure(
                    "플레이어 인벤토리가 준비되지 않아 건설을 진행할 수 없습니다.",
                    "Building construction was blocked because PlayerMainManager is unavailable.");
                return;
            }

            var state = new BuildingUpgradeStateSnapshot
            {
                BuildingId = buildId,
                CurrentLevel = currentLevel
            };

            // RemoveItem은 itemId별 단일 항목을 대상으로 하므로 중복 ID는 손상된 인벤토리로 차단한다.
            var itemIds = new HashSet<string>(StringComparer.Ordinal);

            foreach(CargoEntrySaveData entry in player.HomeInventory)
            {
                if(entry?.item == null ||
                   string.IsNullOrWhiteSpace(entry.item.itemId) ||
                   entry.quantity < 0 ||
                   !itemIds.Add(entry.item.itemId))
                {
                    ShowFailure(
                        "창고 인벤토리 정보가 올바르지 않아 건설을 중단했습니다.",
                        "Building construction was blocked because home inventory is invalid.");
                    return;
                }

                state.HomeInventory.Add(
                    new BuildingUpgradeInventoryEntry
                    {
                        ItemId = entry.item.itemId,
                        Quantity = entry.quantity
                    });
            }

            BuildingUpgradeInputAdapterResult adapted =
                BuildingUpgradeInputAdapter.Build(buildId, state, definition);

            if(adapted == null || !adapted.Success || adapted.Input == null)
            {
                ShowFailure(
                    "건설 조건을 충족하지 못했습니다. 필요한 재료와 현재 건물 레벨을 확인해 주세요.",
                    $"Building construction input was rejected: {adapted?.FailureReason}.");
                return;
            }

            // Port 메서드는 Command가 동기 실행되는 동안에만 이 저장 문맥을 사용한다.
            processingSaveData = saveData;
            processingDisplayName = displayName;

            BuildingUpgradeCommandResult result =
                BuildingUpgradeCommand.Execute(adapted.Input, this);

            if(result == null || !result.Succeeded)
            {
                ShowFailure(
                    GetFailureUserMessage(result),
                    $"Building construction failed: {result?.ErrorCode ?? "NULL_RESULT"}.");
            }
        }
        finally
        {
            processingSaveData = null;
            processingDisplayName = string.Empty;
            isProcessing = false;
        }
    }

    /// <summary>
    /// 플레이어에게는 이해 가능한 문구를 표시하고, 원인 코드는 Console에 별도로 보존한다.
    /// Scene 참조가 비어 있어도 현재 활성 UI의 기존 NoticeUI를 재사용한다.
    /// </summary>
    private void ShowFailure(string userMessage, string diagnosticMessage)
    {
        noticeUI?.Show(userMessage);
        Debug.LogError(diagnosticMessage, this);
    }

    /// <summary>
    /// 내부 실패 코드는 로그와 테스트에서 유지하고, NoticeUI에는 사용자가 이해할 수 있는
    /// 한국어 문구만 전달한다. 알 수 없는 코드는 안전한 공통 문구로 처리한다.
    /// </summary>
    private static string GetFailureUserMessage(BuildingUpgradeCommandResult result)
    {
        string errorCode = result?.ErrorCode ?? string.Empty;

        switch(errorCode)
        {
            case "BUILDING_STAGE_BASECAMP_LEVEL_BLOCKED":
                return "베이스 캠프 레벨이 부족하여 건설하거나 증축할 수 없습니다.";
            case "BUILDING_ALREADY_MAX_LEVEL":
                return "이미 최대 레벨인 건물입니다.";
            case "BUILDING_LEVEL_NOT_FOUND":
                return "다음 건설 단계 정보를 찾을 수 없습니다.";
            case "BUILDING_INSUFFICIENT_MATERIALS":
                return "건설 또는 증축에 필요한 재료가 부족합니다.";
            case "BUILDING_HOME_INVENTORY_CORRUPTED":
            case "BUILDING_STAGE_BUILDING_DATA_INVALID":
            case "BUILDING_STAGE_DUPLICATE_BUILDING":
            case "BUILDING_STAGE_LEVEL_MISMATCH":
            case "BUILDING_STAGE_MATERIAL_MISMATCH":
                return "건물 또는 창고 정보를 확인할 수 없어 건설을 중단했습니다.";
            case "BUILDING_SAVE_FAILED":
                return "저장에 실패하여 건설 변경 사항을 취소했습니다.";
            default:
                return "건설 처리 중 오류가 발생했습니다. 적용된 변경은 취소되었습니다.";
        }
    }

    /// <summary>
    /// 재료 차감과 건물 레벨 변경 전에 트랜잭션이 변경할 저장 필드를 복사한다.
    /// Stage 또는 저장 실패 시 두 변경을 한 단위로 되돌리기 위한 Snapshot이다.
    /// </summary>
    public IBuildingUpgradeTransactionSnapshot CaptureSnapshot()
    {
        if(processingSaveData?.player?.homeInventory == null || processingSaveData.player.villageBuildings == null)
        {
            return null;
        }

        return new TransactionSnapshot
        {
            homeInventory = CloneHomeInventory(processingSaveData.player.homeInventory),
            villageBuildings = CloneVillageBuildings(processingSaveData.player.villageBuildings),
            cottageProduction = CloneCottageProduction(
                processingSaveData.player.cottageProduction)
        };
    }

    /// <summary>
    /// 계획과 현재 저장 상태가 일치하는지 재검증한 뒤 재료 차감과 목표 레벨 변경을 저장 데이터에 임시 반영한다.
    /// </summary>
    public bool TryStage(BuildingUpgradeEconomicPlan plan, out string errorCode)
    {
        errorCode = string.Empty;

        PlayerMainManager player = PlayerMainManager.Instance;
        List<VillageBuildingSaveData> buildings =
            processingSaveData?.player?.villageBuildings;

        if(plan == null ||
           player == null ||
           buildings == null ||
           string.IsNullOrWhiteSpace(processingDisplayName) ||
           plan.TargetLevel < 1)
        {
            errorCode = "BUILDING_STAGE_CONTEXT_INVALID";
            return false;
        }

        // UI 사전 검사는 안내용이다. 실제 상태 변경 경계에서도 현재 SaveData를 다시 확인해
        // 다른 호출 경로나 향후 비동기화가 BaseCamp 레벨 상한을 우회하지 못하게 한다.
        if(!BaseCampBuildingLevelPolicy.CanAdvance(
               plan.BuildingId,
               plan.TargetLevel,
               buildings,
               out _))
        {
            errorCode = "BUILDING_STAGE_BASECAMP_LEVEL_BLOCKED";
            return false;
        }

        VillageBuildingSaveData target = null;

        // 재료를 차감하기 전에 저장된 건물 상태가 계획의 이전 레벨과 같은지 검증한다.
        foreach(VillageBuildingSaveData building in buildings)
        {
            if(building == null ||
               string.IsNullOrWhiteSpace(building.displayName) ||
               building.level < 1)
            {
                errorCode = "BUILDING_STAGE_BUILDING_DATA_INVALID";
                return false;
            }

            if(!string.Equals(
                building.displayName,
                processingDisplayName,
                StringComparison.Ordinal))
            {
                continue;
            }

            if(target != null)
            {
                errorCode = "BUILDING_STAGE_DUPLICATE_BUILDING";
                return false;
            }

            target = building;
        }

        int savedLevel = target != null ? target.level : 0;

        if(savedLevel != plan.PreviousLevel)
        {
            errorCode = "BUILDING_STAGE_LEVEL_MISMATCH";
            return false;
        }

        // 일부 재료만 차감되는 상황을 막기 위해 모든 수량을 먼저 재검증한다.
        foreach(BuildingUpgradeMaterialPlan material in plan.Materials)
        {
            if(material == null ||
               string.IsNullOrWhiteSpace(material.ItemId) ||
               material.RequiredQuantity <= 0 ||
               material.QuantityAfter !=
                   material.QuantityBefore - material.RequiredQuantity ||
               player.GetItemCount(material.ItemId) != material.QuantityBefore)
            {
                errorCode = "BUILDING_STAGE_MATERIAL_MISMATCH";
                return false;
            }
        }

        // 검증이 모두 끝난 뒤 기존 PlayerMainManager API를 통해 실제 거점 재료를 차감한다.
        foreach(BuildingUpgradeMaterialPlan material in plan.Materials)
        {
            if(!player.RemoveItem(material.ItemId, material.RequiredQuantity))
            {
                errorCode = "BUILDING_STAGE_REMOVE_FAILED";
                return false;
            }
        }

        if(target != null)
        {
            target.level = plan.TargetLevel;
        }
        else
        {
            buildings.Add(
                new VillageBuildingSaveData
                {
                    displayName = processingDisplayName,
                    level = plan.TargetLevel
                });
        }

        FrameworkRoot root = FrameworkRoot.Instance;
        if (root?.CottageProduction?.Config != null
            && string.Equals(
                processingDisplayName,
                root.CottageProduction.Config.BuildingDisplayName,
                StringComparison.Ordinal)
            && !root.CottageProduction.TryStageBuildingLevelChange(
                processingSaveData,
                plan.PreviousLevel,
                plan.TargetLevel,
                out errorCode))
        {
            return false;
        }

        return true;
    }

    /// <summary>Stage에서 함께 변경한 인벤토리와 건물 레벨을 기존 SaveService로 한 번에 저장한다.</summary>
    public bool TrySave(out string errorCode)
    {
        errorCode = string.Empty;

        FrameworkRoot root = FrameworkRoot.Instance;

        if(root == null ||
           root.SaveService == null ||
           processingSaveData == null ||
           !ReferenceEquals(root.CurrentSaveData, processingSaveData))
        {
            errorCode = "BUILDING_SAVE_CONTEXT_INVALID";
            return false;
        }

        SaveResult result = root.SaveService.Save(processingSaveData);

        if(result == null || !result.Succeeded)
        {
            errorCode = result != null && !string.IsNullOrWhiteSpace(result.Message)
                ? result.Message
                : "BUILDING_SAVE_FAILED";
            return false;
        }

        return true;
    }

    /// <summary>Stage 또는 저장 실패 시 재료 수량과 건물 저장 정보를 작업 전 Snapshot으로 복원한다.</summary>
    public bool TryRollback(IBuildingUpgradeTransactionSnapshot snapshot, out string errorCode)
    {
        errorCode = string.Empty;

        TransactionSnapshot transactionSnapshot =
            snapshot as TransactionSnapshot;
        PlayerMainManager player = PlayerMainManager.Instance;

        if(transactionSnapshot == null ||
           transactionSnapshot.homeInventory == null ||
           transactionSnapshot.villageBuildings == null ||
           transactionSnapshot.cottageProduction == null ||
           processingSaveData?.player == null ||
           player == null)
        {
            errorCode = "BUILDING_ROLLBACK_CONTEXT_INVALID";
            return false;
        }

        // 정상 Stage는 수량만 감소시키므로 Snapshot과의 차이만 기존 API로 되돌린다.
        foreach(CargoEntrySaveData entry in transactionSnapshot.homeInventory)
        {
            if(entry?.item == null ||
               string.IsNullOrWhiteSpace(entry.item.itemId) ||
               entry.quantity < 0)
            {
                errorCode = "BUILDING_ROLLBACK_INVENTORY_INVALID";
                return false;
            }

            int currentQuantity = player.GetItemCount(entry.item.itemId);
            int difference = entry.quantity - currentQuantity;

            if(difference > 0)
            {
                player.AddItem(entry.item, difference);
            }
            else if(difference < 0 &&
                    !player.RemoveItem(entry.item.itemId, -difference))
            {
                errorCode = "BUILDING_ROLLBACK_INVENTORY_FAILED";
                return false;
            }
        }

        // Runtime Commit 전 단계이므로 저장 목록만 원래 레벨과 배치 정보로 복원한다.
        processingSaveData.player.villageBuildings.Clear();
        processingSaveData.player.villageBuildings.AddRange(
            CloneVillageBuildings(transactionSnapshot.villageBuildings));
        processingSaveData.player.cottageProduction =
            CloneCottageProduction(transactionSnapshot.cottageProduction);

        return true;
    }

    /// <summary>저장이 성공한 목표 레벨을 현재 VillageBuildingRegistry의 실제 건물에 반영한다.</summary>
    public void CommitRuntime(BuildingUpgradeEconomicPlan plan)
    {
        VillageBuildingRegistry registry = VillageBuildingRegistry.Instance;

        if(plan == null ||
           registry == null ||
           string.IsNullOrWhiteSpace(processingDisplayName))
        {
            throw new InvalidOperationException(
                "Building runtime commit context is invalid.");
        }

        // 저장이 확정된 목표 레벨을 적용하며 AddOrUpgrade로 중복 증가시키지 않는다.
        registry.ApplySavedBuildingLevel(
            processingDisplayName,
            plan.TargetLevel);
    }

    /// <summary>저장과 실제 건물 반영까지 성공한 뒤에만 건물 목록 UI를 갱신한다.</summary>
    public void PublishSuccess(BuildingUpgradeEconomicPlan plan)
    {
        // 목록은 저장과 Runtime Commit이 모두 성공한 경우에만 다시 그린다.
        buildingListPanel?.Rebuild();
        // BaseCamp 증축은 빈 Caravan 슬롯의 Locked/Empty 상태를 바꾸므로 저장 성공 후에만 알린다.
        FrameworkEvents.RaiseVillageBuildingsChanged();
        FrameworkEvents.RaiseCottageProductionChanged();
    }

    // 건설은 아이템 정의를 변경하지 않고 수량만 변경하므로 item 참조는 재사용한다.
    // CargoEntrySaveData만 새로 만들어 차감으로 원본 항목이 제거돼도 수량을 복원할 수 있게 한다.
    private static List<CargoEntrySaveData> CloneHomeInventory(List<CargoEntrySaveData> source)
    {
        var clone = new List<CargoEntrySaveData>();

        if (source == null)
        {
            return clone;
        }

        foreach (CargoEntrySaveData entry in source)
        {
            clone.Add(entry == null
                ? null
                : new CargoEntrySaveData
                {
                    item = entry.item,
                    quantity = entry.quantity
                });
        }
        return clone;
    }

    private static CottageProductionSaveData CloneCottageProduction(
        CottageProductionSaveData source)
    {
        if (source == null)
            return new CottageProductionSaveData();
        return new CottageProductionSaveData
        {
            initialized = source.initialized,
            initialSupplyGranted = source.initialSupplyGranted,
            storedWagonCount = source.storedWagonCount,
            storedWagonContentId = source.storedWagonContentId,
            storedDraftAnimalCount = source.storedDraftAnimalCount,
            storedDraftAnimalContentId = source.storedDraftAnimalContentId,
            nextWagonProductionUtcTicks = source.nextWagonProductionUtcTicks,
            nextDraftAnimalProductionUtcTicks = source.nextDraftAnimalProductionUtcTicks,
            lastEvaluatedUtcTicks = source.lastEvaluatedUtcTicks
        };
    }

    // 레벨 롤백이 기존 배치 좌표와 회전 정보를 손실하지 않도록 전체 저장 필드를 복사한다.
    private static List<VillageBuildingSaveData> CloneVillageBuildings(List<VillageBuildingSaveData> source)
    {
        var clone = new List<VillageBuildingSaveData>();

        if(source == null)
        {
            return clone;
        }

        foreach(VillageBuildingSaveData entry in source)
        {
            if(entry == null)
            {
                clone.Add(null);
                continue;
            }

            clone.Add(new VillageBuildingSaveData
            {
                displayName = entry.displayName,
                level = entry.level,

                hasPlacement = entry.hasPlacement,
                gridCellX = entry.gridCellX,
                gridCellZ = entry.gridCellZ,
                yawStep = entry.yawStep
            });
        }

        return clone;
    }

    /// <summary>
    /// 트랜잭션이 실제로 변경할 SaveData에서 현재 건물 레벨을 조회한다.
    /// 저장 항목이 없으면 미건축 Lv.0으로 처리한다.
    /// 저장 목록의 잘못된 항목이나 같은 displayName 중복은 임의 보정하지 않고 실패한다.
    /// </summary>
    private static bool TryGetCurrentLevel(
        ND.Framework.SaveData saveData,
        string displayName,
        out int currentLevel)
    {
        currentLevel = 0;

        if(saveData?.player?.villageBuildings == null || string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        VillageBuildingSaveData found = null;

        foreach(VillageBuildingSaveData entry in saveData.player.villageBuildings)
        {
            if(entry == null ||
               string.IsNullOrWhiteSpace(entry.displayName) ||
               entry.level < 1)
            {
                return false;
            }

            if(!string.Equals(entry.displayName, displayName, StringComparison.Ordinal))
            {
                continue;
            }

            if(found != null)
            {
                return false;
            }

            found = entry;
        }

        currentLevel = found != null && found.level > 0
            ? found.level
            : 0;

        return true;
    }

    /// <summary>
    /// BuildData의 레벨별 재료 요구조건을 기존 Economy 입력 정의로 변환한다.
    /// 별도 비용 Catalog를 만들지 않고 BuildData를 건설 비용의 단일 원본으로 유지한다.
    /// 빈 requireItems는 의도된 무료 건설로 허용하되 잘못된 항목은 요청을 차단한다.
    /// </summary>
    private static bool TryBuildDefinition(
        BuildData buildData,
        out BuildingUpgradeDefinition definition)
    {
        definition = null;

        if(buildData == null ||
           string.IsNullOrWhiteSpace(buildData.BuildId) ||
           buildData.DataPerLevels == null ||
           buildData.DataPerLevels.Length == 0)
        {
            return false;
        }

        var levels = new List<BuildingUpgradeLevelDefinition>();
        int maximumLevel = 0;

        foreach(DataPerLevel levelData in buildData.DataPerLevels)
        {
            // 거래·저장이 시작되기 전에 목표 레벨 외형 누락을 거부해 부분 성공을 막는다.
            if(levelData == null || levelData.level < 1 || levelData.buildPrefab == null)
            {
                return false;
            }

            maximumLevel = Math.Max(maximumLevel, levelData.level);

            var levelDefinition = new BuildingUpgradeLevelDefinition
            {
                Level = levelData.level
            };

            BuildRequireItem[] requirements =
                levelData.buildRequirements?.requireItems
                ?? Array.Empty<BuildRequireItem>();

            foreach(BuildRequireItem requirement in requirements)
            {
                if(requirement == null ||
                   string.IsNullOrWhiteSpace(requirement.itemId) ||
                   requirement.quantity <= 0)
                {
                    return false;
                }

                levelDefinition.Materials.Add(
                    new BuildingUpgradeMaterialRequirement
                    {
                        ItemId = requirement.itemId,
                        Quantity = requirement.quantity
                    });
            }

            levels.Add(levelDefinition);
        }

        definition = new BuildingUpgradeDefinition
        {
            BuildingId = buildData.BuildId,
            MaximumLevel = maximumLevel,
            Levels = levels
        };

        return true;
    }

    // 이 Handler의 TransactionPort 구현에서만 사용하는 복구 데이터이므로 외부 타입으로 노출하지 않는다.
    private sealed class TransactionSnapshot : IBuildingUpgradeTransactionSnapshot
    {
        public List<CargoEntrySaveData> homeInventory;
        public List<VillageBuildingSaveData> villageBuildings;
        public CottageProductionSaveData cottageProduction;
    }
}
