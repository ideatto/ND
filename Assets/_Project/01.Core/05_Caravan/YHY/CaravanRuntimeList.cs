// =============================================================================
// CaravanRuntimeList — 저장된 캐러밴 리스트 → Core 런타임 리스트 변환 헬퍼
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] SaveData.caravans(저장 DTO 리스트)를 Core 런타임 CaravanData 리스트로
//        한 번에 변환한다. 자산 잠금(CaravanAssetLock)·Overview Provider가 "전체 상단"을
//        봐야 하므로, 매퍼의 단건 변환(ToRuntime)을 여러 번 부르는 얇은 래퍼다.
//
// [caravanId 보정] 매퍼(CaravanSaveDataMapper.ToRuntime)는 현재 caravanId를
//        런타임에 옮기지 않는다(Framework 소유라 Core가 매퍼를 못 고침).
//        그대로 두면 런타임 CaravanData.caravanId가 전부 빈 값이 되어
//        자산 잠금의 "자기 자신 제외"가 깨진다. → 변환 후 여기서 caravanId를 채운다.
//        ※ 근본 해결(매퍼가 직접 매핑)은 천성욱님 협의 대상. 이 헬퍼는 그 전까지의 보정.
//
// [경계] 저장 DTO·매퍼 정의는 Framework 소유. 여기선 "소비 + 얇은 조합"만 한다.
//        상태 변경·저장은 하지 않는다(읽어서 런타임 사본을 만들 뿐).
// =============================================================================

using System.Collections.Generic;

// [주의] SaveData·CaravanSaveData·CaravanSaveDataMapper는 ND.Framework 소속인데,
//        전역 네임스페이스에도 SaveData(이종현님)가 있어 이름이 겹친다.
//        그래서 using ND.Framework 대신 풀네임(ND.Framework.*)으로 명시한다.

/// <summary>SaveData의 캐러밴 리스트를 런타임 리스트로 변환한다.</summary>
public static class CaravanRuntimeList
{
    /// <summary>
    /// saveData.caravans 전체를 런타임 CaravanData 리스트로 변환한다(caravanId 보정 포함).
    /// saveData가 없거나 리스트가 비면 빈 리스트를 반환한다(절대 null 아님).
    /// </summary>
    public static List<CaravanData> Build(ND.Framework.SaveData saveData)
    {
        List<CaravanData> result = new List<CaravanData>();
        if (saveData == null || saveData.caravans == null) return result;

        foreach (ND.Framework.CaravanSaveData saved in saveData.caravans)
        {
            if (saved == null) continue;
            CaravanData runtime = ND.Framework.CaravanSaveDataMapper.ToRuntime(saved);
            if (runtime == null) continue;

            // 매퍼가 안 옮기는 caravanId를 저장 DTO에서 직접 채운다(자산 잠금 자기제외용).
            if (string.IsNullOrEmpty(runtime.caravanId) && !string.IsNullOrEmpty(saved.caravanId))
                runtime.caravanId = saved.caravanId;

            result.Add(runtime);
        }
        return result;
    }
}
