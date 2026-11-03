using NINA.Astrometry;
using NINA.Plugin.TargetScheduler.Astrometry;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NINA.Plugin.TargetScheduler.Planning;
using NINA.Plugin.TargetScheduler.Planning.Entities;
using NINA.Plugin.TargetScheduler.Planning.Exposures;
using NINA.Plugin.TargetScheduler.Planning.Interfaces;
using NINA.Plugin.TargetScheduler.Shared.Utility;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;

namespace NINA.Plugin.TargetScheduler.Controls.Planning {

    /// <summary>
    /// Snapshot of the Target Planning parameters used to generate a plan.
    /// </summary>
    public class TargetPlanningParameters {
        public IProfile Profile { get; set; }
        public string TargetName { get; set; }
        public Coordinates Coordinates { get; set; }
        public DateTime StartDate { get; set; }
        public int PlanDays { get; set; }

        public int MinimumTime { get; set; }
        public double MinimumAltitude { get; set; }
        public double MaximumAltitude { get; set; }
        public bool UseCustomHorizon { get; set; }
        public double HorizonOffset { get; set; }
        public int MeridianWindow { get; set; }

        public TwilightLevel TwilightLevel { get; set; }
        public int MinutesOffset { get; set; }
        public bool MoonAvoidanceEnabled { get; set; }
        public double MoonAvoidanceSeparation { get; set; }
        public int MoonAvoidanceWidth { get; set; }
        public double MoonRelaxScale { get; set; }
        public double MoonRelaxMinAltitude { get; set; }
        public double MoonRelaxMaxAltitude { get; set; }
        public bool MoonDownEnabled { get; set; }
    }

    /// <summary>
    /// The imaging circumstances for the target on a single night.
    /// </summary>
    public class DailyDetail {
        public DateTime NightDate { get; set; }
        public bool Accepted { get; set; }
        public string RejectedReason { get; set; }

        // When there is no imaging window, the start/end times are the twilight span for the night
        public bool HasWindow { get; set; }
        public DateTime StartImagingTime { get; set; }
        public DateTime EndImagingTime { get; set; }
        public string StartLimitingFactor { get; set; }
        public string EndLimitingFactor { get; set; }
        public DateTime TransitTime { get; set; }

        public double MoonIllumination { get; set; }
        public double MoonSeparation { get; set; }
        public double? MoonAvoidanceSeparation { get; set; }
        public bool MoonRejected { get; set; }

        public int ImagingMinutes => HasWindow ? (int)(EndImagingTime - StartImagingTime).TotalMinutes : 0;
    }

    /// <summary>
    /// Generate per-night imaging circumstances for a target.  Rather than implementing any astrometry itself, this builds
    /// transient (never persisted) TS project/target/exposure entities from the planning parameters and runs them through
    /// the same code the scheduler uses: <see cref="TargetImagingExpert"/> for visibility (twilight, horizon, maximum
    /// altitude, meridian window, minimum time) and <see cref="MoonAvoidanceExpert"/> for moon avoidance.
    /// </summary>
    public class DailyDetailsGenerator {

        // Target visibility sampling interval (seconds).  The default matches the scheduler's preview sampling - plenty
        // for nightly planning.  Sampling dominates the cost, so longer-range plans use a coarser interval.
        public const int DEFAULT_SAMPLE_INTERVAL = 30;

        private readonly int sampleInterval;

        private const string LIMIT_TWILIGHT = "twilight";
        private const string LIMIT_MINIMUM_ALTITUDE = "minimum altitude";
        private const string LIMIT_CUSTOM_HORIZON = "custom horizon";
        private const string LIMIT_MAXIMUM_ALTITUDE = "maximum altitude";
        private const string LIMIT_MERIDIAN = "meridian window";

