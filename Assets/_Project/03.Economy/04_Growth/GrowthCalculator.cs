namespace ND.Economy
{
    public static class GrowthCalculator
    {
        public static CoreRuntimeStatModifier CalculateM1RuntimeStats(int playerGrowthLevel, int caravanGrowthLevel)
        {
            CoreRuntimeStatModifier modifier = CoreRuntimeStatModifier.Default();

            if (playerGrowthLevel > 0)
            {
                modifier.MaxLoadBonus += 10;
            }

            if (caravanGrowthLevel > 0)
            {
                modifier.MaxLoadBonus += 20;
                modifier.SpeedMultiplier *= 1.05f;
            }

            modifier.Clamp();
            return modifier;
        }
    }
}
