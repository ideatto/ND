// =============================================================================
// VillageGridOverlay — 편집 모드에서 바닥에 격자선을 그린다
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 건물 편집 모드(건물 선택 중)일 때, 마을 바닥(y=0)에 셀 크기 간격의
//        격자선을 LineRenderer로 그려 "어느 칸에 놓이는지" 보이게 한다.
//        배치 컨트롤러가 Show()/Hide()로 켜고 끈다.
//
// [구현] 격자선 하나당 LineRenderer 하나를 런타임 생성해 자식으로 붙인다.
//        셰이더·바닥 텍스처를 건드리지 않아 안전하고, 껐다 켜기 쉽다.
//
// [좌표] VillageGrid와 같은 규칙(원점=격자 중심, 1칸=CellSize). 선은 y를 살짝 띄워
//        (yLift) 바닥과 z-fighting(깜빡임)을 피한다.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>편집 모드에서 바닥에 격자선을 그리는 오버레이(컨트롤러가 Show/Hide).</summary>
public class VillageGridOverlay : MonoBehaviour
{
    [SerializeField] private float yLift = 0.02f;                        // 바닥 위로 살짝 띄움(깜빡임 방지)
    [SerializeField] private float lineWidth = 0.03f;                    // 선 두께(월드 m)
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.35f); // 격자선 색(반투명 흰색)

    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private Material lineMat;
    private bool built;

    /// <summary>width×height 칸 격자를 cellSize 간격으로 만든다(한 번만).</summary>
    public void Build(int width, int height, float cellSize)
    {
        if (built) return;
        built = true;

        // 반투명 선용 머티리얼(내장 Sprites/Default = 정점색 반영 + 알파).
        lineMat = new Material(Shader.Find("Sprites/Default"));

        // 격자 범위: 원점 중심. x는 [-w/2 .. w/2] 칸, z도 동일.
        float halfW = width * 0.5f * cellSize;
        float halfH = height * 0.5f * cellSize;

        // 세로선(z축 방향): x = -halfW .. +halfW, 간격 cellSize
        for (int i = 0; i <= width; i++)
        {
            float x = -halfW + i * cellSize;
            AddLine(new Vector3(x, yLift, -halfH), new Vector3(x, yLift, halfH));
        }
        // 가로선(x축 방향): z = -halfH .. +halfH
        for (int j = 0; j <= height; j++)
        {
            float z = -halfH + j * cellSize;
            AddLine(new Vector3(-halfW, yLift, z), new Vector3(halfW, yLift, z));
        }

        Hide();   // 기본은 숨김
    }

    private void AddLine(Vector3 a, Vector3 b)
    {
        var go = new GameObject("GridLine");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.material = lineMat;
        lr.widthMultiplier = lineWidth;
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.startColor = lr.endColor = lineColor;
        lr.numCapVertices = 0;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lines.Add(lr);
    }

    /// <summary>격자 보이기(편집 모드 진입).</summary>
    public void Show() { gameObject.SetActive(true); }

    /// <summary>격자 숨기기(편집 모드 종료).</summary>
    public void Hide() { gameObject.SetActive(false); }
}