        public const string REJECT_DARKNESS = "darkness";
        public const string REJECT_NEVER_RISES = "never rises";
        public const string REJECT_NOT_VISIBLE = "not visible";
        public const string REJECT_MINIMUM_TIME = "not enough time";
        public const string REJECT_MERIDIAN_WINDOW = "meridian window";
        public const string REJECT_MAXIMUM_ALTITUDE = "maximum altitude";
        public const string REJECT_MERIDIAN_FLIP = "meridian flip";
        public const string REJECT_MOON_AVOIDANCE = "moon avoidance";

        private readonly TargetPlanningParameters parameters;
        private readonly ObserverInfo observerInfo;

        public DailyDetailsGenerator(TargetPlanningParameters parameters, int sampleInterval = DEFAULT_SAMPLE_INTERVAL) {
            this.parameters = parameters;
            this.sampleInterval = sampleInterval;
            this.observerInfo = new ObserverInfo {
                Latitude = parameters.Profile.AstrometrySettings.Latitude,
                Longitude = parameters.Profile.AstrometrySettings.Longitude,
                Elevation = parameters.Profile.AstrometrySettings.Elevation
            };
        }

        public List<DailyDetail> Generate(CancellationToken token) {
            List<DailyDetail> details = new List<DailyDetail>(parameters.PlanDays);
            for (int i = 0; i < parameters.PlanDays; i++) {
                token.ThrowIfCancellationRequested();
                details.Add(GenerateNight(parameters.StartDate.Date.AddDays(i)));
            }

            return details;
        }

        public DailyDetail GenerateNight(DateTime date) {
            DailyDetail detail = new DailyDetail { NightDate = date.Date };

            // Afternoon of the date, so the scheduler code evaluates the upcoming night
            DateTime atTime = date.Date.AddHours(13);

            TwilightCircumstances twilightCircumstances;
            try {
                twilightCircumstances = TwilightCircumstances.AdjustTwilightCircumstances(observerInfo, atTime);
            } catch (ArgumentException) {
                // No civil twilight at all (polar day/night)
                Reject(detail, REJECT_DARKNESS, null);
                SetMoon(detail, null, null, false);
                return detail;
            }

            ITarget target = CreatePlanningTarget();
            IExposure exposure = target.ExposurePlans[0];
            TargetImagingExpert targetExpert = new TargetImagingExpert(parameters.Profile, null, false);

            TimeInterval twilightSpan = targetExpert.GetTwilightSpan(twilightCircumstances, target);
            if (twilightSpan == null || twilightCircumstances.Sunset == null || twilightCircumstances.Sunrise == null
                || twilightCircumstances.Sunset >= twilightCircumstances.Sunrise) {
                Reject(detail, REJECT_DARKNESS, twilightCircumstances.GetTwilightSpan(TwilightLevel.Civil));
                SetMoon(detail, target, exposure, false);
                return detail;
            }

            TargetVisibility targetVisibility = new TargetVisibility(target, observerInfo, twilightCircumstances.OnDate,
                twilightCircumstances.Sunset, twilightCircumstances.Sunrise, sampleInterval);
            detail.TransitTime = targetVisibility.TransitTime;

            if (!HasImagingWindow(targetExpert, atTime, target, twilightCircumstances, targetVisibility)) {
                string reason = GetRejectedReason(target.RejectedReason);

                // The scheduler folds the minimum time check into visibility, so retry without it to distinguish
                // 'not visible' from 'not enough time'
                if (reason == REJECT_NOT_VISIBLE && parameters.MinimumTime > 0) {
                    targetExpert.ClearRejections(target);
                    ((PlanningProject)target.Project).MinimumTime = 0;
                    if (HasImagingWindow(targetExpert, atTime, target, twilightCircumstances, targetVisibility)) {
                        SetWindow(detail, target, twilightSpan, targetVisibility);
                        SetMoon(detail, target, exposure, true);
                        return Reject(detail, REJECT_MINIMUM_TIME);
                    }
                }

                Reject(detail, reason, twilightSpan);
                SetMoon(detail, target, exposure, false);
                return detail;
            }

            SetWindow(detail, target, twilightSpan, targetVisibility);
            SetMoon(detail, target, exposure, true);

            if (detail.MoonRejected) {
                return Reject(detail, REJECT_MOON_AVOIDANCE);
            }

            detail.Accepted = true;
            return detail;
        }

