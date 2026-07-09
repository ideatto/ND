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
using ND.Economy;

public class JourneyRunTest : MonoBehaviour
{
    // 시간을 '실제 시각'으로 잰다. (테스트용으로 서비스 하나 생성)
    private GameTimeService timeService = new GameTimeService();
    private DateTime tradeStartUtc;   // 출발한 실제 시각

    // [임시 보유금] 무역 사이에 자금을 이어주는 테스트용 지갑. (인스펙터에서 확인·초기값 조절)
    //   3회 연속 무역에서 자금이 변하는 걸 보이려는 임시. 진짜 저장·지갑은 M3(정헌·천성욱).
    [Header("보유금 (임시)")]
    [SerializeField] private CurrencyState wallet = new CurrencyState { TradeMoney = 1000 };

    // [임시] 판매 마을 이익 배율. 판매가 = 기본가 × 이 값.
    //   1.0 = 이익 없음(구매가와 동일) / 1.5 = +50%. 인스펙터에서 조절해 이익 변화 확인.
    [Header("판매 마을 이익 배율 (임시)")]
    [SerializeField] private float sellPriceMultiplier = 1.5f;

    [Header("시간 정보 (Play 중 표시)")]
    [SerializeField] private string TradeStartTime = "-";
    [SerializeField] private string TradeEndTime = "-";
    [SerializeField] private string ElapsedTime = "-";

    [Header("상단 구성 (우클릭 '샘플 상단 채우기')")]
    public CaravanData caravan = new CaravanData();

    // ── 상단 캐시 (구성 바뀌면 RecalculateCache로 갱신, 표시·출발은 이 값을 읽음) ──
    [Header("상단 캐시 (읽기용)")]
    public float cachedLoad;             // 현재 적재량
    public float cachedMaxLoad;          // 최대 적재량
    public float cachedFoodPerSec;       // 초당 식량 소모
    public float cachedSpeedEfficiency;  // 동물 수 속도 효율 (말1→1, 2→1.5, 3→2)
    public int cachedCargoCount;         // 현재 무역품 총 개수 (상품별 수량 합)

    // ── 이동 중 상태 표시 (인스펙터 실시간) ──────────────────────────
    //  caravan 안의 중첩 필드는 인스펙터가 실시간 갱신을 잘 안 해서, 직접 필드로 미러링한다.
    [Header("상태 표시 (이동 중 실시간)")]
    [SerializeField] private int st_durability;     // 현재 내구도
    [SerializeField] private int st_cargoCount;     // 무역품 개수
    [SerializeField] private float st_food;         // 남은 식량
    [SerializeField] private float st_progress;     // 진행률 %
    [SerializeField] private bool st_foodDepleted;  // 식량 바닥?

    [Header("이동 거리(Km)")]
    public float distanceKm = 100f;

    [Header("식량 고갈 제한시간(초) — 이 안에 도착 못하면 실패 [M2]")]
    public float starveGraceSeconds = 5f;

    [Header("테스트 이벤트 (-1 = 없음)")]
    public int cargoLossAtSecond = -1;   // 이 초에 무역품 손실 → 부분 성공
    public int cargoLossAmount = 2;
    public int foodLossAtSecond = -1;   // 이 초에 식량 도난(ApplyFoodLoss)
    public float foodLossAmount = 5f;
    public int fatalAtSecond = -1;   // 이 초에 강제 실패
    public int raidAtSecond = -1;    // 이 초에 약탈(전투) 발생 → 용병으로 방어 판정 [M2]
    public int raidCount = 1;        // 이 초에 몇 번 전투가 벌어지나 (용병 수와 비교)
    public int raidDurabilityDamage = 20;  // 방어 실패 시 마차 내구도 손실
    public int raidCargoDamage = 1;        // 방어 실패 시 무역품 손실

