# Cargo Sell 금액 표시 정책 (2026-08-03)

## 결론

Cargo Sell UI의 판매 단가, 행 판매 금액, 총 판매 금액은 `CurrencyTextFormatter.Format(long)`을 재사용한다.
금액이 길어질 때 TMP Auto Size로만 축소하지 않고 `만 / 억 / 조 / 경` 단위로 분해해 표시한다.

## 기존 Trading Currency HUD 방식

- 구현: `Assets/99.Sandbox/_LJH/01.Script/Runtime/ViewData/CurrencyTextFormatter.cs`
- 사용처: `Assets/99.Sandbox/_LJH/01.Script/MonoBehaviour/CurrencyHudPresenter.cs`
- 기준 단위: `1만`, `1억`, `1조`, `1경`
- 단위 사이에는 공백을 둔다.
- 단위 미만의 나머지도 버리지 않고 정수로 표시한다.
- 음수는 전체 결과 앞에 `-`를 붙인다.
- 포맷터는 통화 기호 또는 `G`를 붙이지 않는다.

예시:

| 원본 값 | 표시 숫자 |
| ---: | --- |
| 0 | `0` |
| 9,999 | `9999` |
| 10,000 | `1만` |
| 100,010,005 | `1억 1만 5` |
| 1,334,455 | `133만 4455` |

## Cargo Sell 적용 계약

- ViewData에는 계산 전 원본 `long` 값을 유지한다.
- View에서 표시할 때만 `CurrencyTextFormatter.Format(value)`를 호출한다.
- Cargo Sell의 현재 표기 규칙에 맞춰 결과 뒤에 `G`를 붙인다. 예: `1억 2500만G`.
- 단가, 행 판매 금액, 총 판매 금액에 동일한 규칙을 적용한다.
- 아이템 이름은 제한 폭 안에서 Auto Size 또는 말줄임을 사용할 수 있지만, 금액 축약을 Auto Size에만 의존하지 않는다.
- 총 판매 금액은 라벨과 값 TMP를 분리해 값 영역을 오른쪽 정렬한다.
- 계산, 저장, 정산 데이터에는 포맷된 문자열을 사용하지 않는다.

## 연결 범위

이 정책은 `_LJH` 내부 UI/View 연결만으로 적용 가능하며, Cargo Sell 작업의 수정 금지 스크립트 10종을 변경할 필요가 없다.
실제 판매 확정과 저장 연결은 별도 단계로 보류한다.

## 확인할 경계값

구현 연결 시 최소한 `0`, `9,999`, `10,000`, `99,999,999`, `100,000,000`, `long.MaxValue`를 확인한다.
Cargo Sell 금액은 정상 흐름에서 음수가 될 수 없으므로 음수가 전달되면 표시 이전 데이터 검증 대상으로 취급한다.
