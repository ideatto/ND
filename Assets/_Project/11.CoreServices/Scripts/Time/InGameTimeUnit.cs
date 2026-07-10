/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - 인게임 시간 표시와 식량 소모율 해석에 사용할 시간 단위를 정의한다.
 *
 * Important Notes
 * - 내부 계산은 항상 인게임 초(in-game seconds)로 정규화한다.
 * - 이 enum은 표시·밸런스 테스트 해석용 단위 선택에 사용된다.
 */
namespace ND.Framework
{
    /// <summary>
    /// 인게임 시간을 표시하거나 소모율을 해석할 때 사용하는 시간 단위이다.
    /// </summary>
    public enum InGameTimeUnit
    {
        Second = 0,
        Minute = 1,
        Hour = 2,
        Day = 3
    }

    /// <summary>
    /// InGameTimeUnit을 인게임 초로 변환하는 helper이다.
    /// </summary>
    public static class InGameTimeUnitExtensions
    {
        /// <summary>
        /// 선택한 단위 1개가 몇 인게임 초에 해당하는지 반환한다.
        /// </summary>
        public static double SecondsPerUnit(InGameTimeUnit unit)
        {
            switch (unit)
            {
                case InGameTimeUnit.Minute:
                    return 60d;
                case InGameTimeUnit.Hour:
                    return 3600d;
                case InGameTimeUnit.Day:
                    return 86400d;
                default:
                    return 1d;
            }
        }

        /// <summary>
        /// 인게임 초를 선택한 단위 값으로 변환한다.
        /// </summary>
        public static double ToUnitValue(double inGameSeconds, InGameTimeUnit unit)
        {
            var secondsPerUnit = SecondsPerUnit(unit);
            return secondsPerUnit <= 0d ? 0d : inGameSeconds / secondsPerUnit;
        }

        /// <summary>
        /// 단위 enum을 UI 표시용 짧은 라벨로 변환한다.
        /// </summary>
        public static string ToDisplayLabel(InGameTimeUnit unit)
        {
            switch (unit)
            {
                case InGameTimeUnit.Minute:
                    return "min";
                case InGameTimeUnit.Hour:
                    return "hr";
                case InGameTimeUnit.Day:
                    return "day";
                default:
                    return "sec";
            }
        }
    }
}
