// =============================================================================
// PlaceableBuilding — 배치 가능한 건물 마커 + 점유 칸 수
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 마을 배치 시스템에서 "이 오브젝트는 유저가 집어서 옮길 수 있는 건물"임을
//        표시하는 마커. 배치 컨트롤러가 레이캐스트로 건물을 집을 때, 맞은 콜라이더가
//        건물(이 컴포넌트 보유)인지 바닥인지 구분하는 데 쓴다.
//
// [그리드] 건물은 격자(1칸=1m)에 스냅되어 배치된다. cellsX·cellsZ는 이 건물이
//        차지하는 칸 수다. 대부분 1×1이고, 큰 건물(예: GrayBox)만 2×2 등으로 둔다.
//        피벗은 바닥-중심이므로, 짝수 칸이면 칸 경계에, 홀수 칸이면 칸 중심에 놓인다.
//
// [규칙] 건물 프리팹 루트에 BoxCollider와 함께 붙는다. 회전(Y축, 90° 단위)해도
//        제자리에서 돈다. 회전이 홀수(90°/270°)면 가로·세로 칸이 뒤바뀐다.
// =============================================================================

using UnityEngine;

/// <summary>유저가 집어서 옮길 수 있는 건물 마커 + 격자 점유 칸 수.</summary>
public class PlaceableBuilding : MonoBehaviour
{
    [Tooltip("이 건물이 차지하는 가로 칸 수 (1칸=1m). 대부분 1.")]
    [SerializeField, Min(1)] private int cellsX = 1;

    [Tooltip("이 건물이 차지하는 세로 칸 수 (1칸=1m). 대부분 1.")]
    [SerializeField, Min(1)] private int cellsZ = 1;

    /// <summary>가로 점유 칸 수 (회전 미반영 원본값).</summary>
    public int CellsX => Mathf.Max(1, cellsX);

    /// <summary>세로 점유 칸 수 (회전 미반영 원본값).</summary>
    public int CellsZ => Mathf.Max(1, cellsZ);

    /// <summary>
    /// 현재 회전(Y축)을 반영한 실제 점유 칸 수를 반환한다.
    /// 90°/270°로 돌면 가로·세로가 뒤바뀐다. 45° 등 비격자 각도는 가장 가까운 90°로 반올림해 판단.
    /// </summary>
    public void GetRotatedCells(out int x, out int z)
    {
        int quarter = Mathf.RoundToInt(transform.eulerAngles.y / 90f) & 1; // 0=가로그대로, 1=90도 뒤바뀜
        if (quarter == 0) { x = CellsX; z = CellsZ; }
        else { x = CellsZ; z = CellsX; }
    }
}
