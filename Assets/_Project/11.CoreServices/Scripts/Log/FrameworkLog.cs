/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Framework 로그에 공통 prefix를 붙여 Unity Console에서 CoreServices 로그를 구분한다.
 *
 * Main Features
 * - Info, Warning, Error 로그 wrapper를 제공한다.
 *
 * Usage for Team Members
 * - CoreServices runtime/debug 코드에서 UnityEngine.Debug를 직접 호출하기보다 FrameworkLog를 사용한다.
 *
 * Main Public APIs
 * - Info(...): 일반 로그를 출력한다.
 * - Warning(...): 경고 로그를 출력한다.
 * - Error(...): 오류 로그를 출력한다.
 *
 * Important Notes
 * - 이 wrapper는 로그 필터링이나 build별 비활성화를 수행하지 않는다.
 */
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// CoreServices 로그에 공통 prefix를 붙이는 정적 logging helper이다.
    /// </summary>
    public static class FrameworkLog
    {
        private const string Prefix = "[Framework]";

        /// <summary>
        /// 일반 정보를 Unity Console에 출력한다.
        /// </summary>
        /// <param name="message">출력할 메시지.</param>
        public static void Info(string message)
        {
            Debug.Log($"{Prefix} {message}");
        }

        /// <summary>
        /// 경고 정보를 Unity Console에 출력한다.
        /// </summary>
        /// <param name="message">출력할 메시지.</param>
        public static void Warning(string message)
        {
            Debug.LogWarning($"{Prefix} {message}");
        }

        /// <summary>
        /// 오류 정보를 Unity Console에 출력한다.
        /// </summary>
        /// <param name="message">출력할 메시지.</param>
        public static void Error(string message)
        {
            Debug.LogError($"{Prefix} {message}");
        }
    }
}
