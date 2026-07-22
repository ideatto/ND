/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Core runtime caravan data와 SaveData의 직렬화 가능한 caravan DTO를 상호 변환한다.
 * - 저장 데이터와 Core 계산 모델(M2 포함) 사이의 필드 복사 규칙을 한 곳에 모은다.
 *
 * Main Features
 * - CaravanSaveData를 runtime CaravanData로 복원한다.
 * - runtime CaravanData의 현재 상태를 CaravanSaveData에 복사한다.
 * - caravan ID와 배치 자산의 안정적인 보유 개체 ID를 생성 없이 보존한다.
 * - 저장 DTO의 null list와 M2 기본값을 정규화한다.
 *
 * Usage for Team Members
 * - 저장 데이터를 runtime caravan으로 사용할 때 ToRuntime(...)을 호출한다.
 * - Core 계산 후 저장 데이터에 반영할 때 CopyToSave(...)를 호출한다.
 *
 * Main Public APIs
 * - ToRuntime(...): 저장 DTO를 runtime CaravanData로 변환한다.
 * - CopyToSave(...): runtime CaravanData를 저장 DTO에 덮어쓴다.
 * - Normalize(...): 저장 DTO의 null collection과 기본값을 보정한다.
 *
 * Important Notes
 * - runtimeData 또는 saveData가 null이면 CopyToSave는 저장 데이터를 변경하지 않는다.
 * - CopyToSave는 runtime caravanId가 비어 있으면 저장 DTO의 기존 caravanId를 유지한다.
 * - starveGraceSeconds 기본값은 300초이며 debug harness는 별도로 덮어쓸 수 있다.
 */
using System.Collections.Generic;

namespace ND.Framework
{
    /// <summary>
    /// CaravanSaveData와 runtime CaravanData 사이의 변환을 담당하는 mapper이다.
    /// </summary>
    public static class CaravanSaveDataMapper
    {
        /// <summary>
        /// 식량 고갈 후 도착 제한 시간(초) 기본값이다.
        /// </summary>
        public const float DefaultStarveGraceSeconds = 300f;

        /// <summary>
        /// 저장 DTO를 Core runtime caravan 데이터로 변환한다.
        /// </summary>
        /// <param name="saveData">변환할 저장 caravan 데이터.</param>
        /// <returns>저장 데이터 값이 복사된 runtime CaravanData.</returns>
        public static CaravanData ToRuntime(CaravanSaveData saveData)
        {
            Normalize(saveData);

            var caravan = new CaravanData
            {
                caravanId = saveData.caravanId,
                wagon = ToRuntime(saveData.wagon),
                foodAmount = saveData.foodAmount,
                foodUnitWeight = saveData.foodUnitWeight,
                state = saveData.state,
                currentDistanceKm = saveData.currentDistanceKm,
                totalSeconds = saveData.totalSeconds,
                progress01 = saveData.progress01,
                elapsedInGameSeconds = saveData.elapsedInGameSeconds,
                settlementClaimed = saveData.settlementClaimed,
                runCargoLost = saveData.runCargoLost,
                runFoodLost = saveData.runFoodLost,
                runFatalReason = saveData.runFatalReason,
                currentDurability = saveData.currentDurability,
                runDurabilityLost = saveData.runDurabilityLost,
                runBattlesFought = saveData.runBattlesFought,
                runStartDurability = saveData.runStartDurability,
                runWearRemainder = saveData.runWearRemainder,
                runFoodDepleted = saveData.runFoodDepleted,
                runFoodDepletedProgress = saveData.runFoodDepletedProgress,
                starveGraceSeconds = saveData.starveGraceSeconds,
                lossLimitRate = saveData.lossLimitRate,
                limitRaidDurability = saveData.limitRaidDurability,
                runOriginalCargoCount = saveData.runOriginalCargoCount,
                runDepartureLoad = saveData.runDepartureLoad
            };

            CopyAnimals(saveData.animals, caravan.animals);
            CopyMercenaries(saveData.mercenaries, caravan.mercenaries);
            CopyCargo(saveData.cargo, caravan.cargo);

            return caravan;
        }

