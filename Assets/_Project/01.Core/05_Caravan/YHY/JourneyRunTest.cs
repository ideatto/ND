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
using System.Collections.Generic;
using UnityEngine;
using ND.Framework;
using ND.Economy;

/// <summary>[테스트] 인스펙터에서 무역품 SO + 수량을 한 쌍으로 넣기 위한 입력 항목.</summary>
[Serializable]
public class SoItemInput
{
    public TradeItemData item;   // 무역품 SO
    public int quantity = 1;     // 수량
}

public class JourneyRunTest : MonoBehaviour
{
    // 시간을 '실제 시각'으로 잰다. (테스트용으로 서비스 하나 생성)
    private GameTimeService timeService = new GameTimeService();
    private DateTime tradeStartUtc;   // 출발한 실제 시각

    // [2차 빌드에서 제거됨] 임시 지갑·판매 배율은 "도착 시 자동 판매"용이었다.
    //   자동 판매가 규칙상 금지되어(판매는 도착 후 시장 화면에서 명시적으로만) 함께 걷어냈다.
    //   이 스크립트는 이제 이동·식량·내구도·약탈 계산 확인 전용이다.

    [Header("시간 정보 (Play 중 표시)")]
    [SerializeField] private string TradeStartTime = "-";
    [SerializeField] private string TradeEndTime = "-";
    [SerializeField] private string ElapsedTime = "-";

    // [런타임] 상단은 이제 SO로 채운다 (우클릭 "SO에서 상단 채우기"). 수동 편집 불필요 → 인스펙터에서 숨김.
    //   현재 상태는 아래 "상단 캐시" / "상태 표시"에서 확인.
    [HideInInspector] public CaravanData caravan = new CaravanData();

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
    public float inGameTimeMultiplier = 1f;   // [인게임시간] 현실 경과 × 이 배율 = 인게임 경과. 60이면 식량 60배 빨리 소모. (실게임은 Framework가 채움)

    [Header("테스트 이벤트 (-1 = 없음)")]
    public int cargoLossAtSecond = -1;   // 이 초에 무역품 손실 → 부분 성공
    public int cargoLossAmount = 2;
    public int foodLossAtSecond = -1;   // 이 초에 식량 도난(ApplyFoodLoss)
    public float foodLossAmount = 5f;
    public int fatalAtSecond = -1;   // 이 초에 강제 실패
    public int raidAtSecond = -1;    // 이 초에 산적 이벤트 발생
    public int raidCount = 1;        // 이 초에 몇 번 산적 이벤트를 판정할지
    public int raidBanditCombatPower = 100;
    [Range(0f, 1f)] public float raidCargoLootRate = 0.1f;
    [Range(0f, 1f)] public float raidFodderLootRate = 0.1f;
    [Range(0f, 100f)] public float baseSafetyChancePercent;
    public int raidRandomSeed = 1234;

    // ── SO 연결 (이종현 더미 데이터) ──────────────────────────────
    //  더미 SO를 아래 칸에 드래그 → 우클릭 "SO에서 상단 채우기"
    [Header("SO 연결 (더미 SO 드래그)")]
    [SerializeField] private WagonData soWagon;         // Wagon_Dummy... 드래그
    [SerializeField] private List<DraftAnimalData> soAnimals = new List<DraftAnimalData>();  // 견인 동물 SO들 (여러 마리·여러 종류 넣어 단일종류 검증 테스트)
    [SerializeField] private List<SoItemInput> soItems = new List<SoItemInput>();            // 무역품 SO들 (아이템+수량, 마차 슬롯 수만큼)
    [SerializeField] private int soFoodAmount = 30;     // 실을 식량

