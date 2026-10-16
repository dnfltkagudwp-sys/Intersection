using System;
using System.Collections.Generic;
using System.Globalization;
using Intersection.Data;

namespace Intersection.Core
{
    /// <summary>
    /// 상대 시각(D-14 21:18)을 caseDate 기준 실제 날짜로 바꾸고, 모든 날짜·시각 표기를 한 규칙으로 만든다.
    /// 화면에는 실제 달력 날짜를 표시하고, 상대 시각 값은 내부 계산에만 쓴다.
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

        /// <summary>업무 화면에서 쓰는 실제 날짜.</summary>
        public string FullDate(RelativeTime t) =>
            ToDateTime(t).ToString(text.Get(UIKeys.FormatFullDate), culture);

        /// <summary>업무 화면에서 쓰는 실제 날짜와 시각.</summary>
        public string FullDateTime(RelativeTime t) =>
            text.Format(UIKeys.SeparatorTemplate, ("date", FullDate(t)), ("time", Time(t)));

        /// <summary>실제 날짜 범위. 같은 날이면 날짜를 한 번만 보여준다.</summary>
        public string FullDateTimeRange(RelativeTime from, RelativeTime to)
        {
            if (from.dayOffset == to.dayOffset)
                return text.Format(UIKeys.DateTimeRange, ("date", FullDate(from)), ("start", Time(from)), ("end", Time(to)));
            return text.Format(UIKeys.DateRange, ("from", FullDateTime(from)), ("to", FullDateTime(to)));
        }

        public string FullDateRange(RelativeTime from, RelativeTime to)
        {
            if (from.dayOffset == to.dayOffset)
                return FullDate(from);
            return text.Format(UIKeys.DateRange, ("from", FullDate(from)), ("to", FullDate(to)));
        }

        /// <summary>좁은 목록 오른쪽의 달력 날짜. 같은 사건 연도는 월·일만 표시한다.</summary>
        public string ListLabel(RelativeTime t)
        {
            var date = ToDateTime(t);
            return date.ToString(text.Get(date.Year == caseDate.Year ? UIKeys.FormatMonthDay : UIKeys.FormatFullDate), culture);
        }

        /// <summary>사진·상세·목록 구역에 쓰는 실제 달력 날짜.</summary>
        public string DateLabel(RelativeTime t) => FullDate(t);

        /// <summary>대화 안 날짜 구분선.</summary>
        public string Separator(RelativeTime t) =>
            text.Format(UIKeys.SeparatorTemplate, ("date", FullDate(t)), ("time", Time(t)));

        /// <summary>검색에서 맞춰 볼 수 있는 이 메시지의 표시 날짜들.</summary>
        public IEnumerable<string> SearchDates(RelativeTime t)
        {
            var date = ToDateTime(t);
            yield return date.ToString(text.Get(UIKeys.FormatMonthDay), culture);
            yield return date.ToString(text.Get(UIKeys.FormatFullDate), culture);
        }

        /// <summary>사람이 직접 적은 짧은 날짜(파일 표시 문구의 {date:N}). 예: 4/8.</summary>
        public string NoteDate(int dayOffset) =>
            caseDate.AddDays(dayOffset).ToString(text.Get(UIKeys.FormatNoteDate), culture);

    }
}
