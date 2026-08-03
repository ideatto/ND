using System;

namespace ND.Framework
{
    /// <summary>게임 달력에서 저장하는 최소 권위 데이터다.</summary>
    [Serializable]
    public sealed class GameCalendarSaveData
    {
        /// <summary>시작일로부터 지난 완전한 게임 일수다. 단위: game day.</summary>
        public long totalElapsedDays;

        /// <summary>현재 게임 일 시작점에 대응하는 실제 UTC <see cref="DateTime.Ticks"/>다.</summary>
        public long dayAnchorUtcTicks;
    }
}
