using System;
using UnityEngine;

namespace Intersection.Data
{
    /// <summary>
    /// 사망일(caseDate) 기준 상대 시각. 실제 달력 날짜는 표시 단계에서 caseDate로 계산한다.
    /// </summary>
    [Serializable]
    public struct RelativeTime : IComparable<RelativeTime>
    {
        [Tooltip("사망일 기준 상대 날짜. D-14 → -14, 사망 당일 → 0")]
        public int dayOffset;

        [Range(0, 23)]
        public int hour;

        [Range(0, 59)]
        public int minute;

        public RelativeTime(int dayOffset, int hour, int minute)
        {
            this.dayOffset = dayOffset;
            this.hour = hour;
            this.minute = minute;
        }

        public int TotalMinutes => dayOffset * 1440 + hour * 60 + minute;

        public bool IsValid => hour >= 0 && hour <= 23 && minute >= 0 && minute <= 59;

        public int CompareTo(RelativeTime other) => TotalMinutes.CompareTo(other.TotalMinutes);

        public override string ToString() => $"D{(dayOffset >= 0 ? "+" : "")}{dayOffset} {hour:00}:{minute:00}";
    }
}
