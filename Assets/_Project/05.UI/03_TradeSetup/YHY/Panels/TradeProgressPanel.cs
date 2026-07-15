// =============================================================================
// TradeProgressPanel — ⑦ 무역 진행 중 화면 (와이어프레임 Section 9 v2 - 7번)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 출발 후 이동 진행 상태 표시. 순수 UI + 데모용 카운트다운.
//   · 타이틀: "출발지 → 목적지"
//   · 진행 영역: 3D/2D 렌더 자리(데모는 진행 바로 대체)
//   · 하단 좌: "무역 종료까지 남은 시간" (카운트다운)
//   · 하단 우: 무역 취소 버튼(매니저가 연결 → 7-1 경고창)
//
// [진행 시간] 데모는 이 패널이 Update로 자체 카운트다운한다.
//   실제 통합 시에는 Framework(JourneyRunner)의 진행도(progress01)를 바인딩하고
//   이 자체 타이머는 끈다. SetProgress(0~1)로 외부 진행도를 직접 넣을 수도 있다.
// =============================================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>⑦ 무역 진행 중 표시 패널(순수 UI + 데모 카운트다운).</summary>
public class TradeProgressPanel : MonoBehaviour
{
    [Header("표시")]
    [SerializeField] private TMP_Text titleText;          // 출발 → 목적지
    [SerializeField] private TMP_Text remainingTimeText;  // 무역 종료까지 남은 시간
    [SerializeField] private Image progressFill;          // 진행 바(렌더 영역 데모 대체)

    /// <summary>남은 시간이 0에 도달(도착) — 매니저가 정산으로 이어받는다.</summary>
    public event Action OnArrived;

    private float total;      // 총 소요 시간(초)
    private float remaining;  // 남은 시간(초)
    private bool running;     // 데모 카운트다운 동작 중

    /// <summary>진행 시작 — 출발/목적지 라벨과 총 시간을 세팅하고 카운트다운을 켠다.</summary>
    public void Begin(string fromTown, string toTown, float totalSeconds)
    {
        if (titleText != null) titleText.text = $"{fromTown} → {toTown}";
        total = Mathf.Max(0.01f, totalSeconds);
        remaining = total;
        running = true;
        UpdateView();
    }

    /// <summary>카운트다운 정지(무역 취소 확정 시).</summary>
    public void StopTimer()
    {
        running = false;
    }

    /// <summary>외부 진행도(0~1) 직접 반영 — 실제 통합 시 Framework 진행도 바인딩용.</summary>
    public void SetProgress(float progress01)
    {
        running = false;   // 외부 진행도를 쓰면 자체 카운트다운은 끈다
        float p = Mathf.Clamp01(progress01);
        if (progressFill != null) progressFill.fillAmount = p;
        remaining = total * (1f - p);
        if (remainingTimeText != null)
            remainingTimeText.text = $"무역 종료까지 남은 시간  {FormatHms(remaining)}";
    }

    private void Update()
    {
        if (!running) return;

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            remaining = 0f;
            running = false;
            UpdateView();
            OnArrived?.Invoke();
            return;
        }
        UpdateView();
    }

    private void UpdateView()
    {
        if (progressFill != null)
            progressFill.fillAmount = total > 0f ? 1f - remaining / total : 0f;
        if (remainingTimeText != null)
            remainingTimeText.text = $"무역 종료까지 남은 시간  {FormatHms(remaining)}";
    }

    /// <summary>초 → "hh : mm : ss".</summary>
    private static string FormatHms(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        TimeSpan t = TimeSpan.FromSeconds(seconds);
        return $"{(int)t.TotalHours:00} : {t.Minutes:00} : {t.Seconds:00}";
    }
}
