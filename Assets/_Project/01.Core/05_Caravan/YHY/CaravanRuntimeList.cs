// =============================================================================
// CaravanRuntimeList — 저장된 캐러밴 리스트 → Core 런타임 리스트 변환 헬퍼
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] SaveData.caravans(저장 DTO 리스트)를 Core 런타임 CaravanData 리스트로
//        한 번에 변환한다. 자산 잠금(CaravanAssetLock)·Overview Provider가 "전체 상단"을
//        봐야 하므로, 매퍼의 단건 변환(ToRuntime)을 여러 번 부르는 얇은 래퍼다.
//
// [caravanId] 매퍼(CaravanSaveDataMapper.ToRuntime)가 이제 caravanId·자산 instanceId를
//        런타임으로 직접 매핑한다(2026-07-22 SaveData v6 asset-instance-id-persistence).
//        따라서 여기서 별도 보정은 필요 없다 — 아래 채움은 매퍼가 비워둘 때를 대비한
//        안전장치일 뿐이며, 정상 흐름에선 항상 건너뛴다.
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
    /// saveData.caravans 전체를 런타임 CaravanData 리스트로 변환한다.
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

            // 안전장치: 매퍼가 caravanId를 이미 채운다(v6). 혹시 비어 있으면 저장 DTO에서 채운다.
            if (string.IsNullOrEmpty(runtime.caravanId) && !string.IsNullOrEmpty(saved.caravanId))
                runtime.caravanId = saved.caravanId;

            result.Add(runtime);
        }
        return result;
    }
}
