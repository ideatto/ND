// =============================================================================
// JourneyRunTest — 이동/식량 계산 확인용 임시 스크립트 (UI 나오면 버림)
// =============================================================================
// [작성] 윤호영  /  [영역] Core Gameplay (임시 확인용)
//
// [목적] UI가 아직 없어서, 계산식이 도는지 개발자가 잠깐 눌러보는 용도.
// [시간] 여기선 델타타임을 누적해 진행도(0~1)를 만들어 JourneyRunner에 넘긴다.
//        (반드시 Play 모드 + 일시정지 해제 상태여야 시간이 흐름)
// [쓰는 법] (Play 모드) 우클릭 "샘플 상단 채우기" → 값 조정 → 우클릭 "무역 출발"
// =============================================================================

using System;                 
using System.Collections;
using UnityEngine;
using ND.Framework;          

public class JourneyRunTest : MonoBehaviour
{
    // 시간을 '실제 시각'으로 잰다. (테스트용으로 서비스 하나 생성)
    private GameTimeService timeService = new GameTimeService();
    private DateTime tradeStartUtc;   // 출발한 실제 시각

    [Header("시간 정보 (Play 중 표시)")]
    [SerializeField] private string TradeStartTime = "-";
    [SerializeField] private string TradeEndTime = "-";
    [SerializeField] private string ElapsedTime = "-";

    [Header("상단 구성 (우클릭 '샘플 상단 채우기')")]
    public CaravanData caravan = new CaravanData();

    [Header("이동 거리(Km)")]
    public float distanceKm = 100f;

    [Header("테스트 이벤트 (-1 = 없음)")]
    public int cargoLossAtSecond = -1;   // 이 초에 무역품 손실 → 부분 성공
    public int cargoLossAmount = 2;
    public int foodLossAtSecond = -1;   // 이 초에 식량 도난(ApplyFoodLoss)
    public float foodLossAmount = 5f;
    public int fatalAtSecond = -1;   // 이 초에 강제 실패

    [ContextMenu("무역 출발")]
    public void Depart()
    {
        DepartureValidationResult v = JourneyRunner.TryDepart(caravan, distanceKm);
        if (!v.canDepart)
        {
            Debug.Log($"[출발 X] 사유: {string.Join(", ", v.reasons)}");
            return;
        }

        int animals = caravan.animals.Count;
        float load = CaravanCalculator.GetCurrentLoad(caravan);
        float overLoad = (caravan.wagon != null) ? caravan.wagon.overLoad : 0f;
        float animalEff = CaravanCalculator.GetSpeedEfficiency(animals);
        float loadEff = CaravanCalculator.GetLoadEfficiency(load, overLoad);
        float needFood = CaravanCalculator.GetRequiredFood(caravan);

        Debug.Log($"[출발 O] {distanceKm:0}Km, 동물 {animals}마리(효율 {animalEff:0.##}배) " +
                  $"적재 {load}(기준 {overLoad}) 적재효율 {loadEff:0.##}배 → 약 {caravan.totalSeconds:0.#}초");
        Debug.Log($"[식량] 필요 {needFood:0.#} / 실은 {caravan.foodAmount}" +
                  (needFood > caravan.foodAmount ? "  ⚠ 부족 — 도중 실패 가능" : ""));

        tradeStartUtc = timeService.CurrentUtc;   // ← 추가: 지금 시각을 출발 시각으로

        TradeStartTime = tradeStartUtc.ToString("HH:mm:ss");

        DateTime endUtc = timeService.CalculateTradeEnd(tradeStartUtc, TimeSpan.FromSeconds(caravan.totalSeconds));
        TradeEndTime = endUtc.ToString("HH:mm:ss");

        StopAllCoroutines();
        StartCoroutine(RunTrade());
    }