    [ContextMenu("무역 출발")]
    public void Depart()
    {
        DepartureValidationResult v = JourneyRunner.TryDepart(caravan, distanceKm);
        if (!v.canDepart)
        {
            // 출발 불가 — 사유마다 실제 값까지 붙여 자세히 출력
            string detail = "";
            foreach (DepartureBlockReason r in v.reasons)
                detail += $"\n   ✗ {DescribeBlockReason(r)}";
            Debug.LogWarning($"[출발 불가] {v.reasons.Count}개 사유:{detail}");
            return;
        }

        caravan.starveGraceSeconds = starveGraceSeconds;   // 식량 고갈 제한시간 적용 [M2]

        // [M2] 손실 상한 — 정헌 GrowthCalculator에서 LossLimitRate 받아 적용 (성장 레벨은 임시 0)
        caravan.lossLimitRate = GrowthCalculator.CalculateM1RuntimeStats(0, 0).LossLimitRate;
        caravan.baseSafetyChancePercent = baseSafetyChancePercent;
        Debug.Log($"[손실상한] 비율 {caravan.lossLimitRate:0.##} (원래 무역품 {caravan.runOriginalCargoCount}개 → 최대 {(int)(caravan.lossLimitRate * caravan.runOriginalCargoCount)}개 손실)");

        int animals = caravan.animals.Count;
        float load = CaravanCalculator.GetCurrentLoad(caravan);
        float overLoad = CaravanCalculator.GetFinalEfficientLoad(caravan);   // 마차+동물 적정한계
        float animalEff = CaravanCalculator.GetSpeedEfficiency(animals);
        float loadEff = CaravanCalculator.GetLoadEfficiency(load, overLoad);
        float needFood = CaravanCalculator.GetRequiredFood(caravan, caravan.totalSeconds * inGameTimeMultiplier);

        Debug.Log(
            $"[출발] {distanceKm:0}Km → 예상 {caravan.totalSeconds:0.#}초 · 짐무게 {load:0.#} (무역품무게 {CaravanCalculator.GetCargoWeight(caravan):0.#} + 식량무게 {CaravanCalculator.GetFoodWeight(caravan):0.#}, 적정 {overLoad:0.#}) → {(loadEff < 1f ? $"속도 {loadEff:0.##}배 감속" : "정상속도")}\n" +
            $"   ↳ 동물 {animals}마리(속도효율 {animalEff:0.##}배) · 식량 필요 {needFood:0.#} / 실은 {caravan.foodAmount}" +
            (needFood > caravan.foodAmount ? "  ⚠식량부족" : ""));

        tradeStartUtc = timeService.CurrentUtc;   // ← 추가: 지금 시각을 출발 시각으로

        TradeStartTime = tradeStartUtc.ToString("HH:mm:ss");

        DateTime endUtc = timeService.CalculateTradeEnd(tradeStartUtc, TimeSpan.FromSeconds(caravan.totalSeconds));
        TradeEndTime = endUtc.ToString("HH:mm:ss");

        StopAllCoroutines();
        StartCoroutine(RunTrade());
    }

    /// <summary>[테스트] 출발 불가 사유 하나를 실제 값과 함께 사람이 읽기 쉽게 풀어쓴다.</summary>
    private string DescribeBlockReason(DepartureBlockReason reason)
    {
        CaravanData c = caravan;
        switch (reason)
        {
            case DepartureBlockReason.NoWagon:
                return "마차 없음 — 마차 SO를 넣어야 함";
            case DepartureBlockReason.NotEnoughAnimals:
                return $"견인 동물 부족 — 현재 {c.animals.Count}마리 < 최소 {c.wagon.minAnimals}마리";
            case DepartureBlockReason.TooManyAnimals:
                return $"견인 동물 초과 — 현재 {c.animals.Count}마리 > 최대 {c.wagon.maxAnimals}마리";
            case DepartureBlockReason.Overloaded:
                return $"최대적재 초과 — 짐무게 {CaravanCalculator.GetCurrentLoad(c):0.#} > 최대한계 {CaravanCalculator.GetMaxLoad(c):0.#}";
            case DepartureBlockReason.NoCargo:
                return "무역품 없음 — 실은 물건이 하나도 없음";
            case DepartureBlockReason.BrokenWagon:
                return $"마차 파손 — 내구도 {c.currentDurability} (수리 전 출발 불가)";
            case DepartureBlockReason.SlotExceeded:
                return $"짐칸 부족 — 사용 {CaravanCalculator.GetUsedSlots(c)}칸 > 마차 {CaravanCalculator.GetMaxSlots(c)}칸";
            case DepartureBlockReason.MixedAnimalType:
                return "견인 동물 종류 섞임 — 전부 같은 종류여야 함";
            case DepartureBlockReason.NotInPrepare:
                return $"준비 단계 아님 — 현재 상태 {c.state} (이동/정산 중엔 출발 불가)";
            default:
                return reason.ToString();
        }
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

            // [인게임시간] Framework 흉내 — 현실 경과 × 배율을 인게임 경과로 넣는다(SetProgress 전에).
            caravan.elapsedInGameSeconds = elapsed * inGameTimeMultiplier;

            float progress = (caravan.totalSeconds > 0f) ? elapsed / caravan.totalSeconds : 1f;
            JourneyRunner.SetProgress(caravan, progress);   // 여기서 식량 소진 자동 체크됨(인게임 경과 기준)
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
                    BanditRaidResult raid = JourneyRunner.ResolveBanditRaid(
                        caravan,
                        raidBanditCombatPower,
                        raidCargoLootRate,
                        raidFodderLootRate,
                        raidRandomSeed + i);
                    Debug.Log(raid.passedSafely
                        ? $"[산적] {elapsedSec}초: {i + 1}번째 이벤트 — 무사 통과 ({raid.safePassChancePercent:0.##}%)"
                        : $"[산적] {elapsedSec}초: {i + 1}번째 이벤트 — 실패! 무역품 -{raid.cargoLost}, 여물 -{raid.foodLost}, 용병 소멸 ID={raid.lostMercenaryInstanceId}");
                }
                raidDone = true;
            }

