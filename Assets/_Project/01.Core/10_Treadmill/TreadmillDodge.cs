// =============================================================================
// TreadmillDodge — 캐러밴 회피기동(길 위 나무·바위를 좌우로 피함)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템 2단계
//
// [역할] 트레드밀은 캐러밴이 제자리, 길과 장애물이 앞(+Z)에서 뒤(-Z)로 흐른다. 앞쪽 레인
//        (중앙 좁은 폭)에 회피 대상(avoid 레이어=나무·바위)이 다가오면, 캐러밴(마차+동물)을
//        반대쪽으로 부드럽게 밀고 살짝 틀어(yaw) 피한 뒤, 지나가면 다시 중앙으로 복귀한다.
//
// [연결] TreadmillStage.SetSteer(x, yaw)로 캐러밴 피벗(CaravanRoot)만 움직인다.
//        장애물은 TreadmillRoad.ActiveStrip.Obstacles(avoid 레이어 등록분)에서 읽는다.
// =============================================================================

using UnityEngine;

/// <summary>앞쪽 레인의 장애물을 감지해 캐러밴을 좌우로 피하게 하는 회피기동.</summary>
public class TreadmillDodge : MonoBehaviour
{
    [SerializeField] private TreadmillStage stage;   // 비면 자동 검색
    [SerializeField] private TreadmillRoad road;     // 비면 자동 검색

    [Header("감지")]
    [Tooltip("앞쪽 몇 m 안의 장애물을 미리 볼지.")]
    [SerializeField] private float lookahead = 22f;
    [Tooltip("충돌 지점(동물 앞)의 캐러밴 원점 기준 +Z 오프셋.")]
    [SerializeField] private float frontOffset = 2f;
    [Tooltip("중앙에서 이 폭(±m) 안의 장애물만 위협으로 본다.")]
    [SerializeField] private float dangerHalf = 2.6f;

    [Header("회피 동작(스티어링)")]
    [Tooltip("최대 좌우 회피 거리(m).")]
    [SerializeField] private float dodgeAmount = 2.6f;
    [Tooltip("최대 조향각(도). 클수록 급하게 튼다.")]
    [SerializeField] private float maxYaw = 26f;
    [Tooltip("목표까지 남은 횡거리 1m당 조향각(도).")]
    [SerializeField] private float steerGain = 22f;
    [Tooltip("핸들 꺾는 속도(도/s).")]
    [SerializeField] private float yawRate = 90f;
    [Tooltip("연결점(말)~마차 뒤축 거리(m). 마차가 말을 트레일러처럼 따라 꺾이는 정도.")]
    [SerializeField] private float wheelbase = 2.6f;

    private float currentLateral;   // 말(앞) 횡오프셋 = 연결점(hitch)
    private float currentYaw;       // 말 조향각
    private float wagonRearLat;     // 마차 뒤축 횡위치(연결점을 트레일러처럼 따라감)

    private void Awake()
    {
        if (stage == null || road == null)   // 레인 안이면 형제 부품만(다른 레인과 안 엉키게)
        {
            var lane = TreadmillLane.Of(this);
            if (lane != null) { lane.Resolve(); if (stage == null) stage = lane.stage; if (road == null) road = lane.road; }
        }
        if (stage == null) stage = Object.FindFirstObjectByType<TreadmillStage>(FindObjectsInactive.Include);
        if (road == null) road = Object.FindFirstObjectByType<TreadmillRoad>(FindObjectsInactive.Include);
    }

    private void LateUpdate()   // 스테이지 Update(동물 배치/걷기) 뒤에 조향 적용
    {
        if (stage == null || road == null)   // 레인 안이면 형제 부품만(다른 레인과 안 엉키게)
        {
            var lane = TreadmillLane.Of(this);
            if (lane != null) { lane.Resolve(); if (stage == null) stage = lane.stage; if (road == null) road = lane.road; }
        }
        if (stage == null) stage = Object.FindFirstObjectByType<TreadmillStage>(FindObjectsInactive.Include);
        if (road == null) road = Object.FindFirstObjectByType<TreadmillRoad>(FindObjectsInactive.Include);
        if (stage == null || road == null) return;

        var strip = road.ActiveStrip;
        Vector3 laneC = stage.transform.position;   // 고정 레인 중심(캐러밴 원점)
        float frontZ = laneC.z + frontOffset;

        // 앞쪽 레인에서 가장 가까운(먼저 닿을) 장애물 찾기
        float bestDz = float.MaxValue; float threatDx = 0f; bool threat = false;
        if (strip != null && strip.Obstacles != null)
        {
            var list = strip.Obstacles;
            for (int i = 0; i < list.Count; i++)
            {
                var ob = list[i];
                if (ob == null) continue;
                float dz = ob.position.z - frontZ;                 // +면 앞(다가오는 중)
                if (dz < 0.5f || dz > lookahead) continue;
                float dx = ob.position.x - laneC.x;
                if (Mathf.Abs(dx) > dangerHalf) continue;
                if (dz < bestDz) { bestDz = dz; threatDx = dx; threat = true; }
            }
        }

        // 가고 싶은 횡위치(목표): 장애물 반대쪽, 가까울수록 확실히. 위협 없으면 중앙(0).
        float targetLateral = 0f;
        if (threat)
        {
            float side = threatDx >= 0f ? -1f : 1f;
            float urgency = Mathf.Clamp01(1f - bestDz / lookahead);
            targetLateral = side * dodgeAmount * Mathf.Lerp(0.5f, 1f, urgency);
        }

        float dt = Time.deltaTime;
        float fwd = road.ForwardSpeed;   // 전진 속도(안 흐르면 0)

        if (fwd > 0.05f)
        {
            // ★스티어링: 목표 향해 핸들을 꺾고(desiredYaw), 전진×sin(각)만큼 옆으로 '돌아' 나간다.
            float latError = targetLateral - currentLateral;
            float desiredYaw = Mathf.Clamp(latError * steerGain, -maxYaw, maxYaw);
            currentYaw = Mathf.MoveTowards(currentYaw, desiredYaw, yawRate * dt);
            float drift = fwd * Mathf.Sin(currentYaw * Mathf.Deg2Rad) * dt;
            // 목표를 지나치지 않게(진동 방지)
            if ((latError > 0f && currentLateral + drift > targetLateral) ||
                (latError < 0f && currentLateral + drift < targetLateral))
                currentLateral = targetLateral;
            else
                currentLateral += drift;
        }
        else
        {
            // 정지 중엔 옆으로 못 감 → 핸들만 서서히 정면으로
            currentYaw = Mathf.MoveTowards(currentYaw, 0f, yawRate * dt);
        }

        // ★관절식: 마차는 연결점(hitch = 말 위치)을 트레일러처럼 향해 따라 꺾인다.
        float hitchLat = currentLateral;
        float cartYaw = Mathf.Atan2(hitchLat - wagonRearLat, Mathf.Max(0.1f, wheelbase)) * Mathf.Rad2Deg;
        if (fwd > 0.05f) wagonRearLat += fwd * Mathf.Sin(cartYaw * Mathf.Deg2Rad) * dt;   // 뒤축이 헤딩 방향으로 따라감
        else wagonRearLat = Mathf.MoveTowards(wagonRearLat, hitchLat, dt);                 // 정지 중엔 서서히 정렬

        stage.SetSteerArticulated(currentLateral, currentYaw, hitchLat, cartYaw);
    }
}
