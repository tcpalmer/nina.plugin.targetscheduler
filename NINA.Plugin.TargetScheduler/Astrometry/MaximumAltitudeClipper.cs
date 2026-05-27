using NINA.Astrometry;
using NINA.Plugin.TargetScheduler.Planning;
using System;

namespace NINA.Plugin.TargetScheduler.Astrometry {

    public class MaximumAltitudeClipper {
        private TimeInterval exceededSpan;

        public MaximumAltitudeClipper(TargetVisibility targetVisibility, ObserverInfo location, Coordinates coordinates, DateTime minTime, DateTime maxTime, double maxAltitude) {
            exceededSpan = targetVisibility.MaximumAltitudeExceededInterval(minTime, maxTime, location, coordinates, maxAltitude);
        }

        /// <summary>
        /// Compare the target start/end times with a max altitude exceeded interval:
        /// * If no clip is needed, target start/end is returned
        /// * If the target start/end are entirely inside the exceeded span, return null
        /// * Otherwise, clip the start/end to the exceeded span as needed
        /// </summary>
        /// <param name="targetStartTime"></param>
        /// <param name="targetEndTime"></param>
        /// <returns></returns>
        public TimeInterval Clip(DateTime targetStartTime, DateTime targetEndTime) {
            DateTime startTime = targetStartTime;
            DateTime endTime = targetEndTime;

            // Project has max altitude disabled
            if (exceededSpan == null)
                return new TimeInterval(startTime, endTime);

            // Target span is entirely before the max exceeded span -> OK
            if (endTime < exceededSpan.StartTime)
                return new TimeInterval(startTime, endTime);

            // Target start is before and end is inside the exceeded span -> clip
            if (startTime < exceededSpan.StartTime && exceededSpan.Contains(endTime))
                return new TimeInterval(startTime, exceededSpan.StartTime);

            // Target start and end are inside the exceeded span -> reject
            if (exceededSpan.Contains(startTime) && exceededSpan.Contains(endTime))
                return null;

            // Target start is within and end is after exceeded span -> clip
            if (exceededSpan.Contains(startTime) && endTime > exceededSpan.EndTime)
                return new TimeInterval(exceededSpan.EndTime, endTime);

            // Target span is entirely after the max exceeded span -> OK
            if (startTime > exceededSpan.EndTime)
                return new TimeInterval(startTime, endTime);

            throw new ArgumentException($"Undetected case in max clip:\nstart={targetStartTime}, end={targetEndTime}, exceeded span={exceededSpan}");
        }

        /// <summary>
        /// Determine the next time at which it would be safe to continue.
        /// </summary>
        /// <param name="targetStartTime"></param>
        /// <param name="targetEndTime"></param>
        /// <returns></returns>
        public DateTime NextSafeStart(DateTime targetStartTime, DateTime targetEndTime) {
            if (exceededSpan == null)
                return targetStartTime;

            TimeInterval maxAltSafeSpan = Clip(targetStartTime, targetEndTime);
            return maxAltSafeSpan != null ? maxAltSafeSpan.StartTime : exceededSpan.EndTime;
        }
    }
}