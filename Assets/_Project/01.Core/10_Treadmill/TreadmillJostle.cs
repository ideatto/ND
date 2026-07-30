// =============================================================================
// TreadmillJostle — 트레드밀에서 '덜컹거리며 굴러가는' 연출(절차적 흔들림)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템 2단계 (juice)
//
// [역할] 진짜 물리가 아니라, 모델을 스폰 위치 기준으로 아주 살짝 흔들어(상하 바운스 + 앞뒤·좌우
//        기울기 + 불규칙 노이즈) 길을 덜컹덜컹 굴러가는 느낌을 준다. 위치는 스테이지가 정하고,
//        이 스크립트는 그 위에 '오프셋'만 얹으므로 위치 제어와 싸우지 않는다.
//
// [주의] ExecuteAlways 아님 — 재생 중에만 흔든다(에디트 모드는 정지 상태 유지). 첫 프레임에
//        스테이지가 잡아준 위치를 기준으로 삼는다.
//
// [부착] 흔들고 싶은 모델 프리팹(주로 마차)에 붙인다.
// =============================================================================

using UnityEngine;

/// <summary>모델을 절차적으로 살짝 흔들어 굴러가는 덜컹거림을 연출한다.</summary>
public class TreadmillJostle : MonoBehaviour
{
    [Header("상하 바운스")]
    [SerializeField] private float bobAmount = 0.04f;   // 상하 흔들림 크기(m)
    [SerializeField] private float bobSpeed = 9f;       // 상하 빈도

    [Header("기울기")]
    [SerializeField] private float pitchAmount = 1.6f;  // 앞뒤 끄덕임(도)
    [SerializeField] private float rollAmount = 1.2f;   // 좌우 기울기(도)
    [SerializeField] private float tiltSpeed = 7f;

    [Range(0f, 1f)]
    [SerializeField] private float roughness = 0.5f;    // 불규칙(노이즈) 비율(0=매끈한 사인, 1=거친 노면)
    [SerializeField] private float amplitude = 1f;      // 전체 세기 배율(0이면 정지)

    private bool captured;
    private Vector3 basePos;
    private Quaternion baseRot;
    private float seed;

    private void OnEnable()
    {
        captured = false;                 // 다음 첫 Update에서 스폰 위치를 기준으로 캡처
        seed = Random.value * 10f;        // 인스턴스마다 위상 다르게(여러 대가 똑같이 안 흔들리게)
    }

    private void Update()
    {
        // 스테이지가 위치를 잡은 뒤(첫 프레임) 기준 포즈를 캡처
        if (!captured)
        {
            basePos = transform.localPosition;
            baseRot = transform.localRotation;
            captured = true;
        }

        float t = Time.time;
        float smooth = 1f - roughness;

        // 상하 바운스 = 규칙(사인) + 불규칙(펄린 노이즈)
        float bobN = Mathf.PerlinNoise(t * bobSpeed * 0.5f, seed) - 0.5f;
        float bob = (Mathf.Sin(t * bobSpeed + seed) * smooth + bobN * 2f * roughness) * bobAmount * amplitude;

        // 앞뒤 끄덕임 + 좌우 기울기
        float pitchN = Mathf.PerlinNoise(seed, t * tiltSpeed * 0.5f) - 0.5f;
        float pitch = (Mathf.Sin(t * tiltSpeed + seed) * smooth + pitchN * 2f * roughness) * pitchAmount * amplitude;
        float roll = Mathf.Cos(t * tiltSpeed * 0.85f + seed * 1.3f) * rollAmount * amplitude;

        transform.localPosition = basePos + Vector3.up * bob;
        transform.localRotation = baseRot * Quaternion.Euler(pitch, 0f, roll);
    }

    private void OnDisable()
    {
        // 정지 시 기준 포즈로 되돌림
        if (captured)
        {
            transform.localPosition = basePos;
            transform.localRotation = baseRot;
        }
    }
}
