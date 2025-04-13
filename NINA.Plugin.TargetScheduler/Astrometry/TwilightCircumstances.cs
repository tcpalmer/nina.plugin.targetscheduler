using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NINA.Astrometry;
using NINA.Astrometry.RiseAndSet;
using NINA.Plugin.TargetScheduler.Planning;
using System;
using System.Globalization;
using System.Runtime.Caching;
using System.Text;

namespace NINA.Plugin.TargetScheduler.Astrometry {

    [JsonConverter(typeof(StringEnumConverter))]
    public enum TwilightLevel {
        Nighttime, Astronomical, Nautical, Civil
    };

    [JsonConverter(typeof(StringEnumConverter))]
    public enum TwilightStage {
        Dusk, Dawn
    };

    /// <summary>
    /// Determine the nightly twilight circumstances for the provided date.  The dusk (start) times will be on the provided date
    /// while the dawn (end) times will be on the following day (in general) - determining the potential imaging time span for
    /// a single 'night'.
    ///
    /// This code leverages the NINA Astrometry sun rise/set/altitude time determination code in NINA.Astrometry.RiseAndSet.  Note
    /// that this code works up to latitude 70° - above that, it doesn't return reliable twilight times.
    /// </summary>
    public class TwilightCircumstances {
        public const double DayEndAltitude = 0; // refraction adjustment not needed here
        public const double CivilEndSunAltitude = -6;
        public const double NauticalEndSunAltitude = -12;
        public const double AstronomicalEndSunAltitude = -18;

        public DateTime? CivilTwilightStart { get; protected set; }
        public DateTime? CivilTwilightEnd { get; protected set; }
        public DateTime? NauticalTwilightStart { get; protected set; }
        public DateTime? NauticalTwilightEnd { get; protected set; }
        public DateTime? AstronomicalTwilightStart { get; protected set; }
        public DateTime? AstronomicalTwilightEnd { get; protected set; }
        public DateTime? NighttimeStart { get; protected set; }
        public DateTime? NighttimeEnd { get; protected set; }
        public DateTime? Sunset { get => CivilTwilightStart; }
        public DateTime? Sunrise { get => CivilTwilightEnd; }

        public DateTime OnDate { get; protected set; }
        private ObserverInfo observerInfo;

        public TwilightCircumstances(ObserverInfo observerInfo, DateTime atTime) {
            this.observerInfo = observerInfo;
            OnDate = atTime.Date.AddHours(12); // fix to noon on date

            string cacheKey = GetCacheKey();
            //TSLogger.Trace($"TwilightCircumstances cache key: {cacheKey}");

            TwilightCircumstances cached = TwilightCircumstancesCache.Get(cacheKey);
            if (cached == null) {
                Calculate();
                TwilightCircumstancesCache.Put(this, cacheKey);
            } else {
                CivilTwilightStart = cached.CivilTwilightStart;
                CivilTwilightEnd = cached.CivilTwilightEnd;
                NauticalTwilightStart = cached.NauticalTwilightStart;
                NauticalTwilightEnd = cached.NauticalTwilightEnd;
                AstronomicalTwilightStart = cached.AstronomicalTwilightStart;
                AstronomicalTwilightEnd = cached.AstronomicalTwilightEnd;
                NighttimeStart = cached.NighttimeStart;
                NighttimeEnd = cached.NighttimeEnd;
            }
        }

        public bool HasNighttime() {
            return NighttimeStart != null;
        }

        public bool HasAstronomicalTwilight() {
            return AstronomicalTwilightStart != null;
        }

        public bool HasNauticalTwilight() {
            return NauticalTwilightStart != null;
        }

        public bool HasCivilTwilight() {
            return CivilTwilightStart != null;
        }

        public TimeInterval GetTwilightSpan(TwilightLevel twilightLevel) {
            switch (twilightLevel) {
                case TwilightLevel.Nighttime: return SafeTwilightSpan(NighttimeStart, NighttimeEnd);
                case TwilightLevel.Astronomical: return SafeTwilightSpan(AstronomicalTwilightStart, AstronomicalTwilightEnd);
                case TwilightLevel.Nautical: return SafeTwilightSpan(NauticalTwilightStart, NauticalTwilightEnd);
                case TwilightLevel.Civil: return SafeTwilightSpan(CivilTwilightStart, CivilTwilightEnd);
                default:
                    throw new ArgumentException($"unknown twilight level: {twilightLevel}");
            }
        }

        public TwilightLevel? GetCurrentTwilightLevel(DateTime atTime) {
            if (HasNighttime() && NighttimeStart < atTime && atTime <= NighttimeEnd) return TwilightLevel.Nighttime;
            if (HasAstronomicalTwilight() && AstronomicalTwilightStart < atTime && atTime <= AstronomicalTwilightEnd) return TwilightLevel.Astronomical;
            if (HasNauticalTwilight() && NauticalTwilightStart < atTime && atTime <= NauticalTwilightEnd) return TwilightLevel.Nautical;
            if (HasCivilTwilight() && CivilTwilightStart < atTime && atTime <= CivilTwilightEnd) return TwilightLevel.Civil;
            return null;
        }