        /// <summary>
        /// runtime caravan 데이터를 저장 DTO에 복사한다.
        /// </summary>
        /// <param name="runtimeData">Core 계산에 사용된 runtime caravan 데이터.</param>
        /// <param name="saveData">값을 덮어쓸 저장 DTO.</param>
        /// <remarks>
        /// runtime caravanId가 null이거나 빈 문자열이면 저장 DTO의 기존 caravanId를 유지한다.
        /// debug/sample runtime처럼 ID가 비어 있는 입력이 selectedCaravanId·자산 잠금 연동을 깨뜨리지 않게 하기 위함이다.
        /// </remarks>
        public static void CopyToSave(CaravanData runtimeData, CaravanSaveData saveData)
        {
            if (runtimeData == null || saveData == null)
            {
                return;
            }

            Normalize(saveData);

            CopyWagon(runtimeData.wagon, saveData.wagon);
            CopyAnimals(runtimeData.animals, saveData.animals);
            CopyMercenaries(runtimeData.mercenaries, saveData.mercenaries);
            CopyCargo(runtimeData.cargo, saveData.cargo);

            // 빈 runtime ID로 저장 ID를 지우면 NormalizeData가 새 ID를 발급해 child 연동이 끊긴다.
            if (!string.IsNullOrEmpty(runtimeData.caravanId))
            {
                saveData.caravanId = runtimeData.caravanId;
            }

            saveData.foodAmount = runtimeData.foodAmount;
            saveData.foodUnitWeight = runtimeData.foodUnitWeight;
            saveData.state = runtimeData.state;
            saveData.currentDistanceKm = runtimeData.currentDistanceKm;
            saveData.totalSeconds = runtimeData.totalSeconds;
            saveData.progress01 = runtimeData.progress01;
            saveData.elapsedInGameSeconds = runtimeData.elapsedInGameSeconds;
            saveData.settlementClaimed = runtimeData.settlementClaimed;
            saveData.runCargoLost = runtimeData.runCargoLost;
            saveData.runFoodLost = runtimeData.runFoodLost;
            saveData.runFatalReason = runtimeData.runFatalReason;
            saveData.currentDurability = runtimeData.currentDurability;
            saveData.runDurabilityLost = runtimeData.runDurabilityLost;
            saveData.runBattlesFought = runtimeData.runBattlesFought;
            saveData.runStartDurability = runtimeData.runStartDurability;
            saveData.runWearRemainder = runtimeData.runWearRemainder;
            saveData.runFoodDepleted = runtimeData.runFoodDepleted;
            saveData.runFoodDepletedProgress = runtimeData.runFoodDepletedProgress;
            saveData.starveGraceSeconds = runtimeData.starveGraceSeconds;
            saveData.lossLimitRate = runtimeData.lossLimitRate;
            saveData.limitRaidDurability = runtimeData.limitRaidDurability;
            saveData.runOriginalCargoCount = runtimeData.runOriginalCargoCount;
            saveData.runDepartureLoad = runtimeData.runDepartureLoad;
        }

        /// <summary>
        /// CaravanSaveData의 필수 하위 객체와 collection을 사용할 수 있는 상태로 보정한다.
        /// </summary>
        /// <param name="saveData">정규화할 저장 caravan 데이터.</param>
        public static void Normalize(CaravanSaveData saveData)
        {
            if (saveData == null)
            {
                return;
            }

            if (saveData.wagon == null)
            {
                saveData.wagon = new WagonSaveData();
            }

            if (saveData.animals == null)
            {
                saveData.animals = new List<AnimalSaveData>();
            }

            if (saveData.mercenaries == null)
            {
                saveData.mercenaries = new List<MercenarySaveData>();
            }

            if (saveData.cargo == null)
            {
                saveData.cargo = new List<CargoEntrySaveData>();
            }

            if (saveData.foodUnitWeight <= 0f)
            {
                saveData.foodUnitWeight = 1f;
            }

            if (saveData.wagon.maxDurability <= 0)
            {
                saveData.wagon.maxDurability = 100;
            }

            if (saveData.wagon.inventorySlotCount <= 0)
            {
                saveData.wagon.inventorySlotCount = 1;
            }

            if (saveData.lossLimitRate <= 0f)
            {
                saveData.lossLimitRate = 1f;
            }

            if (saveData.starveGraceSeconds <= 0f)
            {
                saveData.starveGraceSeconds = DefaultStarveGraceSeconds;
            }

            for (var index = 0; index < saveData.cargo.Count; index++)
            {
                var cargo = saveData.cargo[index];
                if (cargo == null)
                {
                    continue;
                }

                if (cargo.item == null)
                {
                    cargo.item = new TradeItemSaveData();
                }

                if (cargo.item.maxCount <= 0)
                {
                    cargo.item.maxCount = 1;
                }
            }
        }

        private static imsiWagonData ToRuntime(WagonSaveData saveData)
        {
            if (saveData == null || string.IsNullOrEmpty(saveData.wagonName))
            {
                return null;
            }

            return new imsiWagonData
            {
                instanceId = saveData.instanceId,
                wagonName = saveData.wagonName,
                overLoad = saveData.overLoad,
                maxLoad = saveData.maxLoad,
                minAnimals = saveData.minAnimals,
                maxAnimals = saveData.maxAnimals,
                speedModifier = saveData.speedModifier,
                maxDurability = saveData.maxDurability,
                inventorySlotCount = saveData.inventorySlotCount
            };
        }

