/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - InGameScreenStateRouter가 발행하는 화면 상태 변경 이벤트를 Unity Console에 출력한다.
 * - 인게임 화면 전환 debug 로그를 간단히 확인할 수 있게 한다.
 *
 * Main Features
 * - OnEnable에서 InGameScreenChanged 이벤트를 구독한다.
 * - 화면 상태가 바뀔 때 현재 상태를 Debug.Log로 출력한다.
 *
 * Usage for Team Members
 * - debug가 필요한 scene의 GameObject에 component로 추가한다.
 * - 화면 전환 검증이 끝나면 scene에서 제거하거나 비활성화한다.
 *
 * Main Public APIs
 * - 없음. Unity lifecycle에서만 동작한다.
 *
 * Important Notes
 * - 이 스크립트는 ND.Framework namespace 밖에 있으며 debug logging 전용이다.
 * - 비활성화 시 이벤트 구독을 해제한다.
 */
using ND.Framework;
using UnityEngine;

/// <summary>
/// 인게임 화면 상태 변경 이벤트를 로그로 출력하는 debug MonoBehaviour이다.
/// </summary>
public class InGameSceneRouterDebug : MonoBehaviour
{

    private void OnEnable()
    {
        // 활성화된 동안만 화면 상태 변경 로그를 받도록 이벤트를 구독한다.
        FrameworkEvents.InGameScreenChanged += OnInGameScreenChanged;
    }

    private void OnDisable()
    {
        // 비활성 debug logger가 이벤트를 계속 받지 않도록 구독을 해제한다.
        FrameworkEvents.InGameScreenChanged -= OnInGameScreenChanged;
    }

    private void OnInGameScreenChanged(InGameScreenState state)
    {
        // router 상태 전환을 scene log에서 바로 확인할 수 있도록 상태값을 출력한다.
        Debug.Log($"[InGame Route Debug] Current Screen State : {state}");
    }
}
