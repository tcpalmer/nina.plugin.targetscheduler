using NINA.Plugin.TargetScheduler.Util;
using System;

namespace NINA.Plugin.TargetScheduler.Planning {

    public class TimeInterval {
        public DateTime StartTime { get; private set; }
        public DateTime EndTime { get; private set; }
        public long Duration { get; private set; }

        public TimeInterval(DateTime startTime, DateTime endTime) {
            if (startTime > endTime) {
                throw new ArgumentException("startTime must be before endTime");
            }

            StartTime = startTime;
            EndTime = endTime;
            Duration = (long)(endTime - startTime).TotalSeconds;
        }

        public TimeInterval Overlap(TimeInterval ti2) {
            if (StartTime > ti2.EndTime) {
                return null;
            }

            if (EndTime < ti2.StartTime) {
                return null;
            }

            DateTime start = ti2.StartTime;
            DateTime end = ti2.EndTime;
            start = start > StartTime ? start : StartTime;
            end = end < EndTime ? end : EndTime;

            return new TimeInterval(start, end);
        }

        public bool Contains(DateTime dt) {
            return dt >= StartTime && dt <= EndTime;
        }

        public override string ToString() {
            return $"{Utils.FormatDateTimeFull(StartTime)} - {Utils.FormatDateTimeFull(EndTime)}";
        }

        public static TimeInterval GetTotalTimeSpan(TimeInterval first, params TimeInterval[] values) {
            DateTime start = first.StartTime;
            DateTime end = first.EndTime;

            for (int i = 0; i < values.Length; i++) {
                TimeInterval val = values[i];
                if (val.StartTime < start) start = val.StartTime;
                if (val.EndTime > end) end = val.EndTime;
            }

            return new TimeInterval(start, end);
        }
    }
}