using NINA.Astrometry;
using NINA.Plugin.TargetScheduler.Planning;
using System;

namespace NINA.Plugin.TargetScheduler.Astrometry {

    /// <summary>
    /// Determine if the time span is clipped due to target being over the project maximum altitude at any point in the time span.
    /// After construction, IsClipped answers whether the time span was clipped all. If it was, then the interval is available
    /// in ClipInterval. Note that the target will be above the maximum for the entire ClipInterval interval.
    /// </summary>
    public class MaximumAltitudeClipper {
        public TimeInterval ClipInterval { get; private set; }
        public bool IsClipped => ClipInterval != null;

        public MaximumAltitudeClipper(TargetVisibility targetVisibility, ObserverInfo location, Coordinates coordinates, DateTime minTime, DateTime maxTime, double maxAltitude) {
            if (minTime >= maxTime)
                throw new ArgumentException($"minTime cannot be after maxTime: {minTime} > {maxTime}");

            // Not enabled for max altitude check
            if (maxAltitude == 0)
                return;

            ClipInterval = targetVisibility.MaximumAltitudeExceededInterval(minTime, maxTime, location, coordinates, maxAltitude);
        }
    }
}