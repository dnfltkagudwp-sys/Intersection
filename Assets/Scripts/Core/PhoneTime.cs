using System;
using System.Collections.Generic;
using System.Globalization;
using Intersection.Data;

namespace Intersection.Core
{
    /// <summary>
    /// 상대 시각(D-14 21:18)을 caseDate 기준 실제 날짜로 바꾸고, 모든 날짜·시각 표기를 한 규칙으로 만든다.
    /// 목록의 오늘·어제·요일은 PC 날짜가 아니라 caseDate를 기준으로 계산한다.
    /// </summary>
    public class PhoneTime
    {
        readonly DateTime caseDate;
        readonly CultureInfo culture;
        readonly UIText text;

        public PhoneTime(GameConfig config, UIText text)
        {
            caseDate = config.CaseDate.Date;
            culture = config.Culture;
            this.text = text;
        }

        public CultureInfo Culture => culture;

        public DateTime ToDateTime(RelativeTime t) =>
            caseDate.AddDays(t.dayOffset).AddHours(t.hour).AddMinutes(t.minute);

        public string Time(RelativeTime t) => ToDateTime(t).ToString(text.Get(UIKeys.FormatTime), culture);

        /// <summary>대화 목록 오른쪽의 날짜 표기.</summary>
        public string ListLabel(RelativeTime t) => DayLabel(t, true);

        /// <summary>대화 안 날짜 구분선.</summary>
        public string Separator(RelativeTime t) =>
            text.Format(UIKeys.SeparatorTemplate, ("date", DayLabel(t, false)), ("time", Time(t)));

        string DayLabel(RelativeTime t, bool forList)
        {
            int daysBefore = -t.dayOffset;
            var date = ToDateTime(t);
            if (daysBefore == 0)
                return forList ? Time(t) : text.Get(UIKeys.DateToday);
            if (daysBefore == 1)
                return text.Get(UIKeys.DateYesterday);
            if (daysBefore > 1 && daysBefore < 7)
                return date.ToString(text.Get(UIKeys.FormatWeekday), culture);
            if (date.Year == caseDate.Year)
                return date.ToString(text.Get(UIKeys.FormatMonthDay), culture);
            return date.ToString(text.Get(UIKeys.FormatFullDate), culture);
        }

        /// <summary>검색에서 맞춰 볼 수 있는 이 메시지의 표시 날짜들.</summary>
        public IEnumerable<string> SearchDates(RelativeTime t)
        {
            var date = ToDateTime(t);
            yield return DayLabel(t, false);
            yield return date.ToString(text.Get(UIKeys.FormatMonthDay), culture);
            yield return date.ToString(text.Get(UIKeys.FormatFullDate), culture);
        }

        /// <summary>업무 패널용 "사망 14일 전" 표기.</summary>
        public string SinceDeath(RelativeTime t)
        {
            if (t.dayOffset < 0)
                return text.Format(UIKeys.DeathBefore, ("n", (-t.dayOffset).ToString(culture)));
            if (t.dayOffset > 0)
                return text.Format(UIKeys.DeathAfter, ("n", t.dayOffset.ToString(culture)));
            return text.Get(UIKeys.DeathDayOf);
        }

        public string SinceDeathWithTime(RelativeTime t) =>
            text.Format(UIKeys.DeathWithTime, ("day", SinceDeath(t)), ("time", Time(t)));

        public string SinceDeathRange(RelativeTime from, RelativeTime to)
        {
            if (from.dayOffset == to.dayOffset)
                return SinceDeath(from);
            return text.Format(UIKeys.DeathRange, ("from", SinceDeath(from)), ("to", SinceDeath(to)));
        }
    }
}
