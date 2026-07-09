using System.Collections.Generic;

namespace ND.Framework
{
    public static class CaravanSaveDataMapper
    {
        public static CaravanData ToRuntime(CaravanSaveData saveData)
        {
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
        }

        private static imsiWagonData ToRuntime(WagonSaveData saveData)
        {
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
