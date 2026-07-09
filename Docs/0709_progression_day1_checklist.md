# Progression Day 1 Checklist

## 오늘 목표

Progression의 1순위 작업은 가격/정산 계산의 계약을 고정하는 것이다.
오늘은 최종 밸런스가 아니라 다른 담당자가 호출할 수 있는 입력/출력과 표시 항목을 확정한다.

참조 문서: `docs/0709_progression_price_settlement_contract.md`

---

## 1. 팀에 공유할 확정 사항

- [x] 가격 계산 순서 확정
- [x] `PriceCalculationInput` 필드 확정
- [x] `PriceCalculationResult` 필드 확정
- [x] `PriceModifierBreakdown` 필드 확정
- [x] `SettlementInput` 필드 확정
- [x] `SettlementBreakdown` 필드 확정
- [x] `SettlementEntry` 종류 확정
- [ ] UI 내부 경제 계산 금지 합의 - UI 담당 확인 대기
- [x] Core에 넘길 런타임 스탯 목록 확정

---

## 2. M1까지 반드시 동작해야 하는 계산

- [x] 기본 구매가 계산
- [x] 기본 판매가 계산
- [x] 상품 구매 비용 합산
- [x] 상품 판매 수익 합산
- [x] 식량 비용 분리
- [x] 용병 비용 분리
- [x] 순이익 계산
- [x] 발전용 재화 보상 계산
- [x] 성장 구매 비용 차감
- [x] 성장 효과 1종을 Core용 스탯으로 반환

---

## 3. UI & Data에 넘길 것

- [x] 최종 구매가
- [x] 최종 판매가
- [x] 구매 비용
- [x] 판매 수익
- [x] 식량 비용
- [x] 용병 비용
- [x] 손실 금액
- [x] 순이익
- [x] 발전용 재화
- [x] 보정 항목 리스트
- [x] 표시명 키
- [x] 계산 실패 사유 코드

---

## 4. Core Gameplay에 넘길 것

- [x] 최대 적재량 보정
- [x] 이동 속도 보정 - M1에서는 `1.0` 고정
- [x] 식량 효율 보정
- [x] 전투력 보정
- [x] 실패 손실 상한
- [x] 성장 효과 적용 결과

---

## 5. Content & Tools에 요청할 것

- [ ] 상품 기본 구매가/판매가 - Content 실제 값 대기
- [ ] 도시별 가격 보정 - Content 실제 값 대기
- [ ] 식량 기본 비용 - RouteData 실제 값 대기
- [ ] 용병 기본 비용 - RouteData 실제 값 대기
- [x] 성장 비용 테스트 값
- [x] 발전용 재화 보상 테스트 값
- [x] 음수/누락 참조 검증 대상 필드

---

## 6. 오늘의 완료 조건

- [x] Core가 더미 값으로 런타임 스탯을 받을 수 있다.
- [x] UI가 정산 화면을 `SettlementBreakdown.entries`만 보고 구성할 수 있다.
- [x] Content가 어떤 가격/비용 필드를 채워야 하는지 안다.
- [x] Framework가 저장해야 할 경제 상태 필드를 식별할 수 있다.
- [x] M1에서는 계절/재난/이벤트/과공급이 비어 있어도 계산이 실패하지 않는다.