    private IEnumerator RunTrade()
    {
        int lastPrintedSec = 0;
        bool cargoDone = false, foodDone = false;
        int totalSecInt = Mathf.Max(1, Mathf.CeilToInt(caravan.totalSeconds));

        while (caravan.state == JourneyState.Traveling)
        {
            // 출발 시각부터 지금까지 '실제로' 흐른 초 (deltaTime 누적 대신)
            float elapsed = (float)(timeService.CurrentUtc - tradeStartUtc).TotalSeconds;
            int elapsedSec = (int)elapsed;

            ElapsedTime = elapsed.ToString("0.0") + "초";

            float progress = (caravan.totalSeconds > 0f) ? elapsed / caravan.totalSeconds : 1f;
            JourneyRunner.SetProgress(caravan, progress);   // 여기서 식량 소진 자동 체크됨

            // 테스트 이벤트
            if (!cargoDone && cargoLossAtSecond >= 0 && elapsedSec >= cargoLossAtSecond)
            {
                JourneyRunner.ApplyCargoLoss(caravan, cargoLossAmount);
                Debug.Log($"[이벤트] {elapsedSec}초: 전투 실패 → 무역품 {cargoLossAmount} 손실");
                cargoDone = true;
            }
            if (!foodDone && foodLossAtSecond >= 0 && elapsedSec >= foodLossAtSecond)
            {
                JourneyRunner.ApplyFoodLoss(caravan, foodLossAmount);
                Debug.Log($"[이벤트] {elapsedSec}초: 식량 도난 → 식량 {foodLossAmount} 손실");
                foodDone = true;
            }
            if (fatalAtSecond >= 0 && elapsedSec >= fatalAtSecond
                && caravan.runFatalReason == JourneyFailureReason.None)
            {
                JourneyRunner.MarkFatal(caravan, JourneyFailureReason.FoodDepleted);
                Debug.Log($"[이벤트] {elapsedSec}초: 강제 실패");
            }

            // 1초 단위 출력 (진행도 + 남은 식량)
            if (elapsedSec > lastPrintedSec && elapsedSec <= totalSecInt)
            {
                float food = CaravanCalculator.GetRemainingFood(caravan);
                Debug.Log($"이동중 {elapsedSec}/{totalSecInt}초  (진행 {caravan.progress01 * 100f:0}%, 식량 {food:0.#})");
                lastPrintedSec = elapsedSec;
            }

            // 도착 또는 실패 → 정산
            if (JourneyRunner.IsArrived(caravan) || caravan.runFatalReason != JourneyFailureReason.None)
            {
                JourneyResultData result = JourneyRunner.Settle(caravan);
                PrintResult(result);
                JourneyRunner.ClaimSettlement(caravan);
                JourneyRunner.ResetToPrepare(caravan);
                yield break;
            }

            yield return null;
        }
    }

    private void PrintResult(JourneyResultData r)
    {
        if (r == null) { Debug.Log("[정산] 결과 없음(상태 불일치)"); return; }

        switch (r.grade)
        {
            case JourneyResultGrade.Success:
                Debug.Log("=== 도착: 완전 성공 (손실 없음) ==="); break;
            case JourneyResultGrade.PartialSuccess:
                Debug.Log($"=== 도착: 부분 성공 (무역품 {r.cargoLost} 손실) ==="); break;
            case JourneyResultGrade.Failed:
                Debug.Log($"=== 무역 실패: {r.failureReason} (무역품 {r.cargoLost} 손실, 거점 복귀) ==="); break;
        }
    }

    [ContextMenu("샘플 상단 채우기")]
    public void FillSample()
    {
        caravan = new CaravanData();
        // 마차: 동물 최소 1 ~ 최대 5 / overLoad 30(속도 100% 한계) / maxLoad 60(출발 불가)
        caravan.wagon = new imsiWagonData { wagonName = "기본 마차", overLoad = 30f, maxLoad = 60f, minAnimals = 1, maxAnimals = 5 };
        // 동물: foodPerKm 0.1 = 1Km당 0.1 소모. 2마리 → Km당 0.2 소모.
        caravan.animals.Add(new imsiAnimalData { animalName = "말", foodPerKm = 0.1f });
        caravan.animals.Add(new imsiAnimalData { animalName = "말", foodPerKm = 0.1f });

        imsiTradeItemData wheat = new imsiTradeItemData { id = "wheat", itemName = "밀", weight = 5f, basePrice = 10 };
        caravan.cargo.Add(new CargoEntry { item = wheat, quantity = 5 });
        caravan.foodAmount = 30;   // 100Km 필요 20(0.2×100) < 30 → 성공. 20 밑으로 줄이면 도중 식량 고갈.

        Debug.Log("[상단] 샘플 채움. foodAmount를 낮추거나 foodLossAtSecond를 켜면 식량 부족 실패 확인.");
    }
}