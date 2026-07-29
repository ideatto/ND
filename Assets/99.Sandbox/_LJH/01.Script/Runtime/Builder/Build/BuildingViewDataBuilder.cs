using UnityEngine;
using ND.Framework;
using System;
using System.Collections.Generic;

/// <summary>
/// BuildData의 정적 설정과 현재 플레이어 상태를 조합하여
/// 건축 상세/확인 UI에 전달할 ViewData를 생성한다.
/// 실제 재료 차감, 건물 생성 및 저장은 수행하지 않는다.
/// </summary>
public class BuildingViewDataBuilder
{
    /// <summary>
    /// 선택한 건물의 현재 레벨을 기준으로 다음 레벨의 상세 UI 데이터를 생성한다.
    /// currentLevel은 현재 임시 연결에서는 0을 전달하고,
    /// 추후 건물 진행 상태 Provider가 SaveData에서 조회한 값을 전달한다.
    /// </summary>
    public BuildingDetailViewData BuildDetail(BuildData buildData, int currentLevel, PlayerMainManager player, ISharedGameDataProvider sharedGameData)
    {
        // 음수 레벨이 들어와도 미건설 상태인 0보다 낮아지지 않도록 보정한다.
        int safeCurrentLevel = Mathf.Max(0, currentLevel);
        int targetLevel = safeCurrentLevel + 1;

        if(buildData == null)
        {
            return CreateInvalidDetail(safeCurrentLevel, targetLevel, "건물 데이터가 없습니다.");
        }

        DataPerLevel targetLevelData = FindTargetLevelData(buildData, targetLevel);

        if(targetLevelData == null)
        {
            return new BuildingDetailViewData
            {
                buildId = buildData.BuildId,
                displayName = buildData.DisplayName,
                description = buildData.Description,

                currentLevel = safeCurrentLevel,
                targetLevel = targetLevel,
                isConstruction = safeCurrentLevel == 0,

                canProceed = false,
                disabledReason = "최대 레벨이거나 목표 레벨 데이터가 없습니다."
            };
        }

        // 상세 UI와 확인 UI가 동일한 기준으로 활성화되도록
        // 재화와 아이템 요구 조건을 한 번에 평가한다.
        RequirementResult requirementResult = EvaluateRequirements(targetLevelData.buildRequirements, player, sharedGameData);

        return new BuildingDetailViewData
        {
            buildId = buildData.BuildId,
            displayName = buildData.DisplayName,
            description = buildData.Description,

            currentLevel = safeCurrentLevel,
            targetLevel = targetLevel,
            isConstruction = safeCurrentLevel == 0,

            previewPrefab = targetLevelData.buildPrefab,

            currencyRequirement = requirementResult.currencyRequirement,
            itemRequirements = requirementResult.itemRequirements,

            canProceed = requirementResult.isSatisfied,
            disabledReason = requirementResult.disabledReason,
        };
    }

    /// <summary>
    /// 상세 UI에서 선택한 건물과 같은 조건을 사용하여
    /// 최종 확인 Popup에 전달할 ViewData를 생성한다.
    /// </summary>
    public BuildingConfirmViewData BuildConfirm(BuildData buildData, int currentLevel, PlayerMainManager player, ISharedGameDataProvider sharedGameData)
    {
        // Detail과 Confirm이 서로 다른 목표 레벨을 표시하지 않도록
        // 두 메서드에서 같은 레벨 보정 규칙을 사용한다.
        int safeCurrentLevel = Mathf.Max(0, currentLevel);
        int targetLevel = safeCurrentLevel + 1;

        if(buildData == null)
        {
            return CreateInvalidConfirm(safeCurrentLevel, targetLevel, "건물 데이터가 없습니다.");
        }

        DataPerLevel targetLevelData = FindTargetLevelData(buildData, targetLevel);

        if(targetLevelData == null)
        {
            return new BuildingConfirmViewData
            {
                buildId = buildData.BuildId,
                displayName = buildData.DisplayName,

                targetLevel = targetLevel,
                isConstruction = safeCurrentLevel == 0,

                canConfirm = false,
                disabledReason = "최대 레벨이거나 목표 레벨 데이터가 없습니다."
            };
        }

        RequirementResult requirementResult = EvaluateRequirements(targetLevelData.buildRequirements, player, sharedGameData);

        return new BuildingConfirmViewData
        {
            buildId = buildData.BuildId,
            displayName = buildData.DisplayName,

            targetLevel = targetLevel,
            isConstruction = safeCurrentLevel == 0,

            previewPrefab = targetLevelData.buildPrefab,

            currencyRequirement = requirementResult.currencyRequirement,
            itemRequirements = requirementResult.itemRequirements,

            canConfirm = requirementResult.isSatisfied,
            disabledReason = requirementResult.disabledReason
        };
    }

