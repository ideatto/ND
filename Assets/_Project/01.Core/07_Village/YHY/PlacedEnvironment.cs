// =============================================================================
// PlacedEnvironment — 설치된 '환경 아이템' 인스턴스 마커
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 마을에 설치된 환경 아이템(오크나무·벤치·울타리 등) 인스턴스 하나에 붙는
//        표식이다. 건물(종류당 1개+레벨)과 달리 환경은 같은 종류를 여러 개 놓을 수 있어,
//        인스턴스마다 고유 instanceId로 구분해야 이동·삭제·저장이 종류별로 안 엉킨다.
//
// [연동] BuildingPlacementController가 이동/회전을 확정할 때 이 컴포넌트가 있으면
//        건물 저장 경로(displayName) 대신 환경 저장 경로(instanceId, villageEnvironments)로
//        분기한다. VillageEnvironmentManager가 생성·삭제·저장을 담당한다.
// =============================================================================

using UnityEngine;

/// <summary>설치된 환경 아이템 인스턴스의 고유 식별 표식(이동/삭제/저장 분기용).</summary>
public sealed class PlacedEnvironment : MonoBehaviour
{
    // 이 설치 인스턴스의 고유 ID. 같은 종류를 여러 개 놓아도 인스턴스마다 다르다(삭제 대상 식별).
    [SerializeField] private string instanceId;
    // 환경 아이템 종류 키(카탈로그 BuildData의 buildId). 저장 복원 시 어떤 종류를 다시 만들지 결정.
    [SerializeField] private string envId;

    /// <summary>이 설치 인스턴스의 고유 ID.</summary>
    public string InstanceId => instanceId;
    /// <summary>환경 아이템 종류 키(BuildData.buildId).</summary>
    public string EnvId => envId;

    /// <summary>생성 직후 1회 식별 정보를 주입한다(런타임 생성·저장 복원 공용).</summary>
    public void Initialize(string newInstanceId, string newEnvId)
    {
        instanceId = newInstanceId;
        envId = newEnvId;
    }
}
