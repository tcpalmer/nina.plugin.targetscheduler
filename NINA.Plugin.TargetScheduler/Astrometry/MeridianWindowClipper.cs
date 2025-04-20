using NINA.Plugin.TargetScheduler.Planning;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using System;

namespace NINA.Plugin.TargetScheduler.Astrometry {

    public class MeridianWindowClipper {

        public MeridianWindowClipper() {
        }

        public TimeInterval Clip(DateTime targetStartTime, DateTime targetTransitTime, DateTime targetEndTime, int meridianWindowMin) {
            DateTime startTime = targetStartTime;
            DateTime transitTime = targetTransitTime;
            DateTime endTime = targetEndTime;

            if (transitTime == DateTime.MinValue) {
                TSLogger.Trace("meridian window: target did not have valid transit time, skipping");
                return new TimeInterval(startTime, endTime);
            }

            // Time in seconds on either side of the meridian
            long meridianWindowSecs = meridianWindowMin * 60;

            // There are eight cases of the timing of start (S), transit (T) and end (E) with respect to the meridian span (M===T===M)
            // Case 1: ------S------M======T======M -> start before the entire span (clip start)
            // Case 2: M======T======M------S------ -> start after the entire span (reject)
            // Case 3: ------M===S===T======M------ -> start in span, before transit (start no change)
            // Case 4: ------M======T===S===M------ -> start in span, after transit (start no change)

            // Case 5: ------E------M======T======M -> end before the entire span (reject)
            // Case 6: M======T======M------E------ -> end after the entire span (clip end)
            // Case 7: ------M===E===T======M------ -> end in span, before transit (end no change)
            // Case 8: ------M======T===E===M------ -> end in span, after transit (end no change)

            // Case 2: M======T======M------S------ -> start after the entire span (reject)
            if (startTime > transitTime.AddSeconds(meridianWindowSecs)) {
                return null;
            }

            // Case 5: ------E------M======T======M -> end before the entire span (reject)
            if (endTime < transitTime.AddSeconds(-meridianWindowSecs)) {
                return null;
            }

            // Case 1: ------S------M======T======M -> start before the entire span (clip start)
            long span = (long)transitTime.Subtract(startTime).TotalSeconds;
            if (span > meridianWindowSecs) {
                startTime = transitTime.AddSeconds(-meridianWindowSecs);
            }

            // Case 6: M======T======M------E------ -> end after the entire span (clip end)
            span = (long)endTime.Subtract(transitTime).TotalSeconds;
            if (span > meridianWindowSecs) {
                endTime = transitTime.AddSeconds(meridianWindowSecs);
            }

            // Cases 3,4,7,8 (the 'no change' cases) are handled implicitly by the above

            return new TimeInterval(startTime, endTime);
        }
    }
}