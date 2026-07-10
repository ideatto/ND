/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - CoreServices에서 사용하는 현재 게임 시간 조회 계약을 정의한다.
 * - 무역 시작/진행 계산이 구체적인 시간 제공 구현에 직접 의존하지 않도록 한다.
 *
 * Main Features
 * - UTC 기준 현재 시각을 제공한다.
 *
 * Usage for Team Members
 * - 시간 기반 진행률이나 저장 tick을 계산하는 서비스에 주입해서 사용한다.
 *
 * Main Public APIs
 * - CurrentUtc: 현재 UTC 시각을 반환한다.
 *
 * Important Notes
 * - 이 계약은 시간 배율이나 pause 정책을 정의하지 않는다.
 * - 반환값은 UTC 기준이어야 저장된 tick과 일관되게 비교할 수 있다.
 */
using System;

namespace ND.Framework
{
    /// <summary>
    /// 저장 데이터와 무역 진행 계산에 사용할 현재 UTC 시간을 제공하는 계약이다.
    /// </summary>
    public interface IGameTimeProvider
    {
        /// <summary>
        /// 현재 UTC 기준 시각을 반환한다.
        /// </summary>
        /// <remarks>
        /// 저장 데이터의 tick 값과 비교되므로 local time이 아닌 UTC 값을 반환해야 한다.
        /// </remarks>
        DateTime CurrentUtc { get; }
    }
}
