using NINA.Astrometry;
using NINA.Plugin.TargetScheduler.Planning;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Caching;
using System.Text;

namespace NINA.Plugin.TargetScheduler.Astrometry {

    /// <summary>
    /// Determine target visibility over the course of an imaging session.  The concept is simple.  We determine the target's
    /// altitude/azimuth from sunset to sunrise at a given sample frequency.  Then, when considering a target, scan the altitude
    /// samples for a period of time.  If the altitude is always greater than the horizon at the same azimuth for some period of
    /// time, then the target can potentially be imaged.
    ///
    /// Unlike an earlier approach to visibility, this method works even for horizons that have peaks (houses,
    /// trees) separating clear regions.
    ///
    /// Keep in mind that this is fundamentally a sampling approach.  The default sample interval is 10s which means timings
    /// could be off by plus or minus 10s.  Transit time on the other hand is calculated exactly, without use of the samples.
    ///
    /// The set of altitude/azimuth samples for a given target, date, and location is cached for performance.
    /// </summary>
    public class TargetVisibility {
        private static readonly VisibilityDetermination NOT_VISIBLE = new VisibilityDetermination(false, null, null);

        public string TargetName { get; private set; }
        public int TargetId { get; private set; }
        public DateTime ImagingDate { get; private set; }
        public DateTime Sunset { get; private set; }
        public DateTime Sunrise { get; private set; }
        public int SampleInterval { get; private set; }
        public DateTime TransitTime { get; private set; }
        public bool ImagingPossible { get; private set; }
        public IList<PositionAtTime> TargetPositions { get; private set; }

        public TargetVisibility(ITarget target, ObserverInfo observerInfo, DateTime imagingDate, DateTime? sunset, DateTime? sunrise, int sampleInterval = 10) :
            this(target.Name, target.DatabaseId, observerInfo, target.Coordinates, imagingDate, sunset, sunrise, sampleInterval) { }

        public TargetVisibility(string targetName, int targetId, ObserverInfo observerInfo, Coordinates coordinates, DateTime imagingDate, DateTime? sunset, DateTime? sunrise, int sampleInterval = 10) {
            if (sunset >= sunrise) {
                throw new ArgumentException("sunset is after sunrise");
            }

            if (sunset == null || sunrise == null) {
                throw new ArgumentException("no sunset/sunrise for this date/location");
            }

            string cacheKey = GetCacheKey(targetId, observerInfo, coordinates, imagingDate, sampleInterval);
            TargetVisibility cached = TargetVisibilityCache.Get(cacheKey);

            if (cached != null) {
                this.TargetName = cached.TargetName;
                this.TargetId = cached.TargetId;
                this.ImagingDate = cached.ImagingDate;
                this.Sunset = cached.Sunset;
                this.Sunrise = cached.Sunrise;
                this.SampleInterval = cached.SampleInterval;
                this.ImagingPossible = cached.ImagingPossible;
                this.TargetPositions = cached.TargetPositions;
                this.TransitTime = cached.TransitTime;
            } else {
                //Stopwatch stopWatch = new Stopwatch();
                //stopWatch.Start();
                TargetName = targetName;
                TargetId = targetId;
                ImagingDate = imagingDate;
                Sunset = (DateTime)sunset;
                Sunrise = (DateTime)sunrise;
                SampleInterval = sampleInterval;
                ImagingPossible = false;

                int nightLength = (int)(Sunrise - Sunset).TotalSeconds;

                // Bail out if 'night' is less than 5m - would only be civil twilight anyway (high latitude summer)
                if (nightLength < 300) {
                    return;
                }

                int numSamples = nightLength / SampleInterval + 2; // ensure sample span is inclusive

                TargetPositions = new List<PositionAtTime>(numSamples);
                DateTime sampleTime = Sunset;
                for (int i = 0; i < numSamples; i++) {
                    HorizontalCoordinate hc = AstrometryUtils.GetHorizontalCoordinates(observerInfo, coordinates, sampleTime);
                    TargetPositions.Add(new PositionAtTime(sampleTime, hc));
                    if (hc.Altitude > 0) { ImagingPossible = true; }
                    sampleTime = sampleTime.AddSeconds(sampleInterval);
                }

                TransitTime = GetImagingTransitTime(observerInfo, coordinates);
                TargetPositions = TargetPositions.AsReadOnly();

                //stopWatch.Stop();
                //TSLogger.Info($"TargetVisibility timing for {cacheKey}:\n*** {stopWatch.Elapsed}");

                TargetVisibilityCache.Put(this, cacheKey);
            }
        }