    /// <summary>
    /// DataPerLevels의 배열 위치가 아니라 각 항목의 level 값으로
    /// 현재 레벨 다음에 해당하는 데이터를 찾는다.
    /// </summary>
    private static DataPerLevel FindTargetLevelData(BuildData buildData, int targetLevel)
    {
        DataPerLevel[] levelDataList = buildData.DataPerLevels;

        for(int i = 0; i < levelDataList.Length; i++)
        {
            DataPerLevel levelData = levelDataList[i];

            if(levelData != null && levelData.level == targetLevel)
            {
                return levelData;
            }
        }

        return null;
    }

    /// <summary>
    /// 재화와 아이템 요구 조건을 평가하여 UI 표시 데이터,
    /// 전체 충족 여부 및 버튼 비활성화 사유를 함께 만든다.
    /// </summary>
    private static RequirementResult EvaluateRequirements(BuildRequirement requirement, PlayerMainManager player, ISharedGameDataProvider sharedGameData)
    {
        var result = new RequirementResult();

        // 요구 조건이 없는 레벨은 별도의 비용 없이 진행할 수 있다.
        if(requirement == null)
        {
            result.isSatisfied = true;
            return result;
        }

        BuildCurrencyRequirement(requirement.requireCurrency, player, result);
        BuildItemRequirements(requirement.requireItems, player, sharedGameData, result);
        result.isSatisfied = !result.hasInvalidData && result.currencyRequirement.isSatisfied && result.areAllItemSatisfied;
        result.disabledReason = ResolveDisabledReason(result);

        return result;
    }

    /// <summary>
    /// PlayerMainManager의 현재 거래 재화와 건물의 요구 재화를 비교한다.
    /// isRequired가 false이면 UI에서 숨기고 충족된 조건으로 처리한다.
    /// </summary>
    private static void BuildCurrencyRequirement(BuildRequireCurrency requirement, PlayerMainManager player, RequirementResult result)
    {
        bool isVisible = requirement != null && requirement.isRequired;

        long requiredAmount = isVisible ? Math.Max(0L, requirement.value) : 0L;
        long ownedAmount = player != null ? Math.Max(0L, player.Gold) : 0L;

        result.currencyRequirement = new CurrencyRequirementViewData
        {
            isVisible = isVisible,
            ownedAmount = ownedAmount,
            requiredAmount = requiredAmount,

            isSatisfied = !isVisible || ownedAmount >= requiredAmount
        };
    }

