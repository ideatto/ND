/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - 실패 무역을 Claim할 때 해당 Caravan의 장착 운송 자산과 적재물을 전량 제거한다.
 * - 호출부의 SaveData snapshot/rollback 범위 안에서만 사용한다.
 */
using System;
using System.Collections.Generic;

namespace ND.Framework
{
    public static class FailedTradeTransportLoss
    {
        /// <summary>
        /// 장착 마차와 동물을 소유 인벤토리에서 제거하고 Caravan 운송 구성을 비운다.
        /// 동일 정산에 다시 적용해도 다른 소유 개체에는 영향을 주지 않는다.
        /// </summary>
        public static void Apply(SaveData saveData, CaravanData caravan)
        {
            Apply(saveData, caravan, caravan?.wagon?.instanceId, GetAnimalInstanceIds(caravan));
        }

        /// <summary>
        /// Claim 직전 SaveData에서 확정한 장착 개체 ID로 실패 손실을 적용한다.
        /// Runtime 매핑 데이터에서 instanceId가 누락되어도 소유 인벤토리 제거가 빠지지 않는다.
        /// </summary>
        public static void Apply(
            SaveData saveData,
            CaravanData caravan,
            string equippedWagonInstanceId,
            IEnumerable<string> equippedAnimalInstanceIds)
        {
            if (saveData?.player == null || caravan == null)
            {
                return;
            }

            string wagonInstanceId = Normalize(equippedWagonInstanceId);
            var animalInstanceIds = new HashSet<string>(StringComparer.Ordinal);
            if (equippedAnimalInstanceIds != null)
            {
                foreach (string value in equippedAnimalInstanceIds)
                {
                    string instanceId = Normalize(value);
                    if (!string.IsNullOrEmpty(instanceId))
                    {
                        animalInstanceIds.Add(instanceId);
                    }
                }
            }

            if (!string.IsNullOrEmpty(wagonInstanceId) && saveData.player.wagonInventory != null)
            {
                saveData.player.wagonInventory.RemoveAll(
                    owned => string.Equals(
                        Normalize(owned?.instanceId), wagonInstanceId, StringComparison.Ordinal));
            }

            if (animalInstanceIds.Count > 0 && saveData.player.draftAnimalInventory != null)
            {
                saveData.player.draftAnimalInventory.RemoveAll(
                    owned => animalInstanceIds.Contains(Normalize(owned?.instanceId)));
            }

            caravan.wagon = null;
            caravan.animals?.Clear();
            caravan.cargo?.Clear();
            caravan.foodAmount = 0;
            caravan.currentDurability = 0;
            caravan.runFatalReason = JourneyFailureReason.None;
            caravan.runWagonDestroyed = false;
        }

        private static IEnumerable<string> GetAnimalInstanceIds(CaravanData caravan)
        {
            if (caravan?.animals == null)
            {
                yield break;
            }

            for (int index = 0; index < caravan.animals.Count; index++)
            {
                yield return caravan.animals[index]?.instanceId;
            }
        }

        private static string Normalize(string value)
        {
            return value?.Trim() ?? string.Empty;
        }
    }
}