        /// <summary>
        /// Determine the next potential time span when the target is visible, starting from the provided time and
        /// ending when the target next goes below the horizon or the imaging interval ends, whichever is earlier.
        ///
        /// Note that there is an implicit (but valid) assumption that the imaging interval will be wholly contained
        /// within the sunset to sunrise time span.  It's valid because in real usage, the interval is based on the
        /// most inclusive twilight span for the night, over all applicable filters.
        /// </summary>
        /// <param name="atTime"></param>
        /// <param name="imagingInterval"></param>
        /// <param name="horizon"></param>
        /// <returns></returns>
        public VisibilityDetermination NextVisibleInterval(DateTime atTime, TimeInterval imagingInterval, HorizonDefinition horizon) {
            if (!ImagingPossible) { return NOT_VISIBLE; }
            if (atTime < imagingInterval.StartTime) { return NextVisibleInterval(imagingInterval.StartTime, imagingInterval, horizon); }
            if (atTime >= imagingInterval.EndTime) { return NOT_VISIBLE; }

            int startPos = FindInterval(atTime, 0, TargetPositions.Count - 1);

            for (int i = startPos; i < TargetPositions.Count; i++) {
                PositionAtTime pat = TargetPositions[i];
                if (pat.AtTime >= imagingInterval.EndTime) { break; }

                if (!IsBelowHorizon(pat, horizon)) {
                    return new VisibilityDetermination(true, pat.AtTime, FindStopTime(i, horizon, imagingInterval.EndTime));
                }
            }

            return NOT_VISIBLE;
        }

        /// <summary>
        /// Determine the next potential time span when the target is visible for at least the minimum number of seconds,
        /// starting from the provided time and ending when the target next goes below the horizon or the imaging interval
        /// ends, whichever is earlier.
        /// </summary>
        /// <param name="atTime"></param>
        /// <param name="imagingInterval"></param>
        /// <param name="horizon"></param>
        /// <param name="minimumTimeSeconds"></param>
        /// <returns></returns>
        public VisibilityDetermination NextVisibleInterval(DateTime atTime, TimeInterval imagingInterval, HorizonDefinition horizon, int minimumTimeSeconds) {
            VisibilityDetermination viz = NextVisibleInterval(atTime, imagingInterval, horizon);
            while (viz.IsVisible) {
                int TimeOnTargetSeconds = (int)(viz.StopTime - viz.StartTime).TotalSeconds;
                if (TimeOnTargetSeconds >= minimumTimeSeconds) {
                    return viz;
                }

                viz = NextVisibleInterval(viz.StopTime, imagingInterval, horizon);
            }

            return viz;
        }

        /// <summary>
        /// Return the target altitude at the provided time.  The altitude is based on the samples so approximate.
        ///
        /// If imaging isn't possible at all or the time falls outside that sampled timespan, DateTime.MinValue is
        /// returned.
        /// </summary>
        /// <param name="atTime"></param>
        /// <returns></returns>
        public double GetAltitude(DateTime atTime) {
            if (!ImagingPossible || atTime < Sunset || atTime > Sunrise)
                return double.MinValue;

            int pos = FindInterval(atTime, 0, TargetPositions.Count - 1);
            return TargetPositions[pos].Altitude;
        }

        /// <summary>
        /// Visibility start times will be returned based on the sample rate.  Determine whether 'now' falls inside
        /// the sample interval, i.e. within plus/minus 2x SampleInterval.  For example, if the sample interval is
        /// 10 seconds, then a calculated start time will match from 10 seconds before to 10 seconds after 'now'.
        /// </summary>
        /// <param name="atTime"></param>
        /// <param name="visibilityTime"></param>
        /// <returns></returns>
        public bool IsApproximatelyNow(DateTime atTime, DateTime visibilityTime) {
            int delta = (int)Math.Abs((atTime - visibilityTime).TotalSeconds);
            return delta <= SampleInterval;
        }

