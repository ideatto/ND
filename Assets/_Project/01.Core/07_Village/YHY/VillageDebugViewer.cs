// =============================================================================
// VillageDebugViewer — 인게임에서 각 무역 마을을 순회 확인하는 디버그 뷰어
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] Play 중 화면 좌상단 버튼(또는 숫자키/←→)으로 마을을 골라
//        기존 카메라 시스템(TradeTownCameraMover)에게 "이 마을 보여줘"를 요청한다.
//        → 카메라를 직접 건드리지 않고, RequestedTownId만 세팅해 기존 이동을 재사용.
//
// [경계] 순수 디버그용. InGame이 additive로 Village_Home을 로드하므로,
//        마을(WorldTowns)이 로드되면 자동으로 목록을 만든다(주기적 재탐색).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;   // 프로젝트가 새 Input System 사용
#endif

/// <summary>Play 중 각 무역 마을을 기존 카메라 시스템으로 순회 확인하는 디버그 뷰어.</summary>
public class VillageDebugViewer : MonoBehaviour
{
    private const string Prefix = "TradeTown_";

    private readonly List<string> townIds = new List<string>();   // "ForestTown", "RiverTown" ...
    private int index = -1;
    private bool active;
    private float rescanTimer;

    private void Update()
    {
        if (!active)
        {
            // 마을이 아직 없으면(=Village_Home 미로드) 주기적으로 재탐색
            rescanTimer -= Time.deltaTime;
            if (rescanTimer <= 0f) { rescanTimer = 0.5f; TryActivate(); }
            return;
        }

#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.rightArrowKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) Select(index + 1);
            if (kb.leftArrowKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame) Select(index - 1);
            for (int n = 0; n < townIds.Count && n < 9; n++)
                if (kb[Key.Digit1 + n].wasPressedThisFrame) Select(n);
        }
#endif
    }

    /// <summary>WorldTowns 밑 TradeTown_* 를 찾아 townId 목록을 만든다(로드되면 활성화).</summary>
    private void TryActivate()
    {
        GameObject wt = GameObject.Find("WorldTowns");
        if (wt == null) return;
        townIds.Clear();
        foreach (Transform t in wt.transform)
            if (t.name.StartsWith(Prefix)) townIds.Add(t.name.Substring(Prefix.Length));
        if (townIds.Count == 0) return;
        active = true;
        Select(0);
    }

    /// <summary>i번 마을을 기존 카메라 시스템에 요청(카메라는 직접 안 건드림).</summary>
    private void Select(int i)
    {
        if (townIds.Count == 0) return;
        index = (i % townIds.Count + townIds.Count) % townIds.Count;   // 순환
        TradeTownCameraMover.RequestedTownId = townIds[index];         // ← 기존 시스템이 카메라 이동
    }

    private void OnGUI()
    {
        if (!active)
        {
            GUI.Label(new Rect(10, 10, 340, 24), "마을 디버그 뷰어: 마을(WorldTowns) 로드 대기 중…");
            return;
        }
        GUILayout.BeginArea(new Rect(10, 10, 200, 44 + townIds.Count * 28));
        GUILayout.Label("── 마을 디버그 뷰어 ──");
        for (int i = 0; i < townIds.Count; i++)
        {
            string label = (i == index ? "▶ " : "   ") + (i + 1) + ". " + townIds[i];
            if (GUILayout.Button(label)) Select(i);
        }
        GUILayout.EndArea();
    }
}
