// =============================================================================
// TreadmillCombat — 트레드밀 전투 연출(산적 미리 등장 → 충돌 먼지 → 승/패)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 전투 연출
//
// [역할] 무역 중 전투가 발생할 지점을 예측기(TreadmillCombatPredictor)로 미리 알아내,
//        도착 마을처럼 산적(임시 큐브)을 저 앞에 'N초 전' 띄워 멀리서 대기하다 다가오게 하고,
//        마차에 닿는 순간(=전투 발생 progress) 먼지를 터뜨린다. 승/패 결과는 트레드밀이
//        따로 계산하지 않고 ★UI(전투 패널)와 똑같은 소스(caravanActivityLogs)에서 읽어 맞춘다.
//
// [불변] 전부 진행도(progress) 동기화 시각 연출이라 무역 이동시간에 전혀 영향 없음.
//        N초는 그 무역 소요시간에서 계산(clamp) — 루트별 하드코딩 아님.
//
// [연결] ProgressSync가 이동 중 매 프레임 Drive(caravanId, progress)를 호출. 정박/미표시 땐 Clear().
// [부착] TreadmillRoad와 같은 축(레인). 산적/먼지 프리팹 비우면 임시 프리미티브 자동 생성.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>전투를 미리 등장→충돌 먼지→승/패로 보여주는 트레드밀 연출(결과는 활동 로그 기준).</summary>
public class TreadmillCombat : MonoBehaviour
{
    [SerializeField] private TreadmillRoad road;   // 비면 자동 검색(레인)

    [Header("배치")]
    [Tooltip("산적을 스폰할 앞쪽 거리(m). 여기서부터 다가온다.")]
    [SerializeField] private float aheadZ = 55f;
    [Tooltip("산적이 마차와 부딪히는 지점(마차 앞 m).")]
    [SerializeField] private float stopZ = 3f;

    [Header("타이밍 — 전투 N '실초' 전에 등장")]
    [Tooltip("산적이 다가오는 실제 시간(초). 게임 시간이 배속이어도 실시간 진행속도를 측정해 이만큼 보이게 한다. 클수록 더 멀리서 오래 다가온다.")]
    [SerializeField] private float warnRealSeconds = 6f;
    [Tooltip("도착과 겹치는 전투는 연출 생략(이 progress 이상은 안 띄움). 데이터상 전투는 그대로 처리됨.")]
    [SerializeField] private float maxVisualProgress = 0.9f;
    [Tooltip("결과(승/패)가 로그에 뜬 뒤, 그 결과를 보여주고 다시 출발하기까지의 시간(초).")]
    [SerializeField] private float resultHoldSeconds = 1.0f;
    [Tooltip("결과가 끝내 안 올 때를 대비한 최대 멈춤 시간(초, 안전장치). 이 시간 넘으면 그냥 다시 출발.")]
    [SerializeField] private float clashMaxSeconds = 4f;

    [Header("연출(비우면 임시 프리미티브 자동 생성)")]
    [SerializeField] private GameObject banditPrefab;   // 산적(NPC 모델). 비면 임시 큐브
    [SerializeField] private GameObject dustPrefab;     // 먼지
    [Tooltip("산적 목표 키(m). 어떤 NPC 모델을 넣어도 이 키에 맞춰 자동 스케일(당나귀≈1.5m보다 조금 작게).")]
    [SerializeField] private float banditHeight = 1.4f;

    // ── 상태 ──
    private enum Phase { None, Approaching, Clash }
    private Phase phase;
    private string curCaravanId, curTradeId;
    private List<TreadmillCombatPredictor.CombatPoint> points;
    private int nextIndex;
    private Transform bandit;
    private GameObject dustGO;       // 충돌~재출발 동안 계속 피어나는 먼지(지속 파티클)
    private float baseY;
    private float clashElapsed;      // 충돌 후 경과(안전 타임아웃용)
    private float resultElapsed;     // 결과가 뜬 뒤 경과(이 시간 지나면 재출발)
    private bool? outcomeVictory;   // 로그에서 읽은 결과(null=아직)
    private float lastDriveP;       // 직전 progress(속도 측정용)
    private float dpdt;             // 실시간 진행속도(progress/실초, 평활) — 게임 배속 무관 실초 환산용