            // 1초 단위 출력 (진행도 + 남은 식량)
            if (elapsedSec > lastPrintedSec && elapsedSec <= totalSecInt)
            {
                float food = CaravanCalculator.GetRemainingFood(caravan);
                string starve = caravan.runFoodDepleted ? "  ⚠식량바닥(제한시간 카운트다운)" : "";
                Debug.Log($"이동중 {elapsedSec}/{totalSecInt}초  (진행 {caravan.progress01 * 100f:0}%, 식량 {food:0.#}, 내구도 {caravan.currentDurability}){starve}");
                lastPrintedSec = elapsedSec;
            }

            if (JourneyRunner.IsArrived(caravan) || caravan.runFatalReason != JourneyFailureReason.None)
            {
                JourneyResultData result = JourneyRunner.Settle(caravan);
                PrintResult(result);

                // [2차 빌드] 도착 시 자동 판매 제거.
                //   판매는 도착 후 "시장 화면에서 플레이어가 명시한 품목·수량"으로만 이뤄진다.
                //   (Progression 요청: 도착만으로 적재 상품을 자동 판매하지 않는다.
                //    이동 결과 Claim은 이동 비용·손실·보상만 확정하고 cargo는 유지한다)

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

        // [M2] 마차 내구도 상태 (이번 무역 손실 + 현재 남은 내구도)
        int maxDur = (caravan.wagon != null) ? caravan.wagon.maxDurability : 0;
        Debug.Log($"[마차] 내구도 {caravan.currentDurability}/{maxDur} (이번 손실 {r.durabilityLost:0})");

        // [M2] 정산 계산값 (마일스톤 완료기준: 정산 데이터에 포함되는 5개 값)
        Debug.Log($"[정산 계산값] 이동 {r.travelSeconds:0.#}초 · 식량소모 {r.foodConsumed:0.#} · 출발짐무게 {r.departureLoad:0.#} · 최종적정 {r.finalEfficientLoad:0.#} · 과적비율 {r.overloadRatio:P0}");
    }

    // [삭제됨 2026-07-10] FillSample(imsi 하드코딩 상단 구성)은 SO 기반 FillFromSO로 대체.
    //   무역품·마차·동물은 이제 이종현 SO(WagonData/DraftAnimalData/TradeItemData)에서 읽어온다.
    //   (imsi 타입 자체는 천성욱 저장 시스템 CaravanSaveDataMapper가 런타임 타입으로 써서 유지)