        /// <summary>
        /// Run the scheduler's visibility determination from the afternoon.  Since the night hasn't started, the
        /// target is normally 'rejected' as not yet visible, but with its upcoming start/end times set.
        /// </summary>
        private bool HasImagingWindow(TargetImagingExpert targetExpert, DateTime atTime, ITarget target,
            TwilightCircumstances twilightCircumstances, TargetVisibility targetVisibility) {
            target.StartTime = DateTime.MinValue;
            if (targetExpert.Visibility(atTime, target, twilightCircumstances, targetVisibility)) {
                return true;
            }

            bool upcoming = target.RejectedReason == Reasons.TargetNotYetVisible
                || target.RejectedReason == Reasons.TargetBeforeMeridianWindow
                || target.RejectedReason == Reasons.TargetMeridianFlipClipped;
            return upcoming && target.StartTime != DateTime.MinValue && target.EndTime > target.StartTime;
        }

        private void SetWindow(DailyDetail detail, ITarget target, TimeInterval twilightSpan, TargetVisibility targetVisibility) {
            detail.HasWindow = true;
            detail.StartImagingTime = target.StartTime;
            detail.EndImagingTime = target.EndTime;
            detail.StartLimitingFactor = GetLimitingFactor(target.StartTime, twilightSpan.StartTime, target.MeridianWindow?.StartTime, targetVisibility);
            detail.EndLimitingFactor = GetLimitingFactor(target.EndTime, twilightSpan.EndTime, target.MeridianWindow?.EndTime, targetVisibility);
        }

        /// <summary>
        /// Set the moon details at the midpoint of the imaging window (or the twilight span if there is no window).
        /// Moon avoidance is only evaluated when there is a window to image in.
        /// </summary>
        private void SetMoon(DailyDetail detail, ITarget target, IExposure exposure, bool evaluateAvoidance) {
            DateTime midpoint = detail.StartImagingTime.AddTicks((detail.EndImagingTime - detail.StartImagingTime).Ticks / 2);
            detail.MoonIllumination = AstrometryUtils.GetMoonIllumination(midpoint);
            detail.MoonSeparation = AstrometryUtils.GetMoonSeparationAngle(observerInfo, midpoint, parameters.Coordinates);

            if (evaluateAvoidance) {
                MoonAvoidanceDetermination determination = new MoonAvoidanceExpert(observerInfo).Evaluate(midpoint, target, exposure);
                detail.MoonRejected = determination.Rejected;
                detail.MoonAvoidanceSeparation = determination.AvoidanceSeparation;
            }
        }

        /// <summary>
        /// Attribute a window boundary to whatever produced it.  The scheduler doesn't report this directly, so it's
        /// inferred by matching the boundary against the twilight span and meridian window, and checking the altitude.
        /// </summary>
        private string GetLimitingFactor(DateTime time, DateTime twilightTime, DateTime? meridianWindowTime, TargetVisibility targetVisibility) {
            if (IsNear(time, twilightTime)) {
                return LIMIT_TWILIGHT;
            }

            if (meridianWindowTime.HasValue && IsNear(time, meridianWindowTime.Value)) {
                return LIMIT_MERIDIAN;
            }

            if (parameters.MaximumAltitude > 0 && targetVisibility.GetAltitude(time) >= parameters.MaximumAltitude - 1) {
                return LIMIT_MAXIMUM_ALTITUDE;
            }

            return parameters.UseCustomHorizon ? LIMIT_CUSTOM_HORIZON : LIMIT_MINIMUM_ALTITUDE;
        }

        private bool IsNear(DateTime time1, DateTime time2) {
            return Math.Abs((time1 - time2).TotalSeconds) <= sampleInterval * 2;
        }