        public static TwilightCircumstances AdjustTwilightCircumstances(ObserverInfo observerInfo, DateTime atTime) {
            TwilightCircumstances twilightCircumstances = new TwilightCircumstances(observerInfo, atTime);
            DateTime? CivilTwilightStart = twilightCircumstances.CivilTwilightStart;
            DateTime? CivilTwilightEnd = twilightCircumstances.CivilTwilightEnd;
            DateTime noon = atTime.Date.AddHours(12);
            DateTime midnight = atTime.Date.AddHours(24);

            if (!CivilTwilightStart.HasValue || !CivilTwilightEnd.HasValue) {
                throw new ArgumentException("Oops!  Need to fix AdjustTwilightCircumstances!");
            }

            // If atTime is between noon and civil start, return next dusk/following dawn
            if (noon <= atTime && atTime < CivilTwilightStart) {
                return twilightCircumstances;
            }

            // If atTime is between civil start/end and atTime is before midnight, return next dusk/following dawn
            if (CivilTwilightStart <= atTime && atTime < CivilTwilightEnd && atTime < midnight) {
                return twilightCircumstances;
            }

            // If atTime is after the previous dawn and before noon, return next dusk/following dawn
            TwilightCircumstances previous = new TwilightCircumstances(observerInfo, atTime.AddDays(-1));
            CivilTwilightEnd = previous.CivilTwilightEnd;
            if (CivilTwilightEnd <= atTime && atTime < noon) {
                return twilightCircumstances;
            }

            // Otherwise, we want NighttimeCircumstances for the previous day
            return previous;
        }

        private void Calculate() {
            var civil = new Daytime(OnDate, observerInfo.Latitude, observerInfo.Longitude);
            CivilTwilightStart = civil.Start;
            CivilTwilightEnd = civil.End;

            var nautical = new CivilTwilight(OnDate, observerInfo.Latitude, observerInfo.Longitude);
            NauticalTwilightStart = nautical.Start;
            NauticalTwilightEnd = nautical.End;

            var astro = new NauticalTwilight(OnDate, observerInfo.Latitude, observerInfo.Longitude);
            AstronomicalTwilightStart = astro.Start;
            AstronomicalTwilightEnd = astro.End;

            var night = new AstronomicalTwilight(OnDate, observerInfo.Latitude, observerInfo.Longitude);
            NighttimeStart = night.Start;
            NighttimeEnd = night.End;
        }

        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            sb.Append($"Civil start:        {CivilTwilightStart}\n");
            sb.Append($"Nautical start:     {NauticalTwilightStart}\n");
            sb.Append($"Astronomical start: {AstronomicalTwilightStart}\n");
            sb.Append($"Night start:        {NighttimeStart}\n");
            sb.Append($"Night end:          {NighttimeEnd}\n");
            sb.Append($"Astronomical end:   {AstronomicalTwilightEnd}\n");
            sb.Append($"Nautical end:       {NauticalTwilightEnd}\n");
            sb.Append($"Civil end:          {CivilTwilightEnd}\n");
            return sb.ToString();
        }

        private string GetCacheKey() {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{OnDate:yyyy-MM-dd-HH-mm-ss}_");
            sb.Append($"{observerInfo.Latitude.ToString("0.000000", CultureInfo.InvariantCulture)}_");
            sb.Append($"{observerInfo.Longitude.ToString("0.000000", CultureInfo.InvariantCulture)}");
            return sb.ToString();
        }

        private TimeInterval SafeTwilightSpan(DateTime? t1, DateTime? t2) {
            if (t1 == null || t2 == null) {
                return null;
            }

            return new TimeInterval((DateTime)t1, (DateTime)t2);
        }
    }

    internal abstract class SunAltitudeEvent : SunCustomRiseAndSet {
        public DateTime? Start { get; private set; }
        public DateTime? End { get; private set; }

        public SunAltitudeEvent(DateTime date, double latitude, double longitude, double sunAltitude)
            : base(date, latitude, longitude, sunAltitude) {
            _ = Calculate().Result;
            Start = Set;
            End = Rise;
        }
    }

    internal class Daytime : SunAltitudeEvent {

        public Daytime(DateTime date, double latitude, double longitude)
            : base(date, latitude, longitude, TwilightCircumstances.DayEndAltitude) {
        }
    }

    internal class CivilTwilight : SunAltitudeEvent {

        public CivilTwilight(DateTime date, double latitude, double longitude)
            : base(date, latitude, longitude, TwilightCircumstances.CivilEndSunAltitude) {
        }
    }

    internal class NauticalTwilight : SunAltitudeEvent {

        public NauticalTwilight(DateTime date, double latitude, double longitude)
            : base(date, latitude, longitude, TwilightCircumstances.NauticalEndSunAltitude) {
        }
    }

    internal class AstronomicalTwilight : SunAltitudeEvent {

        public AstronomicalTwilight(DateTime date, double latitude, double longitude)
            : base(date, latitude, longitude, TwilightCircumstances.AstronomicalEndSunAltitude) {
        }
    }

    internal class TwilightCircumstancesCache {
        private static readonly TimeSpan ITEM_TIMEOUT = TimeSpan.FromHours(12);
        private static readonly MemoryCache _cache = new MemoryCache("Scheduler TwilightCircumstances");

        public static TwilightCircumstances Get(string cacheKey) {
            return (TwilightCircumstances)_cache.Get(cacheKey);
        }

        public static void Put(TwilightCircumstances nighttimeCircumstances, string cacheKey) {
            _cache.Add(cacheKey, nighttimeCircumstances, DateTime.Now.Add(ITEM_TIMEOUT));
        }

        private TwilightCircumstancesCache() {
        }
    }
}