    // [SO 연결] 이종현 더미 SO에서 값을 읽어 imsi 상단으로 조립한다. (SO를 인스펙터에 드래그 후 우클릭)
    //   ※ imsi는 임시 브릿지. 속도는 SO(base 속도 합)와 imsi(배수) 모델이 달라 '근사' 매핑이고,
    //     슬롯·추가효율적재(increaseOverLoad 등)는 Core 신규 계산 붙일 때 반영 예정.
    [ContextMenu("SO에서 상단 채우기")]
    public void FillFromSO()
    {
        caravan = new CaravanData();

        if (soWagon != null)
        {
            caravan.wagon = new imsiWagonData
            {
                wagonName     = soWagon.DisplayName,
                overLoad      = soWagon.Overload,
                maxLoad       = soWagon.MaxLoad,
                minAnimals    = soWagon.MinRequireAnimals,
                maxAnimals    = soWagon.MaxPullAnimals,
                maxDurability = soWagon.MaxDurability,
                inventorySlotCount = soWagon.InventorySlotCount,   // [M2] 마차 짐칸 수
            };
            caravan.currentDurability = soWagon.MaxDurability;   // 내구도 최대로 시작
        }

        // 견인 동물: 리스트의 SO를 한 마리씩 추가 (같은 SO 여러 번 넣으면 그만큼 여러 마리 / 다른 종류 섞으면 단일종류 검증에 걸림)
        foreach (DraftAnimalData a in soAnimals)
        {
            if (a == null) continue;
            caravan.animals.Add(new imsiAnimalData
            {
                animalName = a.DisplayName,
                // [속도 모델 불일치] SO는 baseMoveSpeed(더하기식) / imsi는 배수(말=1). 지금은 배수 1(정상 속도)로 둠. [M2 신규 작업]
                speed      = 1f,
                foodPerKm  = a.FeedConsumption,        // 초당 소모 (둘 다 per-sec)
                increaseOverLoad = a.IncreaseOverLoad, // [M2] 동물이 적정적재 늘림
                increaseMaxLoad  = a.IncreaseMaxLoad,  // [M2] 동물이 최대적재 늘림
                animalType = a.AnimalType,             // [M2] 종류 → 단일종류 검증
            });
        }

        // 무역품: 리스트의 (아이템 SO + 수량)을 각각 화물로 추가 (마차 슬롯 수 초과하면 출발 검증에서 막힘)
        foreach (SoItemInput input in soItems)
        {
            if (input == null || input.item == null) continue;
            caravan.cargo.Add(new CargoEntry
            {
                item = new imsiTradeItemData
                {
                    id        = input.item.ItemId,
                    itemName  = input.item.DisplayName,
                    weight    = input.item.Weight,
                    basePrice = input.item.BaseBuyPrice,
                    maxCount  = input.item.MaxCount,   // [M2] 한 칸 스택 크기
                },
                quantity = input.quantity,
            });
        }

        caravan.foodAmount = soFoodAmount;
        caravan.mercenaries.Add(new imsiMercenaryData { mercName = "용병", combatPower = 10, contractCount = 3 });  // [임시] 용병 SO 없음

        RecalculateCache();
        UpdateStatusDisplay();

        string wn = soWagon != null ? soWagon.DisplayName : "없음";
        string typeOk = CaravanCalculator.IsAnimalTypeUniform(caravan) ? "단일종류 O" : "종류섞임 ✗";
        Debug.Log(
            $"[SO 구성] 마차 {wn} · 동물 {caravan.animals.Count}마리({typeOk}) · 무역품 {caravan.cargo.Count}종 {CaravanCalculator.GetCargoCount(caravan)}개 · 식량 {caravan.foodAmount}\n" +
            $"   ↳ 짐무게 {cachedLoad:0.#} (무역품무게 {CaravanCalculator.GetCargoWeight(caravan):0.#} + 식량무게 {CaravanCalculator.GetFoodWeight(caravan):0.#}) / 최대한계 {cachedMaxLoad:0.#} / 칸 {CaravanCalculator.GetUsedSlots(caravan)}/{CaravanCalculator.GetMaxSlots(caravan)} / 내구도 {caravan.currentDurability}");
    }

