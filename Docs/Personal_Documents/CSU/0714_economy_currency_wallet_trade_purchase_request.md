# Economy 요청 — CurrencyWallet 무역 구매·환불 API

**요청 일자:** 2026-07-14  
**요청자:** Framework & Integration (천성욱)  
**대상 담당:** Economy  
**관련 Framework 브랜치:** `feature/framework/market-inventory-save-schema`  
**원본 요청:** [`Docs/Personal_Documents/JJH/0714_Progression MarketInventory_Change_Request.md`](../JJH/0714_Progression%20MarketInventory_Change_Request.md)

---

## 쉬운 요약 (먼저 읽을 것)

### 이게 뭐예요?

상점에서 물건을 **확정 구매**할 때 돈을 빼고, **취소·다시 편집**할 때 돈을 돌려주는 **지갑 API**입니다.

지금은 무역이 끝난 뒤 정산·성장에서만 지갑을 씁니다 (`ApplySettlement`, `ApplyGrowthPurchase`).  
JJH가 만든 **상점 재고·적재 저장 연동**을 켜려면, 준비 화면에서 사는 순간에도 같은 지갑으로 차감·환불이 필요합니다.

### 왜 필요해요?

1. **돈과 재고를 한곳에서 다루기 위해서**  
   확정 시: 돈 차감 + 상점 재고 감소 + 화물 저장을 같이 맞춥니다.  
   취소·재편집 시: 돈 환불 + 재고 복구가 필요합니다.
2. **잘못된 금액·잔액 부족을 분명히 거절하기 위해서**  
   음수 금액, 잔액 부족, 잘못된 환불을 에러 코드로 구분합니다.
3. **UI가 이미 이 API 이름을 호출하도록 짜여 있어서**  
   `MarketInventorySession`이 `ApplyTradePurchase` / `ApplyTradeRefund`를 부릅니다.  
   (지금은 `ND_MARKET_SAVE_SCHEMA_VNEXT` 심볼로 꺼져 있음)

> 이 요청은 Framework가 Save 작업 중 **새로 발견한 버그가 아닙니다.**  
> JJH 변경 요청서의 Economy 항목을 담당자용으로 정리한 handoff입니다.

### 지금 당장 꼭 해야 하나요? (현실적인 위치)

| 질문 | 답 |
|------|----|
| 1차 빌드 **핵심 무역 루프**(준비→출발→정산→성장)가 깨지나요? | **아니요.** 심볼이 꺼져 있으면 이 API 없이도 루프는 돌아갑니다. |
| 공식 M1~M3 마일스톤에 “이 API를 이날까지”라고 적혀 있나요? | **없습니다.** M1 Economy는 정산·성장 지갑으로 이미 정의되어 있습니다. |
| 기획서와는 맞닿아 있나요? | **예.** 도시 상점 재고를 시드/세이브로 복원하는 설계와 연결됩니다. 다만 1차 최소 목표에는 안 들어가 있습니다. |
| **언제 구현하는 게 현실적일까요?** | **상점 영속 연동(`ND_MARKET_SAVE_SCHEMA_VNEXT`)을 켤 때**, 보통은 **1차 제출 이후(2차)** 또는 팀이 “동결 예외로 VNEXT 통합”을 **합의한 경우**만 1차 말미. |

**일정 맥락 (2026-07-14 기준)**

- M4 기능 동결: **7월 14일 오후 6시** — 이후 신규 기능·구조 변경 병합은 원칙적으로 금지
- JJH 원본 요청 작성일: **7월 14일** → 1차 필수 일정에 끼워 넣기엔 늦음
- 1차 축소 목표: Title/InGame에서 **버튼으로 한 사이클**이 도는 것 (상점 UTC 재고 + 원자적 구매 지갑은 최소 목표 밖)

**정리:**  
Economy 담당에게 “지금 당장 M1 블로커”로 요청할 필요는 없습니다.  
**상점 저장·구매 연동을 실제로 켤 계획일 때** 이 API(또는 동등한 거래 API)를 넣으면 됩니다.

---

## Purpose (기술)

상점 구매 확정·재편집·취소 시 UI가 `CurrencyWallet`으로 TradeMoney를 원자적으로 차감·환불해야 한다.  
Framework는 `SaveData` 상점 재고·구매 준비 스키마와 `JsonSaveService` 정규화를 제공한다. Economy는 아래 거래 API를 추가해 달라.

---

## Content 팀 관련 정리

JJH 원본 요청의 “Content — `JsonSaveService` 정규화”는 실제 소유가 `Assets/_Project/11.CoreServices/Scripts/Save/JsonSaveService.cs`(Framework)이다.

- Framework PR에서 `world.marketInventories` / `stocks` / `marketPurchasePreparation` null 보정을 처리한다.
- Content 팀에 별도 코드 변경 요청은 없다.

---

## Requested Changes