    // 길 커브 반영 지면 높이(도착 연출과 동일) — 산적이 그리드 위에 서게.
    private float GroundY(float z) { float c = road != null ? road.Curvature : 0.0025f; return baseY - c * z * z; }

    private void Awake() => ResolveRoad();

    private void ResolveRoad()
    {
        if (road == null) { var lane = TreadmillLane.Of(this); if (lane != null) { lane.Resolve(); road = lane.road; } }
        if (road == null) road = Object.FindFirstObjectByType<TreadmillRoad>(FindObjectsInactive.Include);
    }

    /// <summary>ProgressSync가 이동 중 매 프레임 호출(p=진행도 0~1).</summary>
    public void Drive(string caravanId, float p)
    {
        if (string.IsNullOrEmpty(caravanId)) { Clear(); return; }

        // 표시 캐러밴/무역이 바뀌면 전투 예측을 새로 계산
        string tradeId = GetTradeId(caravanId);
        if (caravanId != curCaravanId || tradeId != curTradeId)
        {
            curCaravanId = caravanId; curTradeId = tradeId;
            points = TreadmillCombatPredictor.Predict(caravanId);
            nextIndex = 0; phase = Phase.None; outcomeVictory = null;
            if (road != null) road.SetCombatHold(false); DespawnDust(); DespawnBandit();
            lastDriveP = p; dpdt = 0f;
            if (points != null) while (nextIndex < points.Count && points[nextIndex].progress01 <= p) nextIndex++;  // 이미 지난 건 스킵
        }

        // 실시간 진행속도(progress/실초) 측정 — 게임 시간이 배속이어도 '실제 N초' 등장을 위해.
        float rdt = Time.deltaTime;
        if (p >= lastDriveP && rdt > 1e-4f)
        {
            float inst = (p - lastDriveP) / rdt;
            dpdt = dpdt <= 0f ? inst : Mathf.Lerp(dpdt, inst, 0.15f);
        }
        lastDriveP = p;

        // 충돌 후 결과 연출 중이면 그 처리만
        if (phase == Phase.Clash) { TickClash(); return; }

        if (points == null || nextIndex >= points.Count) { DespawnBandit(); phase = Phase.None; return; }

        var cp = points[nextIndex];
        if (cp.progress01 > maxVisualProgress) { nextIndex++; DespawnBandit(); phase = Phase.None; return; }  // 도착 겹침 생략

        float remain = cp.progress01 - p;                                // 전투까지 남은 progress
        // 'N 실초'를 progress 창으로 환산(측정 진행속도 기반). 초기 속도 미측정 땐 최소창.
        float warnWin = Mathf.Clamp(warnRealSeconds * dpdt, 0.03f, 0.45f);

        if (remain > warnWin) { DespawnBandit(); phase = Phase.None; return; }   // 아직 등장 전

        if (remain > 0f)
        {
            // 등장~접근: t01=1(먼)→0(충돌). 진행도로 위치 결정(도착 마을과 동일).
            EnsureBandit();
            float t01 = Mathf.Clamp01(remain / Mathf.Max(1e-4f, warnWin));
            float z = Mathf.Lerp(stopZ, aheadZ, t01);
            if (bandit != null) bandit.localPosition = new Vector3(0f, GroundY(z), z);
            phase = Phase.Approaching;
        }
        else
        {
            // 충돌 지점 도달(progress ≥ 전투지점) → 마차 잠깐 멈춤 + 먼지 왕창 + 결과 연출 시작
            EnsureBandit();
            if (bandit != null) bandit.localPosition = new Vector3(0f, GroundY(stopZ), stopZ);
            if (road != null) road.SetCombatHold(true);   // ★마차 정지(시각 전용 — 이동시간·도착엔 영향 없음)
            SpawnDust();                                   // 충돌~재출발 내내 피어나는 먼지
            phase = Phase.Clash; clashElapsed = 0f; resultElapsed = 0f; outcomeVictory = null;
        }
    }