    // [테스트 편의] 마차 내구도를 최대로 복구. (기획상 1차엔 "수리 시스템" 대신 "재장착"이 정식 — 이건 테스트용 리셋)
    [ContextMenu("마차 수리하기")]
    public void RepairWagon()
    {
        if (caravan == null || caravan.wagon == null)
        {
            Debug.LogWarning("[수리] 마차가 없음 — 먼저 'SO에서 상단 채우기'");
            return;
        }
        if (caravan.state == JourneyState.Traveling)
        {
            Debug.LogWarning("[수리] 이동 중엔 수리 불가 — 무역 끝난 뒤에");
            return;
        }

        int before = caravan.currentDurability;
        caravan.currentDurability = caravan.wagon.maxDurability;   // 내구도 최대로 복구
        UpdateStatusDisplay();
        Debug.Log($"[수리] 마차 내구도 {before} → {caravan.currentDurability}/{caravan.wagon.maxDurability} (완전 수리)");
    }

    // [테스트] BuildPrepareDisplay 결과 확인 — UI(이종현님)가 받을 "준비 화면 값 묶음"을 로그로.
    [ContextMenu("준비 표시 확인")]
    public void LogPrepareDisplay()
    {
        if (caravan == null || caravan.wagon == null)
        {
            Debug.LogWarning("[준비표시] 상단이 없음 — 먼저 'SO에서 상단 채우기'");
            return;
        }

        PrepareDisplayData p = CaravanCalculator.BuildPrepareDisplay(caravan, distanceKm, inGameTimeMultiplier);
        Debug.Log(
            $"[준비 표시] 짐무게 {p.currentLoad:0.#} (무역품 {p.cargoWeight:0.#} + 식량 {p.foodWeight:0.#}) / 적정 {p.overloadLimit:0.#} / 최대 {p.maxLoad:0.#}\n" +
            $"   ↳ 칸 {p.usedSlots}/{p.maxSlots} · 물건 {p.cargoCount}개 · 과적 {(p.isOverloaded ? $"O({p.overloadRatio:P0}, 속도 {p.loadSpeedModifier:0.##}배)" : "X")}\n" +
            $"   ↳ 예상 이동 {p.estimatedTravelSeconds:0.#}초 · 예상 식량 {p.requiredFood:0.#}");
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

        Debug.Log(
            $"[상단 상태] 짐무게 {cachedLoad:0.#} (무역품무게 {CaravanCalculator.GetCargoWeight(caravan):0.#} + 식량무게 {CaravanCalculator.GetFoodWeight(caravan):0.#}) / 적정한계 {CaravanCalculator.GetFinalEfficientLoad(caravan):0.#} / 최대한계 {cachedMaxLoad:0.#} / 물건 {cachedCargoCount}개 · 칸 {CaravanCalculator.GetUsedSlots(caravan)}/{CaravanCalculator.GetMaxSlots(caravan)}\n" +
            $"   ↳ 적정한계 = 마차 {CaravanCalculator.GetBaseEfficientLoad(caravan):0.#} + 동물 {CaravanCalculator.GetAdditionalEfficientLoad(caravan):0.#}  (짐무게가 적정 넘으면 감속 · 최대 넘으면 출발불가)\n" +
            $"   ↳ 초당식량 {cachedFoodPerSec:0.##} · 동물속도효율 {cachedSpeedEfficiency:0.##}배 · 과적 {(CaravanCalculator.IsOverloaded(caravan) ? $"O({CaravanCalculator.GetOverloadRatio(caravan):P0}, 속도 {CaravanCalculator.GetLoadSpeedModifier(caravan):0.##}배)" : "X")}");
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