    [ContextMenu("무역 출발")]
    public void Depart()
    {
        DepartureValidationResult v = JourneyRunner.TryDepart(caravan, distanceKm);
        if (!v.canDepart)
        {
            Debug.Log($"[출발 X] 사유: {string.Join(", ", v.reasons)}");
            return;
        }

        caravan.starveGraceSeconds = starveGraceSeconds;   // 식량 고갈 제한시간 적용 [M2]

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
        bool cargoDone = false, foodDone = false, raidDone = false;
        int totalSecInt = Mathf.Max(1, Mathf.CeilToInt(caravan.totalSeconds));

        while (caravan.state == JourneyState.Traveling)
        {
            // 출발 시각부터 지금까지 '실제로' 흐른 초 (deltaTime 누적 대신)
            float elapsed = (float)(timeService.CurrentUtc - tradeStartUtc).TotalSeconds;
            int elapsedSec = (int)elapsed;

            ElapsedTime = elapsed.ToString("0.0") + "초";

            float progress = (caravan.totalSeconds > 0f) ? elapsed / caravan.totalSeconds : 1f;
            JourneyRunner.SetProgress(caravan, progress);   // 여기서 식량 소진 자동 체크됨
            UpdateStatusDisplay();                          // 인스펙터 상태 표시 갱신

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
            if (!raidDone && raidAtSecond >= 0 && elapsedSec >= raidAtSecond)
            {
                for (int i = 0; i < raidCount; i++)
                {
                    bool defended = JourneyRunner.ResolveRaid(caravan, raidDurabilityDamage, raidCargoDamage);
                    Debug.Log(defended
                        ? $"[약탈] {elapsedSec}초: {i + 1}번째 전투 — 용병 방어 성공"
                        : $"[약탈] {elapsedSec}초: {i + 1}번째 전투 — 방어 실패! 내구도 -{raidDurabilityDamage}, 무역품 -{raidCargoDamage}");
                }
                raidDone = true;
            }

            // 1초 단위 출력 (진행도 + 남은 식량)
            if (elapsedSec > lastPrintedSec && elapsedSec <= totalSecInt)
            {
                float food = CaravanCalculator.GetRemainingFood(caravan);
                string starve = caravan.runFoodDepleted ? "  ⚠식량바닥(제한시간 카운트다운)" : "";
                Debug.Log($"이동중 {elapsedSec}/{totalSecInt}초  (진행 {caravan.progress01 * 100f:0}%, 식량 {food:0.#}){starve}");
                lastPrintedSec = elapsedSec;
            }

            if (JourneyRunner.IsArrived(caravan) || caravan.runFatalReason != JourneyFailureReason.None)
            {
                JourneyResultData result = JourneyRunner.Settle(caravan);
                PrintResult(result);

                if (result != null && result.grade != JourneyResultGrade.Failed)   // ← 추가
                    LogEconomyResult(caravan);                                     // ← 추가: 성공이면 판매가 계산

                JourneyRunner.ClaimSettlement(caravan);
                JourneyRunner.ResetToPrepare(caravan);
                yield break;
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
    // [임시] 도착 성공 시 cargo 상품을 정헌 계산기에 넣어 판매가/순이익을 콘솔에 표시.
    //        cargo 없는 값(계절·재화 등)은 임시. 나중에 SO/정헌 값으로 교체.
    private void LogEconomyResult(CaravanData caravan)
    {
        if (caravan.cargo.Count == 0 || caravan.cargo[0].item == null) return;

        var input = new EconomyM1LoopInput();
        // cargo에서 진짜로 (첫 상품)
        input.PriceInput.TradeItemId = caravan.cargo[0].item.id;
        input.PriceInput.Quantity = caravan.cargo[0].quantity;
        input.PriceInput.BaseBuyPrice = caravan.cargo[0].item.basePrice;
        input.PriceInput.BaseSellPrice = Mathf.RoundToInt(caravan.cargo[0].item.basePrice * sellPriceMultiplier);   // [임시] 판매 마을 이익 배율 적용
        // 나머지 [임시]
        input.TradeId = "trade_test";
        input.PriceInput.RouteId = "route_01";
        input.PriceInput.SeasonId = "summer";
        input.CurrencyState = wallet.Clone();   // 현재 보유금을 넣기 (복제해서 계산 중 원본 보호)
        input.FoodCost = 20;
        input.PriceInput.FromTownId = "town_A";   // [임시] 추가
        input.PriceInput.ToTownId = "town_B";   // [임시] 추가

        long before = wallet.TradeMoney;                       // 정산 전 보유금
        var r = EconomyM1LoopCalculator.Execute(input);
        if (r.Success)
        {
            if (r.FinalCurrencyState != null)                  // 정산 후 보유금으로 갱신 → 다음 무역에 이어짐
            {
                wallet.TradeMoney = r.FinalCurrencyState.TradeMoney;
                wallet.DevelopmentCurrency = r.FinalCurrencyState.DevelopmentCurrency;
            }
            Debug.Log($"[정산] {caravan.cargo[0].item.itemName} {caravan.cargo[0].quantity}개 → 판매가 {r.PriceResult.TotalSellPrice} / 순이익 {r.Settlement.NetProfit} / 보유금 {before} → {wallet.TradeMoney}");
        }
        else
            Debug.Log($"[정산] 계산 실패: {r.ErrorCode}");
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

        // [M2] 마차 내구도 상태 (이번 무역 손실 + 현재 남은 내구도)
        int maxDur = (caravan.wagon != null) ? caravan.wagon.maxDurability : 0;
        Debug.Log($"[마차] 내구도 {caravan.currentDurability}/{maxDur} (이번 손실 {r.durabilityLost:0})");
    }

    [ContextMenu("샘플 상단 채우기")]
    public void FillSample()
    {
        caravan = new CaravanData();
        // 마차: 동물 최소 1 ~ 최대 5 / overLoad 30(속도 100% 한계) / maxLoad 60(출발 불가)
        caravan.wagon = new imsiWagonData { wagonName = "기본 마차", overLoad = 30f, maxLoad = 60f, minAnimals = 1, maxAnimals = 5 };
        // 동물: foodPerKm(=이제 '초당' 소모) 0.1. 2마리 → 초당 0.2 소모.
        caravan.animals.Add(new imsiAnimalData { animalName = "말", foodPerKm = 0.1f });
        caravan.animals.Add(new imsiAnimalData { animalName = "말", foodPerKm = 0.1f });

        imsiTradeItemData wheat = new imsiTradeItemData { id = "wheat", itemName = "밀", weight = 5f, basePrice = 10 };
        caravan.cargo.Add(new CargoEntry { item = wheat, quantity = 5 });
        caravan.foodAmount = 30;   // 필요 식량 = 초당 0.2 × 총소요초. 총시간보다 적게 실으면 도중 식량 고갈(foodAmount 낮추면 부족 재현).

        caravan.currentDurability = caravan.wagon.maxDurability;   // 내구도 최대로 시작 [M2]
        caravan.mercenaries.Add(new imsiMercenaryData { mercName = "용병", combatPower = 10, contractCount = 3 });  // [임시] 용병 1마리

        RecalculateCache();     // 구성 다 채웠으니 캐시 갱신
        UpdateStatusDisplay();  // 상태 표시도 초기값으로
        Debug.Log("[상단] 샘플 채움. foodAmount를 낮추거나 foodLossAtSecond를 켜면 식량 부족 실패 확인.");
    }

    // [캐시 갱신] 구성이 바뀌면 불러서 파생값(적재·속도 등)을 다시 계산해 캐시한다.
    //            계산은 CaravanCalculator(순수 계산기)가 하고, 여기선 결과만 저장.
    public void RecalculateCache()
    {
        cachedLoad            = CaravanCalculator.GetCurrentLoad(caravan);
        cachedMaxLoad         = CaravanCalculator.GetMaxLoad(caravan);
        cachedFoodPerSec      = CaravanCalculator.GetConsumptionPerSec(caravan);
        cachedSpeedEfficiency = CaravanCalculator.GetSpeedEfficiency(caravan.animals.Count);

        cachedCargoCount = CaravanCalculator.GetCargoCount(caravan);   // 개수 합산은 계산기로 위임

        Debug.Log($"[상단 캐시] 적재 {cachedLoad} / 최대 {cachedMaxLoad} / 초당식량 {cachedFoodPerSec} / 속도효율 {cachedSpeedEfficiency} / 무역품 {cachedCargoCount}개");
    }

    // [테스트 표시] caravan 안의 중첩 필드는 인스펙터 실시간 반영이 안 돼서, 직접 필드로 미러링한다.
    private void UpdateStatusDisplay()
    {
        st_durability   = caravan.currentDurability;
        st_cargoCount   = CaravanCalculator.GetCargoCount(caravan);
        st_food         = CaravanCalculator.GetRemainingFood(caravan);
        st_progress     = caravan.progress01 * 100f;
        st_foodDepleted = caravan.runFoodDepleted;
    }


}