    // 충돌 후: 마차는 멈춘 채, 결과(승/패)를 로그에서 읽어 색으로 표시. 결과를 잠깐 보여준 뒤
    // 멈춤을 풀고 다시 출발한다(결과가 끝내 안 오면 clashMaxSeconds 안전장치로 재출발).
    private void TickClash()
    {
        clashElapsed += Time.deltaTime;
        if (outcomeVictory == null) outcomeVictory = ReadOutcome(curCaravanId, curTradeId, CurrentEventId());
        if (outcomeVictory != null)
        {
            resultElapsed += Time.deltaTime;   // 결과가 뜬 순간부터 카운트
            if (bandit != null)
            {
                // 승리=산적 붉게 쓰러지듯 아래로, 패배=산적 그대로(임시 표현)
                var mr = bandit.GetComponentInChildren<Renderer>();
                if (mr != null) mr.sharedMaterial.color = outcomeVictory.Value ? new Color(0.2f, 0.5f, 0.2f) : new Color(0.6f, 0.15f, 0.15f);
                if (outcomeVictory.Value)
                {
                    var lp = bandit.localPosition; lp.y = GroundY(stopZ) - Mathf.Min(1.5f, resultElapsed * 1.5f); bandit.localPosition = lp;
                }
            }
        }

        // 재출발 조건: 결과를 resultHoldSeconds만큼 보여줬거나, 안전 타임아웃 초과.
        bool resultShownEnough = outcomeVictory != null && resultElapsed >= resultHoldSeconds;
        if (resultShownEnough || clashElapsed >= clashMaxSeconds)
        {
            if (road != null) road.SetCombatHold(false);   // ★멈춤 해제 → 다시 이동
            DespawnDust();
            DespawnBandit();
            nextIndex++;               // 다음 전투로
            phase = Phase.None;
            outcomeVictory = null;
        }
    }

    /// <summary>연출 정리(정박/미표시).</summary>
    public void Clear()
    {
        if (road != null) road.SetCombatHold(false);   // 멈춤이 걸린 채로 끝나지 않게(안전)
        DespawnDust();
        DespawnBandit();
        phase = Phase.None; outcomeVictory = null;
        curCaravanId = null; curTradeId = null; points = null; nextIndex = 0;
    }

    // ── 내부 ──

    private string CurrentEventId()
        => (points != null && nextIndex < points.Count) ? points[nextIndex].eventId : string.Empty;

    // 활동 로그(UI가 읽는 그 소스)에서 이 전투의 승/패를 읽는다. null=아직 기록 안 됨.
    private static bool? ReadOutcome(string caravanId, string tradeId, string eventId)
    {
        var save = ND.Framework.FrameworkRoot.Instance != null ? ND.Framework.FrameworkRoot.Instance.CurrentSaveData : null;
        var logs = save != null ? save.caravanActivityLogs : null;
        if (logs == null || string.IsNullOrEmpty(eventId)) return null;
        for (int i = logs.Count - 1; i >= 0; i--)
        {
            var e = logs[i];
            if (e == null || e.caravanId != caravanId || e.tradeId != tradeId || e.routeEventId != eventId) continue;
            if (e.eventType == ND.Framework.CaravanActivityLogType.CombatVictory) return true;
            if (e.eventType == ND.Framework.CaravanActivityLogType.CombatDefeat) return false;
        }
        return null;
    }

    private static string GetTradeId(string caravanId)
    {
        var save = ND.Framework.FrameworkRoot.Instance != null ? ND.Framework.FrameworkRoot.Instance.CurrentSaveData : null;
        if (save != null && ND.Framework.SaveDataLookup.TryGetTradeProgress(save, caravanId, out var e) && e != null)
            return e.activeTradeId ?? string.Empty;
        return string.Empty;
    }

    // 무역 총 소요시간(초) = (도착예정 - 출발) 틱 → 초. N초 계산용.
    private static float GetTradeSeconds(string caravanId)
    {
        var save = ND.Framework.FrameworkRoot.Instance != null ? ND.Framework.FrameworkRoot.Instance.CurrentSaveData : null;
        if (save != null && ND.Framework.SaveDataLookup.TryGetTradeProgress(save, caravanId, out var e) && e != null
            && e.expectedTradeEndUtcTick > e.tradeStartUtcTick)
            return (float)((e.expectedTradeEndUtcTick - e.tradeStartUtcTick) / (double)System.TimeSpan.TicksPerSecond);
        return 0f;
    }

