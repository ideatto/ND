// =============================================================================
// CaravanAssetLock — 멀티 상단 자산 잠금 (같은 마차·동물·용병 중복 배치 차단)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [문제] 상단을 여러 개(최대 4) 운영하면, 보유한 마차 1대를 2개 상단에 동시에
//        배치해버릴 수 있다. 그러면 마차 하나로 두 무역이 동시에 출발하는 사태가 난다.
//
// [규칙] 한 자산(보유 개체)은 한 번에 한 상단만 쓸 수 있다.
//        특히 그 상단이 "이동 중(Traveling)" 또는 "정산 대기(Settling)"면
//        자산이 묶여 있으므로 다른 상단이 가져갈 수 없다.
//          · 근거: Multi_Caravan_Save_Architecture — "not locked by another
//                  Traveling or SettlementPending flow"
//          · 근거: 0721_Caravan_Overview_UI_Contract — "자산 중복 배치 차단 규칙"(Core 요청)
//
// [식별] 자산은 종류가 아니라 <b>보유 개체 식별자(instanceId)</b>로 구분한다.
//        같은 종류 마차를 2대 가질 수 있으므로 종류 ID로는 판별할 수 없다.
//        instanceId는 소유·저장 시스템(Framework)이 부여하고 Core는 비교만 한다.
//
// [주의] 준비(Prepare) 상태 상단끼리도 같은 자산을 겹쳐 넣을 수 없다(출발 시 충돌).
//        다만 "잠금"의 강도는 다르다 — Traveling/Settling은 되돌릴 수 없고,
//        Prepare끼리는 사용자가 구성을 바꿔 풀 수 있다. 사유로 구분해 돌려준다.
//
// [순수 로직] static · UnityEngine 의존 없음 → 테스트 가능.
// =============================================================================

using System.Collections.Generic;

/// <summary>자산이 막힌 이유.</summary>
public enum AssetLockReason
{
    WagonInUse,       // 그 마차를 다른 상단이 쓰는 중
    AnimalInUse,      // 그 견인 동물을 다른 상단이 쓰는 중
    MercenaryInUse    // 그 용병을 다른 상단이 쓰는 중
}

/// <summary>막힌 자산 한 건.</summary>
public class AssetLockConflict
{
    public AssetLockReason reason;      // 무엇이 막혔나
    public string assetInstanceId;      // 막힌 보유 개체 ID
    public string lockedByCaravanId;    // 그걸 쓰고 있는 상단
    public bool lockedByActiveJourney;  // true면 이동/정산 중이라 되돌릴 수 없음
}

/// <summary>자산 잠금 검사 결과.</summary>
public class AssetLockResult
{
    public bool canUse;   // 충돌이 하나도 없으면 true
    public List<AssetLockConflict> conflicts = new List<AssetLockConflict>();
}

/// <summary>멀티 상단 자산 중복 배치 차단. 자세한 설명은 파일 상단 주석 참고.</summary>
public static class CaravanAssetLock
{
    /// <summary>
    /// 이 상단이 자산을 "묶어두는" 상태인가.
    /// 이동 중·정산 대기면 구성을 바꿀 수 없으므로 자산이 잠긴다.
    /// </summary>
    public static bool HoldsAssets(CaravanData caravan)
    {
        if (caravan == null) return false;
        return caravan.state == JourneyState.Traveling
            || caravan.state == JourneyState.Settling;
    }

    /// <summary>
    /// target 상단이 쓰려는 자산들이 다른 상단과 겹치는지 검사한다.
    /// </summary>
    /// <param name="target">검사할 상단(구성 중이거나 출발하려는 상단).</param>
    /// <param name="allCaravans">플레이어의 전체 상단 목록(target 포함해도 됨 — caravanId로 자기 자신 제외).</param>
    public static AssetLockResult Validate(CaravanData target, IEnumerable<CaravanData> allCaravans)
    {
        AssetLockResult result = new AssetLockResult();
        if (target == null || allCaravans == null)
        {
            result.canUse = (target != null);
            return result;
        }

        foreach (CaravanData other in allCaravans)
        {
            if (other == null) continue;
            if (ReferenceEquals(other, target)) continue;
            // caravanId가 같으면 같은 상단 — 목록에 사본이 들어와도 자기 자신은 건너뛴다.
            if (!string.IsNullOrEmpty(target.caravanId)
                && string.Equals(other.caravanId, target.caravanId, System.StringComparison.Ordinal))
                continue;

            bool active = HoldsAssets(other);

            // 마차
            if (IsSameAsset(target.wagon, other.wagon))
                AddConflict(result, AssetLockReason.WagonInUse,
                    target.wagon.instanceId, other.caravanId, active);

            // 견인 동물
            if (target.animals != null && other.animals != null)
            {
                foreach (imsiAnimalData mine in target.animals)
                {
                    if (mine == null || string.IsNullOrEmpty(mine.instanceId)) continue;
                    foreach (imsiAnimalData theirs in other.animals)
                    {
                        if (theirs == null) continue;
                        if (string.Equals(mine.instanceId, theirs.instanceId, System.StringComparison.Ordinal))
                        {
                            AddConflict(result, AssetLockReason.AnimalInUse,
                                mine.instanceId, other.caravanId, active);
                            break;
                        }
                    }
                }
            }

            // 용병
            if (target.mercenaries != null && other.mercenaries != null)
            {
                foreach (imsiMercenaryData mine in target.mercenaries)
                {
                    if (mine == null || string.IsNullOrEmpty(mine.instanceId)) continue;
                    foreach (imsiMercenaryData theirs in other.mercenaries)
                    {
                        if (theirs == null) continue;
                        if (string.Equals(mine.instanceId, theirs.instanceId, System.StringComparison.Ordinal))
                        {
                            AddConflict(result, AssetLockReason.MercenaryInUse,
                                mine.instanceId, other.caravanId, active);
                            break;
                        }
                    }
                }
            }
        }

        result.canUse = (result.conflicts.Count == 0);
        return result;
    }

    /// <summary>두 마차가 같은 보유 개체인가(instanceId 비교). 하나라도 없으면 비교 불가로 false.</summary>
    private static bool IsSameAsset(imsiWagonData a, imsiWagonData b)
    {
        if (a == null || b == null) return false;
        if (string.IsNullOrEmpty(a.instanceId) || string.IsNullOrEmpty(b.instanceId)) return false;
        return string.Equals(a.instanceId, b.instanceId, System.StringComparison.Ordinal);
    }

    private static void AddConflict(
        AssetLockResult result, AssetLockReason reason,
        string assetInstanceId, string lockedByCaravanId, bool active)
    {
        result.conflicts.Add(new AssetLockConflict
        {
            reason = reason,
            assetInstanceId = assetInstanceId,
            lockedByCaravanId = lockedByCaravanId,
            lockedByActiveJourney = active
        });
    }
}
