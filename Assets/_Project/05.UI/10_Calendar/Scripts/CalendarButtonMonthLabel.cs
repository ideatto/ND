// =============================================================================
// CalendarButtonMonthLabel — 달력 버튼(CalenderButton)에 "현재 월"을 표시
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 달력을 여는 버튼의 라벨(TMP_Text)에 현재 게임 월을 항상 최신으로 표시한다.
//        달력 패널을 열지 않아도 버튼만 보고 지금이 몇 월인지 알 수 있게 한다.
//
// [구현] 이벤트 구독은 활성/타이밍에 따라 초기 표시를 놓칠 수 있어, 여기서는
//        매 프레임 FrameworkRoot의 GameCalendarService에서 현재 스냅샷을 폴링한다.
//        값이 바뀔 때만 텍스트를 갱신하므로 비용은 무시할 수준(정수 비교 1회).
//        월 계산 자체는 Framework(GameCalendarDate)가 담당, 여기선 표시만 한다.
//
// [형식] showYear=false → "3월"  /  showYear=true → "1년 3월"
// =============================================================================

using ND.Framework;
using TMPro;
using UnityEngine;

namespace ND.UI.Calendar
{
    /// <summary>달력 버튼 라벨에 현재 월(옵션: 연도)을 폴링 방식으로 표시한다.</summary>
    public sealed class CalendarButtonMonthLabel : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;        // 버튼 안의 텍스트(예: CalenderButton/Text (TMP))
        [SerializeField] private bool showYear = false; // true면 "1년 3월", false면 "3월"
        [SerializeField] private bool logOnce = true;   // 최초 1회 상태 로그(문제 진단용, 확인 후 꺼도 됨)

        private int lastYear = int.MinValue;   // 마지막으로 표시한 연/월(불필요한 갱신 방지)
        private int lastMonth = int.MinValue;
        private bool loggedNoCalendar;         // "달력 없음" 로그 1회 제한
        private bool loggedShown;              // "표시됨" 로그 1회 제한

        private void OnEnable()
        {
            // 켜질 때 즉시 한 번 갱신(가능하면). 이후는 Update가 계속 감시.
            lastYear = int.MinValue;
            lastMonth = int.MinValue;
            Poll();
        }

        private void Update()
        {
            Poll();
        }

        /// <summary>현재 달력 값을 읽어 변경 시에만 라벨을 갱신한다.</summary>
        private void Poll()
        {
            if (label == null)
            {
                return;
            }

            GameCalendarService calendar = FrameworkRoot.Instance?.GameCalendar;
            if (calendar == null || !calendar.TryGetCurrent(out GameCalendarSnapshot snapshot))
            {
                // 아직 달력이 초기화되지 않음(부트 시퀀스 진행 중). 값이 생기면 다음 프레임에 채움.
                if (logOnce && !loggedNoCalendar)
                {
                    loggedNoCalendar = true;
                    Debug.Log("[CalendarButtonMonthLabel] 달력 현재값 없음(HasCurrent=false) — 초기화 대기 중.", this);
                }
                return;
            }

            if (snapshot.Year == lastYear && snapshot.Month == lastMonth)
            {
                return; // 변화 없음 → 갱신 생략
            }

            lastYear = snapshot.Year;
            lastMonth = snapshot.Month;

            label.text = showYear
                ? $"{snapshot.Year}년 {snapshot.Month}월"
                : $"{snapshot.Month}월";

            if (logOnce && !loggedShown)
            {
                loggedShown = true;
                Debug.Log($"[CalendarButtonMonthLabel] 표시됨 → '{label.text}' (label={label.name})", this);
            }
        }
    }
}
