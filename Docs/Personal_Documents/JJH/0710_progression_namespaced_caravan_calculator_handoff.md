# Progression Namespaced CaravanCalculator Handoff

작성일: 2026-07-10  
담당: Progression  
대상: Core Gameplay

## 목적

Core 원본 `CaravanCalculator`를 수정하지 않고, 식량 부족 속도 감소 계산을 포함한
수정본을 `ND.Economy` 네임스페이스로 분리했다.

파일:

```text
Assets/_Project/03.Economy/06_Integration/CaravanCalculator.cs
```

```csharp
namespace ND.Economy
{
    public static class CaravanCalculator
}
```

원본 Core `CaravanCalculator`와 이름 충돌 없이 공존하며, Core는 수정본을 비교한 뒤
필요한 메서드만 병합하거나 파일 전체를 교체할 수 있다.

## 추가된 식량 부족 속도 계산

출발 시 기존 속도 계산으로 예상 이동 시간을 먼저 구한다.

```text
기본 예상 필요 식량 = 초당 식량 소모 × 기본 예상 이동 시간
식량 부족률 = (필요 식량 - 적재 식량) / 필요 식량
식량 속도 효율 = 1 - 0.5 × 식량 부족률
최소 식량 속도 효율 = 0.5
```

최종 이동 시간:

```text
최종 이동 시간 = 기본 이동 시간 / 식량 속도 효율
```

필요 식량 이상을 적재하면 식량 속도 효율은 `1.0`이다.
절반만 적재하면 `0.75`이며, 식량이 없으면 최소값 `0.5`이다.

## Core 적용 시

원본 Core 구현으로 교체하려면 다음만 처리한다.

1. `namespace ND.Economy { ... }`를 제거한다.
2. `global::CaravanConfig`를 `CaravanConfig`로 바꾼다.
3. 기존 Core 파일의 변경사항과 충돌을 확인한 뒤 병합한다.

수정본은 출발 시점 식량 부족만 계산한다. 이동 중 식량 도난·고갈에 따른 실시간 속도 변경은
현재 범위에 포함하지 않는다.

## 검증

`ND/Economy/Run All M1 Economy Checks`를 실행해 성공을 확인했다.

검증 항목:

- 필요 식량 이상: 속도 배율 `1.0`
- 필요 식량의 절반: 속도 배율 `0.75`
- 식량 없음: 최소 속도 배율 `0.5`
