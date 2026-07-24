// =============================================================================
// VillageNeed — NPC의 욕구(니즈) 종류 + 건물 역할 태그
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 빌리지 전용 "니즈 기반 NPC" 시스템의 공용 정의.
//        · NeedType: NPC가 가진 숨은 욕구 종류(허기·체력·심심).
//        · BuildingNeedProvider: "이 건물은 어떤 욕구를 채워주나"를 표시하는 건물 태그.
//
// [경계] 이 시스템은 빌리지 씬 안에서만 돈다. NPC 스탯은 저장하지 않고(SaveData 무관),
//        무역·경제 등 다른 도메인과 엮이지 않는 독립 놀이터다.
// =============================================================================

using UnityEngine;

/// <summary>NPC의 숨은 욕구 종류.</summary>
public enum NeedType
{
    Hunger,   // 허기 — 빵집에서 채움
    Energy,   // 체력 — 집(오두막)에서 쉼
    Fun,      // 심심 — 상점·구경 등에서 채움
}

/// <summary>이 건물이 채워주는 욕구를 표시하는 태그(건물 프리팹/씬 오브젝트에 붙임).</summary>
public class BuildingNeedProvider : MonoBehaviour
{
    [Tooltip("이 건물을 방문하면 채워지는 욕구 종류.")]
    public NeedType satisfies = NeedType.Fun;

    [Tooltip("한 번 방문(상호작용)으로 회복되는 양(0~100).")]
    [Range(10f, 100f)] public float restoreAmount = 60f;
}
