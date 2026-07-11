/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Economy M1 정산 claim 이후 반영할 runtime stat modifier 스냅샷을 저장한다.
 * - growth level 변경은 PlayerSaveData에, caravan 적용값은 mapper가 runtime caravan에 반영한다.
 *
 * Main Features
 * - CoreRuntimeStatModifier 필드를 직렬화 가능한 DTO로 보관한다.
 *
 * Important Notes
 * - 이 DTO는 claim 시점에만 갱신되며 settle preview 단계에서는 사용하지 않는다.
 */
using System;

namespace ND.Framework
{
    /// <summary>
    /// Economy M1 결과로부터 파생된 runtime stat modifier 스냅샷이다.
    /// </summary>
    [Serializable]
    public sealed class RuntimeStatsSaveData
    {
        public int maxLoadBonus;
        public float maxLoadMultiplier = 1f;
        public float speedMultiplier = 1f;
        public float foodEfficiencyMultiplier = 1f;
        public int combatPowerBonus;
        public float combatPowerMultiplier = 1f;
        public float lossLimitRate = 0.5f;
        public float riskMultiplier = 1f;
        public long minRecoveryTradeMoney;
    }
}