    private void EnsureBandit()
    {
        if (bandit != null) return;
        ResolveRoad();
        Transform parent = road != null ? road.transform : transform;   // 길과 같은 축
        GameObject go;
        if (banditPrefab != null)
        {
            // 마을 NPC 프리팹 등엔 AI 스크립트(VillageNpc 등)가 붙어 있어, 트레드밀에 그대로 스폰하면
            // 제멋대로 움직이거나 마을 매니저를 찾다 에러를 낼 수 있다. → '비활성 홀더' 밑에서
            // 인스턴스화(=Awake 안 돎)한 뒤 시각 외 컴포넌트(MonoBehaviour)를 전부 제거해 순수
            // 정적 소품으로 만든다. 이러면 어떤 NPC 프리팹을 넣어도 안전하게 산적으로 쓸 수 있다.
            var holder = new GameObject("__banditHolder"); holder.SetActive(false);
            go = Instantiate(banditPrefab, holder.transform);
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null) DestroyImmediate(mb);                     // AI/애니 스크립트 제거(Awake 전)
            go.transform.SetParent(parent, false);                        // 진짜 부모(길 축)로 이동
            DestroyImmediate(holder);                                     // 빈 홀더 정리
            go.SetActive(true);
        }
        else
        {
            // 임시: 어두운 큐브(산적)
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var col = go.GetComponent<Collider>(); if (col != null) DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(4f, 4f, 4f);
            var mr = go.GetComponent<Renderer>();
            var sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh == null) sh = Shader.Find("Standard");
            var m = new Material(sh); m.color = new Color(0.15f, 0.13f, 0.12f); mr.sharedMaterial = m;
        }
        go.name = "TreadmillBandit";
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 마차(카메라) 쪽을 보게
        go.transform.localPosition = new Vector3(0f, 0f, aheadZ);
        bandit = go.transform;

        // 목표 키에 맞춰 자동 스케일(어떤 NPC 모델이든 일정 크기로) — banditHeight 기준.
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0 && banditHeight > 0.01f)
        {
            Bounds b0 = rends[0].bounds; for (int i = 1; i < rends.Length; i++) b0.Encapsulate(rends[i].bounds);
            if (b0.size.y > 0.001f) go.transform.localScale *= banditHeight / b0.size.y;
        }
        // 발밑을 지면(로컬 y 0)에 스냅 → 커브 위에 서게.
        rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds; for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float footLocalY = parent.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z)).y;
            baseY = go.transform.localPosition.y - footLocalY;
        }
        else baseY = 0f;
        var sp = go.transform.localPosition; sp.y = GroundY(aheadZ); go.transform.localPosition = sp;
    }

    private void DespawnBandit()
    {
        if (bandit != null) Destroy(bandit.gameObject);
        bandit = null;
    }

    // 충돌~재출발 동안 계속 피어나는 '지속형' 먼지. dustGO에 담아 두고 재출발 때 DespawnDust로 끈다.
    private void SpawnDust()
    {
        DespawnDust();   // 혹시 남아 있으면 정리(중복 방지)
        ResolveRoad();
        Transform parent = road != null ? road.transform : transform;
        if (dustPrefab != null)
        {
            dustGO = Instantiate(dustPrefab, parent);
            dustGO.transform.localPosition = new Vector3(0f, GroundY(stopZ), stopZ);
            return;   // 프리팹 먼지는 재출발 때 DespawnDust로 정리(수명 자동파괴 안 함)
        }
        // 임시: 절차 먼지 — 큰 초기 버스트 + 멈춰 있는 동안 지속 방출(왕창).
        var go = new GameObject("TreadmillDust");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, GroundY(stopZ) + 1f, stopZ);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 1.1f; main.startSpeed = 5.5f; main.startSize = 2.0f;   // 더 크고 오래
        main.startColor = new Color(0.72f, 0.64f, 0.52f, 0.85f); main.gravityModifier = 0.15f; main.maxParticles = 400;
        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 60f;   // 멈춰 있는 내내 계속 뿜음
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 120) });   // 충돌 순간 큰 한 방
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 2.6f;
        var r = ps.GetComponent<ParticleSystemRenderer>();
        var mat = Shader.Find("Universal Render Pipeline/Particles/Unlit"); if (mat == null) mat = Shader.Find("Sprites/Default");
        r.material = new Material(mat);
        ps.Play();
        dustGO = go;   // 수명 자동파괴 대신, 재출발 때 DespawnDust로 끈다
    }

    private void DespawnDust()
    {
        if (dustGO != null) Destroy(dustGO);
        dustGO = null;
    }
}
