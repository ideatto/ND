// =============================================================================
// WeatherLuckyStore — 무역별 '낙뢰 행운' 발생 횟수를 우리 자체 파일에 저장/조회
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 날씨 시스템 — 낙뢰 행운
//
// [왜] "번개 맞으면 정산 +10%" 를 위해 한 무역에서 낙뢰 행운이 몇 번 떴는지 세서 정산에
//      넘겨야 한다. 이 카운트를 프레임워크 save 스키마(정헌님 파일)에 얹지 않고 우리
//      자체 파일(persistentDataPath)에 따로 저장해 완전히 분리한다.
//
// [무엇] tradeId → 누적 횟수 를 json 파일에 보관. 낙뢰 행운 판정(MinimapWeatherEventDetector)이
//        Add로 올리고, 정산 쪽이 GetCount로 읽어 +10%×count 적용 후 Consume로 정리한다.
//
// [주의] 프레임워크 저장 스키마를 안 건드리는 대신, 정산 반영은 경제 쪽에서 GetCount를 읽는
//        '한 줄'이 필요하다(돈 계산이 거기라서). 이 저장소 자체는 어떤 담당 파일도 안 건드림.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>무역별 낙뢰 행운 횟수를 우리 자체 json 파일에 저장/조회하는 정적 저장소.</summary>
public static class WeatherLuckyStore
{
    // json 직렬화용(JsonUtility는 Dictionary를 못 하므로 리스트로 보관)
    [System.Serializable] private class Entry { public string tradeId; public int count; }
    [System.Serializable] private class Data { public List<Entry> entries = new List<Entry>(); }

    private static Data data;   // 최초 접근 시 파일에서 로드(없으면 빈 것)

    // 우리 전용 저장 파일 경로(프레임워크 save와 별개)
    private static string FilePath => Path.Combine(Application.persistentDataPath, "weather_lucky.json");

    private static void EnsureLoaded()
    {
        if (data != null) return;
        try { data = File.Exists(FilePath) ? (JsonUtility.FromJson<Data>(File.ReadAllText(FilePath)) ?? new Data()) : new Data(); }
        catch { data = new Data(); }   // 손상돼도 빈 것으로 안전 복구
    }

    private static void Save()
    {
        try { File.WriteAllText(FilePath, JsonUtility.ToJson(data)); }
        catch (System.Exception e) { Debug.LogWarning("[WeatherLucky] 저장 실패: " + e.Message); }
    }

    private static Entry Find(string tradeId)
    {
        for (int i = 0; i < data.entries.Count; i++)
            if (data.entries[i] != null && data.entries[i].tradeId == tradeId) return data.entries[i];
        return null;
    }

    /// <summary>이 무역의 낙뢰 행운 +1(저장). 누적 횟수 반환.</summary>
    public static int Add(string tradeId)
    {
        if (string.IsNullOrEmpty(tradeId)) return 0;
        EnsureLoaded();
        var e = Find(tradeId);
        if (e == null) { e = new Entry { tradeId = tradeId, count = 0 }; data.entries.Add(e); }
        e.count++;
        Save();
        return e.count;
    }

    /// <summary>이 무역의 낙뢰 행운 누적 횟수(없으면 0). ★정산이 이걸 읽어 +10%×count 적용.</summary>
    public static int GetCount(string tradeId)
    {
        if (string.IsNullOrEmpty(tradeId)) return 0;
        EnsureLoaded();
        var e = Find(tradeId);
        return e != null ? e.count : 0;
    }

    /// <summary>정산 반영 후 이 무역 기록 삭제(오래된 항목 안 쌓이게).</summary>
    public static void Consume(string tradeId)
    {
        if (string.IsNullOrEmpty(tradeId)) return;
        EnsureLoaded();
        int removed = data.entries.RemoveAll(x => x == null || x.tradeId == tradeId);
        if (removed > 0) Save();
    }
}