        private DailyDetail Reject(DailyDetail detail, string reason, TimeInterval span) {
            if (span != null) {
                detail.StartImagingTime = span.StartTime;
                detail.EndImagingTime = span.EndTime;
            } else {
                detail.StartImagingTime = detail.NightDate.AddHours(12);
                detail.EndImagingTime = detail.NightDate.AddHours(36);
            }

            return Reject(detail, reason);
        }

        private DailyDetail Reject(DailyDetail detail, string reason) {
            detail.Accepted = false;
            detail.RejectedReason = reason;
            detail.StartLimitingFactor = reason;
            detail.EndLimitingFactor = reason;
            return detail;
        }

        private string GetRejectedReason(string schedulerReason) {
            switch (schedulerReason) {
                case Reasons.TargetNeverRises: return REJECT_NEVER_RISES;
                case Reasons.TargetMeridianWindowClipped: return REJECT_MERIDIAN_WINDOW;
                case Reasons.TargetMaxAltitude: return REJECT_MAXIMUM_ALTITUDE;
                case Reasons.TargetMeridianFlipClipped: return REJECT_MERIDIAN_FLIP;
                case Reasons.TargetAllExposurePlans: return REJECT_DARKNESS;
                default:
                    TSLogger.Trace($"target planning: no visibility, scheduler reason: {schedulerReason}");
                    return REJECT_NOT_VISIBLE;
            }
        }

        /// <summary>
        /// Build the planning entities from transient database entities so that the real constructors (horizon
        /// determination, exposure handling) are used.  Nothing here is ever attached to a database context.
        /// </summary>
        private ITarget CreatePlanningTarget() {
            string profileId = parameters.Profile.Id.ToString();

            ExposureTemplate exposureTemplate = new ExposureTemplate(profileId, "Target Planning", "") {
                TwilightLevel = parameters.TwilightLevel,
                MinutesOffset = parameters.MinutesOffset,
                MoonAvoidanceEnabled = parameters.MoonAvoidanceEnabled,
                MoonAvoidanceSeparation = parameters.MoonAvoidanceSeparation,
                MoonAvoidanceWidth = parameters.MoonAvoidanceWidth,
                MoonRelaxScale = parameters.MoonRelaxScale,
                MoonRelaxMinAltitude = parameters.MoonRelaxMinAltitude,
                MoonRelaxMaxAltitude = parameters.MoonRelaxMaxAltitude,
                MoonDownEnabled = parameters.MoonDownEnabled
            };

            ExposurePlan exposurePlan = new ExposurePlan(profileId) { ExposureTemplate = exposureTemplate };

            // TargetVisibility caches by target id and coordinates, but its maximum altitude clipper depends on the project's
            // maximum altitude.  A negative id (never a real target) that varies with maximum altitude keeps the cache correct.
            Target target = new Target {
                Id = -1 - (int)Math.Round(parameters.MaximumAltitude),
                Name = string.IsNullOrEmpty(parameters.TargetName) ? "Target Planning" : parameters.TargetName,
                Coordinates = parameters.Coordinates
            };
            target.ExposurePlans.Add(exposurePlan);

            Project project = new Project(profileId) {
                Name = "Target Planning",
                MinimumTime = parameters.MinimumTime,
                MinimumAltitude = parameters.MinimumAltitude,
                MaximumAltitude = parameters.MaximumAltitude,
                UseCustomHorizon = parameters.UseCustomHorizon,
                HorizonOffset = parameters.HorizonOffset,
                MeridianWindow = parameters.MeridianWindow,
                FilterSwitchFrequency = 0
            };
            project.Targets.Add(target);

            // No grading/throttle: the single exposure plan (1 desired, 0 acquired) is always incomplete
            PlanningProject planningProject = new PlanningProject(parameters.Profile, project, new ExposureCompletionHelper(false, 0, 100));
            return planningProject.Targets[0];
        }
    }
}
