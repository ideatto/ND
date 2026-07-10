/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - 인게임 시간 배율 기본값과 표시·식량 해석 단위를 Inspector에서 설정한다.
 *
 * Usage for Team Members
 * - Resources/InGameTimePolicyConfig asset을 수정해 기본 배율과 테스트 단위를 조정한다.
 *
 * Important Notes
 * - Release 빌드에서는 defaultInGameTimeMultiplier만 runtime 초기값으로 사용된다.
 * - 런타임 배율 변경은 debug API에서만 허용된다.
 */
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// 인게임 시간 배율과 단위 해석 정책을 정의하는 ScriptableObject이다.
    /// </summary>
    [CreateAssetMenu(fileName = ResourceName, menuName = "ND/Framework/In-Game Time Policy Config")]
    public sealed class InGameTimePolicyConfig : ScriptableObject
    {
        public const string ResourceName = "InGameTimePolicyConfig";

        [Tooltip("현실 1초당 인게임 N초를 나타내는 기본 배율입니다. Release 빌드 초기값으로 사용됩니다.")]
        [SerializeField]
        private float defaultInGameTimeMultiplier = 1f;

        [Tooltip("경과 인게임 시간을 UI에 표시할 때 사용할 단위입니다.")]
        [SerializeField]
        private InGameTimeUnit elapsedTimeDisplayUnit = InGameTimeUnit.Hour;

        [Tooltip("견인 동물 foodPerKm(raw rate)를 해석할 때 사용할 인게임 시간 단위입니다.")]
        [SerializeField]
        private InGameTimeUnit foodConsumptionUnit = InGameTimeUnit.Hour;

        /// <summary>
        /// Release·Editor 공통 기본 인게임 시간 배율이다.
        /// </summary>
        public float DefaultInGameTimeMultiplier => Mathf.Max(0f, defaultInGameTimeMultiplier);

        /// <summary>
        /// 경과 인게임 시간 표시 단위이다.
        /// </summary>
        public InGameTimeUnit ElapsedTimeDisplayUnit => elapsedTimeDisplayUnit;

        /// <summary>
        /// 식량 소모율 해석 단위이다.
        /// </summary>
        public InGameTimeUnit FoodConsumptionUnit => foodConsumptionUnit;
    }
}
