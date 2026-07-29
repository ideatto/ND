// =============================================================================
// WeatherNoticeEffect — 날씨 이벤트 데모 효과(화면 알림)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 날씨 이벤트 시스템 — 효과 예시
//
// [역할] IWeatherEffect 구현 예시. 날씨 이벤트가 발생하면 화면 좌상단에 잠깐
//        "☔ 캐러밴 비 맞음 + 효과예정(식량↓·지연)"을 띄운다.
//        실제 게임플레이 효과는 프레임워크 API 협의 후 별도 구현체로. 이건 연출/확인용.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>날씨 이벤트를 화면 알림으로 보여주는 데모 효과(IWeatherEffect).</summary>
public class WeatherNoticeEffect : MonoBehaviour, IWeatherEffect
{
    [SerializeField] private float noticeSeconds = 4f;   // 알림 표시 지속(초)
    [SerializeField] private int maxLines = 6;

    private struct Notice { public string text; public float until; }
    private readonly List<Notice> notices = new List<Notice>();
    private GUIStyle style;

    /// <summary>날씨 이벤트 1건 → 화면 알림 추가(효과 파라미터도 표시).</summary>
    public void Apply(WeatherEventOccurrence e)
    {
        string t = "☔ '" + e.eventId + "' 캐러밴 " + Short(e.caravanId)
                 + " 셀(" + e.cellRow + "," + e.cellCol + ") 세기 " + e.intensity.ToString("F2")
                 + "  [효과예정: 지연 +" + (e.delayRate * 100f).ToString("F0") + "%]";
        notices.Add(new Notice { text = t, until = Time.time + noticeSeconds });
        if (notices.Count > maxLines) notices.RemoveAt(0);
    }

    private static string Short(string id)
        => string.IsNullOrEmpty(id) ? "?" : (id.Length > 6 ? id.Substring(0, 6) : id);

    private void OnGUI()
    {
        for (int i = notices.Count - 1; i >= 0; i--)   // 만료 제거
            if (Time.time > notices[i].until) notices.RemoveAt(i);
        if (notices.Count == 0) return;

        if (style == null) { style = new GUIStyle(GUI.skin.label); style.normal.textColor = new Color(0.6f, 0.82f, 1f); }
        float s = Mathf.Max(1f, Screen.height / 1080f);
        style.fontSize = Mathf.RoundToInt(18f * s);
        float y = 200f * s;
        for (int i = 0; i < notices.Count; i++)
        {
            GUI.Label(new Rect(20f * s, y, 1000f * s, 30f * s), notices[i].text, style);
            y += 28f * s;
        }
    }
}