    /// <summary>
    /// 각 요구 아이템의 이름은 Shared Game Data에서,
    /// 현재 보유 수량은 PlayerMainManager의 거점 인벤토리에서 조회한다.
    /// CaravanCargo는 건축 재료 수량에 포함하지 않는다.
    /// </summary>
    private static void BuildItemRequirements(BuildRequireItem[] requirements, PlayerMainManager player, ISharedGameDataProvider sharedGameData, RequirementResult result)
    {
        BuildRequireItem[] safeRequirements = requirements ?? Array.Empty<BuildRequireItem>();

        var itemViewDataList = new List<ItemRequirementViewData>();

        for (int i = 0; i < safeRequirements.Length; i++)
        {
            BuildRequireItem requirement = safeRequirements[i];

            // 요구 아이템은 유효한 ID와 1 이상의 수량을 가져야 한다.
            if (requirement == null || string.IsNullOrWhiteSpace(requirement.itemId) || requirement.quantity <= 0)
            {
                result.hasInvalidData = true;
                continue;
            }

            int ownedQuantity = player != null ? Mathf.Max(0, player.GetItemCount(requirement.itemId)) : 0;

            SharedTradeItemDefinition itemDefinition = null;

            // Shared Game Data는 저장 수량이 아니라
            // 아이템 표시 이름과 추후 연결될 아이콘 같은 정적 정보를 제공한다.
            bool foundItemDefinition = sharedGameData != null && sharedGameData.TryGetTradeItem(requirement.itemId, out itemDefinition);

            if (!foundItemDefinition || itemDefinition == null)
            {
                result.hasInvalidData = true;
            }

            bool isSatisfied = ownedQuantity >= requirement.quantity;

            if (!isSatisfied)
            {
                result.areAllItemSatisfied = false;
            }

            itemViewDataList.Add(new ItemRequirementViewData
            {
                itemId = requirement.itemId,
                displayName = foundItemDefinition && itemDefinition != null ? itemDefinition.DisplayName : requirement.itemId,

                // TODO: SharedTradeItemDefinition.Icon 매핑 작업이 병합되면
                // itemDefinition.Icon을 연결한다.
                icon = null,

                ownedQuantity = ownedQuantity,
                requiredQuantity = requirement.quantity,
                isSatisfied = isSatisfied,
            });
        }

        result.itemRequirements = itemViewDataList.ToArray();
    }

    /// <summary>
    /// UI 버튼이 비활성화된 가장 우선적인 이유를 반환한다.
    /// 데이터 오류를 자원 부족보다 먼저 노출하여 잘못된 설정을 구분한다.
    /// </summary>
    private static string ResolveDisabledReason(RequirementResult result)
    {
        if (result.hasInvalidData)
        {
            return "건축 요구 데이터가 올바르지 않습니다.";
        }
        if (!result.currencyRequirement.isSatisfied)
        {
            return "보유 재화가 부족합니다.";
        }
        if (!result.areAllItemSatisfied)
        {
            return "보유 재료가 부족합니다.";
        }

        return string.Empty;
    }

    /// <summary>
    /// BuildData가 없는 경우에도 Presenter가 null 대신
    /// 비활성화된 상세 ViewData를 받을 수 있게 한다.
    /// </summary>
    private static BuildingDetailViewData CreateInvalidDetail(int currentLevel, int targetLevel, string reason)
    {
        return new BuildingDetailViewData
        {
            currentLevel = currentLevel,
            targetLevel = targetLevel,
            isConstruction = currentLevel == 0,

            canProceed = false,
            disabledReason = reason
        };
    }

    /// <summary>
    /// BuildData가 없는 경우 사용할 비활성화된 확인 ViewData를 생성한다.
    /// </summary>
    private static BuildingConfirmViewData CreateInvalidConfirm(int currentLevel, int targetLevel, string reason)
    {
        return new BuildingConfirmViewData
        {
            targetLevel = targetLevel,
            isConstruction = currentLevel == 0,

            canConfirm = false,
            disabledReason = reason
        };
    }

    // 요구 조건 평가 과정에서 생성되는 여러 값을
    // Builder 내부에서만 전달하기 위한 보조 결과 타입이다.
    private sealed class RequirementResult
    {
        public CurrencyRequirementViewData currencyRequirement = new CurrencyRequirementViewData
        {
            isSatisfied = true
        };

        public ItemRequirementViewData[] itemRequirements = Array.Empty<ItemRequirementViewData>();

        public bool areAllItemSatisfied = true;
        public bool hasInvalidData;
        public bool isSatisfied;
        public string disabledReason = string.Empty;
    }
}
