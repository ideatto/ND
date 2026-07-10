/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Core runtime caravan data와 SaveData의 직렬화 가능한 caravan DTO를 상호 변환한다.
 * - 저장 데이터와 Core 계산 모델 사이의 필드 복사 규칙을 한 곳에 모은다.
 *
 * Main Features
 * - CaravanSaveData를 runtime CaravanData로 복원한다.
 * - runtime CaravanData의 현재 상태를 CaravanSaveData에 복사한다.
 * - 저장 DTO의 null list와 기본값을 정규화한다.
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
 * - list 복사는 target을 Clear한 뒤 다시 채우므로 기존 list 항목 참조는 유지되지 않는다.
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
        /// 저장 DTO를 Core runtime caravan 데이터로 변환한다.
        /// </summary>
        /// <param name="saveData">변환할 저장 caravan 데이터. null이면 Normalize 내부에서 처리되지 않아 호출자가 null을 피해야 한다.</param>
        /// <returns>저장 데이터 값이 복사된 runtime CaravanData.</returns>
        /// <remarks>
        /// 변환 전 saveData의 하위 collection과 기본값을 정규화한다.
        /// </remarks>
        public static CaravanData ToRuntime(CaravanSaveData saveData)
        {
            // 저장 데이터에서 누락된 list나 기본값을 먼저 보정해 runtime 객체 생성 중 null 접근을 막는다.
            Normalize(saveData);

            var caravan = new CaravanData
            {
                wagon = ToRuntime(saveData.wagon),
                foodAmount = saveData.foodAmount,
                foodUnitWeight = saveData.foodUnitWeight,
                state = saveData.state,
                currentDistanceKm = saveData.currentDistanceKm,
                totalSeconds = saveData.totalSeconds,
                progress01 = saveData.progress01,
                settlementClaimed = saveData.settlementClaimed,
                runCargoLost = saveData.runCargoLost,
                runFoodLost = saveData.runFoodLost,
                runFatalReason = saveData.runFatalReason
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
        /// null 입력이 있으면 저장 DTO를 변경하지 않는다. 성공 시 list 항목은 runtime 값으로 재구성된다.
        /// </remarks>
        public static void CopyToSave(CaravanData runtimeData, CaravanSaveData saveData)
        {
            // 어느 한쪽이 없으면 부분 복사로 저장 데이터가 불완전해질 수 있으므로 조용히 중단한다.
            if (runtimeData == null || saveData == null)
            {
                return;
            }

            // target collection을 안전하게 비우고 다시 채울 수 있도록 저장 DTO를 먼저 정규화한다.
            Normalize(saveData);

            CopyWagon(runtimeData.wagon, saveData.wagon);
            CopyAnimals(runtimeData.animals, saveData.animals);
            CopyMercenaries(runtimeData.mercenaries, saveData.mercenaries);
            CopyCargo(runtimeData.cargo, saveData.cargo);

            saveData.foodAmount = runtimeData.foodAmount;
            saveData.foodUnitWeight = runtimeData.foodUnitWeight;
            saveData.state = runtimeData.state;
            saveData.currentDistanceKm = runtimeData.currentDistanceKm;
            saveData.totalSeconds = runtimeData.totalSeconds;
            saveData.progress01 = runtimeData.progress01;
            saveData.settlementClaimed = runtimeData.settlementClaimed;
            saveData.runCargoLost = runtimeData.runCargoLost;
            saveData.runFoodLost = runtimeData.runFoodLost;
            saveData.runFatalReason = runtimeData.runFatalReason;
        }

        /// <summary>
        /// CaravanSaveData의 필수 하위 객체와 collection을 사용할 수 있는 상태로 보정한다.
        /// </summary>
        /// <param name="saveData">정규화할 저장 caravan 데이터. null이면 아무 작업도 하지 않는다.</param>
        public static void Normalize(CaravanSaveData saveData)
        {
            // 저장 데이터 자체가 없으면 호출자가 새 CaravanSaveData를 만들 책임을 가진다.
            if (saveData == null)
            {
                return;
            }

            // JsonUtility 로드나 구버전 저장 데이터에서 누락될 수 있는 하위 객체를 기본값으로 보정한다.
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
        }

        private static imsiWagonData ToRuntime(WagonSaveData saveData)
        {
            // wagon 이름이 없으면 선택된 wagon이 없는 상태로 복원해 Core 검증이 처리하게 한다.
            if (saveData == null || string.IsNullOrEmpty(saveData.wagonName))
            {
                return null;
            }

            return new imsiWagonData
            {
                wagonName = saveData.wagonName,
                overLoad = saveData.overLoad,
                maxLoad = saveData.maxLoad,
                minAnimals = saveData.minAnimals,
                maxAnimals = saveData.maxAnimals,
                speedModifier = saveData.speedModifier
            };
        }

        private static void CopyWagon(imsiWagonData runtimeData, WagonSaveData saveData)
        {
            // runtime wagon이 없으면 이전 wagon 값이 저장 데이터에 남지 않도록 빈 값으로 초기화한다.
            if (runtimeData == null)
            {
                saveData.wagonName = string.Empty;
                saveData.overLoad = 0f;
                saveData.maxLoad = 0f;
                saveData.minAnimals = 0;
                saveData.maxAnimals = 0;
                saveData.speedModifier = 0f;
                return;
            }

            saveData.wagonName = runtimeData.wagonName ?? string.Empty;
            saveData.overLoad = runtimeData.overLoad;
            saveData.maxLoad = runtimeData.maxLoad;
            saveData.minAnimals = runtimeData.minAnimals;
            saveData.maxAnimals = runtimeData.maxAnimals;
            saveData.speedModifier = runtimeData.speedModifier;
        }

        private static void CopyAnimals(List<AnimalSaveData> source, List<imsiAnimalData> target)
        {
            // 저장 DTO를 runtime list로 재구성하기 위해 target을 먼저 비운다.
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
                    animalName = animal.animalName,
                    speed = animal.speed,
                    foodPerKm = animal.foodPerKm
                });
            }
        }

        private static void CopyAnimals(List<imsiAnimalData> source, List<AnimalSaveData> target)
        {
            // runtime list를 저장 DTO로 재구성하기 위해 target을 먼저 비운다.
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
                    animalName = animal.animalName ?? string.Empty,
                    speed = animal.speed,
                    foodPerKm = animal.foodPerKm
                });
            }
        }

        private static void CopyMercenaries(List<MercenarySaveData> source, List<imsiMercenaryData> target)
        {
            // null 항목은 저장 파일 손상 또는 임시 데이터로 보고 runtime 복원 대상에서 제외한다.
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
                    mercName = mercenary.mercName,
                    combatPower = mercenary.combatPower,
                    contractCount = mercenary.contractCount
                });
            }
        }

        private static void CopyMercenaries(List<imsiMercenaryData> source, List<MercenarySaveData> target)
        {
            // 저장 데이터에는 null 문자열을 남기지 않도록 빈 문자열로 보정해 복사한다.
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
                    mercName = mercenary.mercName ?? string.Empty,
                    combatPower = mercenary.combatPower,
                    contractCount = mercenary.contractCount
                });
            }
        }

        private static void CopyCargo(List<CargoEntrySaveData> source, List<CargoEntry> target)
        {
            // item이 없는 cargo는 Core 계산에 사용할 수 없으므로 runtime 복원에서 제외한다.
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
                        basePrice = cargo.item.basePrice
                    },
                    quantity = cargo.quantity
                });
            }
        }

        private static void CopyCargo(List<CargoEntry> source, List<CargoEntrySaveData> target)
        {
            // 저장 DTO는 item 정보를 값으로 복사해 runtime 객체 참조에 의존하지 않게 한다.
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
                        basePrice = cargo.item.basePrice
                    },
                    quantity = cargo.quantity
                });
            }
        }
    }
}
