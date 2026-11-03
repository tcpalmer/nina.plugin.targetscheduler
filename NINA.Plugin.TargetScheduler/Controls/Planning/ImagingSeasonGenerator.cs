using System;
using System.Collections.Generic;
using System.Threading;

namespace NINA.Plugin.TargetScheduler.Controls.Planning {

    /// <summary>
    /// Determine the target's imaging season: the run of nights, containing or following the start date, on which the
    /// target can be imaged with the planning parameters.  Rather than estimating the season limits astronomically
    /// (e.g. heliacal rising/setting), the limits come from the same nightly evaluation used for Daily Details:
    ///
    /// 1. A coarse pass evaluates every 7th night for a year (with coarse visibility sampling) to locate the off-season:
    ///    the longest run of nights with no imaging window.  A target that's never out of season gets a full year.
    /// 2. A daily pass evaluates every night between the off-seasons to get the season's nightly details.
    /// 3. The result is trimmed to the first and last accepted nights.
    ///
    /// Nights rejected only for moon avoidance still count as in season for locating the off-season - they're shown as
    /// rejected within the season.
    /// </summary>
    public class ImagingSeasonGenerator {

        // Sampling intervals (seconds) for the coarse and daily passes.  Visibility sampling dominates the cost and a few
        // minutes of imaging time precision is plenty for a season.
        public const int COARSE_SAMPLE_INTERVAL = 300;
        public const int SEASON_SAMPLE_INTERVAL = 120;

        private const int COARSE_STEP_DAYS = 7;
        private const int DAYS_IN_YEAR = 365;

        private readonly TargetPlanningParameters parameters;

        public ImagingSeasonGenerator(TargetPlanningParameters parameters) {
            this.parameters = parameters;
        }

        /// <summary>
        /// Generate the nightly details for the season, or an empty list if the target is never imageable.
        /// </summary>
        public List<DailyDetail> Generate(CancellationToken token) {
            (int startDay, int endDay)? window = FindSeasonWindow(token);
            if (window == null) {
                return new List<DailyDetail>();
            }

            DailyDetailsGenerator generator = new DailyDetailsGenerator(parameters, SEASON_SAMPLE_INTERVAL);
            DateTime startDate = parameters.StartDate.Date;
            List<DailyDetail> details = new List<DailyDetail>(window.Value.endDay - window.Value.startDay + 1);
            for (int day = window.Value.startDay; day <= window.Value.endDay; day++) {
                token.ThrowIfCancellationRequested();
                details.Add(generator.GenerateNight(startDate.AddDays(day)));
            }

            return TrimToAccepted(details);
        }

        /// <summary>
        /// Locate the season as days relative to the start date, from the off-season preceding it to the off-season
        /// following it.  Returns null if the target is never in season.
        /// </summary>
        public (int startDay, int endDay)? FindSeasonWindow(CancellationToken token) {
            DailyDetailsGenerator generator = new DailyDetailsGenerator(parameters, COARSE_SAMPLE_INTERVAL);
            DateTime startDate = parameters.StartDate.Date;
            int samples = DAYS_IN_YEAR / COARSE_STEP_DAYS + 1;

            bool[] inSeason = new bool[samples];
            for (int i = 0; i < samples; i++) {
                token.ThrowIfCancellationRequested();
                inSeason[i] = InSeason(generator.GenerateNight(startDate.AddDays(i * COARSE_STEP_DAYS)));
            }

            (int start, int length) gap = LongestCircularRun(inSeason, false);
            if (gap.length == samples) {
                return null; // never in season
            }

            if (gap.length == 0) {
                return (0, DAYS_IN_YEAR - 1); // always in season: show a year
            }

            // The off-season begins between the last in-season sample before the gap (a - 7) and its first sample (a), and
            // ends between its last sample (b) and the next in-season sample.  The season repeats yearly, so the seasons on
            // either side of the gap are [b - 365, a] and [b, a + 365].  Use the one containing or following the start date.
            int a = gap.start * COARSE_STEP_DAYS;
            int b = (gap.start + gap.length - 1) * COARSE_STEP_DAYS;
            return a > 0 ? (b - DAYS_IN_YEAR, a) : (b, a + DAYS_IN_YEAR);
        }

        private static bool InSeason(DailyDetail detail) {
            return detail.Accepted || detail.MoonRejected;
        }

        /// <summary>
        /// Find the longest run of the given value in the array treated as circular, returning its start index (in
        /// [0, length)) and length (capped at the array length).
        /// </summary>
        public static (int start, int length) LongestCircularRun(bool[] values, bool value) {
            int n = values.Length;
            int bestStart = 0, bestLength = 0, runStart = 0, runLength = 0;

            for (int i = 0; i < 2 * n; i++) {
                if (values[i % n] == value) {
                    if (runLength == 0) {
                        runStart = i;
                    }

                    runLength++;
                    if (runLength > bestLength && runStart < n) {
                        bestStart = runStart;
                        bestLength = Math.Min(runLength, n);
                    }
                } else {
                    runLength = 0;
                }
            }

            return (bestStart, bestLength);
        }

        private static List<DailyDetail> TrimToAccepted(List<DailyDetail> details) {
            int first = details.FindIndex(d => d.Accepted);
            if (first < 0) {
                return new List<DailyDetail>();
            }

            int last = details.FindLastIndex(d => d.Accepted);
            return details.GetRange(first, last - first + 1);
        }
    }
}