### 대상 파일

- `Assets/_Project/03.Economy/05_Currency/CurrencyWallet.cs`
- 필요 시 `Assets/_Project/03.Economy/05_Currency/CurrencyModels.cs` (에러 상수만)

### API

```csharp
CurrencyApplyResult ApplyTradePurchase(CurrencyState state, long purchaseCost);
CurrencyApplyResult ApplyTradeRefund(CurrencyState state, long refundAmount);
```

기존 `ApplySettlement` / `ApplyGrowthPurchase`와 동일하게 `CurrencyApplyResult`(Success, ErrorCode, Before, After)를 반환한다.

### 요구 동작

| 조건 | 결과 |
|------|------|
| `state == null` | 실패 (`INVALID_CURRENCY_STATE` 기존 상수 재사용) |
| `purchaseCost < 0` 또는 `refundAmount < 0` | 실패, state 미변경 |
| 구매 시 `state.TradeMoney < purchaseCost` | 실패, state 미변경 |
| 구매 성공 | `TradeMoney -= purchaseCost`, before/after 스냅샷 반환 |
| 환불 성공 | `TradeMoney`에 가산. `long` 오버플로 시 실패 또는 `long.MaxValue` 클램프 정책을 명시하고 구현 |
| 실패 시 | 입력 `state` 필드를 변경하지 않음 |

`DevelopmentCurrency`는 이번 API에서 변경하지 않는다.

### 에러 코드 제안

기존 `INVALID_*` 스타일에 맞춘다.

| 상수 | 사용 시점 |
|------|-----------|
| `INVALID_TRADE_PURCHASE` | 음수 구매 금액 등 잘못된 구매 |
| `INSUFFICIENT_TRADE_MONEY` | TradeMoney 잔액 부족 |
| `INVALID_TRADE_REFUND` | 음수 환불 금액 등 잘못된 환불 |

UI(`MarketInventorySession`)는 실패 시 `currencyResult.ErrorCode`를 그대로 전달한다.

### 호출자

- `Assets/Scripts/UI/MarketInventoryIntegration.cs`의 `MarketInventorySession`
  - `CommitPurchase` → `ApplyTradePurchase`
  - `ReopenCommittedAsDraft` / `CancelPreparation` → `ApplyTradeRefund`
- 해당 파일은 `#if ND_MARKET_SAVE_SCHEMA_VNEXT`로 감싸져 있으므로, Economy API 병합 후 심볼 활성화 시 컴파일된다.

---

## 선행 / 후속

1. **선행 (Framework):** `feature/framework/market-inventory-save-schema`  
   - `WorldSaveData.marketInventories` / `marketPurchasePreparation`  
   - `JsonSaveService.NormalizeData` null 보정  
   - `SaveData.CurrentVersion = 5` 유지
2. **이번 요청 (Economy):** `ApplyTradePurchase` / `ApplyTradeRefund`  
   - **시점:** VNEXT 상점 연동을 켤 때 (권장: 1차 제출 이후, 또는 팀 합의된 동결 예외)
3. **후속 (UI / JJH):** 아래 Activation Handoff

---

## UI(JJH) Activation Handoff

Core Save 스키마 PR과 Economy API PR이 `dev2`에 병합된 뒤:

1. Core + Economy 병합 커밋이 로컬에 있는지 확인한다.
2. Unity Scripting Define Symbols에 `ND_MARKET_SAVE_SCHEMA_VNEXT`를 추가한다.
3. `MarketInventoryIntegrationProbe`를 한 번 실행해 결과를 재확인한다.
4. 실제 씬에서 구매 확정 → 재실행 → 적재 복원 → 취소 흐름을 회귀 테스트한다.

상점 기능 검증 시에는 재고·준비 상태가 비어 있는 **새 세이브 또는 Reset 후 생성**을 권장한다.  
version 5 기존 세이브는 wipe되지 않으며, Load 시 Framework Normalize가 빈 market 컨테이너를 채운다.

---

## Not in Scope

- `SaveData` / `JsonSaveService` 수정 (Framework 담당)
- `ND_MARKET_SAVE_SCHEMA_VNEXT` 심볼 활성화 (UI 통합 단계)
- Scene / Prefab / ProjectSettings 변경
- `loadedLines` 별도 저장 필드 추가 (적재는 `caravan.cargo` 재사용)
- 1차 빌드 M1~M3 필수 통과 조건으로 이 API를 요구하는 것 (해당 없음)

---

## Verification (Economy 담당)

- 음수 구매/환불 → 실패, state 불변
- 잔액 부족 구매 → `INSUFFICIENT_TRADE_MONEY`, state 불변
- 정상 구매/환불 → before/after TradeMoney 일치
- 환불 시 `long.MaxValue` 근처 오버플로 경로 확인
- 기존 `ApplySettlement` / `ApplyGrowthPurchase` 스모크 회귀
