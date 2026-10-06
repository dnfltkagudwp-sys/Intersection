using System;
using Intersection.Data;

namespace Intersection.Core
{
    /// <summary>상태바 시계 공급부. 스토리·정렬·진행 로직에는 사용하지 않는다.</summary>
    public interface IClock
    {
        DateTime Now { get; }
    }

    public class SystemClock : IClock
    {
        public DateTime Now => DateTime.Now;
    }

    public class FixedClock : IClock
    {
        readonly DateTime now;

        public FixedClock(int hour, int minute)
        {
            now = DateTime.Today.AddHours(hour).AddMinutes(minute);
        }

        public DateTime Now => now;
    }

    public static class ClockFactory
    {
        public static IClock Create(GameConfig config)
        {
            if (config != null && config.statusClock == StatusClockMode.Fixed)
                return new FixedClock(config.fixedClockHour, config.fixedClockMinute);
            return new SystemClock();
        }
    }
}
