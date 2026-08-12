// =============================================================================
// TradeSummaryPanel — ⑥ 무역 요약 화면 (와이어프레임 Section 9 v2 - 6번)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 출발 전 마지막 요약 표시. 순수 UI — 값은 SummaryData(DTO)로 받아 바인딩만 한다.
//   · 타이틀: "출발 도시 → 목적지"
//   · 좌측 정보: 예상 위험도·고용 용병 / 예상 음식 소모·적재 /
//                무역 준비 코스트 / 예상 종료 시간(hh:mm:ss)
//   · 우측: 이미지 자리(더미)
//   버튼(뒤로·무역 취소·무역 시작)은 매니저가 씬에서 직접 연결한다.
//
// [와이어프레임 규칙]
//   · 예상 위험도: 루트 내 최고 습격 이벤트 값(없으면 0) / 고용 용병 수치와 나란히
// =============================================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>⑥ 무역 요약 표시 패널(순수 UI). 값은 SummaryData로 주입.</summary>
public class TradeSummaryPanel : MonoBehaviour
{
    /// <summary>요약 화면에 표시할 값 묶음. 매니저가 조립해서 넘긴다.</summary>
    public struct SummaryData
    {
        public string fromTown;       // 출발 도시
        public string toTown;         // 목적지 도시
        public Sprite destinationSprite;
        public int expectedRisk;      // 전체 여정에서 하나 이상의 이벤트가 발생할 확률(%)
        public int mercenaryPower;    // 고용 용병 전투력
        public float expectedFood;    // 예상 음식 소모량
        public int loadedFood;        // 음식 적재량
        public long prepareCost;      // 무역 준비 코스트(적재 구매 + 용병 고용)
        public float durationSeconds; // 예상 소요 시간(초) → hh:mm:ss 표시
    }

    [Header("타이틀 (출발 → 목적지)")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image destinationImage;

    [Header("좌측 정보 줄")]
    [SerializeField] private TMP_Text riskText;     // 예상 위험도 / 고용 용병
    [SerializeField] private TMP_Text foodText;     // 예상 음식 소모 / 적재
    [SerializeField] private TMP_Text costText;     // 무역 준비 코스트
    [SerializeField] private TMP_Text timeText;     // 예상 종료 시간

    /// <summary>요약 값을 채워 표시한다(활성화는 매니저 담당).</summary>
    public void Show(SummaryData d)
    {
        if (titleText != null) titleText.text = $"{d.fromTown} → {d.toTown}";
        if (destinationImage != null)
        {
            bool hasDestinationSprite = d.destinationSprite != null;
            destinationImage.sprite = d.destinationSprite;
            destinationImage.color = hasDestinationSprite
                ? Color.white
                : new Color(0.82f, 0.82f, 0.86f, 1f);
            destinationImage.preserveAspect = hasDestinationSprite;

            for (int index = 0; index < destinationImage.transform.childCount; index++)
                destinationImage.transform.GetChild(index).gameObject.SetActive(!hasDestinationSprite);
        }
        if (riskText != null) riskText.text = $"예상 위험도 {d.expectedRisk} / 고용 용병 {d.mercenaryPower}";
        if (foodText != null)
        {
            int requiredFood = Mathf.Max(0, Mathf.CeilToInt(d.expectedFood));
            int loadedFood = Mathf.Max(0, d.loadedFood);
            int shortage = Mathf.Max(0, requiredFood - loadedFood);
            foodText.text = shortage > 0
                ? $"먹이 요구량 {requiredFood} / 적재 먹이량 {loadedFood}  (부족 {shortage})"
                : $"먹이 요구량 {requiredFood} / 적재 먹이량 {loadedFood}";
            foodText.color = shortage > 0
                ? new Color32(214, 92, 72, 255)
                : new Color32(45, 40, 39, 255);
        }
        if (costText != null) costText.text = $"무역 준비 코스트 {d.prepareCost:N0} G";
        if (timeText != null) timeText.text = $"예상 종료 시간 {FormatHms(d.durationSeconds)}";
    }

    /// <summary>초 → "hh : mm : ss" (와이어프레임 표기).</summary>
    private static string FormatHms(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        System.TimeSpan t = System.TimeSpan.FromSeconds(seconds);
        return $"{(int)t.TotalHours:00} : {t.Minutes:00} : {t.Seconds:00}";
    }
}