        /// <summary>
        /// Since we're interested in the transit from the perspective of restricting imaging to a meridian
        /// window, we want the transit in or closest to the sunset/sunrise span, arbitrarily expanded
        /// by 2 hours on each end.  We expand to ensure that we catch a transit occuring just before/after
        /// sunset/sunrise that might still allow meridian window imaging after dusk or before dawn.
        ///
        /// If imaging isn't possible at all or the transit falls outside that expanded timespan,
        /// DateTime.MinValue is returned.
        ///
        /// When comparing to Stellarium, note that it seems to flip the transit day at the target anti-meridian,
        /// so it's determining the closest transit whether forward or backwards in time.  The approach based
        /// on NINA utils flips at local midnight.
        ///
        /// </summary>
        /// <param name="location"></param>
        /// <param name="coordinates"></param>
        /// <returns></returns>
        public DateTime GetImagingTransitTime(ObserverInfo location, Coordinates coordinates) {
            if (!ImagingPossible) {
                return DateTime.MinValue;
            }

            DateTime checkDate = Sunset.Date.AddHours(12);
            DateTime minTime = Sunset.AddHours(-2);
            DateTime maxTime = Sunrise.AddHours(2);

            DateTime transitTime = AstrometryUtils.GetTransitTime(location, coordinates, checkDate);
            if (transitTime > minTime && transitTime < maxTime) {
                return transitTime;
            }

            if (transitTime < Sunset) {
                transitTime = AstrometryUtils.GetTransitTime(location, coordinates, checkDate.AddDays(1));
                if (transitTime > minTime && transitTime < maxTime) {
                    return transitTime;
                }
            }

            return DateTime.MinValue;
        }

        /// <summary>
        /// Return true if the target transits the meridian during the imaging night samples (extended) and
        /// imaging is possible.
        /// </summary>
        /// <returns></returns>
        public bool HasTransit() {
            return TransitTime != DateTime.MinValue;
        }

        private bool IsBelowHorizon(PositionAtTime pat, HorizonDefinition horizon) {
            return pat.Altitude - horizon.GetTargetAltitude(pat.Azimuth) <= 0;
        }

        private DateTime? FindStopTime(int pos, HorizonDefinition horizon, DateTime imagingStop) {
            while (true) {
                if (++pos == TargetPositions.Count) { return imagingStop; }
                PositionAtTime pat = TargetPositions[pos];
                if (IsBelowHorizon(pat, horizon)) { return pat.AtTime; }
                if (pat.AtTime >= imagingStop) { return imagingStop; }
            }
        }

        public int FindInterval(DateTime atTime, int low, int high) {
            if (low >= high) {
                return low;
            }

            int mid = low + (high - low) / 2; // binary search

            if (atTime >= TargetPositions[mid].AtTime && atTime < TargetPositions[mid + 1].AtTime) {
                return mid;
            }

            if ((atTime < TargetPositions[mid].AtTime)) {
                return FindInterval(atTime, low, mid);
            }

            return FindInterval(atTime, mid + 1, high);
        }

