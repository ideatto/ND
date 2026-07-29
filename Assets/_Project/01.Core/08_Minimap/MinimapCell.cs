// =============================================================================
// MinimapCell / TerrainType — 미니맵 격자 한 칸의 정보
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 프로토타입
//
// [역할] 미니맵 격자(MinimapGrid)의 셀 하나가 담는 데이터.
//        좌표(row,col + 월드 중심) + 땅 정보(TerrainType) + 확장 여지.
//        날씨/이벤트/이동 등은 이 셀 정보를 기준으로 얹는다.
//
// [직렬화] [Serializable]이라 flat List<MinimapCell>로 저장/인스펙터 노출 가능.
//          런타임 빠른 접근은 MinimapGrid의 2D 배열이 담당.
// =============================================================================

using System;
using UnityEngine;

/// <summary>미니맵 격자 셀의 땅 종류.</summary>
/// <remarks>기존 직렬화 값(정수 인덱스)이 깨지지 않도록 새 항목은 항상 끝에 추가한다.</remarks>
public enum TerrainType
{
    Plain,     // 평지(기본)
    Grass,     // 풀밭/들판
    Forest,    // 나무 우거진 들판(숲)
    Farmland,  // 논밭
    River,     // 강(물길) — 이동 불가
    Bridge,    // 다리 — 이동 가능(강 위를 건넘)
    Mountain,  // 산/바위 — 이동 불가
    Water,     // 호수/넓은 물 — 이동 불가
    Cloud,     // 구름(맵 밖 여백) — 이동 불가
    Riverbank  // 강변 — 이동 가능(강에 인접한 땅)
}

/// <summary>미니맵 격자 한 칸의 정보(좌표 + 땅정보 + 확장).</summary>
[Serializable]
public class MinimapCell
{
    public int row;                 // 세로 인덱스(아래=0)
    public int col;                 // 가로 인덱스(왼쪽=0)
    public Vector3 worldCenter;     // 셀 중심의 월드 좌표
    public TerrainType terrain = TerrainType.Plain;  // 땅 정보

    // --- 확장 슬롯(프로토타입: 날씨/이벤트 등은 나중에 여기 추가) ---
    // public string weatherId;
    // public string eventId;

    /// <summary>이 셀이 이동 가능한가(terrain 기준).</summary>
    public bool Passable => IsPassable(terrain);

    /// <summary>
    /// 지형별 이동 가능 여부. 강·호수·산·구름은 이동 불가, 그 외(평지·풀·숲·논밭·다리·강변)는 가능.
    /// </summary>
    public static bool IsPassable(TerrainType t)
    {
        switch (t)
        {
            case TerrainType.River:
            case TerrainType.Water:
            case TerrainType.Mountain:
            case TerrainType.Cloud:
                return false;
            default:
                return true;
        }
    }

    public MinimapCell() { }

    public MinimapCell(int row, int col, Vector3 worldCenter, TerrainType terrain = TerrainType.Plain)
    {
        this.row = row;
        this.col = col;
        this.worldCenter = worldCenter;
        this.terrain = terrain;
    }

    public override string ToString() => $"Cell({row},{col}) {terrain} @ {worldCenter}";

    // ------------------------------------------------------------------ 글자 ↔ 지형 (텍스트맵용)

    /// <summary>지형 → 한 글자 코드. (P평지 R강 B강변 D다리 M산 W호수 F숲 L논밭 C구름 G풀)</summary>
    public static char ToChar(TerrainType t)
    {
        switch (t)
        {
            case TerrainType.River:     return 'R';
            case TerrainType.Riverbank: return 'B';
            case TerrainType.Bridge:    return 'D';
            case TerrainType.Mountain:  return 'M';
            case TerrainType.Water:     return 'W';
            case TerrainType.Forest:    return 'F';
            case TerrainType.Farmland:  return 'L';
            case TerrainType.Cloud:     return 'C';
            case TerrainType.Grass:     return 'G';
            default:                    return 'P'; // Plain
        }
    }

    /// <summary>한 글자 코드 → 지형(알 수 없으면 Plain).</summary>
    public static TerrainType FromChar(char ch)
    {
        switch (char.ToUpperInvariant(ch))
        {
            case 'R': return TerrainType.River;
            case 'B': return TerrainType.Riverbank;
            case 'D': return TerrainType.Bridge;
            case 'M': return TerrainType.Mountain;
            case 'W': return TerrainType.Water;
            case 'F': return TerrainType.Forest;
            case 'L': return TerrainType.Farmland;
            case 'C': return TerrainType.Cloud;
            case 'G': return TerrainType.Grass;
            default:  return TerrainType.Plain;
        }
    }
}