        private static void CopyWagon(imsiWagonData runtimeData, WagonSaveData saveData)
        {
            if (runtimeData == null)
            {
                saveData.instanceId = string.Empty;
                saveData.wagonName = string.Empty;
                saveData.overLoad = 0f;
                saveData.maxLoad = 0f;
                saveData.minAnimals = 0;
                saveData.maxAnimals = 0;
                saveData.speedModifier = 0f;
                saveData.maxDurability = 100;
                saveData.inventorySlotCount = 1;
                return;
            }

            saveData.instanceId = runtimeData.instanceId ?? string.Empty;
            saveData.wagonName = runtimeData.wagonName ?? string.Empty;
            saveData.overLoad = runtimeData.overLoad;
            saveData.maxLoad = runtimeData.maxLoad;
            saveData.minAnimals = runtimeData.minAnimals;
            saveData.maxAnimals = runtimeData.maxAnimals;
            saveData.speedModifier = runtimeData.speedModifier;
            saveData.maxDurability = runtimeData.maxDurability > 0 ? runtimeData.maxDurability : 100;
            saveData.inventorySlotCount = runtimeData.inventorySlotCount > 0 ? runtimeData.inventorySlotCount : 1;
        }

        private static void CopyAnimals(List<AnimalSaveData> source, List<imsiAnimalData> target)
        {
            target.Clear();
            if (source == null)
            {
                return;
            }

            foreach (var animal in source)
            {
                if (animal == null)
                {
                    continue;
                }

                target.Add(new imsiAnimalData
                {
                    instanceId = animal.instanceId,
                    animalName = animal.animalName,
                    speed = animal.speed,
                    foodPerKm = animal.foodPerKm,
                    increaseOverLoad = animal.increaseOverLoad,
                    increaseMaxLoad = animal.increaseMaxLoad,
                    animalType = animal.animalType
                });
            }
        }

        private static void CopyAnimals(List<imsiAnimalData> source, List<AnimalSaveData> target)
        {
            target.Clear();
            if (source == null)
            {
                return;
            }

            foreach (var animal in source)
            {
                if (animal == null)
                {
                    continue;
                }

                target.Add(new AnimalSaveData
                {
                    instanceId = animal.instanceId ?? string.Empty,
                    animalName = animal.animalName ?? string.Empty,
                    speed = animal.speed,
                    foodPerKm = animal.foodPerKm,
                    increaseOverLoad = animal.increaseOverLoad,
                    increaseMaxLoad = animal.increaseMaxLoad,
                    animalType = animal.animalType
                });
            }
        }

        private static void CopyMercenaries(List<MercenarySaveData> source, List<imsiMercenaryData> target)
        {
            target.Clear();
            if (source == null)
            {
                return;
            }

            foreach (var mercenary in source)
            {
                if (mercenary == null)
                {
                    continue;
                }

                target.Add(new imsiMercenaryData
                {
                    instanceId = mercenary.instanceId,
                    mercName = mercenary.mercName,
                    combatPower = mercenary.combatPower,
                    contractCount = mercenary.contractCount
                });
            }
        }

        private static void CopyMercenaries(List<imsiMercenaryData> source, List<MercenarySaveData> target)
        {
            target.Clear();
            if (source == null)
            {
                return;
            }

            foreach (var mercenary in source)
            {
                if (mercenary == null)
                {
                    continue;
                }

                target.Add(new MercenarySaveData
                {
                    instanceId = mercenary.instanceId ?? string.Empty,
                    mercName = mercenary.mercName ?? string.Empty,
                    combatPower = mercenary.combatPower,
                    contractCount = mercenary.contractCount
                });
            }
        }

        private static void CopyCargo(List<CargoEntrySaveData> source, List<CargoEntry> target)
        {
            target.Clear();
            if (source == null)
            {
                return;
            }

            foreach (var cargo in source)
            {
                if (cargo == null || cargo.item == null)
                {
                    continue;
                }

                target.Add(new CargoEntry
                {
                    item = new imsiTradeItemData
                    {
                        id = cargo.item.itemId,
                        itemName = cargo.item.itemName,
                        weight = cargo.item.weight,
                        basePrice = cargo.item.basePrice,
                        maxCount = cargo.item.maxCount > 0 ? cargo.item.maxCount : 1
                    },
                    quantity = cargo.quantity
                });
            }
        }

        private static void CopyCargo(List<CargoEntry> source, List<CargoEntrySaveData> target)
        {
            target.Clear();
            if (source == null)
            {
                return;
            }

            foreach (var cargo in source)
            {
                if (cargo == null || cargo.item == null)
                {
                    continue;
                }

                target.Add(new CargoEntrySaveData
                {
                    item = new TradeItemSaveData
                    {
                        itemId = cargo.item.id ?? string.Empty,
                        itemName = cargo.item.itemName ?? string.Empty,
                        weight = cargo.item.weight,
                        basePrice = cargo.item.basePrice,
                        maxCount = cargo.item.maxCount > 0 ? cargo.item.maxCount : 1
                    },
                    quantity = cargo.quantity
                });
            }
        }
    }
}