        // Debugging/testing
        public string ShowAltitudeDelta(HorizonDefinition horizon) {
            StringBuilder sb = new StringBuilder();
            sb.Append($"\nTarget: {TargetName}\n");
            for (int i = 0; i < TargetPositions.Count; ++i) {
                PositionAtTime pat = TargetPositions[i];
                sb.Append(pat.AtTime.ToString("MM/dd/yyyy HH:mm:ss   "));
                //sb.Append($"{pat.Altitude - horizon.GetTargetAltitude(pat.Azimuth)}   {pat.Azimuth}\n");
                sb.Append($"{pat.Altitude} - {horizon.GetTargetAltitude(pat.Azimuth)}: {pat.Altitude - horizon.GetTargetAltitude(pat.Azimuth)}   {pat.Azimuth}\n");
            }

            return sb.ToString();
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            sb.Append($"Target: {TargetName} ({TargetId}) on {Utils.FormatDateTimeFull(ImagingDate)} with transit at {Utils.FormatDateTimeFull(TransitTime)}, imaging possible: {ImagingPossible}\n");
            sb.Append($"Samples: {TargetPositions.Count} from {Utils.FormatDateTimeFull(Sunset)} to {Utils.FormatDateTimeFull(Sunrise)}\n");
            sb.Append($"ImagingPossible: {ImagingPossible}\n");

            foreach (var item in TargetPositions) {
                sb.Append(String.Format("{0,13:F4} {1,13:F4} ", Utils.FormatDegrees(item.Altitude), Utils.FormatDegrees(item.Azimuth)));
                sb.Append(item.AtTime.ToString("MM/dd/yyyy HH:mm:ss\n"));
            }

            return sb.ToString();
        }

        private string GetCacheKey(int targetId, ObserverInfo observerInfo, Coordinates coordinates, DateTime imagingDate, int sampleInterval) {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{targetId}_{imagingDate:yyyy-MM-dd-HH-mm-ss}_");
            sb.Append($"{observerInfo.Latitude.ToString("0.000000", CultureInfo.InvariantCulture)}_");
            sb.Append($"{observerInfo.Longitude.ToString("0.000000", CultureInfo.InvariantCulture)}_");
            sb.Append($"{coordinates.RADegrees.ToString("0.000000", CultureInfo.InvariantCulture)}_");
            sb.Append($"{coordinates.Dec.ToString("0.000000", CultureInfo.InvariantCulture)}_");
            sb.Append(sampleInterval);
            return sb.ToString();
        }
    }

    public class PositionAtTime {
        public DateTime AtTime { get; private set; }
        public HorizontalCoordinate HorizontalCoordinate { get; private set; }
        public double Altitude => HorizontalCoordinate.Altitude;
        public double Azimuth => HorizontalCoordinate.Azimuth;

        public PositionAtTime(DateTime atTime, HorizontalCoordinate horizontalCoordinate) {
            AtTime = atTime;
            HorizontalCoordinate = horizontalCoordinate;
        }

        public override string ToString() {
            return $"{Utils.FormatDateTimeFull(AtTime)} {String.Format("{0,13:F4} {1,13:F4} ", Utils.FormatDegrees(Altitude), Utils.FormatDegrees(Azimuth))}";
        }
    }

    public class VisibilityDetermination {
        public bool IsVisible { get; private set; }
        public DateTime StartTime { get; private set; }
        public DateTime StopTime { get; private set; }

        public VisibilityDetermination(bool isVisible, DateTime? startTime, DateTime? stopTime) {
            this.IsVisible = isVisible;
            this.StartTime = (DateTime)(startTime == null ? DateTime.MinValue : startTime);
            this.StopTime = (DateTime)(stopTime == null ? DateTime.MinValue : stopTime);
        }

        public override string ToString() {
            string start = StartTime == DateTime.MinValue ? "n/a" : StartTime.ToString();
            string stop = StopTime == DateTime.MinValue ? "n/a" : StopTime.ToString();
            return $"isVisible={IsVisible}, start={start}, stop={stop}";
        }
    }

    public class TargetVisibilityCache {
        private static readonly TimeSpan ITEM_TIMEOUT = TimeSpan.FromHours(12);
        private static MemoryCache _cache = new MemoryCache("Scheduler TargetVisibility");

        public static TargetVisibility Get(string cacheKey) {
            return (TargetVisibility)_cache.Get(cacheKey);
        }

        public static void Put(TargetVisibility targetVisibility, string cacheKey) {
            _cache.Add(cacheKey, targetVisibility, DateTime.Now.Add(ITEM_TIMEOUT));
        }

        public static void Clear() {
            _cache.Dispose();
            _cache = new MemoryCache("Scheduler TargetVisibility");
        }

        private TargetVisibilityCache() {
        }
    }
}