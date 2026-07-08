using System;

namespace ND.Economy
{
    [Serializable]
    public sealed class CoreRuntimeStatModifier
    {
        public int MaxLoadBonus;
        public float MaxLoadMultiplier = 1f;
        public float SpeedMultiplier = 1f;
        public float FoodEfficiencyMultiplier = 1f;
        public int CombatPowerBonus;
        public float CombatPowerMultiplier = 1f;
        public float LossLimitRate = 0.5f;
        public float RiskMultiplier = 1f;
        public int MinRecoveryTradeMoney;

        public static CoreRuntimeStatModifier Default()
        {
            return new CoreRuntimeStatModifier();
        }

        public void Clamp()
        {
            MaxLoadBonus = Math.Max(0, MaxLoadBonus);
            MaxLoadMultiplier = Math.Max(1f, MaxLoadMultiplier);
            SpeedMultiplier = Math.Max(0.1f, SpeedMultiplier);
            FoodEfficiencyMultiplier = Math.Max(0.1f, FoodEfficiencyMultiplier);
            CombatPowerBonus = Math.Max(0, CombatPowerBonus);
            CombatPowerMultiplier = Math.Max(0.1f, CombatPowerMultiplier);
            LossLimitRate = Math.Max(0f, Math.Min(1f, LossLimitRate));
            RiskMultiplier = Math.Max(0f, RiskMultiplier);
            MinRecoveryTradeMoney = Math.Max(0, MinRecoveryTradeMoney);
        }
    }
}
