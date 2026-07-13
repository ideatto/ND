// =============================================================================
// TradePreparePanel — 무역 준비 패널 컨트롤러 (Core 제공)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 준비 화면에 Core 계산값(짐무게·적정·최대·슬롯·과적·예상식량·예상시간)을 표시하고,
//        출발 버튼을 Framework 출발 API에 연결한다.
//        · 표시/검증 = Core (CaravanCalculator, CaravanValidator)
//        · 출발 실행 = Framework 가이드 API `FrameworkRoot.Instance.TradeStart.TryStartTrade(...)`
//          (UI에서 Core JourneyRunner를 직접 부르지 않는다 — 가이드 규칙)
//
// [쓰는 법 — 유니티에서]
//   1) 준비 패널 Prefab 루트에 이 스크립트를 붙인다.
//   2) 인스펙터에서 TMP_Text / Button 필드를 화면 요소에 연결한다.
//   3) 구성이 바뀔 때 Refresh(상단, 거리, 배율)를 호출한다.
//   4) 출발 버튼은 자동으로 TradeStart에 연결된다(별도 배선 불필요).
// =============================================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ND.Framework;

/// <summary>무역 준비 패널 — Core 계산값 표시 + Framework 출발 API 연결. [1차 빌드]</summary>
public class TradePreparePanel : MonoBehaviour
{
    [Header("적재 표시")]
    [SerializeField] private TMP_Text loadText;      // 짐무게 (무역품 + 식량)
    [SerializeField] private TMP_Text limitText;     // 적정 / 최대 한계
    [SerializeField] private TMP_Text slotText;      // 칸 사용/최대
    [SerializeField] private TMP_Text overloadText;  // 과적 상태

    [Header("식량 · 시간 표시")]
    [SerializeField] private TMP_Text foodText;      // 예상 필요 식량
    [SerializeField] private TMP_Text timeText;      // 예상 이동 시간

    [Header("출발")]
    [SerializeField] private Button departButton;        // 출발 버튼
    [SerializeField] private TMP_Text blockReasonText;   // 출발 불가 사유

    [Header("무역로")]
    [SerializeField] private string routeId = "route_01";  // 선택된 무역로 ID (임시 기본값 — 라우트 선택에서 세팅 예정)

    // 마지막 Refresh로 받은 상단·거리·배율 (출발 시 재사용)
    private CaravanData caravan;
    private float distanceKm;
    private float inGameTimeMultiplier = 1f;

    private void Awake()
    {
        // 출발 버튼을 Framework 출발 API 호출에 연결한다.
        if (departButton != null)
            departButton.onClick.AddListener(Depart);
    }

    /// <summary>준비 상단·거리·배율로 표시를 갱신한다(출발에 쓸 값도 저장). Core 계산기·검증기를 그대로 사용.</summary>
    public void Refresh(CaravanData caravan, float distanceKm, float inGameTimeMultiplier)
    {
        this.caravan = caravan;
        this.distanceKm = distanceKm;
        this.inGameTimeMultiplier = inGameTimeMultiplier;
        if (caravan == null) return;

        PrepareDisplayData d = CaravanCalculator.BuildPrepareDisplay(caravan, distanceKm, inGameTimeMultiplier);
        DepartureValidationResult v = CaravanValidator.Validate(caravan);

        // 적재
        SetText(loadText,  $"짐무게 {d.currentLoad:0.#} (무역품 {d.cargoWeight:0.#} + 식량 {d.foodWeight:0.#})");
        SetText(limitText, $"적정 {d.overloadLimit:0.#} / 최대 {d.maxLoad:0.#}");
        SetText(slotText,  $"칸 {d.usedSlots}/{d.maxSlots}");
        SetText(overloadText, d.isOverloaded
            ? $"과적 {d.overloadRatio:P0} (속도 {d.loadSpeedModifier:0.##}배)"
            : "정상");

        // 식량 · 시간
        SetText(foodText, $"예상 식량 {d.requiredFood:0.#}");
        SetText(timeText, $"예상 {d.estimatedTravelSeconds:0.#}초");

        // 출발 가능 여부 → 버튼 활성 + 사유 표시
        if (departButton != null) departButton.interactable = v.canDepart;
        SetText(blockReasonText, v.canDepart ? "" : "출발 불가: " + string.Join(", ", v.reasons));
    }

    /// <summary>출발 버튼 — 가이드의 Framework API(TradeStart.TryStartTrade)를 직접 호출한다.</summary>
    private void Depart()
    {
        if (caravan == null) return;

        FrameworkRoot root = FrameworkRoot.Instance;
        if (root == null || root.TradeStart == null)
        {
            Debug.LogWarning("[준비 패널] FrameworkRoot/TradeStart 없음 — Boot 씬부터 실행해야 함");
            return;
        }

        string tradeId = Guid.NewGuid().ToString();   // 고유 무역 ID 생성
        DepartureValidationResult result = root.TradeStart.TryStartTrade(caravan, distanceKm, tradeId, routeId);

        // canDepart(코어 검증) + LastRecordSucceeded(framework 기록) 둘 다 성공해야 진짜 출발
        if (result.canDepart && root.TradeStart.LastRecordSucceeded)
            Debug.Log($"[준비 패널] 출발! trade={tradeId} route={routeId}");
        else
            Debug.LogWarning($"[준비 패널] 출발 실패 (canDepart={result.canDepart}, 기록={root.TradeStart.LastRecordSucceeded}) 사유: {string.Join(", ", result.reasons)}");

        Refresh(caravan, distanceKm, inGameTimeMultiplier);   // 버튼 상태 재갱신
    }

    private static void SetText(TMP_Text field, string value)
    {
        if (field != null) field.text = value;
    }
}
