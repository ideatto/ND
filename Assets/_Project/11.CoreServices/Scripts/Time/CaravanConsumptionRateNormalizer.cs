/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - SharedGameData 또는 debug 설정의 raw 식량 소모율을 Core caravan이 사용하는 인게임 초당 소모율로 정규화한다.
 *
 * Main Features
 * - IInGameTimeProvider.ToConsumptionPerInGameSecond를 caravan 동물 목록에 일괄 적용한다.
 *
 * Usage for Team Members
 * - caravan runtime 조립 직후, 무역 출발 직전에 ApplyToCaravan(...)을 한 번 호출한다.
 * - ToRuntime 복원이나 매 tick progress 갱신 시에는 호출하지 않는다. (이중 정규화 방지)
 *
 * Important Notes
 * - Core는 foodPerKm 필드명을 인게임 1초당 소모율로 해석한다. (필드 rename은 별도 PR 예정)
 * - inGameTimeProvider가 null이면 caravan을 변경하지 않는다.
 */
namespace ND.Framework
{
    /// <summary>
    /// caravan 동물 식량 소모율을 인게임 초당 단위로 정규화하는 helper이다.
    /// </summary>
    public static class CaravanConsumptionRateNormalizer
    {
        /// <summary>
        /// caravan 동물 목록의 raw 식량 소모율을 인게임 초당 소모율로 변환한다.
        /// </summary>
        /// <param name="caravan">정규화할 runtime caravan. null이면 아무 작업도 하지 않는다.</param>
        /// <param name="inGameTimeProvider">FoodConsumptionUnit 정책을 제공하는 서비스.</param>
        /// <remarks>
        /// 출발 직전 또는 SharedGameData에서 runtime을 조립한 직후에만 호출해야 한다.
        /// 저장 데이터에서 복원한 caravan에는 이미 정규화된 값이 있을 수 있다.
        /// </remarks>
        public static void ApplyToCaravan(CaravanData caravan, IInGameTimeProvider inGameTimeProvider)
        {
            if (caravan?.animals == null || inGameTimeProvider == null)
            {
                return;
            }

            foreach (var animal in caravan.animals)
            {
                if (animal == null)
                {
                    continue;
                }

                animal.foodPerKm = inGameTimeProvider.ToConsumptionPerInGameSecond(animal.foodPerKm);
            }
        }
    }
}
