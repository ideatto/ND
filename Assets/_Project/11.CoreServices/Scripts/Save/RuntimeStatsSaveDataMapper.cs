/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Economy CoreRuntimeStatModifier와 SaveData/player·caravan runtime 상태를 동기화한다.
 *
 * Main Features
 * - Economy modifier를 RuntimeStatsSaveData DTO로 복사한다.
 * - claim 시 caravan.lossLimitRate 등 Core가 참조하는 runtime 값을 갱신한다.
 * - growth level을 PlayerSaveData에 반영한다.
 */
using ND.Economy;

namespace ND.Framework
{
    /// <summary>
    /// Economy runtime stat과 Framework 저장 데이터 사이의 변환을 담당한다.
    /// </summary>
    public static class RuntimeStatsSaveDataMapper
    {
        /// <summary>
        /// Economy runtime modifier를 저장 DTO로 복사한다.
        /// </summary>
        public static RuntimeStatsSaveData ToSaveData(CoreRuntimeStatModifier source)
        {
            if (source == null)
            {
                return new RuntimeStatsSaveData();
            }

            return new RuntimeStatsSaveData
            {
                maxLoadBonus = source.MaxLoadBonus,
                maxLoadMultiplier = source.MaxLoadMultiplier,
                speedMultiplier = source.SpeedMultiplier,
                foodEfficiencyMultiplier = source.FoodEfficiencyMultiplier,
                combatPowerBonus = source.CombatPowerBonus,
                combatPowerMultiplier = source.CombatPowerMultiplier,
                lossLimitRate = source.LossLimitRate,
                riskMultiplier = source.RiskMultiplier,
                minRecoveryTradeMoney = source.MinRecoveryTradeMoney
            };
        }

        /// <summary>
        /// Economy M1 결과의 화폐·growth·runtime stat을 SaveData와 runtime caravan에 반영한다.
        /// </summary>
        /// <returns>저장 데이터 반영에 성공하면 true.</returns>
        public static bool ApplyEconomyResult(
            SaveData saveData,
            CaravanData runtimeCaravan,
            EconomyM1LoopResult economyResult)
        {
            if (saveData == null || economyResult == null || !economyResult.Success)
            {
                return false;
            }

            if (economyResult.FinalCurrencyState != null && saveData.player != null)
            {
                saveData.player.tradingCurrency = economyResult.FinalCurrencyState.TradeMoney;
                saveData.player.developmentCurrency = economyResult.FinalCurrencyState.DevelopmentCurrency;
            }

            if (economyResult.GrowthPurchase != null && economyResult.GrowthPurchase.Success)
            {
                saveData.player.playerGrowthLevel = economyResult.GrowthPurchase.NewLevel;
            }

            if (economyResult.RuntimeStats != null)
            {
                if (runtimeCaravan != null)
                {
                    runtimeCaravan.lossLimitRate = economyResult.RuntimeStats.LossLimitRate;
                }
            }

            return true;
        }
    }
}
