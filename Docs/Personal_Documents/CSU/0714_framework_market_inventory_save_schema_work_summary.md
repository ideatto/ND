# 상점 재고 저장 스키마 Framework 작업 요약

**작성일:** 2026-07-14  
**작성:** Framework & Integration (천성욱)  
**브랜치:** `feature/framework/market-inventory-save-schema`  
**Base:** `dev2`

---

## 1. 한 줄 요약

JJH 상점 재고·적재 저장 연동을 위해, Framework Save에 **상점 재고·구매 준비 DTO**를 넣고 **로드/저장 시 null 정규화**를 보강했다.  
스키마 version은 **5 유지**. Economy 지갑 API는 이번 브랜치에서 구현하지 않고 **요청서만** 작성했다.

---

## 2. 왜 이 작업을 했나

원본 요청: [`Docs/Personal_Documents/JJH/0714_Progression MarketInventory_Change_Request.md`](../JJH/0714_Progression%20MarketInventory_Change_Request.md)

UI 쪽은 이미 `world.marketInventories` / `world.marketPurchasePreparation`와 지갑 구매·환불을 쓰도록 짜여 있지만, 소유권 충돌을 피하려고 `ND_MARKET_SAVE_SCHEMA_VNEXT`로 **꺼 둔 상태**다.  
Framework가 먼저 Save 스키마와 정규화를 맞춰 두면, 나중에 심볼을 켰을 때 저장 필드가 없어서 깨지는 일을 막을 수 있다.

---

## 3. 이번 브랜치에서 한 일

### 3-1. 코드

| 파일 | 변경 |
|------|------|
| [`Assets/_Project/11.CoreServices/Scripts/Save/SaveData.cs`](../../../Assets/_Project/11.CoreServices/Scripts/Save/SaveData.cs) | `WorldSaveData`에 `marketInventories`, `marketPurchasePreparation` 추가. DTO 3종(`MarketInventorySaveData`, `MarketStockSaveData`, `MarketPurchasePreparationSaveData`) 추가. 헤더·주석 갱신. **`CurrentVersion = 5` 유지** |
| [`Assets/_Project/11.CoreServices/Scripts/Save/JsonSaveService.cs`](../../../Assets/_Project/11.CoreServices/Scripts/Save/JsonSaveService.cs) | `NormalizeData`에서 market 리스트/stocks/구매 준비 null → 빈 컨테이너·기본 객체로 보정 |

**의도적으로 하지 않은 것**

- `loadedLines` 별도 저장 필드 추가 (적재는 기존 `caravan.cargo` 재사용)
- `SaveData.CurrentVersion` 증가 (구 version 5 세이브 wipe 방지)
- `CurrencyWallet` 구현
- `ND_MARKET_SAVE_SCHEMA_VNEXT` 심볼 활성화
- UI / Scene / Prefab / ProjectSettings 수정

### 3-2. 문서

| 파일 | 내용 |
|------|------|
| [`0714_economy_currency_wallet_trade_purchase_request.md`](0714_economy_currency_wallet_trade_purchase_request.md) | Economy에 `ApplyTradePurchase` / `ApplyTradeRefund` 요청. **왜 필요한지·언제 하면 현실적인지**를 쉬운 말로 정리. Content 정규화는 Framework가 처리한다고 명시. UI 활성화 handoff 포함 |

---

## 4. 스키마 version 결정

**결정:** version **5 유지** + `NormalizeData` 보강 (마이그레이션·version bump 없음).

| 세이브 | 동작 |
|--------|------|
| 기존 version 5 (새 필드 없음) | Load 시 필드가 null → Normalize가 빈 리스트/기본 객체 생성 → **로드 가능** |
| 새 세이브 | 필드 초기값으로 빈 컨테이너 생성 → **정상** |

상점 기능 자체 검증은 재고가 비어 있는 **새 세이브 또는 Reset 후**가 단순하다.  
기존 세이브를 “반드시” 써야 하는 것은 아니다.

---

## 5. 검증

- IDE 린트: `SaveData.cs` / `JsonSaveService.cs` 오류 없음
- Unity 에디터가 프로젝트를 연 상태에서 `Assembly-CSharp.dll`에 `MarketInventorySaveData` 등 타입 문자열 **포함 확인**
- Editor.log: `error CS` / `Scripts have compiler errors` **0**
- UI(`MarketInventoryIntegration`)가 기대하는 필드명·타입과 소스 계약 **일치**
- 배치 모드 단독 컴파일은 에디터 점유 때문에 불가했음 (열린 에디터 컴파일로 대체)

---

## 6. 이번 작업과 Economy 요청의 관계

| 항목 | 1차 빌드 핵심 루프 | 상점 VNEXT 연동을 켤 때 |
|------|-------------------|-------------------------|
| Save DTO + Normalize (이번 브랜치) | 있어도 무방, 루프를 깨지 않음 | **필요** |
| `ApplyTradePurchase` / `ApplyTradeRefund` | **불필요** (정산·성장 지갑으로 충분) | **필요** (또는 동등 API) |

Economy 요청은 공식 M1~M3에 날짜가 박힌 필수 항목이 아니다.  
현실적으로는 **VNEXT를 켤 때** / 보통 **1차 제출 이후**에 구현하면 된다.  
자세한 설명은 Economy 요청서 상단 「쉬운 요약」을 본다.

---

## 7. 아직 안 한 것 / 다음 단계

1. **커밋·푸시·PR** (base: `dev2`) — 사용자 요청 시 진행
2. Economy: 지갑 구매·환불 API (요청서 기준, 시점 합의 후)
3. UI/JJH: Core + Economy 병합 후 `ND_MARKET_SAVE_SCHEMA_VNEXT` 활성화 → `MarketInventoryIntegrationProbe` → 씬 회귀

---

## 8. 관련 문서

- 원본 변경 요청: [`../JJH/0714_Progression MarketInventory_Change_Request.md`](../JJH/0714_Progression%20MarketInventory_Change_Request.md)
- Economy 요청서: [`0714_economy_currency_wallet_trade_purchase_request.md`](0714_economy_currency_wallet_trade_purchase_request.md)
- 1차 축소·동결 맥락: [`0713_first_build_progress_scope_and_roles.md`](0713_first_build_progress_scope_and_roles.md)
- 팀 마일스톤: [`Docs/Planning_Milestone/00_Team_Rules_and_Milestone.md`](../../Planning_Milestone/00_Team_Rules_and_Milestone.md)
